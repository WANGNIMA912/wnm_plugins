using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ExtensibleSaveFormat;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Studio;

namespace KKS.CoordinateExporter
{
    /// <summary>
    /// 服装卡导出的实际逻辑：复制选中角色的当前服装槽位，补上缩略图，
    /// 再交给游戏自己的 <see cref="ChaFileCoordinate.SaveFile"/> 写盘。
    /// </summary>
    internal static class CoordinateExportService
    {
        internal const string DefaultDirectory = "UserData/ExportedCoordinate";

        private static bool _busy;

        internal static bool IsBusy
        {
            get { return _busy; }
        }

        /// <summary>把配置里的导出目录解析成绝对路径（相对路径以游戏根目录为基准）。</summary>
        internal static string ResolveDirectory()
        {
            string dir = CoordinateExporterPlugin.ExportDirectory != null
                ? CoordinateExporterPlugin.ExportDirectory.Value
                : null;

            if (string.IsNullOrEmpty(dir)) dir = DefaultDirectory;
            dir = dir.Trim();
            if (dir.Length == 0) dir = DefaultDirectory;

            if (!Path.IsPathRooted(dir)) dir = Path.Combine(Paths.GameRootPath, dir);

            return Path.GetFullPath(dir);
        }

        /// <summary>导出当前工作室里所有被选中的角色。</summary>
        internal static void ExportSelected(bool makerApiPatchApplied)
        {
            var targets = new List<ChaControl>();
            foreach (var oci in StudioAPI.GetSelectedCharacters())
            {
                if (oci == null || oci.charInfo == null) continue;
                targets.Add(oci.charInfo);
            }

            if (targets.Count == 0)
            {
                CoordinateExporterPlugin.Log.LogWarning("No selected character, nothing exported.");
                ExportToast.Failure("没有选中角色，未导出服装卡。", "No character selected, nothing exported.");
                return;
            }

            string dir;
            try
            {
                dir = ResolveDirectory();
            }
            catch (Exception ex)
            {
                CoordinateExporterPlugin.Log.LogError("Invalid export directory: " + ex.Message);
                ExportToast.Failure("导出目录无效，未导出服装卡。", "Export directory is invalid, nothing exported.");
                return;
            }

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                CoordinateExporterPlugin.Log.LogError("Cannot create export directory " + dir + ": " + ex.Message);
                ExportToast.Failure("无法创建导出目录，未导出服装卡。", "Cannot create export directory, nothing exported.");
                return;
            }

            var exported = new List<string>();
            int failed = 0;

            _busy = true;
            try
            {
                foreach (var chaCtrl in targets)
                {
                    try
                    {
                        string path = ExportOne(chaCtrl, dir, makerApiPatchApplied);
                        if (path != null) exported.Add(Path.GetFileName(path));
                        else failed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        CoordinateExporterPlugin.Log.LogError("Exception while exporting character: " + ex);
                    }
                }
            }
            finally
            {
                _busy = false;
            }

            CoordinateExporterPlugin.Log.LogInfo(string.Format(
                "Export finished: {0} succeeded, {1} failed, directory {2}", exported.Count, failed, dir));

            if (exported.Count > 0)
            {
                string first = exported[0];
                string more = exported.Count > 1
                    ? string.Format("（共 {0} 张）", exported.Count)
                    : string.Empty;
                string moreEn = exported.Count > 1
                    ? string.Format(" ({0} cards)", exported.Count)
                    : string.Empty;

                ExportToast.Success(
                    "已导出服装卡：" + first + more,
                    "Exported: " + first + moreEn);
            }

            if (failed > 0)
            {
                ExportToast.Failure(
                    string.Format("有 {0} 个角色导出失败，详见日志。", failed),
                    string.Format("{0} character(s) failed to export, see log.", failed));
            }
        }

        private static string ExportOne(ChaControl chaCtrl, string dir, bool makerApiPatchApplied)
        {
            var log = CoordinateExporterPlugin.Log;

            var chaFile = chaCtrl.chaFile;
            if (chaFile == null)
            {
                log.LogError("角色没有 ChaFile，无法导出。");
                return null;
            }

            var slots = chaFile.coordinate;
            if (slots == null || slots.Length == 0)
            {
                log.LogError("角色没有服装数据槽位，无法导出。");
                return null;
            }

            int coordinateType = 0;
            if (chaCtrl.fileStatus != null) coordinateType = chaCtrl.fileStatus.coordinateType;
            if (coordinateType < 0 || coordinateType >= slots.Length)
            {
                log.LogWarning(string.Format(
                    "当前服装类型 {0} 超出范围（0..{1}），按 Plain 处理。", coordinateType, slots.Length - 1));
                coordinateType = 0;
            }

            var source = slots[coordinateType];
            if (source == null)
            {
                log.LogError("当前服装槽位为空，无法导出。");
                return null;
            }

            // 用游戏自己的序列化做一次深拷贝，避免改动角色身上的 ChaFileCoordinate。
            var coordinate = new ChaFileCoordinate();
            try
            {
                if (!coordinate.LoadBytes(source.SaveBytes(), source.loadVersion))
                {
                    log.LogError("复制当前服装数据失败（LoadBytes 返回 false）。");
                    return null;
                }
            }
            catch (Exception ex)
            {
                log.LogError("复制当前服装数据时发生异常: " + ex);
                return null;
            }

            string typeName = ((ChaFileDefine.CoordinateType)coordinateType).ToString();
            string charaName = GetCharaName(chaFile);

            coordinate.coordinateName = charaName + " [" + typeName + "]";
            // 本体会用 CustomCapture 现拍一张 252x352 的预览图；我们照做，
            // 拍不到（角色隐藏、渲染失败）时 Capture 自己会回退到角色卡 PNG。
            coordinate.pngData = ThumbnailCapture.Capture(chaCtrl, chaFile);

            string destPath = BuildDestinationPath(dir, charaName, typeName);
            var started = DateTime.Now;

            // Studio 里没有 maker，KKAPI 的保存链会调用 MakerAPI.GetCharacterControl()
            // 拿角色；拿不到 null 就直接放弃，ABMX / DBDE 的数据也就不会写。
            // 先实测一下补丁是否真的生效（方法可能被 Mono 内联，光看 PatchAll
            // 成功不算数），没生效就手动触发一次保存链，保证扩展数据一定写入。
            bool patchActive = makerApiPatchApplied && IsMakerApiPatchActive(chaCtrl);
            if (!patchActive) InvokeKkapiSaveHook(chaCtrl, coordinate);

            using (MakerApiExportScope.Enter(chaCtrl))
            {
                coordinate.SaveFile(destPath);
            }

            var elapsed = DateTime.Now - started;
            var info = new FileInfo(destPath);
            log.LogInfo(string.Format(
                "[{0}] 已导出 {1}（{2} 字节，用时 {3:0} ms）",
                charaName, Path.GetFileName(destPath), info.Exists ? info.Length : -1, elapsed.TotalMilliseconds));

            LogExtendedData(coordinate, charaName);
            return destPath;
        }

