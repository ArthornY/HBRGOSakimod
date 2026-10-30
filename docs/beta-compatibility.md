# Stable 与 public beta 兼容说明

本次源码适配覆盖游戏 **stable v0.107.1** 与 **public-beta v0.111.0**，以同一份 stable 编译的模组 DLL 在两套运行环境中验证。这是开发状态说明，尚未打包或发布到 Steam。

开发分支为 `codex/beta-compat-20261030`，使用独立 worktree。起点包含 `test-hbr-numerics` 的数值测试修改及相关本地修复，该分支同时保留这些玩法改动。

截至 **2026-10-30**，本次目标 beta 版本已对照 [Steam 官方 v0.111.0 更新公告](https://store.steampowered.com/news/app/2868840/view/671751488532383386)核对。后续游戏或依赖更新需要重新验证。

## 运行依赖与编译引用

两套验证环境均使用 **BaseLib 3.4.7**、**STS2-RitsuLib 0.6.10**、Harmony 2.4.2 和 GodotSharp 4.5.1；Ritsu 必须选择与游戏版本相符的运行程序集。

| 游戏 | Ritsu 版本目录 | 共用目录 |
| --- | --- | --- |
| stable v0.107.1 | `compat/0.107.1/` | `shared/` |
| public-beta v0.111.0 | `compat/0.111.0/` | `shared/` |

匹配的运行配置包含五个程序集：版本目录内的 `STS2-RitsuLib.dll`、`STS2-RitsuLib.Runtime.dll`，以及 `shared/` 内的 `STS2-RitsuLib.Shared.dll`、`STS2-RitsuLib.Settings.dll`、`STS2-RitsuLib.Ui.dll`。正常安装由 Ritsu 自身管理这些目录，不能只取其中一个 DLL 当作完整运行依赖。

当前项目的生产编译引用仍保留既有的单体 Ritsu 程序集；本次没有把编译依赖布局一并迁移。源码另外使用 beta 游戏程序集进行严格编译，已通过且无警告、无错误。跨版本运行验证使用同一份 stable 编译 DLL，运行时换成上表匹配的最新依赖配置。

## 本次适配内容

- **伤害上限**：适配 beta 为原生伤害上限钩子增加 `CardPlay` 参数的变化，共用伤害桥仍调用浮游炮免疫和柴犬无效化的规则。
- **Doom 判定**：兼容 beta 将 `IsOwnerDoomed` 改为私有方法，保留大生命值单位的实际 HP 判定。
- **事件战斗启动**：stable 的队列启动补丁与 beta 的原生事件战斗同步流程分别启用，使用 `Prepare` 选择对应补丁，避免在 beta 上套用旧启动方法签名。
- **确认操作**：自动绑定 stable 的 `accept` 或 beta 的 `confirm` 动作，保留 `select` 和游戏内按键重映射；绑定结果缓存后复用。
- **药水与牌组 UI**：兼容药水弹窗移除状态由字段变为属性；牌组计数监听按版本在退出树或删除通知中清理。

## 验证与复现

候选 DLL 的 SHA256 为 `8AB7AABB5094F331C27C5262DC6723908265F28D45ABAD2DA73BD381260834D4`。两套环境的报告均记录候选、游戏及依赖程序集的身份和哈希。

| 验证内容 | stable | beta |
| --- | ---: | ---: |
| Harmony 补丁类 | 513 | 513 |
| 实际注册的补丁类 | 511 | 511 |
| 按版本由 `Prepare` 跳过 | 2 | 2 |
| 补丁方法与目标组合 | 903 | 906 |
| 参数绑定检查 | 1202 | 1203 |
| 模组元数据引用 | 7570 | 7570 |
| 脆弱运行流程检查 | 650 | 650 |
| UI 兼容检查 | 73 | 74 |

上述运行环境检查均通过，没有未解决的元数据引用或补丁注册问题。另外，卡面、属性能力、脆弱攻击、全身铳击、浮游炮显示等六套现有回归在 stable 上共通过 **3584 项**。

两套可移植兼容工具的参数和测试边界见：

- [程序集引用与 Harmony 兼容审计](../validation/beta-compat/README.md)：输入候选 DLL、游戏、依赖、运行目录和 GodotSharp 路径，解析全部引用、目标和参数，并安装实际补丁。`--expected-mod-sha256` 可锁定候选，`--embedded-resource` 可检查嵌入式兼容 DLL。
- [UI 兼容验证](../validation/beta-ui-compat/README.md)：输入候选 DLL、哈希、运行配置目录及报告路径，验证确认动作、药水状态读取和牌组监听清理。

本地运行配置及报告位于被 Git 忽略的 `outputs/beta-compat/`；配置中的 `profile-manifest.json` 记录依赖来源和逐文件哈希。仓库不分发游戏或依赖程序集。

## 验证边界

beta 的独立托管测试只对**输出目录中的测试副本**隔离 Sentry 模块初始化：`<Module>..cctor` 必须恰好是 `call SentryAutoInit.Init` 后接 `ret`，仅将该调用改成 NOP，逐字节确认其余内容完全不变，并记录原件和副本哈希。游戏原始程序集保持不变，测试副本不用于发布。

这些检查没有启动 beta 游戏，未验证实机战斗、渲染或多人联机。严格编译、托管流程与补丁注册通过，不代表上述场景已经完成验收。

嵌入式 `HextechCompat` 在两套游戏下各有 152 个元数据引用通过解析；可选 `HextechRunes` 仅使用编译引用桩提供接口形状，因此这里只确认游戏 API 绑定，没有真实插件运行或玩法验证。
