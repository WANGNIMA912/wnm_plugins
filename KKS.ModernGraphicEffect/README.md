# KKS.ModernGraphicEffect

Koikatsu Sunshine（CharaStudio）的画面处理效果集合。**SSR 只是第一个效果**，后续会加入其他画面处理（SSAO、SSGI、体积光等）。

## 当前功能（v0.2.0）

- **渲染路径热切换**：`F8`（可配置）在 `Forward` 与 `DeferredShading` 之间实时切换 Studio 主相机。
- **屏幕空间反射（SSR）**：读取 Deferred GBuffer 的 `specular+smoothness`、`world normal` 和 reversed-Z 深度，做屏幕空间 raymarch。
  - 四段式管线：`trace`（raymarch）→ `denoise`（空域降噪）→ `composite`（菲涅尔 + 能量守恒）→ `debug`。
  - 精确透视重建（`LinearEyeDepth` + 手动焦距），射线长度是世界单位（米）。
- **调试热键**：
  - `F7`：循环 9 种调试视图（raw depth / linear depth / world normal / specular / smoothness / reflection / mask / gloss mask / off）；
  - `F6`：切换法线来源（GBuffer ↔ 深度重建）。

## 配置

文件：`BepInEx/config/com.wangnima912.kks.moderngraphiceffect.cfg`

| 键 | 默认 | 说明 |
|---|---|---|
| `Hotkeys/ToggleRenderingPath` | `F8` | 切换 Forward / DeferredShading |
| `Rendering/ApplyDefaultPathOnLoad` | `false` | 首次看到主相机时是否应用 `DefaultPath` |
| `Rendering/DefaultPath` | `Forward` | `Forward` 或 `DeferredShading` |
| `Effects.SSR/Enabled` | `true` | 是否启用 SSR（要求相机在 DeferredShading） |
| `Effects.SSR/Intensity` | `1.0` | 反射混合强度（0–4） |
| `Effects.SSR/MaxRayLength` | `8.0` | raymarch 最大长度，**世界单位（米）**；同时被 `8 × 局部场景深度` 夹紧 |
| `Effects.SSR/GlossMin` | `0.25` | 低于此光滑度的表面不接收反射；到 `GlossMin + 0.30` 达满强度 |
| `Effects.SSR/Thickness` | `0.15` | 命中厚度容差，越大命中越多但细物体越容易糊 |
| `Effects.SSR/Steps` | `32` | raymarch 步数（8–96），越高命中越准、开销越大 |
| `Effects.SSR/Jitter` | `true` | 逐像素射线抖动；关掉可看原始 march |
| `Effects.SSR/UseDepthNormals` | `false` | 用深度重建法线代替 GBuffer 法线（用于隔离 GBuffer 问题） |

## 构建与部署

```bash
dotnet build KKS.ModernGraphicEffect/KKS.ModernGraphicEffect.csproj

dotnet build KKS.ModernGraphicEffect/KKS.ModernGraphicEffect.csproj ^
  -p:Deploy=true ^
  -p:GameDir="E:\kk\[ScrewThisNoise] Koikatsu Sunshine EX BetterRepack R12"
```

SSR 的 shader 以 AssetBundle 形式嵌入 DLL（`resources/kks_ssr`），由 Unity 2019.4.9f1 构建；构建工程与命令见 `AGENTS.md`。

## 版本

版本号唯一来源：`KKS.ModernGraphicEffect.csproj` 的 `<Version>`。
编译时生成 `obj/<Configuration>/net46/PluginVersion.g.cs`，源码中不写死版本。

## 技术前提

- 目标进程 `CharaStudio.exe`，BIRP，Unity 2019.4.9f1，net46。
- SSR 依赖 Deferred GBuffer：
  - `_CameraGBufferTexture1.rgb` = specular color (F0)，`.a` = smoothness
  - `_CameraGBufferTexture2.rgb` = world normal (`n*0.5+0.5`)
  - `_CameraDepthTexture` = reversed-Z 深度
- 材质需要支持 `DEFERRED` pass（当前场景使用 `Az/Standard*`，已验证）。

## 已知限制

- 没有时域累积（无 TAA / 历史帧重投影），只做了单帧空域降噪；快速转动镜头时反射仍会有轻微闪烁。
- 粗糙表面靠射线抖动近似，没有真正的 GGX 重要性采样。
- 屏幕外信息不可见：反射源在画面外时不会出现（屏幕空间方法固有限制）。
- 切到 DeferredShading 会改变画面观感（整体略暗、光照响应不同），属于 `Az/Standard` 两套 pass 的差异。
- 透明材质（`Az/StandardLiteAlpha` 等）走 forward，不进入 GBuffer，因此不接收 SSR。

## 文件结构

```
KKS.ModernGraphicEffect/
├─ KKS.ModernGraphicEffect.csproj
├─ README.md
├─ AGENTS.md                  # AI 代理项目级指引
├─ docs/CODE-WALKTHROUGH.md   # 代码走读
├─ resources/kks_ssr          # SSR shader AssetBundle（嵌入 DLL）
└─ src/
   ├─ ModernGraphicEffectPlugin.cs
   ├─ RenderingPathSwitcher.cs
   ├─ EffectManager.cs
   ├─ EffectHost.cs
   ├─ IGraphicEffect.cs
   ├─ ScreenSpaceReflectionEffect.cs
   └─ Toast.cs
```
