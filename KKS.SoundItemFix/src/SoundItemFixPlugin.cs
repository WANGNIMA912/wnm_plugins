using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace KKS.SoundItemFix
{
    public enum Stereo3DMode
    {
        Keep = 0,
        Force2D = 1
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("CharaStudio.exe")]
    public partial class SoundItemFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wangnima912.kks.sounditemfix";
        public const string PluginName = "KKS Sound Item Fix";

        internal static SoundItemFixPlugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> PositionSync { get; private set; }

        internal static ConfigEntry<bool> FixEnvRolloff { get; private set; }

        internal static ConfigEntry<Stereo3DMode> Stereo3D { get; private set; }

        internal static ConfigEntry<float> FadeInSeconds { get; private set; }

        internal static ConfigEntry<float> FadeOutSeconds { get; private set; }

        internal static ConfigEntry<bool> VerboseLog { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            PositionSync = Config.Bind(
                "General",
                "PositionSync",
                true,
                "Move the AudioSource to the item position before it starts playing. " +
                "This fixes the first-frame 3D position glitch that makes newly enabled sounds pop.");

            FixEnvRolloff = Config.Bind(
                "General",
                "FixEnvRolloff",
                true,
                "If maxDistance <= minDistance (the ENV sound items use 7/7), raise maxDistance " +
                "to minDistance + 0.1 so Unity has a valid linear rolloff range.");

            Stereo3D = Config.Bind(
                "General",
                "Stereo3D",
                Stereo3DMode.Force2D,
                "How to handle stereo clips on 3D sources. Force2D sets spatialBlend to 0 " +
                "(recommended for the traffic/footstep clips); Keep leaves the source untouched.");

            FadeInSeconds = Config.Bind(
                "Fade",
                "FadeInSeconds",
                0f,
                new ConfigDescription(
                    "Fade-in duration for newly enabled sounds, in seconds. 0 disables it. " +
                    "The original Play() already starts at full volume, so enabling this briefly dips " +
                    "the volume in the first frame.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            FadeOutSeconds = Config.Bind(
                "Fade",
                "FadeOutSeconds",
                0.03f,
                new ConfigDescription(
                    "Fade-out duration before Stop() when an SEComponent is disabled, in seconds. " +
                    "0 restores the original hard Stop().",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            VerboseLog = Config.Bind(
                "Debug",
                "VerboseLog",
                false,
                "Log every SEComponent enable/disable fix. Keep disabled during normal use.");

            if (!SeComponentAccess.IsAvailable)
            {
                Logger.LogWarning(
                    "Studio.Sound.SEComponent._audioSource was not found. " +
                    "The plugin loaded, but the patches cannot do anything on this build.");
            }

            FadeHost.CreateHost();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(SEComponentPatches));

            Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded. " +
                "PositionSync=" + PositionSync.Value +
                ", FixEnvRolloff=" + FixEnvRolloff.Value +
                ", Stereo3D=" + Stereo3D.Value +
                ", FadeOut=" + FadeOutSeconds.Value.ToString("0.###") + "s");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            FadeHost.DestroyHost();
            Instance = null;
            Log = null;
        }

        internal static void DebugLog(string message)
        {
            if (VerboseLog != null && VerboseLog.Value && Log != null)
            {
                Log.LogDebug(message);
            }
        }
    }
}
