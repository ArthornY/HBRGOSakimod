# 丸山「让你见识一下什么才是品位」与加量原生验证（需求81）

稳定版与 Beta 版各 211 项通过；同阶段 31F 羁绊回归各 205 项。最终规范报告为各档的 `<suite>-final-report.json`。精确候选、报告路径和进程退出码见 `outputs/31f-cards-80-82-20261115/maruyama-and-31f-validation-index.json`。

本套保留真实的 `CardModel.CanPlay`、原生能量/星星资源判断、`SpendResources`、私有 `SpendEnergy`、History 与 `AfterEnergySpent`。覆盖零能量借贷、部分/足额支付、泛用第二张牌、改费/零费/31F抽牌后折扣、原生星星费用及能量换星支付、不可打/钩子限制和HBR-only。真实 `OnPlayWrapper`/`GeneratePlayCount` 重放系列只支付一次，`CardCmd.AutoPlay` 不制造欠账；全部升级保留消耗。

普通回合运行原生 `SetupPlayerTurn`，确认能量回满后扣除欠账。OD和个人额外回合使用实际生产token及共同 `HbrInsertedTurnStartHelper.Start`；重复回调不重扣，本回合新借款不提前结算，超过回满能量的欠款清空且不结转，队友独立。

卡体保留实际前后攻击钩子、连击段数及共享伤害计算。验证174%×4、破坏系数24、DP1.3逐段随Break退出，心眼/士气在前而新中连击在全部攻击后；中连击只增后续完整攻击的3段、持续3次。英雄与双方主幻影获得团队效果，普通宠物和幻影爪牙排除。柳美音回手保留同一实体及升级，多个候选真实调用选择器边界，一玩家一战只成功一次，空候选不消耗。回手保留完整原生 `CardPileCmd.Add`，只开启 `skipVisuals`；原生满手转弃牌堆规则实际执行。

DP专属防降实际经过百分比减益应用、属性快照、独立两槽、强弱替换与时长、Artifact、Double、继承保存字段的原生 `SavedProperties` JSON Fill。原生Amount不属于SavedProperties扩展，测试按独立counter载荷用 `ApplyInternal` 重建，之后填入扩展。共享真实伤害加区验证普通30%+DP50%=1.8，HP阶段与显式HP-only补伤不吃DP专属效果。

隔离范围：音效、UI纹理/时钟/等待、攻击动画和canonical伤害命令末端场景叶受控；后者运行真实伤害公式、BeforeDamageReceived、DamageBlockInternal、DP/HP资源及回执。选择器只控制用户选项。没有启动Godot、渲染卡面或验证真实多端联机。
