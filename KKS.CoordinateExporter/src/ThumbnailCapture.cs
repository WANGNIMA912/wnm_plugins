using System;
using BepInEx.Logging;
using Studio;
using UnityEngine;

namespace KKS.CoordinateExporter
{
    /// <summary>
    /// 给导出的服装卡生成缩略图。
    ///
    /// 尺寸和本体服装卡一致（252 x 352，见 `ChaCustom.CustomCapture.CapCoordinateCard`）：
    /// 用一台临时相机从当前工作室视角的方向拍角色，3 倍超采样后缩到卡片尺寸。
    /// 拍不到时回退到角色卡自带的 PNG，保证不会因为渲染失败而导出没有预览的卡。
    /// </summary>
    internal static class ThumbnailCapture
    {
        internal const int CardWidth = 252;
        internal const int CardHeight = 352;

        private const int Supersample = 3;
        private const float VerticalFov = 35f;
        
        private const float FitScale = 0.5f;

        // 距离下限，避免退化包围盒时相机贴到角色脸上或穿过近裁剪面。
        private const float MinDistance = 0.25f;

        private static readonly Color Background = new Color(0.22f, 0.24f, 0.28f, 1f);

        /// <summary>
        /// 生成卡片缩略图；失败时返回角色卡 PNG（可能为 null）。
        /// </summary>
        internal static byte[] Capture(ChaControl chaCtrl, ChaFileControl chaFile)
        {
            byte[] png = null;
            try
            {
                png = Render(chaCtrl);
            }
            catch (Exception ex)
            {
                LogWarning("生成缩略图失败，改用角色卡 PNG: " + ex);
            }

            if (png != null && png.Length > 0)
            {
                LogDebug(string.Format("缩略图已生成（{0}x{1}，{2} 字节）", CardWidth, CardHeight, png.Length));
                return png;
            }

            if (chaFile != null && chaFile.pngData != null && chaFile.pngData.Length > 0)
            {
                LogDebug("缩略图回退到角色卡 PNG（" + chaFile.pngData.Length + " 字节）");
                return chaFile.pngData;
            }

            return null;
        }

