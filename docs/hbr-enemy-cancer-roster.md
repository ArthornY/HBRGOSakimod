# 星癌体敌人名单（需求78）

`HbrEnemyCancerHelper.IsCancer(Creature)` 是唯一公共判断入口。按实际敌人模型类型识别，使用固定类型集合；不会增加战斗能力、状态图标或更改敌人的出招、血量、伤害。

本表核对四幕遭遇注册表的 `GetAll()` 和每个真实 `EncounterModel.AllPossibleMonsters`，包括尚未解锁的遭遇。39种星癌体：35种表怪和4种独立Boss模型。同一模型跨幕使用时仍只有一种；表中的幕是当前遭遇注册位置，不限制该模型在其他房间的身份。

| 类名 | 当前幕 | 来源类型 | 星癌体 |
|---|---|---|---|
| `AbyssKnocker` | 1 | 表怪 | 是 |
| `AbyssKnockerKiller` | 2 | 表怪 | 是 |
| `ArachneLine` | 3 | 表怪 | 是 |
| `BalbFlea` | 3 | 表怪 | 是 |
| `BarrelWalker` | 2 | 表怪 | 是 |
| `BigotryGate` | 3 | 表怪 | 是 |
| `CatHorn` | 1 | 表怪 | 是 |
| `CatHornKiller` | 2 | 表怪 | 是 |
| `ClayKnocker` | 1 | 表怪 | 是 |
| `CorollaKnife` | 3 | 表怪 | 是 |
| `CrestHopper` | 1 | 表怪 | 是 |
| `CrestHopperKiller` | 2 | 表怪 | 是 |
| `CrowHorse` | 1 | 表怪／Boss遭遇 | 是 |
| `DarkKnockerKiller` | 2 | 表怪 | 是 |
| `DeathSlug` | 2 | Boss | 是 |
| `DiamondEyeball` | 2 | 表怪 | 是 |
| `FlatHand` | 4 | Boss | 是 |
| `GiraffeHalo` | 3 | 表怪 | 是 |
| `GoldHopper` | 1 | 表怪 | 是 |
| `HealHopper` | 1、2 | 表怪 | 是 |
| `IvoryScooper` | 1 | 表怪／Boss遭遇 | 是 |
| `Knocker` | 1 | 表怪 | 是 |
| `Marionette` | 1 | 表怪 | 是 |
| `MarionetteKiller` | 2、3 | 表怪 | 是 |
| `MolArms` | 3 | 表怪 | 是 |
| `NeedleBird` | 1 | 表怪 | 是 |
| `OctopusTail` | 1 | 表怪 | 是 |
| `OctopusTailKiller` | 2 | 表怪 | 是 |
| `PileGolemEdge` | 2 | 表怪 | 是 |
| `PillarGuardian` | 3 | 表怪 | 是 |
| `RedCrimsonBossMonster` | 3 | Boss | 是 |
| `ShrimpCrab` | 3 | 表怪 | 是 |
| `SkullFeather` | 4 | Boss | 是 |
| `SlimySerpent` | 3 | 表怪 | 是 |
| `SmallHopper` | 1 | 表怪 | 是 |
| `TargetWireFrame` | 1 | 表怪 | 是 |
| `TargetWireFrameKiller` | 2 | 表怪 | 是 |
| `TriClaw` | 3 | 表怪 | 是 |
| `WireSlasher` | 1 | 表怪 | 是 |

明确排除：

| 类名或单位 | 理由 |
|---|---|
| `TezukaSaki` | 手冢咲，人类；即使在第一幕Boss遭遇中也不是星癌体 |
| `NararaShibaBossMonster` | 奈良良柴犬 |
| 原版 `MonsterModel` 实现 | 需求明确为非星癌体 |
| `RedCrimson` | 我方魔剑召唤物；敌方第三幕Boss的类名是 `RedCrimsonBossMonster` |
| `HotaruHappinessCannon`、`HotaruCoffinCannon`、`HotaruContestCannon`、`HotaruLongingCannon`、`HotaruAbsolutionCannon`、`HotaruFinalCannon` | 我方浮游炮 |
| 我方幻影、宠物或其他召唤物 | 不是敌方身份 |

运行边界：除了类型在表内，还必须为 `CombatSide.Enemy`、没有玩家本体／宠物主人。将表怪用作我方幻影也返回否；真实无主敌人仅多了某个Power不会改变其种类。敌方召唤的小怪仍是星癌体；死亡、Break、转阶段和HBR数值开关不会改变固有类别。未知的新怪不会因为继承 `IHbrgoMonster` 自动入表，新增时必须同时核对名单。

验证宿主：[validation/hbr-enemy-cancer/README.md](../validation/hbr-enemy-cancer/README.md)。宿主执行真实公开接口和原生四幕遭遇注册、全名单、原版模型与敌我身份边界；不启动Godot、不建立真实多人会话。最终结果绑定候选DLL的SHA-256。
