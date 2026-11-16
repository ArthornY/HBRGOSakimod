# 92：数字乃万物之源

该宿主运行真实卡牌 `OnPlayWrapper`、自动出牌、原生 `GeneratePlayCount` 重放及能力的 Before/AfterCardPlayed 监听，不以纯函数调用替代序列推进。

覆盖三步攻击／技能序列的独立命名随机流、10% 与 10/20/30% 卡牌标记、中断／奖励、全部七项升级、额外攻击、同一物理 wrapper 三次重放只推进一次、普通回合暂停／个人额外回合／恢复、原生普通结束钩子、保存重载、异常和已关闭 ExecutionContext 的隔离。

数值检查包含真实 DP 回复入口、原生 NotYet 的来源明确 HP 回复 callsite、无来源回复不借标记、原生 ModifyBlock，以及准备 20% 与计算 10% 合并后仅乘一次 1.3。伤害使用与原作攻击增益相同加区，预览不会推进序列、抽取随机数或变成实际标记。

场景移动、VFX、历史记录、抽牌／能量／普通 HP 回复写入受控；额外攻击保留真实 `AttackCommand`，damage 终端记录原始威力、共享最终值与破坏系数。未运行 Godot 引擎、渲染或联机。

运行：

```powershell
& outputs/31x-cards-89-94-20261115/run-suite.ps1 -Suite 31x-vritika -Profile stable
& outputs/31x-cards-89-94-20261115/run-suite.ps1 -Suite 31x-vritika -Profile beta
```

同一项目的两个 profile 顺序运行。最终候选与报告路径由本阶段 `owned-final-index-read_req28.json` 指定；旧失败和开发报告保留以区分 fixture 修正与生产问题。

最终 stable / beta 各 115 项通过，分别绑定 `EA3BA34D…588BAC7` / `EB900597…327A5FE`；完整 SHA 与报告 SHA 见上述索引。生产源冻结 1533 文件，无漂移。
