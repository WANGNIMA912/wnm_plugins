using System;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>
    /// Screen space reflection for the Deferred GBuffer.
    ///
    /// Data sources (measured in KKS Studio):
    ///   _CameraGBufferTexture1.rgb = specular color (F0)
    ///   _CameraGBufferTexture1.a   = smoothness
    ///   _CameraGBufferTexture2.rgb = world normal (n * 0.5 + 0.5)
    ///   _CameraDepthTexture        = reversed-Z depth (near = 1, far = 0)
    ///
    /// The shader is an AssetBundle embedded in the DLL and built by Unity 2019.4.9f1.
    /// It contains four passes: trace (0), denoise (1), composite (2), debug (3).
    /// </summary>
    internal sealed class ScreenSpaceReflectionEffect : IGraphicEffect
    {
        private const string BundleResource = "KKS.ModernGraphicEffect.kks_ssr";

        private const int PassTrace = 0;
        private const int PassDenoise = 1;
        private const int PassComposite = 2;
        private const int PassDebug = 3;

        private readonly ManualLogSource _log;
        private Material _material;
        private bool _loadFailed;
        private RenderTexture _reflectionBuffer;
        private RenderTexture _denoiseBuffer;

        public ScreenSpaceReflectionEffect(ManualLogSource log)
        {
            _log = log;
            Enabled = true;
        }

        public string Id
        {
            get { return "ssr"; }
        }

        public string DisplayName
        {
            get { return "Screen Space Reflection"; }
        }

        public bool Enabled { get; set; }

        public bool RequiresDeferred
        {
            get { return true; }
        }

        public void OnCameraChanged(Camera camera)
        {
            if (camera == null) return;

            // The depth texture may be missing when no other effect asked for it,
            // and the shader cannot reconstruct anything without it.
            try
            {
                DepthTextureMode required = DepthTextureMode.Depth;
                if ((camera.depthTextureMode & required) != required)
                {
                    camera.depthTextureMode |= required;
                    _log.LogInfo("SSR requested DepthTextureMode.Depth on camera '" + camera.name + "'");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("SSR could not set depthTextureMode: " + ex.Message);
            }
        }

        public bool Render(RenderTexture source, RenderTexture destination)
        {
            if (!EnsureMaterial()) return false;
            if (source == null || destination == null) return false;

            ApplyParameters();

            int debugMode = ModernGraphicEffectPlugin.SsrDebugMode;
            int width = source.width;
            int height = source.height;

            // Debug modes 1..5 only need the GBuffer/depth, but modes 6 and 7 show the
            // reflection buffer, so the trace stage always runs. It is cheap enough at
            // the configured step count and keeps the debug view honest.
            RenderTexture traceTarget = GetBuffer(ref _reflectionBuffer, width, height, "SSR_Reflection");
            Graphics.Blit(source, traceTarget, _material, PassTrace);

            RenderTexture denoiseTarget = GetBuffer(ref _denoiseBuffer, width, height, "SSR_Denoise");
            Graphics.Blit(traceTarget, denoiseTarget, _material, PassDenoise);

            _material.SetTexture("_SSR_ReflTex", denoiseTarget);

            if (debugMode > 0)
            {
                _material.SetFloat("_SSR_Debug", debugMode);
                Graphics.Blit(source, destination, _material, PassDebug);
                return true;
            }

            Graphics.Blit(source, destination, _material, PassComposite);
            return true;
        }

        private void ApplyParameters()
        {
            _material.SetFloat("_SSR_Intensity", ModernGraphicEffectPlugin.SsrIntensity.Value);
            _material.SetFloat("_SSR_MaxDistance", ModernGraphicEffectPlugin.SsrMaxRayLength.Value);
            _material.SetFloat("_SSR_GlossMin", ModernGraphicEffectPlugin.SsrGlossMin.Value);
            _material.SetFloat("_SSR_Thickness", ModernGraphicEffectPlugin.SsrThickness.Value);
            _material.SetFloat("_SSR_Steps", ModernGraphicEffectPlugin.SsrSteps.Value);
            _material.SetFloat("_SSR_Jitter", ModernGraphicEffectPlugin.SsrJitter.Value ? 1f : 0f);
            _material.SetFloat("_SSR_NormalSource", ModernGraphicEffectPlugin.SsrDepthNormals.Value ? 1f : 0f);
            _material.SetFloat("_SSR_Debug", 0f);
            _material.SetFloat("_SSR_Frame", (float)(Time.frameCount % 4096));
        }

        private RenderTexture GetBuffer(ref RenderTexture buffer, int width, int height, string name)
        {
            if (buffer != null && buffer.width == width && buffer.height == height)
                return buffer;

            ReleaseBuffer(ref buffer);

            buffer = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.DefaultHDR,
                RenderTextureReadWrite.Linear);
            buffer.name = name;
            return buffer;
        }

        private static void ReleaseBuffer(ref RenderTexture buffer)
        {
            if (buffer == null) return;
            RenderTexture.ReleaseTemporary(buffer);
            buffer = null;
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            if (_loadFailed) return false;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream(BundleResource);
                if (stream == null)
                {
                    _log.LogError("SSR bundle resource not found: " + BundleResource);
                    _loadFailed = true;
                    return false;
                }

                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                stream.Dispose();

                AssetBundle bundle = AssetBundle.LoadFromMemory(bytes);
                if (bundle == null)
                {
                    _log.LogError("SSR bundle failed to load.");
                    _loadFailed = true;
                    return false;
                }

                Shader[] shaders = bundle.LoadAllAssets<Shader>();
                Shader shader = shaders.Length > 0 ? shaders[0] : null;
                if (shader == null)
                {
                    _log.LogError("SSR bundle contains no shader.");
                    _loadFailed = true;
                    return false;
                }

                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;

                if (!shader.isSupported)
                {
                    // A shader whose variants failed to compile renders nothing and would
                    // silently look like "SSR does nothing".
                    _log.LogError("SSR shader is not supported on this platform - the bundle is probably stale or incompatible.");
                    _loadFailed = true;
                    return false;
                }

                _log.LogInfo("SSR shader loaded: " + shader.name + " (passes=" + shader.passCount + ")");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError("SSR material init failed: " + ex);
                _loadFailed = true;
                return false;
            }
        }
    }
}
