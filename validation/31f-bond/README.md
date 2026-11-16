# 31F「执事的嗜好」原生验证（需求79、80–82）

80–82新增后，当前稳定版与Beta版各205项通过，报告位于 `outputs/31f-cards-80-82-20261115/{stable,beta}/31f-bond-final-report.json`，索引 `maruyama-and-31f-validation-index.json`。实际生产31F牌精确为柳美音、丸山奏多、华村诗纪三张；本套读取真实Content/Garden角色映射，并由首张永久成员调用 `TeamCollectionRelics.EnsureObtained` → 原生 `RelicCmd.Obtain` 获得羁绊标记、重复通知不复制。未来未实装成员仍由测试程序集提供，以保留4/6档回归。

以下记录是需求79最初验收结果：

stable、beta 各195项通过。候选分别为 `BA34BE76868F1480196336C0D2BB46B4B5DCEA0EAD9000BB6BECF23E7E358E03`、`E588AF3C80A134724CF4DDE161F8373C2C67AB5F87FF6B5F858E53A330ADE24E`。报告位于新阶段 `{stable,beta}/owned-final/31f-bond-final-report.json`，整个批次的精确候选与进程退出码见 `owned-eight-final-index.json`。

需求79验收时，只有测试程序集提供六名未来31F成员牌，彼时生产中尚无31F卡牌。测试验证实际成员计数、2/4/6档门槛、HBR-only、真实标题／数字描述及原作420×420图标字节。

实际执行的路径：

- 原生 `CombatManager.SetupPlayerTurn`：正常起手抽牌前重排31F，保留原版Innate规则；五个真实种子产生不同内部顺序，恰好使用成员数减1次同步RNG，超出起手容量的31F继续置顶，非成员相对顺序不变。
- 原生 `CardPileCmd.Draw` 和生产31F前后边界：零抽、满手、禁止抽、普通中途抽、嵌套的起始抽牌、OD与个人额外回合各自账本；只修改起始抽牌完成时的合格手牌。
- 原生 `CardEnergyCost`：资源不足仍能参与，排除X／逻辑不可打牌，费用只降；真实全局+1／+2、半费、减费、零费、每牌特有+1，无法达到的固定下限有限估算后保留原值；后续本地改费与原生回合结束清理照常。
- 真实 `Monster.PerformMove` 和canonical Damage作用域进入原生 `DamageBlockInternal`：非致死先扣DP，致死及同一动作其余命令免伤，每场一次，下次动作恢复正常；真实格挡之后判断、初始零DP、独立攻击兜底、异步、异常、敌人中途死亡、Buffer不偷用，嵌套玩家／无施加者不冒充外层敌人。
- 真实Phantom目标重定向和 `Hook.BeforeDamageReceived`：从玩家目标进入30A主幻影、原生主人格挡回调再回到主幻影资源；普通宠物虽然同样经过主人格挡，也不会误用主人31F的次数。两名玩家各自保护、各自反击、各自记录一次。
- 反击确实为持有者的遗物来源 `AttackCommand`，432%×1、破坏系数2.5，使用普通攻击属性／增益路线（固定基准16662，50%攻击增益后24994），真实伤害回执／AfterDamageGiven给予2.5OD；不是打出攻击牌。

验证边界：Godot音效／对话和 `CardPileCmd.Add` 的场景叶受控，后者按真实对象修改牌堆，其独立换堆钩子和动画不在本套范围内；原生Draw、History、AfterCardDrawn与费用边界保持。伤害factory的末端场景叶给敌方提供可控的已结算伤害，再真实执行BeforeDamageReceived／原生block／DP／HP和回执；反击使用共享伤害公式，攻击动画叶受控。没有启动Godot、建立真实联机或打包。

该套曾用更精确的原生宠物路线发现真实生产问题：普通宠物受击先进入主人的格挡，旧31F误将主人当作实际受击者。已在当前canonical命令中记录逐目标PhysicalTarget，保留30A重定向并拒绝普通宠物冒用。旧失败和旧候选其他套件结果保存在 `owned-final/pre79-native-pet-receiver/`，不会用旧193项结果替代修复后的195项结果。
