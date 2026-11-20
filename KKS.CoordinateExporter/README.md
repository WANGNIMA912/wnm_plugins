# KKS Coordinate Exporter

- The first plugin in my plugin collection, initially vibecoded. 
- I only fix some camera range issue and rename the GUID 


[English](#english) | [中文](#中文)

## English

**KKS Coordinate Exporter** is a BepInEx 5 plugin for **Koikatsu Sunshine (KKS)** that adds one-key outfit export to
**CharaStudio**. Select a character, press the hotkey, and the current outfit is written as a standard coordinate card
that can be loaded directly in the main game or in Studio.
Currently KKS only

### Features

- Exports every part of the current outfit slot: Tops / Bottoms / Bra / Shorts / Gloves / Pantyhose / Socks / Indoor
  Shoes / Accessories
- Includes makeup when the slot has makeup enabled
- Preserves extended data written by other plugins: **KKSABMX** bone edits, **DBDE** dynamic bones, and any other plugin
  registered through `CharaCustomFunctionController`
- The thumbnail is rendered live at 252×352 from your **current view direction**, instead of reusing the character card
  image
- Results appear as a **top-left on-screen toast**: green for success, red for failure, fading out after about 4.5
  seconds

### Requirements

| Component            | Version                                      | Required                                                         |
|----------------------|----------------------------------------------|------------------------------------------------------------------|
| BepInEx              | 5.4.x (5.x only; BepInEx 6 is not supported) | Yes                                                              |
| KKSAPI               | 1.20+ (probably)                             | Yes                                                              |
| ExtensibleSaveFormat | Any but don't be too 'ancient'               | Yes                                                              |
| ConfigurationManager | any                                          | No, needed for in-game hotkey rebinding (F1)                     |
| KKSABMX / DBDE       | any                                          | No, data is only exported if installed and used by the character |

### Installation

Just install it like the other koikatsu plugins, it should be at BepInEx/plugins/KKS_CoordinateExporter.

### Usage

1. **Select** one or more characters in Studio (hold Ctrl to multi-select).
2. Press **F8** (configurable).
3. The toast reports the result, and the card is saved to `UserData/ExportedCoordinate/`.
4. File names look like `<chara name>_<slot>_<yyyyMMdd_HHmmss_fff>.png`, for example
   `Kasumi_Plain_20261021_181530_012.png`. Duplicate names get a `_1`, `_2` suffix.

The slot is whatever the character is currently wearing: **Plain / Swim / Pajamas / Bathing**.

### Configuration

You can search for "KKS Coordinate Exporter" in the F1 menu to configure the default export directory and hotkeys,
UserData/ExportedCoordinate and "F8" by default.

### How it works

1. Reads the selected character's current outfit slot and makes a deep copy, leaving the character itself untouched.
2. Renders a 252×352 full-body thumbnail from the current view direction with a temporary camera; falls back to the
   character card image if rendering fails.
3. Triggers the KKAPI coordinate-save chain so KKSABMX, DBDE and other controllers write their extended data into the
   copied card.
4. Calls the game's own `ChaFileCoordinate.SaveFile`; ExtensibleSaveFormat appends the extended-data block, so the file
   is byte-for-byte the same format as a vanilla coordinate card.

---

## 中文

**KKS Coordinate Exporter** 是一个 BepInEx 5 插件，为 **Koikatsu Sunshine（KKS）** 的 **CharaStudio**
提供一键服装卡导出：选中角色后按键，当前服装会被写成一个正常服装卡，可直接在本编或工作室的服装载入界面读取。

### 功能

- 导出当前服装槽位的全部部件：Tops / Bottoms / Bra / Shorts / Gloves / Pantyhose / Socks / Indoor Shoes / Accessories
- 该槽位启用了妆容时，一并向导出的卡里写入 makeup
- 保留已安装插件写进卡里的扩展数据： **KKSABMX** 的骨骼修正、 **DBDE** 的动态骨骼，以及其它通过
  `CharaCustomFunctionController` 注册的插件数据
- 缩略图按你 **当前视角的水平方向**实时拍摄（252×352 全身像），不是复用角色卡图片
- 导出结果在屏幕 **左上角**提示：成功为绿色色条，失败为红色色条，约 4.5 秒后淡出

### 环境要求

| 组件                 | 版本                              | 必需                                       |
|----------------------|-----------------------------------|--------------------------------------------|
| BepInEx              | 5.4.x（仅 5.x，不支持 BepInEx 6） | 是                                         |
| KKSAPI               | 1.20 及以上                       | 是                                         |
| ExtensibleSaveFormat | 任意版本，但是不要太远古          | 是                                         |
| ConfigurationManager | 任意版本                          | 否，装了才能在游戏里按 F1 改键             |
| KKSABMX / DBDE       | 任意版本                          | 否，装了且角色用过，导出时才会带上对应数据 |

### 安装

解压至游戏目录，正确情况是在BepInEx/plugins/KKS_CoordinateExporter

### 使用

1. 在工作室里 **选中**要导出的角色，按住 Ctrl 可多选。
2. 按 **F8**（可在 ConfigurationManager 里改）。
3. 左上角提示会显示结果，导出的卡保存到 `UserData/ExportedCoordinate/`。
4. 文件名格式为 `<角色名>_<槽位>_<yyyyMMdd_HHmmss_fff>.png`，例如 `Kasumi_Plain_20261021_181530_012.png`；重名时自动追加
   `_1`、`_2`。

槽位取自角色当前穿着的那一套： **Plain / Swim / Pajamas / Bathing**。

### 配置

Studio内按下f1，找到"KKS Coordinate Exporter"，即可配置服装卡导出路径以及快捷键，默认UserData/ExportedCoordinate以及"F8"。

修改后即时生效（导出目录在每次导出时重新解析）。

### 工作原理（简述）

1. 读取选中角色当前的服装槽位，做一份深拷贝，不修改角色身上的数据。
2. 用临时相机按当前视角方向拍一张 252×352 的全身缩略图；拍不到时回退到角色卡自带的图。
3. 触发 KKAPI 的服装保存链，让 KKSABMX / DBDE 等控制器把扩展数据写进这张卡。
4. 调用游戏自带的 `ChaFileCoordinate.SaveFile` 写文件，ExtensibleSaveFormat 会自动追加扩展数据块，所以文件格式与本编导出的服装卡完全一致。
