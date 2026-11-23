# KKS.SoundItemFix

修复 Koikatsu Sunshine 工作室中 `Studio.Sound.SEComponent` 声音物品在 Timeline 开关时的爆音/呲声。

## 背景

`[AX_MMD] Komastic's KA Sounds` 这类声音物品的 prefab 上只有一个 `Studio.Sound.SEComponent`，没有独立 `AudioSource`。它的开关逻辑是：

- `OnEnable`：`_audioSource == null` 时调用 `Utils.Sound.Play` 新建并播放；否则在停止状态下 `Play()`。
- `OnDisable`：直接 `Stop()`。
- `Update`：每帧把 `_audioSource.transform.position` 同步到物品位置。

Timeline 的 `objectEnabled` 阶跃式切 `treeNodeObject.visible` → `OCIItem.visible` → `seComponent.enabled`，因此开关 Timing 完全由 Timeline 决定。

新建的 `AudioSource` 由 `Manager.Sound.Play` 实例化在全局根节点下，第一帧不在物品位置；3D 源（`GameSE3D`，线性衰减 `0.1–7m`）会用错误位置算衰减和声像，下一帧才被 `Update` 拉回物品位置——听感就是启用瞬间“呲/爆”一下。`Stop()` 的硬切则会让循环音在任意相位产生 click。

## 功能

- **PositionSync**：`OnEnable` 播放后、下一帧之前把 `AudioSource` 放到物品世界坐标，消除首帧 3D 位置错误。
- **FadeOutSeconds**：`OnDisable` 默认 30ms 淡出后再 `Stop()`，消除硬切爆音；如果期间重新启用，淡出会被取消。
- **FixEnvRolloff**：`maxDistance <= minDistance`（ENV 环境音是 `7/7`）时把 `maxDistance` 提到 `minDistance + 0.1`，给 Unity 一个合法的线性衰减区间。
- **Stereo3D**：默认 `Force2D`，把 3D 声源上的立体声 clip 的 `spatialBlend` 置 0，避免 Unity 对 3D 立体声的异常处理（traffic / footsteps_leaves / revolver 等 25 个物品）。

## 配置

配置文件：`BepInEx/config/com.wangnima912.kks.sounditemfix.cfg`

| 段 | 键 | 默认 | 说明 |
| --- | --- | --- | --- |
| General | PositionSync | true | 播放时同步 AudioSource 到物品位置 |
| General | FixEnvRolloff | true | 修正 `maxDistance <= minDistance` |
| General | Stereo3D | Force2D | `Force2D` / `Keep` |
| Fade | FadeInSeconds | 0 | 淡入时长（0 关闭） |
| Fade | FadeOutSeconds | 0.03 | 淡出时长（0 用原版硬切） |
| Debug | VerboseLog | false | 每次开关记录修正日志 |

`FadeInSeconds` 默认关闭是有意的：原版 `Play()` 已经以满音量开始，淡入只能先把音量压到 0 再升回来，第一帧会有一次音量下陷。启用前请先对比试听。

## 构建

```bash
dotnet build KKS.SoundItemFix/KKS.SoundItemFix.csproj
```

## 部署

```bash
dotnet build KKS.SoundItemFix/KKS.SoundItemFix.csproj ^
  -p:GameDir="E:\kk\[ScrewThisNoise] Koikatsu Sunshine EX BetterRepack R12" ^
  -p:Deploy=true
```

默认部署到 `<GameDir>\BepInEx\plugins\KKS.SoundItemFix`。

## 说明

- 只修改运行时行为，不动 zipmod 资源。
- 目标进程：`CharaStudio.exe`；BepInEx 5.4.x / Harmony 2.9 / net46。
- 详细代码走读见 `docs/CODE-WALKTHROUGH.md`。
