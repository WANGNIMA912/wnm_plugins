# WNM PLUGINS

[English](#english) | [中文](#中文)

---

## English

A small collection of BepInEx 5 plugins for KK,
written to fill some practical needs under pure-studio/main game environment.

Each plugin lives in its own directory, ships as an independent DLL, and can be installed
on its own. Nothing here requires another plugin from this collection to work.

### Plugins

| Plugin                                                          | Status          | What it does                                                                                                                       |
|-----------------------------------------------------------------|-----------------|------------------------------------------------------------------------------------------------------------------------------------|
| **[KKS Coordinate Exporter](KKS.CoordinateExporter/README.md)** | v1.0.3 · stable | One-key outfit export in Studio: select characters, press **F8**, and the current outfit is written as a standard coordinate card. |

### Requirements

Common to every plugin in this collection:

| Component                | Version                                        | Required                                                              |
|--------------------------|------------------------------------------------|-----------------------------------------------------------------------|
| **Game**                 | Koikatsu Sunshine / Koikatsu                   | Yes                                                                   |
| **BepInEx**              | 5.4.x — **BepInEx 5 only**, 6 is not supported | Yes                                                                   |
| **KKSAPI**               | 1.20+                                          | Yes                                                                   |
| **ExtensibleSaveFormat** | Any recent version                             | Yes                                                                   |
| **ConfigurationManager** | Any                                            | No — only needed to rebind hotkeys in-game (**F1**)                   |
| **KKSABMX / DBDE**       | Any                                            | No — data is only carried over if installed and used on the character |

### Installation

Drop each plugin folder into `BepInEx/plugins/`. The layout looks like this:

```
BepInEx/
└─ plugins/
   └─WNM912/
     └─ KKS.CoordinateExporter.dll
        ......
```

Plugins are independent — install only what you need, and remove a folder to uninstall it.

### Building from source

Requires the **.NET SDK 10** build machine. All compile-time references live in `lib/`,
so **no game installation is needed to build**:

```bash
# single plugin
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj

# whole collection
dotnet build KKStudioPlugins.sln
```

To deploy straight into the game:

```bash
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj \
  -p:GameDir="<path to KKS>" -p:Deploy=true
```

### Notes

- Plugins write into `UserData/` and always back up before overwriting a card.

---

## 中文

随便整的插件集，包括一些工具和修复，以解决纯工作室/本体环境下会遇到的问题

每个插件各占一个目录、编译成独立 DLL，可以单独安装；
本集合内没有插件依赖另一个插件才能工作。

### 插件列表

| 插件                                                            | 状态          | 作用                                                                  |
|-----------------------------------------------------------------|---------------|-----------------------------------------------------------------------|
| **[KKS Coordinate Exporter](KKS.CoordinateExporter/README.md)** | v1.0.3 · 稳定 | 工作室一键导出服装卡：选中角色按 **F8**，当前服装直接写成标准服装卡。 |

### 环境要求

本集合所有插件共用：

| 组件                     | 版本                                | 必需                                         |
|--------------------------|-------------------------------------|----------------------------------------------|
| **游戏**                 | Koikatsu Sunshine / Koikatsu        | 是                                           |
| **BepInEx**              | 5.4.x —— **仅 BepInEx 5**，不支持 6 | 是                                           |
| **KKAPI**                | 1.20 及以上                         | 是                                           |
| **ExtensibleSaveFormat** | 较新版本即可，不要太远古            | 是                                           |
| **ConfigurationManager** | 任意                                | 否 —— 只有在游戏内按 **F1** 改键时才需要     |
| **KKSABMX / DBDE**       | 任意                                | 否 —— 装了且角色用过，导出时才会带上对应数据 |

### 安装

把每个插件文件夹直接放进 `BepInEx/plugins/`，结构如下：

```
BepInEx/
└─ plugins/
   └─WNM912/
     └─ KKS.CoordinateExporter.dll
        ......
```

各插件互相独立——需要哪个装哪个，删除文件夹即卸载。

### 从源码编译

构建机需要 **.NET SDK 10**。所有编译期引用都放在 `lib/` 里， **编译不依赖游戏安装目录**：

```bash
# 单个插件
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj

# 整个插件集
dotnet build KKStudioPlugins.sln
```

直接部署到游戏：

```bash
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj \
  -p:GameDir="<KKS 游戏目录>" -p:Deploy=true
```

完整的编译 / 部署 / 验证流程与编码约定见 [AGENTS.md](AGENTS.md)。

### 说明

- 游戏内提示跟随系统语言（英文 / 简体中文 / 繁体中文）。
- 插件只往 `UserData/` 写文件，且覆盖卡片前一律先备份。

---

## Repository layout

```
.
├─ KKS.CoordinateExporter/    # plugin project: .csproj + src/ + docs/ + README.md
├─ CoordinateLoadDBFix/       # plugin project (work in progress)
├─ lib/                       # shared compile-time reference DLLs + MANIFEST.md
├─ tools/
│  ├─ CardInspector/          # Python/tkinter card viewer & editor (independent of C#)
│  └─ verify/                 # prebuilt assembly checker (net10)
├─ KKStudioPlugins.sln        # solution for the whole collection
└─ README.md
```
