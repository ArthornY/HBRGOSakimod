# 六宇亚与信念验证

覆盖需求71的实际能力牌、七项升级、原生能力计数接口与精确资源存档。超过 `int.MaxValue` 的信念仍按 `BigInteger` 保留；真实「翻倍」牌不得通过显示代理截断计数。

敌人动作使用真实 `MonsterModel.PerformMove` 与生产异步动作作用域。多次伤害命令经过生产 `HpOnlyDamageContext` 收据与 `ItsMygoIncomingDamageCapture.AppendResults`，验证正常命中、全格挡、闪避、真实 DP 吸收，以及一次逻辑攻击重定向到另一位玩家时只奖励实际受体。伤害场景终端受控；没有用手写的 Muua 命中集合代替生产判定。

回合测试调用普通 `AfterEnergyReset` 和实际 `HbrInsertedTurnStartHelper.Start`，后者使用真实 OD／个人额外回合 session 与 owner 判定。验证夏日恩惠先加信念、五点成组兑换、重复回调幂等、返回普通回合保留收据和 OD 专属增益。

DP 测试使用真实 Deflector setter、吹雪的 DP 成本、幻影外壳 setter 及原生 `PowerCmd.Remove`；检查每战斗仅救援一次、先永久清除彩妆再恢复 DP、重建外壳及资源存档。

受控叶为场景伤害、攻击动画、抽牌与获得能量的场景终端；实际卡牌、资源变化、百分比能力、费用、存档、动作作用域与救援逻辑不替换。未启动 Godot，未进行真实多人或 UI 操作，也未打包。

通过本阶段 `run-suite.ps1 -Suite muua-faith -Profile stable` 运行，完成后顺序运行 beta。报告绑定不可变候选 SHA。
