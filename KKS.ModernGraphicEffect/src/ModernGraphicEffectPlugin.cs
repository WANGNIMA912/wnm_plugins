using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("CharaStudio.exe")]
    public partial class ModernGraphicEffectPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wangnima912.kks.moderngraphiceffect";
        public const string PluginName = "KKS Modern Graphic Effect";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> TogglePathHotkey;
        internal static ConfigEntry<bool> AutoApplyDefaultPath;
        internal static ConfigEntry<string> DefaultRenderingPath;

        internal static ConfigEntry<bool> SsrEnabled;
        internal static ConfigEntry<float> SsrIntensity;
        internal static ConfigEntry<float> SsrMaxRayLength;
        internal static ConfigEntry<float> SsrGlossMin;
        internal static ConfigEntry<float> SsrThickness;
        internal static ConfigEntry<int> SsrSteps;
        internal static ConfigEntry<bool> SsrJitter;
        internal static ConfigEntry<bool> SsrDepthNormals;

        internal static int SsrDebugMode;

        private EffectManager _effects;
        private RenderingPathSwitcher _pathSwitcher;
        private ScreenSpaceReflectionEffect _ssr;
        private Camera _hostCamera;
        private EffectHost _host;

        private void Awake()
        {
            Log = Logger;

            TogglePathHotkey = Config.Bind(
                "Hotkeys",
                "ToggleRenderingPath",
                new KeyboardShortcut(KeyCode.F8),
                "Switch the Studio main camera between Forward and DeferredShading in real time.");

            AutoApplyDefaultPath = Config.Bind(
                "Rendering",
                "ApplyDefaultPathOnLoad",
                false,
                "If true, applies DefaultPath to the main camera when it is first seen.");

            DefaultRenderingPath = Config.Bind(
                "Rendering",
                "DefaultPath",
                "Forward",
                "Rendering path used when ApplyDefaultPathOnLoad is true: Forward or DeferredShading.");

            SsrEnabled = Config.Bind(
                "Effects.SSR",
                "Enabled",
                true,
                "Enable screen space reflection. Requires the camera to be in DeferredShading.");

            SsrIntensity = Config.Bind(
                "Effects.SSR",
                "Intensity",
                1.0f,
                new ConfigDescription(
                    "Reflection blend strength multiplier.",
                    new AcceptableValueRange<float>(0f, 4f)));

            SsrMaxRayLength = Config.Bind(
                "Effects.SSR",
                "MaxRayLength",
                8.0f,
                new ConfigDescription(
                    "Maximum ray length in metres (view space). Rays are also capped at 8x the local scene depth.",
                    new AcceptableValueRange<float>(0.1f, 200f)));

            SsrGlossMin = Config.Bind(
                "Effects.SSR",
                "GlossMin",
                0.25f,
                new ConfigDescription(
                    "Minimum GBuffer smoothness for a surface to receive reflections. The ramp reaches full strength at GlossMin + 0.30.",
                    new AcceptableValueRange<float>(0f, 1f)));

            SsrThickness = Config.Bind(
                "Effects.SSR",
                "Thickness",
                0.15f,
                new ConfigDescription(
                    "Relative hit thickness. Larger values catch more hits but smear thin geometry.",
                    new AcceptableValueRange<float>(0.01f, 1f)));

            SsrSteps = Config.Bind(
                "Effects.SSR",
                "Steps",
                32,
                new ConfigDescription(
                    "Ray march step count. Higher = tighter hits, more GPU cost.",
                    new AcceptableValueRange<int>(8, 96)));

            SsrJitter = Config.Bind(
                "Effects.SSR",
                "Jitter",
                true,
                "Per-pixel ray jitter. Turn off to see the raw (noisy) march for debugging.");

            SsrDepthNormals = Config.Bind(
                "Effects.SSR",
                "UseDepthNormals",
                false,
                "Reconstruct normals from the depth buffer instead of the GBuffer. Useful to isolate a broken GBuffer.");

            _effects = new EffectManager();
            _ssr = new ScreenSpaceReflectionEffect(Log);
            _ssr.Enabled = SsrEnabled.Value;
            _effects.Register(_ssr);

            _pathSwitcher = new RenderingPathSwitcher();

            Log.LogInfo(string.Format(
                "{0} {1} loaded. F8 toggles Forward/DeferredShading, F7 cycles SSR debug, F6 toggles normal source.",
                PluginName, PluginVersion));
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                bool depthNormals = !SsrDepthNormals.Value;
                SsrDepthNormals.Value = depthNormals;
                string en = "SSR normals: " + (depthNormals ? "depth-derived" : "GBuffer");
                string zh = "SSR 法线：" + (depthNormals ? "深度重建" : "GBuffer");
                Log.LogInfo(en);
                Toast.Show(en, zh, zh);
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                SsrDebugMode = (SsrDebugMode + 1) % 9;
                string name = DebugName(SsrDebugMode);
                Log.LogInfo("SSR debug mode -> " + SsrDebugMode + " (" + name + ")");
                Toast.Show("SSR debug: " + name, "SSR 调试：" + name, "SSR 調試：" + name);
            }

            _ssr.Enabled = SsrEnabled.Value;
            _pathSwitcher.Tick();
            EnsureHost(_pathSwitcher.CurrentCamera);
        }

        private void OnGUI()
        {
            Toast.Draw();
        }

        private static string DebugName(int mode)
        {
            switch (mode)
            {
                case 1: return "raw depth";
                case 2: return "linear depth";
                case 3: return "world normal";
                case 4: return "specular color";
                case 5: return "smoothness";
                case 6: return "reflection color";
                case 7: return "reflection mask";
                case 8: return "gloss mask";
                default: return "off";
            }
        }

        private void EnsureHost(Camera camera)
        {
            if (camera == null) return;

            if (camera != _hostCamera)
            {
                // 相机换人时把旧宿主摘掉，否则旧相机会继续跑同一个 manager。
                if (_host != null)
                {
                    try
                    {
                        Destroy(_host);
                    }
                    catch (System.Exception)
                    {
                        // 场景卸载过程中对象可能已经销毁。
                    }
                    _host = null;
                }

                _hostCamera = camera;
                _effects.OnCameraChanged(camera);
            }

            if (_host != null && _host.gameObject == camera.gameObject) return;

            EffectHost host = camera.GetComponent<EffectHost>();
            if (host == null)
            {
                host = camera.gameObject.AddComponent<EffectHost>();
                host.hideFlags = HideFlags.HideAndDontSave;
                host.Initialize(_effects);
                Log.LogInfo("EffectHost attached to camera '" + camera.name +
                    "' path=" + camera.actualRenderingPath +
                    " depthMode=" + camera.depthTextureMode);
            }
            _host = host;
        }
    }
}