        /// <summary>
        /// 在导出作用域里调用一次 MakerAPI.GetCharacterControl()，
        /// 看补丁是否真的把它劫持了（Mono 可能把原方法内联进调用方，
        /// 这种情况下 PatchAll 成功也拦不到，必须走反射兜底）。
        /// </summary>
        private static bool IsMakerApiPatchActive(ChaControl chaCtrl)
        {
            using (MakerApiExportScope.Enter(chaCtrl))
            {
                return ReferenceEquals(MakerAPI.GetCharacterControl(), chaCtrl);
            }
        }

        // KKAPI 没有公开入口触发保存链，只有这个 private static 方法；
        // 正常情况下用 Harmony 补丁更干净，这里只作为补丁失败时的兜底。
        private static readonly System.Reflection.MethodInfo KkapiSaveHook = typeof(CharacterApi).GetMethod(
            "OnCoordinateBeingSaved",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        private static void InvokeKkapiSaveHook(ChaControl chaCtrl, ChaFileCoordinate coordinate)
        {
            if (KkapiSaveHook == null)
            {
                CoordinateExporterPlugin.Log.LogError(
                    "KKAPI.Chara.CharacterApi.OnCoordinateBeingSaved not found; ABMX / DBDE data may not have been written.");
                return;
            }

            try
            {
                KkapiSaveHook.Invoke(null, new object[] { chaCtrl, coordinate });
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException && ex.InnerException != null
                    ? ex.InnerException
                    : ex;
                CoordinateExporterPlugin.Log.LogError("Failed to manually raise the KKAPI save chain: " + inner);
            }
        }

        private static void LogExtendedData(ChaFileCoordinate coordinate, string charaName)
        {
            var log = CoordinateExporterPlugin.Log;

            Dictionary<string, PluginData> extendedData;
            try
            {
                extendedData = ExtendedSave.GetAllExtendedData(coordinate);
            }
            catch (Exception ex)
            {
                log.LogWarning("读取扩展数据失败: " + ex.Message);
                return;
            }

            if (extendedData == null || extendedData.Count == 0)
            {
                log.LogWarning("[" + charaName + "] 卡片里没有扩展数据；" +
                               "如果角色用了 ABMX / DBDE，请确认这两个插件已安装并已在工作室生效。");
                return;
            }

            log.LogInfo("[" + charaName + "] 写入的扩展数据: " + string.Join(", ", extendedData.Keys.ToArray()));

            if (CoordinateExporterPlugin.VerboseLog != null && CoordinateExporterPlugin.VerboseLog.Value)
            {
                foreach (var pair in extendedData)
                {
                    int bytes = 0;
                    if (pair.Value != null && pair.Value.data != null)
                    {
                        foreach (var item in pair.Value.data.Values)
                        {
                            var blob = item as byte[];
                            if (blob != null) bytes += blob.Length;
                        }
                    }

                    log.LogDebug(string.Format("  - {0}: version={1}, 约 {2} 字节",
                        pair.Key, pair.Value != null ? pair.Value.version : -1, bytes));
                }
            }
        }

        private static string GetCharaName(ChaFile chaFile)
        {
            string name = null;

            if (chaFile.parameter != null) name = chaFile.parameter.fullname;
            if (string.IsNullOrEmpty(name)) name = chaFile.charaFileName;

            name = (name ?? string.Empty).Trim();
            return name.Length == 0 ? "Chara" : name;
        }

        private static string BuildDestinationPath(string dir, string charaName, string typeName)
        {
            string safeName = SanitizeFileName(charaName);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string baseName = safeName + "_" + typeName + "_" + stamp;

            string path = Path.Combine(dir, baseName + ".png");
            int suffix = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(dir, baseName + "_" + suffix + ".png");
                suffix++;
            }

            return path;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name != null ? name.Length : 0);

            if (name != null)
            {
                foreach (char c in name)
                {
                    if (char.IsControl(c)) continue;
                    if (Array.IndexOf(invalid, c) >= 0) continue;
                    sb.Append(c);
                }
            }

            string result = sb.ToString().Trim().TrimEnd('.');
            if (result.Length == 0) result = "Chara";
            if (result.Length > 48) result = result.Substring(0, 48);
            return result;
        }
    }
}
