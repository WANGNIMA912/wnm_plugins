# lib/ — 编译引用程序集

本目录是 **KKS.CoordinateExporter 的编译引用缓存**，从本机游戏目录复制而来。
目的是让 `dotnet build <工程>.csproj` **不再依赖游戏安装目录**。

（`0Harmony.dll`、`KKS_ExtensibleSaveFormat.dll`、`UnityEngine.ImageConversionModule.dll` 由当前工程使用；`UnityEngine.UI.dll`、`UnityEngine.UIModule.dll` 暂未被 csproj 引用，保留备用。）

- 来源游戏：`E:\kk\[ScrewThisNoise] Koikatsu Sunshine EX BetterRepack R12`
- 生成日期：2026-10-19
- 共 13 个 DLL，总计 8654848 字节；当前 csproj 引用其中 11 个
- `lib/` 已被 `.gitignore` 忽略（游戏 DLL 不进入版本库）

## 文件清单

| 文件 | 游戏内相对路径 | 字节 | SHA-256 | 用途 |
| --- | --- | ---: | --- | --- |
| `BepInEx.dll` | `BepInEx\core\BepInEx.dll` | 128512 | `8255b28902886085c578b9e427d3073c97002db85176d2090cdeda90ef14ce70` | BepInEx 5.4.23.5 核心（BaseUnityPlugin / ConfigEntry / ManualLogSource） |
| `KKSAPI.dll` | `BepInEx\plugins\KKSAPI.dll` | 505856 | `5f53462207e28b029422a885aecc697353b18d1ee248dc1307e196562bc9fd57` | KKSAPI 1.48（KoikatuAPI / StudioAPI / OCIChar） |
| `Assembly-CSharp.dll` | `KoikatsuSunshine_Data\Managed\Assembly-CSharp.dll` | 6185472 | `d6a1fc24375fdc05165dfd268f524df373a844ef43847dd5818e13b0c7dd9924` | 游戏本体（Studio.Studio / CameraControl 等） |
| `UnityEngine.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.dll` | 84992 | `79e04323bf6c4715586f158e24b40be18cb754ad5da6c3d7b755b699df5cad5a` | Unity 引擎门面程序集 |
| `UnityEngine.CoreModule.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.CoreModule.dll` | 1005056 | `620f0e9d742e1e1a28d74bd248464b41df01154e8def4fac08e6a9ffcab46e52` | Unity 核心（GameObject / MonoBehaviour / Input 基础） |
| `UnityEngine.IMGUIModule.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.IMGUIModule.dll` | 156160 | `10fefeb0cd80584cb71f0b9fc024568ae261662487a0eaf351ebaef206e05e91` | IMGUI（GUILayout / GUI / GUIStyle） |
| `UnityEngine.InputLegacyModule.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.InputLegacyModule.dll` | 25600 | `565c72e9a82dec16f45f43c97176dc6f8470d38adcec78fd146d696420483b49` | 旧版 Input（Input.GetKeyDown / mousePosition） |
| `UnityEngine.TextRenderingModule.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.TextRenderingModule.dll` | 27648 | `a92759e1f33d8146a64764218b39b5511b0467b8da6a64da745cc1a856d5a0c8` | 字体（Font / FontStyle） |
| `UnityEngine.UIModule.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.UIModule.dll` | 22528 | `4878da3b2e3615c65599c6b0d9353eeac6dce798c14e4dd9f73825c246a1aeaa` | UGUI 基础（Canvas / RenderMode） |
| `UnityEngine.UI.dll` | `KoikatsuSunshine_Data\Managed\UnityEngine.UI.dll` | 225280 | `7bd099799cbf5cf07c5f9267f7867d5e7d44ac5bd6914b50d2ad010d40f7e7b0` | UGUI（Image / GraphicRaycaster） |
| `0Harmony.dll` | `BepInEx\core\0Harmony.dll` | 204800 | `1a21cc03424fc82c3dd1346905d16494536b9595ae4162228d99fb7c285c1031` | BepInEx 5 自带 Harmony 2.9（CoordinateExporter 打 MakerAPI 补丁用） |
| `KKS_ExtensibleSaveFormat.dll` | `BepInEx\plugins\KKS_BepisPlugins\KKS_ExtensibleSaveFormat.dll` | 70656 | `120de0a577e82ec5dddc3f5adf31798faaf33f1b3ad143e6a39cd4e9ed13065f` | ESF 21.1.4（读写服装卡扩展数据：ABMX / DBDE） |
| `UnityEngine.ImageConversionModule.dll` | `CharaStudio_Data\Managed\UnityEngine.ImageConversionModule.dll` | 12288 | `eb4c9c86a7e8ccfe725870e74956a291119a0dd26786a7ed1620340761789ae4` | `Texture2D.EncodeToPNG`（CoordinateExporter 生成卡片缩略图） |

## 未复制（不需要参与编译）

| 文件 | 游戏内相对路径 | 说明 |
| --- | --- | --- |
| `ConfigurationManager.dll` | `BepInEx\plugins\ConfigurationManager\ConfigurationManager.dll` | 软依赖；本插件只用 GUID 字符串，编译时不引用程序集 |

## 重新生成

游戏更新或换机器后，按上表把文件从游戏目录复制到本目录即可。
复制后运行：

```bash
dotnet build KKS.CoordinateExporter/KKS.CoordinateExporter.csproj
```

不传 `-p:GameDir` 也能编译成功，说明引用已完全指向本目录。
