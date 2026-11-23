using System.Reflection;
using Studio.Sound;
using UnityEngine;

namespace KKS.SoundItemFix
{
    internal static class SeComponentAccess
    {
        private static readonly FieldInfo AudioSourceField =
            typeof(SEComponent).GetField("_audioSource", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool IsAvailable
        {
            get { return AudioSourceField != null; }
        }

        internal static AudioSource GetAudioSource(SEComponent component)
        {
            if (component == null || AudioSourceField == null)
            {
                return null;
            }

            return AudioSourceField.GetValue(component) as AudioSource;
        }
    }
}
