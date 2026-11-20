# KKS.ModernGraphicEffect — CODE WALKTHROUGH

## 1. 总体架构

```
ModernGraphicEffectPlugin (BepInEx BaseUnityPlugin)
 ├─ Config
 │   ├─ Hotkeys/ToggleRenderingPath       (KeyboardShortcut, 默认 F8)
 │   ├─ Rendering/ApplyDefaultPathOnLoad  (bool)
 │   ├─ Rendering/DefaultPath             (string)
 │   └─ Effects.SSR/*                     (Enabled / Intensity / MaxRayLength /
 │                                         GlossMin / Thickness / Steps / Jitter /
 │                                         UseDepthNormals)
 ├─ RenderingPathSwitcher
 │   └─ 找 Studio 主相机 → 读 renderingPath / actualRenderingPath → 写 renderingPath
 ├─ EffectManager
 │   ├─ IGraphicEffect[]（注册顺序 = 执行顺序）
 │   ├─ ping-pong 中间缓冲（多效果时不再"读 A 写 A"）
 │   └─ ScreenSpaceReflectionEffect（RequiresDeferred = true）
 ├─ EffectHost (MonoBehaviour，挂在主相机上)
 │   └─ OnRenderImage → EffectManager.Render（异常兜底为纯 Blit）
 └─ Toast (OnGUI 三语提示)
```

## 2. 文件职责

| 文件 | 职责 |
|---|---|
| `ModernGraphicEffectPlugin.cs` | 入口：读配置、创建 Manager/Switcher、每帧驱动、把 `EffectHost` 挂到主相机 |
| `RenderingPathSwitcher.cs` | 主相机发现与 `Forward` / `DeferredShading` 热切换 |
| `EffectManager.cs` | 效果注册表；`OnRenderImage` 时按顺序执行启用中的效果，多效果走 ping-pong |
| `IGraphicEffect.cs` | 效果契约（Id / Enabled / RequiresDeferred / OnCameraChanged / Render） |
| `EffectHost.cs` | 挂在相机上的 `OnRenderImage` 宿主，异常时退化为纯 `Blit` |
| `ScreenSpaceReflectionEffect.cs` | SSR：加载嵌入 bundle、驱动 trace → denoise → composite/debug 四个 Pass |
| `Toast.cs` | 游戏内提示（英/简中/繁中，按 `Application.systemLanguage`） |

## 3. 渲染路径热切换原理

Studio 主相机来自：

```csharp
Studio.Studio.Instance.cameraCtrl.mainCmaera
```

- `Camera.renderingPath` 是可写属性；
- `Camera.actualRenderingPath` 是只读，但会随 `renderingPath` 实时变化；
- 切换到 `RenderingPath.DeferredShading` 后，所有带 `DEFERRED` pass 的 shader（Az/Standard 系列）进入 GBuffer；
- 切换回 `RenderingPath.Forward` 后恢复前向渲染。

注意：

- BIRP 的 Deferred 不支持 MSAA；KKS 当前 `QualitySettings.antiAliasing = 0`，所以没有损失；
- Koikano 等无 deferred pass 的 shader 在 Deferred 下会 fallback 到 forward，仍可见；
- 透明材质（`QUEUE = Transparent`）始终走 forward，不进入 GBuffer。

## 4. 效果框架扩展方式

新增一个效果：

1. 在 `src/` 新建类实现 `IGraphicEffect`；
2. `Id` 用稳定短名（如 `"ssao"`）；
3. `RequiresDeferred` 表示是否需要 Deferred GBuffer；
4. 在 `ModernGraphicEffectPlugin.Awake()` 里 `_effects.Register(new YourEffect());`；
5. 若效果需要 shader，把 shader 打包进 AssetBundle，并在效果内部用 `AssetBundle.LoadFromMemory` 加载。

`EffectManager.Render` 会：

- 跳过 `Enabled == false` 的效果；
- 跳过 `RequiresDeferred == true` 但相机不在 Deferred 的效果；
- 多个效果时用 ping-pong 临时 RT 串联，最后一个可能写入的效果直接写 `destination`；
- 没有任何效果写入时直接 `Graphics.Blit(source, destination)`。

## 5. SSR 实现（2026-11 重写）

### 5.1 数据来源

| 纹理 | 内容 |
|---|---|
| `_MainTex` | 相机颜色（deferred 已光照，HDR） |
| `_CameraGBufferTexture1` | `.rgb` = specular color (F0)，`.a` = smoothness |
| `_CameraGBufferTexture2` | `.rgb` = world normal (`n*0.5+0.5`) |
| `_CameraDepthTexture` | reversed-Z（近 = 1，远 = 0） |