        private static byte[] Render(ChaControl chaCtrl)
        {
            if (chaCtrl == null) return null;

            // 角色被隐藏时拍出来是一张空背景，直接用角色卡 PNG 更合适。
            if (!chaCtrl.visibleAll)
            {
                LogDebug("角色当前处于隐藏状态，跳过缩略图渲染。");
                return null;
            }

            Camera source = FindSourceCamera();
            Bounds bounds = ComputeBounds(chaCtrl);

            int ssWidth = CardWidth * Supersample;
            int ssHeight = CardHeight * Supersample;

            var go = new GameObject("KKS.CoordinateExporter.CaptureCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();

            RenderTexture supersampled = null;
            RenderTexture target = null;
            Texture2D texture = null;

            try
            {
                ConfigureCamera(cam, source, chaCtrl, bounds);

                supersampled = RenderTexture.GetTemporary(
                    ssWidth, ssHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target = RenderTexture.GetTemporary(
                    CardWidth, CardHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                supersampled.filterMode = FilterMode.Bilinear;
                target.filterMode = FilterMode.Bilinear;

                cam.targetTexture = supersampled;
                cam.Render();

                // 3 倍超采样 + 双线性缩小，等效于不错的抗锯齿（252x352 直接拍会明显有锯齿）。
                Graphics.Blit(supersampled, target);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                try
                {
                    texture = new Texture2D(CardWidth, CardHeight, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0f, 0f, CardWidth, CardHeight), 0, 0);
                    texture.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                }

                return texture.EncodeToPNG();
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (texture != null) UnityEngine.Object.Destroy(texture);
                if (supersampled != null) RenderTexture.ReleaseTemporary(supersampled);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(go);
            }
        }

        private static void ConfigureCamera(Camera cam, Camera source, ChaControl chaCtrl, Bounds bounds)
        {
            if (source != null)
            {
                cam.CopyFrom(source);
            }
            else
            {
                cam.cullingMask = ~0;
            }

            cam.orthographic = false;
            cam.fieldOfView = VerticalFov;
            cam.aspect = (float)CardWidth / CardHeight;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.targetTexture = null;
            cam.enabled = false;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 300f;

            // 工作室的选中框、IK 手柄等画在 "Studio/Camera" 层上，拍进卡里很难看。
            int cullingMask = cam.cullingMask;
            int studioCameraLayer = LayerMask.NameToLayer("Studio/Camera");
            if (studioCameraLayer >= 0) cullingMask &= ~(1 << studioCameraLayer);
            cam.cullingMask = cullingMask;

            float tanVertical = Mathf.Tan(VerticalFov * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * cam.aspect;

            // 用当前视角的水平方向决定从哪一面拍：用户把镜头转到正面，卡就是正面。
            Vector3 direction = Vector3.zero;
            if (source != null)
            {
                direction = source.transform.position - bounds.center;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = chaCtrl.transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.back;
            direction.Normalize();

            // 朝向只由 direction 决定，与相机放在哪无关；先摆好朝向，才能把包围盒
            // 的 8 个角换算到相机空间里解距离。
            Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
            cam.transform.rotation = rotation;

            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            // 逐角投影解距离：相机放在 bounds.center + direction * d 处时，
            // 某个角的相机空间坐标为 (x, y, depth + d)。要求它落在视锥内
            // （|x| <= tanH * z 且 |y| <= tanV * z），即可解出该角需要的最小 d。
            // 取 8 个角的最大值，包围盒就恰好贴住画面较紧的那条边，不留额外留白。
            float distance = MinDistance;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);

                Vector3 offset = corner - bounds.center;
                float depth = Vector3.Dot(offset, forward);
                float offsetX = Mathf.Abs(Vector3.Dot(offset, right));
                float offsetY = Mathf.Abs(Vector3.Dot(offset, up));

                float required = Mathf.Max(offsetX / tanHorizontal, offsetY / tanVertical) * FitScale - depth;
                if (required > distance) distance = required;
            }

            cam.transform.position = bounds.center + direction * distance;
            cam.transform.rotation = rotation;
        }

        private static Camera FindSourceCamera()
        {
            try
            {
                if (Studio.Studio.IsInstance() && Studio.Studio.Instance != null)
                {
                    // Studio 里有两个同名的 CameraControl（全局的和工作室的），
                    // 这里必须写全名，否则会被全局那个抢走。
                    Studio.CameraControl ctrl = Studio.Studio.Instance.cameraCtrl;
                    if (ctrl != null && ctrl.mainCmaera != null) return ctrl.mainCmaera;
                }
            }
            catch (Exception ex)
            {
                LogDebug("读取工作区相机失败: " + ex.Message);
            }

            if (Camera.main != null) return Camera.main;

            Camera best = null;
            foreach (Camera candidate in Camera.allCameras)
            {
                if (candidate == null || candidate.targetTexture != null) continue;
                if (best == null || candidate.depth > best.depth) best = candidate;
            }

            return best;
        }

        /// <summary>
        /// 合并角色所有可见 Renderer 的包围盒，并用头顶/脚底修正垂直范围，
        /// 防止超长头发或大饰品把镜头推得太远。
        /// </summary>
        private static Bounds ComputeBounds(ChaControl chaCtrl)
        {
            Vector3 origin = chaCtrl.transform.position;
            Bounds bounds = new Bounds(origin + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            bool any = false;

            Renderer[] renderers = chaCtrl.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                // 骨骼特效/拖尾的包围盒没有参考价值，而且会撑大画面。
                string typeName = renderer.GetType().Name;
                if (typeName == "ParticleSystemRenderer") continue;
                if (renderer is TrailRenderer || renderer is LineRenderer) continue;

                Bounds current = renderer.bounds;
                if (current.size.x < 0.001f && current.size.y < 0.001f && current.size.z < 0.001f) continue;

                if (!any)
                {
                    bounds = current;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(current);
                }
            }

            float top = bounds.max.y;
            if (chaCtrl.objHead != null)
            {
                top = Mathf.Max(top, chaCtrl.objHead.transform.position.y + 0.12f);
            }

            if (!any)
            {
                float feet = origin.y;
                float height = Mathf.Max(top - feet, 1.2f);
                return new Bounds(
                    new Vector3(origin.x, feet + height * 0.5f, origin.z),
                    new Vector3(0.6f, height, 0.6f));
            }

            float bottom = Mathf.Min(bounds.min.y, top - 1f);
            Vector3 center = bounds.center;
            center.y = (bottom + top) * 0.5f;

            return new Bounds(center, new Vector3(
                Mathf.Max(bounds.size.x, 0.4f),
                Mathf.Max(top - bottom, 1.15f),
                Mathf.Max(bounds.size.z, 0.4f)));
        }

        private static void LogDebug(string message)
        {
            ManualLogSource log = CoordinateExporterPlugin.Log;
            if (log != null) log.LogDebug(message);
        }

        private static void LogWarning(string message)
        {
            ManualLogSource log = CoordinateExporterPlugin.Log;
            if (log != null) log.LogWarning(message);
        }
    }
}
