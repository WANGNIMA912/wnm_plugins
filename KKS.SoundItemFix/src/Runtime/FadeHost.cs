using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KKS.SoundItemFix
{
    internal sealed class FadeHost : MonoBehaviour
    {
        private sealed class FadeState
        {
            internal AudioSource Source;
            internal Coroutine Routine;
        }

        private static FadeHost _instance;

        private readonly Dictionary<int, FadeState> _states = new Dictionary<int, FadeState>();

        internal static FadeHost Instance
        {
            get { return _instance; }
        }

        internal static void CreateHost()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject go = new GameObject("KKS.SoundItemFix.FadeHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<FadeHost>();
        }

        internal static void DestroyHost()
        {
            if (_instance == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        internal void FadeIn(AudioSource source, float targetVolume, float duration)
        {
            if (source == null)
            {
                return;
            }

            Cancel(source);

            if (duration <= 0f)
            {
                source.volume = targetVolume;
                return;
            }

            FadeState state = new FadeState { Source = source };
            _states[source.GetInstanceID()] = state;
            state.Routine = StartCoroutine(FadeRoutine(state, 0f, targetVolume, duration, false));
        }

        internal void FadeOut(AudioSource source, float duration)
        {
            if (source == null)
            {
                return;
            }

            Cancel(source);

            if (duration <= 0f)
            {
                source.Stop();
                return;
            }

            FadeState state = new FadeState { Source = source };
            _states[source.GetInstanceID()] = state;
            state.Routine = StartCoroutine(FadeRoutine(state, source.volume, 0f, duration, true));
        }

        internal void Cancel(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            FadeState state;
            if (!_states.TryGetValue(id, out state))
            {
                return;
            }

            if (state.Routine != null)
            {
                StopCoroutine(state.Routine);
            }

            _states.Remove(id);
        }

        private IEnumerator FadeRoutine(FadeState state, float from, float to, float duration, bool stopAtEnd)
        {
            AudioSource source = state.Source;
            int id = source != null ? source.GetInstanceID() : 0;
            float elapsed = 0f;

            while (elapsed < duration && source != null)
            {
                // Audio is not affected by Time.timeScale; use unscaled time so a
                // Timeline time-scale change cannot stretch or freeze the fade.
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (source != null)
            {
                source.volume = to;
                if (stopAtEnd)
                {
                    source.Stop();
                }
            }

            if (id != 0)
            {
                _states.Remove(id);
            }

            state.Routine = null;
        }

        private void OnDestroy()
        {
            foreach (FadeState state in _states.Values)
            {
                if (state.Routine != null)
                {
                    StopCoroutine(state.Routine);
                }
            }

            _states.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
