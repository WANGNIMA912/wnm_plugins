using System;
using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>
    /// 在运行时切换主相机的渲染路径（Forward ↔ DeferredShading）。
    /// Studio 相机是 BIRP，两种路径都受 Unity 相机设置实时控制。
    /// </summary>
    internal sealed class RenderingPathSwitcher
    {
        private Camera _camera;
        private bool _appliedDefault;

        public Camera CurrentCamera
        {
            get { return _camera; }
        }

        public void Tick()
        {
            Camera camera = FindMainCamera();
            if (camera != _camera)
            {
                _camera = camera;
                _appliedDefault = false;
            }

            if (_camera != null && !_appliedDefault && ModernGraphicEffectPlugin.AutoApplyDefaultPath.Value)
            {
                _appliedDefault = true;
                ApplyConfiguredPath(_camera);
            }

            if (ModernGraphicEffectPlugin.TogglePathHotkey.Value.IsDown())
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (_camera == null)
            {
                Toast.Show("No Studio camera found.", "未找到工作室相机。", "未找到工作室相機。");
                return;
            }

            bool deferred = _camera.actualRenderingPath == RenderingPath.DeferredShading;
            RenderingPath next = deferred ? RenderingPath.Forward : RenderingPath.DeferredShading;
            _camera.renderingPath = next;

            RenderingPath actual = _camera.actualRenderingPath;
            ModernGraphicEffectPlugin.Log.LogInfo(string.Format(
                "Rendering path switched: requested={0} actual={1} depthMode={2}",
                next, actual, _camera.depthTextureMode));

            if (actual != next)
            {
                Toast.Show(
                    "Rendering path change did not take effect.",
                    "渲染路径切换未生效。",
                    "渲染路徑切換未生效。");
                return;
            }

            if (actual == RenderingPath.DeferredShading)
            {
                Toast.Show("Rendering path: Deferred", "渲染路径：延迟", "渲染路徑：延遲");
            }
            else
            {
                Toast.Show("Rendering path: Forward", "渲染路径：前向", "渲染路徑：前向");
            }
        }

        private void ApplyConfiguredPath(Camera camera)
        {
            string configured = ModernGraphicEffectPlugin.DefaultRenderingPath.Value;
            RenderingPath path = configured == "DeferredShading"
                ? RenderingPath.DeferredShading
                : RenderingPath.Forward;
            camera.renderingPath = path;
            ModernGraphicEffectPlugin.Log.LogInfo("Applied default rendering path: " + path);
        }

        private static Camera FindMainCamera()
        {
            try
            {
                if (Studio.Studio.IsInstance() && Studio.Studio.Instance != null)
                {
                    Studio.CameraControl control = Studio.Studio.Instance.cameraCtrl;
                    if (control != null && control.mainCmaera != null) return control.mainCmaera;
                }
            }
            catch (Exception)
            {
                // 场景尚未加载完成时读取 cameraCtrl 可能抛异常。
            }
            return Camera.main;
        }
    }
}
