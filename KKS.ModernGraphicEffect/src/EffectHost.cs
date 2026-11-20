using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>
    /// 挂在主相机上的图像效果宿主。所有 IGraphicEffect 通过这里进入 OnRenderImage 链。
    /// </summary>
    internal sealed class EffectHost : MonoBehaviour
    {
        private EffectManager _manager;
        private int _renderCount;

        public void Initialize(EffectManager manager)
        {
            _manager = manager;
            ModernGraphicEffectPlugin.Log.LogInfo("EffectHost initialized on '" + name + "'");
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            _renderCount++;
            if (_renderCount == 1 || _renderCount % 300 == 0)
            {
                ModernGraphicEffectPlugin.Log.LogInfo("EffectHost.OnRenderImage frame=" + _renderCount +
                    " manager=" + (_manager != null) + " source=" + Describe(source));
            }

            if (_manager == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            try
            {
                _manager.Render(source, destination);
            }
            catch (System.Exception ex)
            {
                // 一个效果抛异常不应该让整个相机画面变黑。
                ModernGraphicEffectPlugin.Log.LogError("EffectHost render failed: " + ex);
                Graphics.Blit(source, destination);
            }
        }

        private static string Describe(RenderTexture rt)
        {
            if (rt == null) return "null";
            return rt.width + "x" + rt.height + " fmt=" + rt.format;
        }
    }
}
