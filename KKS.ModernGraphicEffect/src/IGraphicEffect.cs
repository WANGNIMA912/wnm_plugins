using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>
    /// 一个画面处理效果的统一契约。插件本体只负责驱动，
    /// 具体效果（SSR、未来其他效果）各自实现本接口。
    /// </summary>
    public interface IGraphicEffect
    {
        /// <summary>稳定标识，用于配置与日志。</summary>
        string Id { get; }

        /// <summary>显示名，用于游戏内提示。</summary>
        string DisplayName { get; }

        /// <summary>是否启用。关闭时 Manager 会跳过它。</summary>
        bool Enabled { get; set; }

        /// <summary>该效果是否要求相机处于 DeferredShading。</summary>
        bool RequiresDeferred { get; }

        /// <summary>主相机发生变化时调用。</summary>
        void OnCameraChanged(Camera camera);

        /// <summary>
        /// 在相机图像效果链里执行。实现方可以只处理需要的部分；
        /// 返回 false 表示没有写 dst，由 Manager 继续后续处理。
        /// </summary>
        bool Render(RenderTexture source, RenderTexture destination);
    }
}
