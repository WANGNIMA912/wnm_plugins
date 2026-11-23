using System;
using HarmonyLib;
using Studio.Sound;
using UnityEngine;

namespace KKS.SoundItemFix
{
    internal static class SEComponentPatches
    {
        private static bool _enableErrorLogged;
        private static bool _disableErrorLogged;

        [HarmonyPatch(typeof(SEComponent), "OnEnable")]
        [HarmonyPostfix]
        private static void OnEnablePostfix(SEComponent __instance)
        {
            try
            {
                AudioSource source = SeComponentAccess.GetAudioSource(__instance);
                if (source == null)
                {
                    return;
                }

                FadeHost host = FadeHost.Instance;

                // A new enable cancels a fade-out that may still be running.
                if (host != null)
                {
                    host.Cancel(source);
                }

                if (SoundItemFixPlugin.PositionSync.Value)
                {
                    source.transform.position = __instance.transform.position;
                }

                if (SoundItemFixPlugin.FixEnvRolloff.Value && source.maxDistance <= source.minDistance)
                {
                    source.maxDistance = source.minDistance + 0.1f;
                }

                if (SoundItemFixPlugin.Stereo3D.Value == Stereo3DMode.Force2D
                    && source.clip != null
                    && source.clip.channels == 2
                    && source.spatialBlend > 0f)
                {
                    source.spatialBlend = 0f;
                }

                float fadeIn = SoundItemFixPlugin.FadeInSeconds.Value;
                if (fadeIn > 0f && host != null)
                {
                    host.FadeIn(source, source.volume, fadeIn);
                }

                SoundItemFixPlugin.DebugLog(
                    "OnEnable fixed: " + __instance.name +
                    ", pos=" + source.transform.position +
                    ", min=" + source.minDistance.ToString("0.###") +
                    ", max=" + source.maxDistance.ToString("0.###") +
                    ", spatial=" + source.spatialBlend.ToString("0.###"));
            }
            catch (Exception e)
            {
                if (!_enableErrorLogged)
                {
                    _enableErrorLogged = true;
                    if (SoundItemFixPlugin.Log != null)
                    {
                        SoundItemFixPlugin.Log.LogError("SEComponent.OnEnable patch failed: " + e);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SEComponent), "OnDisable")]
        [HarmonyPrefix]
        private static bool OnDisablePrefix(SEComponent __instance)
        {
            try
            {
                float fadeOut = SoundItemFixPlugin.FadeOutSeconds.Value;
                if (fadeOut <= 0f)
                {
                    return true;
                }

                AudioSource source = SeComponentAccess.GetAudioSource(__instance);
                if (source == null || !source.isPlaying)
                {
                    return true;
                }

                FadeHost host = FadeHost.Instance;
                if (host == null)
                {
                    return true;
                }

                host.FadeOut(source, fadeOut);
                SoundItemFixPlugin.DebugLog("OnDisable fade-out " + fadeOut.ToString("0.###") + "s: " + __instance.name);
                return false;
            }
            catch (Exception e)
            {
                if (!_disableErrorLogged)
                {
                    _disableErrorLogged = true;
                    if (SoundItemFixPlugin.Log != null)
                    {
                        SoundItemFixPlugin.Log.LogError("SEComponent.OnDisable patch failed: " + e);
                    }
                }

                return true;
            }
        }
    }
}
