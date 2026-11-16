# 91：忏悔的时间

该宿主调用真实 `RepentanceTimeHbrgoSaki.OnPlayWrapper`、能力监听、原生资源回复和原生 HP 扣除。七项升级使用实际升级实体，保存检查使用 `SavedProperties` 的 JSON 往返。

覆盖献祭当前 DP 的 30%、固定 2 点信念、信念快照与每回合鼓舞、惩戒、叩击、仅出牌时净化、战士祝福的普通／OD／个人回合限次、晚餐与玩家／主幻影目标，以及全队首次致命伤害保留 1 HP。

保命检查运行真实 canonical `CreatureCmd.Damage` 和 HP 写入，包含原生格挡、30A 重定向、大生命值单击上限、真实 Bloodletting 成本、Galvanic 的同卡反应和月城友伤。安装共享 health-cost callsite 与既有 native damage-profile 两组补丁，成本和无关嵌套伤害分别识别。整场一次的回执存入玩家已有手册：全部玛利亚能力被驱散后保存、重载、再打牌，仍不能再次保命；正常战斗结束后清空。临时生成的牌也使用同一手册，无需额外发放队伍遗物。

场景移动、VFX、历史记录、抽牌／能量写入和普通 HP 回复终端受控；叩击仍运行真实 `AttackCommand`，其 outgoing damage 终端受控以记录系数及共享增幅。此项不是游戏内渲染或联机验证。

运行：

```powershell
& outputs/31x-cards-89-94-20261115/run-suite.ps1 -Suite 31x-maria -Profile stable
& outputs/31x-cards-89-94-20261115/run-suite.ps1 -Suite 31x-maria -Profile beta
```

同一项目的 stable/beta 顺序运行；报告中的 `modSha256` 绑定实际不可变候选，不能把开发候选报告作为最终产物证据。最终候选与报告路径由本阶段 `owned-final-index-read_req28.json` 指定。

最终 stable / beta 各 129 项通过，分别绑定 `EA3BA34D…588BAC7` / `EB900597…327A5FE`；四份完整 SHA 与报告 SHA 见上述索引。生产源冻结 1533 文件，无漂移。
