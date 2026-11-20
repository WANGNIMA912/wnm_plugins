using System.Collections.Generic;
using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>
    /// 效果注册表与执行链。第一版只负责把启用中的效果按注册顺序串起来；
    /// 单个效果内部如何做（CommandBuffer、OnRenderImage）由效果自己决定。
    /// </summary>
    internal sealed class EffectManager
    {
        private readonly List<IGraphicEffect> _effects = new List<IGraphicEffect>();
        private Camera _camera;

        // 双缓冲：中间链路里"读 A 写 A"是非法采样，必须 ping-pong。
        private RenderTexture _ping;
        private RenderTexture _pong;

        public IList<IGraphicEffect> Effects
        {
            get { return _effects; }
        }

        public void Register(IGraphicEffect effect)
        {
            if (effect == null) return;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Id == effect.Id) return;
            }
            _effects.Add(effect);
        }

        public void OnCameraChanged(Camera camera)
        {
            _camera = camera;
            for (int i = 0; i < _effects.Count; i++)
            {
                _effects[i].OnCameraChanged(camera);
            }
        }

        public void Render(RenderTexture source, RenderTexture destination)
        {
            RenderTexture current = source;
            bool written = false;

            try
            {
                for (int i = 0; i < _effects.Count; i++)
                {
                    IGraphicEffect effect = _effects[i];
                    if (effect == null || !effect.Enabled) continue;
                    if (effect.RequiresDeferred && _camera != null &&
                        _camera.actualRenderingPath != RenderingPath.DeferredShading)
                    {
                        continue;
                    }

                    // 最后一个"可能写入"的效果直接写 destination，否则就要一张中间缓冲。
                    // 双缓冲 ping-pong：不能"读 A 写 A"，否则 GPU 会同时把同一张 RT 当输入和输出。
                    RenderTexture target = destination;
                    if (HasLaterWriter(i))
                    {
                        bool usePong = current == _ping;
                        target = usePong
                            ? GetBuffer(ref _pong, source)
                            : GetBuffer(ref _ping, source);
                    }

                    if (effect.Render(current, target))
                    {
                        written = true;
                        current = target;
                    }
                }

                if (!written)
                {
                    Graphics.Blit(source, destination);
                }
                else if (current != destination)
                {
                    Graphics.Blit(current, destination);
                }
            }
            finally
            {
                ReleaseBuffer(ref _ping);
                ReleaseBuffer(ref _pong);
            }
        }

        /// <summary>后面是否还有效果可能写入（用于决定当前效果能否直接写 destination）。</summary>
        private bool HasLaterWriter(int index)
        {
            for (int i = index + 1; i < _effects.Count; i++)
            {
                IGraphicEffect effect = _effects[i];
                if (effect == null || !effect.Enabled) continue;
                if (effect.RequiresDeferred && _camera != null &&
                    _camera.actualRenderingPath != RenderingPath.DeferredShading)
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private static RenderTexture GetBuffer(ref RenderTexture buffer, RenderTexture source)
        {
            if (buffer != null && buffer.width == source.width && buffer.height == source.height)
                return buffer;

            ReleaseBuffer(ref buffer);
            buffer = RenderTexture.GetTemporary(
                source.width, source.height, 0, source.format, RenderTextureReadWrite.Linear);
            return buffer;
        }

        private static void ReleaseBuffer(ref RenderTexture buffer)
        {
            if (buffer == null) return;
            RenderTexture.ReleaseTemporary(buffer);
            buffer = null;
        }
    }
}