相机实测：`near = 0.01`、`far = 1000`、`depthMode = Depth, DepthNormals`、`colorSpace = Linear`、HDR。

### 5.2 四个 Pass

| Pass | 名称 | 作用 |
|---|---|---|
| 0 | `TRACE` | 逐像素 raymarch，输出 `float4(hitColor.rgb, mask)`，mask 为 0 表示无命中 |
| 1 | `DENOISE` | 5×5 深度感知空域滤波，压掉单帧抖动噪点 |
| 2 | `COMPOSITE` | Schlick 菲涅尔加权 + 能量守恒混合回 `_MainTex` |
| 3 | `DEBUG` | 9 种调试视图（见 §6） |

### 5.3 关键实现点

- **精确透视重建**：`tanHalfFov = rcp(abs(unity_CameraProjection[0][0]/[1][1]))`，
  `viewPos = ndc * tanHalf * eyeDepth`，`eyeDepth = LinearEyeDepth(rawDepth)`。
  **不使用** `lerp(nearRatio, 1, depth)` 这类归一化近似——本场景 `near/far = 1e-5`，
  该近似会丢掉全部精度（这是旧实现的主要失效原因之一）。
- **法线变换必须用 `unity_MatrixV`，不能用 `unity_WorldToCamera`**：
  - `unity_WorldToCamera` 是 Unity 相机空间（**+Z 向前**）；
  - 本 shader 的 `viewPos` 用 `-eyeDepth`（**-Z 向前**，与 `unity_MatrixV` 一致）；
  - 混用会让法线 z 翻转，反射射线全部打进表面背后 → SSR 视觉上完全没反应。
  - 症状：只有 `UseDepthNormals = true` 才有效（深度重建法线本身就在 -Z 空间）。
  - 详见 `AGENTS.md` §7.1。
- **循环内一律 `tex2Dlod`**：避免 D3D11 的 "gradient instruction used in a loop" 未定义行为。
  旧 shader 在 raymarch 循环里用 `SAMPLE_DEPTH_TEXTURE`，构建时就报过该告警。
- **厚度命中判定**：`diff = rayZ - sceneEye`，只有 `0 < diff < thickness` 才算命中，
  再做 5 次二分细化。旧实现用的是 `diff < max(0.001, sd*0.02)` 的单阈值，
  这是"条状/碎裂"伪影的直接来源。
- **射线长度是世界单位（米）**：`MaxRayLength` 默认 8，同时被 `min(rayLength, eyeDepth*8)` 二次夹紧。
  旧文档误称其为"归一化深度 0–1"，属于错误说法，已废弃。
- **逐像素抖动**：hash 抖动步长起点与方向微扰（粗糙度越大扰动越大），
  再由 `DENOISE` pass 收敛；`Jitter = false` 可关掉看原始 march。
- **法线兜底**：`UseDepthNormals = true` 时由深度重建法线，用于隔离 GBuffer 是否损坏。
- **能量守恒**：`base * (1 - strength*0.65) + refl * strength`，避免反射只是叠亮。

## 6. 调试视图（F7 循环）

| mode | 视图 |
|---|---|
| 0 | 关闭（正常合成） |
| 1 | raw depth |
| 2 | linear eye depth（`eye/20` 归一化） |
| 3 | world normal |
| 4 | specular color (F0) |
| 5 | smoothness |
| 6 | reflection color（trace+denoise 结果） |
| 7 | reflection mask |
| 8 | gloss mask（用于现场调 `GlossMin`） |

F6 切换法线来源（GBuffer ↔ 深度重建）。

## 7. 已知坑

1. **shader AssetBundle 是硬前置条件**：BepInEx 插件不能在运行时编译 shader，必须用 Unity 2019.4.9f1 预构建。
2. **Unity 授权**：batchmode 构建需要有效许可证；授权过期时构建会静默失败（退出码仍为 0），
   必须回查 `tmp/SSRShaderProject-build.log` 里的 `Bundle built` 标记。
3. **PPv2 `PostProcessLayer`** 当前是 Studio 相机上唯一活跃的后处理；SSR 在 `OnRenderImage` 里运行。
4. **两个 CameraControl**：`Studio.CameraControl` 与全局同名类，取主相机必须写全名。
5. **相机切换**：场景加载/切换相机后 `EffectHost` 会重新挂到新相机（`EnsureHost` 已处理）。
6. **Deferred 画面差异**：切 Deferred 后整体亮度略低，阴影与高光响应不同，属于 Az 两套 pass 的差异。
7. **不要把 GBufferProbe 的导图当证据**：同一个 `CommandBuffer` 连续 Blit 多个
   `BuiltinRenderTextureType` 会复用源绑定，5 张图 SHA 完全相同（已验证）。一律用 SSR 的 F7 视图。
