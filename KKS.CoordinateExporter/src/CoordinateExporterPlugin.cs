using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Studio;
using UnityEngine;

namespace KKS.CoordinateExporter
{
    /// <summary>
    /// 把 CharaStudio 里选中角色的当前服装导出成「服装卡」。
    ///
    /// 导出走游戏自己的 <see cref="ChaFileCoordinate.SaveFile"/>，
    /// 因此文件格式、PNG 缩略图与 ESF 扩展数据块都与本体的服装卡一致；
    /// 扩展数据由 KKAPI 的 CoordinateBeingSaved 处理链写入
    /// （KKSABMX / DBDE 都在这个链上）。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("CharaStudio.exe")]
    [BepInDependency(KoikatuApiGuid, "1.40")]
    [BepInDependency(ExtendedSaveGuid)]
    [BepInDependency(ConfigurationManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class CoordinateExporterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wangnima912.kks.coordinateexporter";
        public const string PluginName = "KKS Coordinate Exporter";

        // PluginVersion 由 csproj 的 <Version> 在编译时生成（obj/.../PluginVersion.g.cs）。
        // 不要在源码里再写死版本号。

        private const string KoikatuApiGuid = "marco.kkapi";
        private const string ExtendedSaveGuid = "com.bepis.bepinex.extendedsave";
        private const string ConfigurationManagerGuid = "com.bepis.bepinex.configurationmanager";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ExportHotkey;
        internal static ConfigEntry<string> ExportDirectory;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony _harmony;
        private bool _makerApiPatchApplied;

        private void Awake()
        {
            Log = Logger;

            ExportHotkey = Config.Bind(
                "General", "ExportHotkey", new KeyboardShortcut(KeyCode.F8),
                "导出选中角色服装卡的快捷键。默认 F8。");

            ExportDirectory = Config.Bind(
                "General", "ExportDirectory", CoordinateExportService.DefaultDirectory,
                "服装卡导出目录。相对路径以游戏根目录为基准，也可以填绝对路径。默认 UserData/ExportedCoordinate。");

            VerboseLog = Config.Bind(
                "General", "VerboseLog", false,
                "输出更详细的日志（每个角色写入的扩展数据键、耗时等）。");

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(CoordinateExporterPlugin).Assembly);
                _makerApiPatchApplied = true;
            }
            catch (Exception ex)
            {
                // 补丁失败不影响导出：退回手动触发 KKAPI 的保存事件，
                // 只是日志里会多一条 "outside chara maker" 报错。
                Log.LogError("Failed to patch MakerAPI.GetCharacterControl; falling back to manually raising the KKAPI save event: " + ex);
            }

            string dir;
            try
            {
                dir = CoordinateExportService.ResolveDirectory();
            }
            catch (Exception ex)
            {
                dir = "<无效: " + ex.Message + ">";
            }

            Log.LogInfo(string.Format(
                "{0} {1} loaded. Hotkey {2}, export directory {3}.",
                PluginName, PluginVersion, ExportHotkey.Value, dir));
        }

        private void Update()
        {
            if (CoordinateExportService.IsBusy) return;

            var hotkey = ExportHotkey.Value;
            if (hotkey.MainKey == KeyCode.None) return;

            // 先做一次轻量的 GetKeyDown 过滤，避免每帧调用 IsDown 的开销。
            if (!Input.GetKeyDown(hotkey.MainKey)) return;
            if (!hotkey.IsDown()) return;

            if (!StudioAPI.InsideStudio)
            {
                Log.LogWarning("Studio is not fully loaded yet, cannot export.");
                ExportToast.Failure("工作室还没有加载完成，暂时不能导出。", "Studio is not ready yet, cannot export.");
                return;
            }

            try
            {
                CoordinateExportService.ExportSelected(_makerApiPatchApplied);
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while exporting coordinate card: " + ex);
                ExportToast.Failure("导出时发生异常，详见日志。", "Export failed, see log for details.");
            }
        }

        // IMGUI 提示要在这里画：每次 OnGUI 都可能被调用多次，
        // ExportToast.Draw 在列表为空时会立刻返回，平时几乎没有开销。
        private void OnGUI()
        {
            ExportToast.Draw();
        }

        private void OnDestroy()
        {
            if (_harmony == null) return;
            try
            {
                _harmony.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to unpatch Harmony: " + ex.Message);
            }
        }
    }
}
