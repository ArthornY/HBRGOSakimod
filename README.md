# HBRGOSakiMod

《Slay the Spire 2》的角色与玩法模组，以丰川祥子和《炽焰天穹 × MyGO》相关设定为基础。

模组包含祥子的 31O、30A 玩法与小萤：六感成长与掷骰、DP 护盾与破坏率、幻影、五门浮游炮及其升级，以及 OD 额外回合。具体规则可在游戏内的角色手册中查看。

## 安装与设置

在 [Steam 创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3799277911)订阅本模组，并同时订阅该页面列出的 **BaseLib** 和 **STS2-RitsuLib** 必须物品。启动游戏时启用这些模组。

运行依赖的最低版本和模组版本以根目录的 [HBRGOSakiMod.json](HBRGOSakiMod.json) 为准。声音资源由独立的 SoundPack 项目提供。

**ModUI** 默认开启，可在游戏的模组配置菜单或 RitsuLib 配置菜单中调整。开启后使用模组的 HP/DP、能力、遗物与药水布局。

如需手动安装，将同一次发布的 `HBRGOSakiMod.json`、`HBRGOSakiMod.dll` 和 `HBRGOSakiMod.pck` 放入游戏的 `mods/HBRGOSakiMod/` 目录。PDB 用于调试，不是运行必需文件。不要混用不同版本的文件。

## 开发环境

- [Godot 4.5.1 .NET / Mono](https://godotengine.org/download/archive/4.5.1-stable/)，默认编辑器和导出版本。
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。
- 与目标游戏版本匹配的本地依赖程序集。

仓库保留源代码和运行资源。`libs/`、编译产物、SDK 缓存、素材草稿与历史包只放在本地，不进入 Git。

首次构建前创建 `libs/`，放入以下文件：

| 文件 | 获取位置 |
| --- | --- |
| `sts2.dll` | 已安装的《Slay the Spire 2》游戏程序集目录 |
| `0Harmony.dll` | 本地游戏模组运行环境使用的 Harmony 程序集 |
| `BaseLib.dll` | 已安装的 BaseLib 模组 |
| `STS2-RitsuLib.dll` | 已安装的 STS2-RitsuLib 模组 |

游戏和依赖模组的安装以官方发行及其工坊页面为准。仓库不分发这些程序集。

如果依赖位于其他目录，可以通过 MSBuild 参数 `Sts2AssemblyPath`、`HarmonyAssemblyPath`、`BaseLibAssemblyPath`、`RitsuLibAssemblyPath` 指定完整路径。

在项目根目录执行：

```powershell
dotnet restore .\HBRGOSakiMod.csproj
dotnet build .\HBRGOSakiMod.csproj --no-restore -c Debug -p:SkipModOutputCopy=true
dotnet build .\HBRGOSakiMod.csproj --no-restore -c Release -p:SkipModOutputCopy=true
```

`SkipModOutputCopy=true` 用于源码验证，避免覆盖本地交付文件。主项目会构建并嵌入 `HextechCompat`；其 `Reference/` 是必要的编译接口源码，应保留。

## 打包与分支

`main` 用于已验证的版本，`test` 用于性能优化与回归验证。`test` 的修改通过验证后再决定是否合入主分支。

打包使用 Godot 的 `Windows Desktop` 导出预设，将 C# 程序集、PCK 与根目录的模组清单写入本地 `build/`。常规交付为 JSON、DLL、PCK 和用于调试的 PDB，默认不生成 ZIP。

普通源码构建不等于完整打包。打包和上传按明确的发布请求执行；SoundPack 未变更时不重新发布。

## 目录结构

| 路径 | 内容 |
| --- | --- |
| `Core/` | 卡牌、角色、遗物、能力、战斗逻辑与 UI |
| `HextechCompat/` | 可选兼容逻辑和编译接口源码 |
| `HBRGOSakiMod/localization/` | 本地化文本 |
| `images/`、`fonts/` | 游戏使用的图片和字体 |
| `scenes/`、`themes/`、`materials/` | Godot 场景、主题和材质 |
| `story/` | 事件剧情数据 |
| `docs/` | 玩法设定参考 |
| `HBRGOSakiMod.json` | 模组版本与运行依赖 |
| `project.godot`、`export_presets.cfg` | Godot 项目与导出配置 |
| `HBRGOSakiMod.csproj`、`HBRGOSakiMod.sln` | C# 构建入口 |

整理仓库只改变 Git 跟踪范围；已有的本地素材、备份和发布包继续保留。旧提交中的历史文件也仍可检索。
