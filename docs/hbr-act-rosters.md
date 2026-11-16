# 三幕怪物名单

2026-11-05 按原版前三幕的池子规模补齐模组遭遇，编队照原作主线的出怪：

| 幕 | 原版 | 弱怪 | 普通 | 精英 | Boss | 合计 | 原有 | 新增 |
|---|---|---|---|---|---|---|---|---|
| 第一幕 | 密林 | 4 | 12 | 3 | 3 | 22 | 7 | 15 |
| 第二幕 | 巢穴 | 4 | 10 | 3 | 3 | 20 | 1（DeathSlug） | 19 |
| 第三幕 | 荣耀 | 3 | 9 | 3 | 3 | 18 | 1（RedCrimson） | 17 |

模组遭遇是**加进**原版池子，不替换原版遭遇。每一幕的登记表在 `Core/Models/Encounters/HbrgoAct{One,Two,Three}EncounterRegistry.cs`，由 `Core/Patches/HbrgoAct*EncounterPoolPatch.cs` 按幕序号（0、1、2）追加到原版的全部、弱怪、普通、精英、Boss 和怪物列表。设置里的“强制模组精英／Boss”只影响精英和 Boss；弱怪、普通怪仍和原版混在一起抽。

## 通用规则

数值写尖塔原生量级，HBR 下照现有规则自动换算：33类表怪的HP/DP按原作42个变种的比例分配，每点原生HP总量仍为900；SmallHopper参考14→HP8000/DP4800。未列入的原版怪和特殊Boss保持旧系数（HP×700，柴犬×1000，兼容DP每点原生HP200），攻击走敌方受击曲线（默认体偏），固定 DP 恢复 ×100，状态值和破坏率按地图位置和房型。怪物显示名沿用模组惯例，用原作的英文代号。

下表括号里是高难度（ToughEnemies 加血、DeadlyEnemies 加伤）的数值。

- 攻击下降、防御下降走模组的 `HbrPercentDebuffApplication`：HBR 下是独立的 HBR 能力，旧模式是原版虚弱、易伤。
- “目标有 DP 时再追加”的招式照 AbyssKnocker 的 RageWave 写：意图显示合计值，实际先打本体伤害，再对有 DP 的目标单独追加。这是 11-05 版的写法；改成原作出招表后，深渊重锤S（第二批）和 AbyssKnocker（第四批）都去掉了，现在没有招式这样写。

<!-- RESOURCE-TABLE-20261114:START -->
<a id="enemy-resource-table-20261114"></a>
## 默认 HP／DP 对照（2026-11-14）

以下为 HBR 数值开启、普通难度、单人、HP 设置 100%、外壳设置 20%、HP／DP 每幕成长均为 0 时的最大资源。每格范围依次对应这只怪原生 HP 的最小值、最大值；没有范围的值表示固定原生 HP。旧值为本次修改前的 HP×700、DP×200；新值保留每点原生 HP 总量 900 的校准基准，按所选原作变种的整数 HP／DP 比例分配。

`新最大 HP = floor(原生 HP × 900 × 原作 HP ÷ (原作 HP + 原作 DP))`；`新最大 DP = floor(新最大 HP × 原作 DP ÷ 原作 HP)`。DP 使用已经取整的新最大 HP，两个资源分别向下取整，合计可能产生取整差。原作表只提供比例，不直接搬入原作的绝对血量。

表中列出 33 类怪物的全部 42 个变种。相同比例的变种仍分别列出，供遭遇登记核对。CrestHopper 的群体降血标记先改变原生 HP，再应用同一资源比例；表内列的是未带该标记的默认单体范围。高难度、多人与配置增长的实际验证值另见本轮 `resource-profile-values.json`，不混入这张默认表。

| 怪物 | 原作变种 | 原生 HP | 旧 HP | 旧 DP | 新 HP | 新 DP |
|---|---|---:|---:|---:|---:|---:|
| 提线木偶 `Marionette` | `MarionetteDefaultVirtual` | 24–28 | 16,800–19,600 | 4,800–5,600 | 10,800–12,600 | 10,800–12,600 |
| 提线木偶 `Marionette` | `MarionetteSmallVirtual_first` | 24–28 | 16,800–19,600 | 4,800–5,600 | 10,800–12,600 | 10,800–12,600 |
| 猫角兽 `CatHorn` | `CatHornDefaultVirtual` | 30–34 | 21,000–23,800 | 6,000–6,800 | 18,000–20,400 | 9,000–10,200 |
| 猫角兽 `CatHorn` | `CatHornSmallVirtual_first` | 30–34 | 21,000–23,800 | 6,000–6,800 | 18,000–20,400 | 9,000–10,200 |
| 旋轮 `WireSlasher` | `WireSlasherDefaultDark` | 20–24 | 14,000–16,800 | 4,000–4,800 | 8,086–9,704 | 9,911–11,895 |
| 章鱼尾 `OctopusTail` | `OctopusTailDefaultVirtual` | 26–30 | 18,200–21,000 | 5,200–6,000 | 9,360–10,800 | 14,040–16,200 |
| 线圈巨轮 `TargetWireFrame` | `TargetWireFrameSmallVirtual` | 24–28 | 16,800–19,600 | 4,800–5,600 | 11,895–13,878 | 9,703–11,321 |
| 针嘴鸟 `NeedleBird` | `NeedleBirdSmallVirtual_MC02` | 82–86 | 57,400–60,200 | 16,400–17,200 | 39,260–41,175 | 34,538–36,223 |
| 鸦头马 `CrowHorse` | `CrowHorseDefaultVirtual` | 150 | 105,000 | 30,000 | 81,000 | 54,000 |
| 象牙异蛛 `IvoryScooper` | `IvoryScooperDefaultVirtual` | 180 | 126,000 | 36,000 | 122,727 | 39,272 |
| CrestHopper（嵴冠跳虫） `CrestHopper` | `CrestHopperDefaultVirtual` | 46–50 | 32,200–35,000 | 9,200–10,000 | 25,777–28,018 | 15,622–16,980 |
| HealHopper（治愈跳虫） `HealHopper` | `CrestHopperHealVirtual` | 14–18 | 9,800–12,600 | 2,800–3,600 | 7,845–10,086 | 4,754–6,112 |
| HealHopper（治愈跳虫） `HealHopper` | `CrestHopperHealVirtual_MC03` | 14–18 | 9,800–12,600 | 2,800–3,600 | 7,845–10,086 | 4,754–6,112 |
| Knocker（重锤） `Knocker` | `DarkKnockerSmallVirtual` | 38–42 | 26,600–29,400 | 7,600–8,400 | 24,428–27,000 | 9,771–10,800 |
| ClayKnocker（黏土重锤） `ClayKnocker` | `DarkKnockerDefaultVirtual` | 76–80 | 53,200–56,000 | 15,200–16,000 | 48,857–51,428 | 19,542–20,571 |
| AbyssKnocker（深渊重锤） `AbyssKnocker` | `AbyssKnockerDefaultVirtual_MC02` | 90–94 | 63,000–65,800 | 18,000–18,800 | 50,607–52,857 | 30,391–31,742 |
| 死亡玩偶／诅咒玩偶 `MarionetteKiller` | `MarionetteKiller2` | 44–48 | 30,800–33,600 | 8,800–9,600 | 19,800–21,600 | 19,800–21,600 |
| 死亡玩偶／诅咒玩偶 `MarionetteKiller` | `MarionetteKillerCurse` | 44–48 | 30,800–33,600 | 8,800–9,600 | 24,678–26,921 | 14,921–16,277 |
| 杀手跳虫 `CrestHopperKiller` | `CrestHopperKiller2` | 48–52 | 33,600–36,400 | 9,600–10,400 | 27,000–29,250 | 16,200–17,550 |
| 杀手重锤 `DarkKnockerKiller` | `DarkKnockerKillerVirtual` | 64–68 | 44,800–47,600 | 12,800–13,600 | 40,623–43,162 | 16,976–18,037 |
| 猫角兽γ `CatHornKiller` | `CatHornKillerVirtual` | 58–62 | 40,600–43,400 | 11,600–12,400 | 34,800–37,200 | 17,400–18,600 |
| 章鱼尾γ `OctopusTailKiller` | `OctopusTailKillerVirtual` | 46–50 | 32,200–35,000 | 9,200–10,000 | 16,479–17,912 | 24,919–27,086 |
| 深渊重锤S `AbyssKnockerKiller` | `AbyssKnockerKillerVirtual_MC03` | 150–156 | 105,000–109,200 | 30,000–31,200 | 84,489–87,869 | 50,509–52,530 |
| 线圈巨轮γ `TargetWireFrameKiller` | `TargetWireFrameKiller_Day2E01` | 90–94 | 63,000–65,800 | 18,000–18,800 | 52,650–54,990 | 28,350–29,610 |
| 石柱像 `PileGolemEdge` | `PileGolemEdgeSmall` | 170–176 | 119,000–123,200 | 34,000–35,200 | 103,186–106,827 | 49,813–51,571 |
| 钻石眼魔 `DiamondEyeball` | `DiamondEyeballDefaultVirtual_MC03_01` | 320 | 224,000 | 64,000 | 107,380 | 180,618 |
| BarrelWalker `BarrelWalker` | `BarrelWalkerDefaultFire_MC04_02` | 360 | 252,000 | 72,000 | 195,230 | 128,768 |
| 灯胆跳虫 `BalbFlea` | `BalbFleaDefault` | 52–56 | 36,400–39,200 | 10,400–11,200 | 28,800–31,015 | 18,000–19,384 |
| 三爪镖／三角镖 `TriClaw` | `TriClawSmall` | 58–62 | 40,600–43,400 | 11,600–12,400 | 18,792–20,088 | 33,408–35,712 |
| 三爪镖／三角镖 `TriClaw` | `TriClawDefault` | 58–62 | 40,600–43,400 | 11,600–12,400 | 20,076–21,461 | 32,121–34,337 |
| 异议之门 `BigotryGate` | `BigotryGateSmall_01` | 100–106 | 70,000–74,200 | 20,000–21,200 | 26,785–28,392 | 63,212–67,005 |
| 异议之门 `BigotryGate` | `BigotryGateSmall_02` | 100–106 | 70,000–74,200 | 20,000–21,200 | 26,785–28,392 | 63,212–67,005 |
| 异议之门 `BigotryGate` | `BigotryGateDefault_01` | 100–106 | 70,000–74,200 | 20,000–21,200 | 29,166–30,916 | 60,831–64,481 |
| 花冠利刃 `CorollaKnife` | `CorollaKnifeSmall_01` | 78–82 | 54,600–57,400 | 15,600–16,400 | 41,060–43,166 | 29,139–30,633 |
| 花冠利刃 `CorollaKnife` | `CorollaKnifeSmall_02` | 78–82 | 54,600–57,400 | 15,600–16,400 | 41,060–43,166 | 29,139–30,633 |
| 巨肌魔臂 `MolArms` | `MolArmsSmall` | 120–126 | 84,000–88,200 | 24,000–25,200 | 77,142–81,000 | 30,856–32,400 |
| 磐石异蟹 `ShrimpCrab` | `ShrimpCrabDefault_01` | 120–126 | 84,000–88,200 | 24,000–25,200 | 30,600–32,130 | 77,400–81,270 |
| 磐石异蟹 `ShrimpCrab` | `ShrimpCrabDefault_02` | 120–126 | 84,000–88,200 | 24,000–25,200 | 30,600–32,130 | 77,400–81,270 |
| 触手巨蛇 `SlimySerpent` | `SlimySerpentDefault` | 250–260 | 175,000–182,000 | 50,000–52,000 | 46,875–48,750 | 178,125–185,250 |
| 长颈怪环 `GiraffeHalo` | `GiraffeHaloDefault_01` | 240–250 | 168,000–175,000 | 48,000–50,000 | 166,153–173,076 | 49,845–51,922 |
| 阿拉克尼巨蛛 `ArachneLine` | `ArachneLineDefault` | 420 | 294,000 | 84,000 | 218,226 | 159,772 |
| 三柱守卫 `PillarGuardian` | `PillarGuardianDefault` | 460 | 322,000 | 92,000 | 306,666 | 107,333 |

### SmallHopper：独立校准

SmallHopper 不在上述 42 个原作变种中。它保留参考总量 12,800：原生 HP 14 的参考资源由 HP 9,800／DP 3,000 改为 HP 8,000／DP 4,800。普通难度的原生 HP 范围为 12–16，高难度为 13–17；实际掷出的原生 HP 仍参与换算。

| 原生 HP | 旧 HP | 旧 DP | 新 HP | 新 DP |
|---:|---:|---:|---:|---:|
| 12 | 8,400 | 2,571 | 6,857 | 4,114 |
| 13 | 9,100 | 2,785 | 7,428 | 4,456 |
| 14 | 9,800 | 3,000 | 8,000 | 4,800 |
| 15 | 10,500 | 3,214 | 8,571 | 5,142 |
| 16 | 11,200 | 3,428 | 9,142 | 5,485 |
| 17 | 11,900 | 3,642 | 9,714 | 5,828 |

GoldHopper 仍固定 HP 1／DP 10；未列入的原版怪物、RedCrimson 等特殊 Boss 保留兼容系数，奈良良柴犬仍为 HP×1000。玩家、宠物和 30A 幻影不进入这份敌方资源重分配。新的最大资源不会反向改写已保存的当前 DP，转阶段只对新的原始 HP 应用一次换算。

数据由[资源验证套件](../validation/hbr-enemy-resources/README.md)输出的 42 行实际资源证据生成，并用独立整数分数重新校验；下方既有遭遇与原作出招说明保留；其中 2026-11-08 章节的 HP 9,800／DP 3,000 等资源描述属于当时记录，当前资源以上表为准，招式与伤害不因本次重新分配而变化。

<!-- RESOURCE-TABLE-20261114:END -->

## 第一幕

### 遭遇

| 房型 | 遭遇 | 编队 | 原作出处 |
|---|---|---|---|
| 弱怪 | 一具提线木偶 `MarionetteWeak` | 提线木偶（教学关变种） | MC01_01_Battle01 |
| 普通 | 三只杂鱼 `SmallHoppersNormal` | SmallHopper ×3 | MC01_12_Battle01b |
| 普通 | 嵴冠跳虫群 `CrestHopperSwarmNormal` | CrestHopper ×3（降血，见下） | MC01_12_Battle01event |
| 普通 | 成对的猫角兽 `CatHornsNormal` | 猫角兽 ×2（α 变种） | MC01_05_Battle01 |
| 普通 | 木偶剧场 `MarionettesWithHealerNormal` | 提线木偶 + HealHopper + 提线木偶 | MC02_10_Battle01 |
| 普通 | 章鱼尾小队 `OctopusTailSquadNormal` | CrestHopper + 章鱼尾 + HealHopper | MC02_10_Battle02 |
| 普通 | 线圈巨轮小队 `TargetWireFrameSquadNormal` | CrestHopper + 线圈巨轮 + HealHopper | MC02_11_Battle01 |
| 普通 | 重锤与治愈 `KnockersWithHealerNormal` | Knocker + HealHopper + Knocker | MC02_12_Battle02 |
| 普通 | 木偶与猫角兽 `MarionetteAndCatHornsNormal` | 猫角兽 + 提线木偶 + 猫角兽 | MC02_13_Battle01 |
| 普通 | 两条章鱼尾 `OctopusTailsNormal` | 章鱼尾 ×2 | MC02_14_Battle01 |
| 普通 | 线圈巨轮阵 `TargetWireFramesNormal` | 线圈巨轮 + CrestHopper + 线圈巨轮 | MC02_14_Battle02 |
| 普通 | 三连旋轮 `WireSlashersNormal` | 旋轮 ×3 | MC02_25 |
| 精英 | NeedleBird `NeedleBirdElite` | 针嘴鸟 | MC02_20_Battle01 |
| Boss | CrowHorse `CrowHorseBoss` | 鸦头马 | MC02_15_Battle01 |
| Boss | IvoryScooper `IvoryScooperBoss` | 象牙异蛛 | MC02_21_Battle02 |

原作的 `MC02_12_Battle02` 是两只黏土重锤；这里换成两只 Knocker，避免和精英“大锤小锤”重复。

### 新怪物

| 怪物 | 原生 HP | 招式循环 |
|---|---|---|
| 提线木偶 Marionette | 24–28（26–30） | 按原作出招表（第三批）：普通攻击 6（7）、普通攻击 8（9）、回复（最大 DP 的 16.7%）；弱怪战用教学关变种（没有 hp 表，回复 30%） |
| 猫角兽 CatHorn | 30–34（32–36） | 按原作出招表（第三批）：双角突刺 6（7）、Creepy Ball 8（9），命中即施加封印、横扫 3（4）×2（全体、精偏）、回复（最大 DP 的 13%）；成对的猫角兽用 α 变种（回复 23%） |
| 旋轮 WireSlasher | 20–24（22–26） | 按原作出招表（第三批，暗）：普通攻击 6（7）、连击 1×7、全体攻击 7（9）、回复（最大 DP 的 9.2%） |
| 章鱼尾 OctopusTail | 26–30（28–32） | 按原作出招表（第三批）：尾鞭 6（6）、Erosion Slice 7（9），命中即施加病毒、全体攻击 7（8，精偏）、回复（最大 DP 的 8.3%） |
| 线圈巨轮 TargetWireFrame | 24–28（26–30） | 按原作出招表（第三批，α）：线圈射击 6（6）、Illusion Spin 8（9），命中即施加幻惑 1 层、散射 7（8，全体、精偏）、回复（最大 DP 的 11%） |
| 针嘴鸟 NeedleBird（精英） | 82–86（86–90） | 按原作出招表（见「原作出招表」一节）：啄击 14（17）、Murder Blade 16（19，原作全体、精偏）、蓄力、Ruin Bullet（蓄力中才放，目标 DP 清零）、整备（回复最大 DP 的 2.5%） |
| 鸦头马 CrowHorse（Boss） | 150（160） | 按原作出招表（第二批）：践踏 12（14）、Destruction Ball 15（18）+ OD 下降 5%（10%）、Extinction Ray 13（16，全体、精偏）、回复（最大 DP 的 13.9%，只在 Break 的 hp 表里） |
| 象牙异蛛 IvoryScooper（Boss） | 180（192） | 按原作出招表（第二批）：掘取 7（9）、Energy Drain 3（4）×3，按打进 DP 和生命的伤害吸取 DP、Destroy Bash 9（10，全体、精偏）；没有回复技能 |

嵴冠跳虫群的三只带 `IsSwarmMember` 标记，原生 HP 46–50 降为 26–30（高难度 28–32），否则三只合计 147 HP，远超普通战。标记由遭遇在生成怪物时设置，并随存档保存。

普通战的原生 HP 合计在 36–106 之间，原版密林普通战大约 42–95；弱怪 24–28；精英 82–86；Boss 150、180（原版密林 Boss 173–306，手冢咲 100）。

### 30A 融合

| 怪物 | 融合效果（首次，之后每次） |
|---|---|
| 提线木偶 | 伤害减免 +3%（+1%） |
| 猫角兽 | 伤害增幅 +4%（+2%） |
| 旋轮 | 第 1 次攻击意图段数 +1（+1） |
| 章鱼尾 | 最大偏导护盾 +2（+1），HBR 下 ×100 |
| 线圈巨轮 | 每第 3 次攻击意图额外造成 4（+2）点伤害 |
| 针嘴鸟 | 伤害增幅 +8%（+3%） |
| 鸦头马 | 伤害增幅 +10%（+4%） |
| 象牙异蛛 | 最大生命值 +8（+4），HBR 下 ×100 |

## 第二幕

编队照原作第 3 章（训练机和金峰山探索）加第 4 章开头（云取山）的出怪。

### 遭遇

| 房型 | 遭遇 | 编队 | 原作出处 |
|---|---|---|---|
| 弱怪 | 死亡玩偶 `MarionetteKillerWeak` | 死亡玩偶 | MC03ExpMtKinpu01_R01 |
| 弱怪 | 杀手跳虫 `CrestHopperKillerWeak` | 杀手跳虫 | MC03ExpMtKinpu01_R03 |
| 弱怪 | 杀手重锤 `DarkKnockerKillerWeak` | 杀手重锤 | MC03ExpTraining01Virtual_R04 |
| 弱怪 | 两具死亡玩偶 `MarionetteKillersWeak` | 死亡玩偶 ×2 | MC03ExpMtKinpu01_R02 |
| 普通 | 杀手跳虫与玩偶 `CrestHopperKillerAndMarionettesNormal` | 死亡玩偶 + 杀手跳虫 + 死亡玩偶 | MC03ExpMtKinpu01_R04 |
| 普通 | 训练机小队 `KillerTrainingSquadNormal` | 死亡玩偶 + HealHopper + 杀手跳虫 | MC03ExpTraining01Virtual_R02 |
| 普通 | 重锤小队 `DarkKnockerKillerSquadNormal` | 杀手重锤 + HealHopper + 杀手跳虫 | MC03ExpTraining01Virtual_R05 |
| 普通 | 猫角兽γ与治愈跳虫 `CatHornKillerWithHealersNormal` | HealHopper + 猫角兽γ + HealHopper | MC03ExpTraining01Virtual_S01 |
| 普通 | 杀手跳虫群 `CrestHopperKillersNormal` | 杀手跳虫 ×3 | MC03ExpMtKinpu02_R03 |
| 普通 | 两台杀手重锤 `DarkKnockerKillersNormal` | 杀手重锤 ×2 | MC03ExpTraining02Virtual_R05 |
| 普通 | 重锤与死亡玩偶 `DarkKnockerKillerAndMarionettesNormal` | 死亡玩偶 + 杀手重锤 + 死亡玩偶 | MC03ExpMtKinpu02_R05 |
| 普通 | 成对的猫角兽γ `CatHornKillersNormal` | 猫角兽γ ×2 | MC03_15_Battle02 |
| 普通 | 章鱼尾γ `OctopusTailKillersNormal` | 章鱼尾γ + 杀手跳虫 + 章鱼尾γ | MC03_17_Battle01 |
| 普通 | 死亡玩偶与猫角兽γ `MarionetteKillerAndCatHornsNormal` | 猫角兽γ + 死亡玩偶 + 猫角兽γ | MC03_19_Battle01 |
| 精英 | AbyssKnockerKiller `AbyssKnockerKillerElite` | 深渊重锤S | MC03_18_Battle01 |
| 精英 | 线圈巨轮γ `TargetWireFrameKillerElite` | 杀手跳虫 + 线圈巨轮γ + 杀手跳虫 | 编队 MC03ExpMtKinpu02_E02；招式照线圈巨轮γ `TargetWireFrameKiller_Day2E01`（MC04ExpMtKumotori01_E01），见「原作出招表（第二批）」 |
| 精英 | PileGolemEdge `PileGolemEdgeElite` | 石柱像 | MC04ExpMtKumotori01_S01 |
| Boss | DiamondEyeball `DiamondEyeballBoss` | 钻石眼魔 | MC03ExpTraining02Virtual_BOSS01 |
| Boss | BarrelWalker `BarrelWalkerBoss` | BarrelWalker | MC04ExpMtKumotori01_BOSS01 |

### 新怪物

| 怪物 | 原生 HP | 招式循环 |
|---|---|---|
| 死亡玩偶 MarionetteKiller | 44–48（47–51） | 按原作出招表（第三批）：普通攻击 11（13）、普通攻击 13（14）、回复（最大 DP 的 35%）；门与诅咒玩偶里是诅咒玩偶：普通攻击 8（8）、Curse Stamp 9（10）+ 灾厄 20（30）层、攻击力上升 100%、回复（回满） |
| 杀手跳虫 CrestHopperKiller | 48–52（51–55） | 按原作出招表（第三批）：普通攻击 6（8）、连击 1×8、修复α（最大 DP 的 25%） |
| 杀手重锤 DarkKnockerKiller | 64–68（68–72） | 按原作出招表（第三批）：普通攻击 8（8）、连续攻击 5×2、全体攻击 11（12）；没有回复 |
| 猫角兽γ CatHornKiller | 58–62（62–66） | 按原作出招表（第三批）：双角突刺 9（10）、Creepy Ball 12（13），命中即施加封印、横扫 5（6）×2（全体、精偏）、回复（最大 DP 的 17.5%） |
| 章鱼尾γ OctopusTailKiller | 46–50（49–53） | 按原作出招表（第三批）：尾鞭 8（9）、Erosion Slice 11（12），命中即施加病毒、全体攻击 9（10，精偏）、回复（最大 DP 的 11%） |
| 深渊重锤S AbyssKnockerKiller（精英） | 150–156（160–166） | 按原作出招表（第二批）：普通攻击 13（15）、连续攻击 8（9）×2、Rage Wave 15（17，全体、精偏，不再“目标有 DP 时再追加”）、回复（最大 DP 的 27%） |
| 线圈巨轮γ TargetWireFrameKiller（精英） | 90–94（96–100） | 按原作出招表（第二批，γ）：线圈射击 12（13）、Illusion Spin 16（18），命中即施加幻惑 1 层、散射 14（16，全体、精偏）、回复（最大 DP 的 71%） |
| 石柱像 PileGolemEdge（精英） | 170–176（180–186） | 按原作出招表（第二批）：崩落 10（11，全体）、震波 11（13，全体、精偏）、Confusion Spike 12（14），命中即施加混乱 3 回合、攻击力上升 100%、回复（回满 DP） |
| 钻石眼魔 DiamondEyeball（Boss） | 320（340） | 按原作出招表：Laser Bind 13（15），命中即施加束缚；棱光射线 17（19），30%（高难37.5%）基础防御下降；Disaster Ray 3×5（原作全体、精偏）；晶面护盾（回复最大 DP 的 2.1%） |
| BarrelWalker（Boss） | 360（380） | 按原作出招表（第二批）：炮击 14（16）、重炮 17（20，打 DP 最低的玩家）、Rage Magnum 2×8（全体、精偏）+ 25%（高难31.25%）基础防御下降、攻击力上升 100%、回复（最大 DP 的 48%） |

原生 HP 合计：弱怪 44–96，普通 86–172，精英 150–198，Boss 320、360。

### 30A 融合

| 怪物 | 融合效果（首次，之后每次） |
|---|---|
| 死亡玩偶 | 伤害减免 +4%（+1%） |
| 杀手跳虫 | 伤害增幅 +6%（+2%） |
| 杀手重锤 | 每第 3 次攻击意图额外造成 6（+3）点伤害 |
| 猫角兽γ | 伤害增幅 +6%（+3%） |
| 章鱼尾γ | 最大偏导护盾 +3（+1），HBR 下 ×100 |
| 深渊重锤S | 每第 3 次攻击意图额外造成 8（+4）点伤害 |
| 线圈巨轮γ | 第 1 次攻击意图段数 +1（+1） |
| 石柱像 | 最大生命值 +10（+5），HBR 下 ×100 |
| 钻石眼魔 | 伤害增幅 +12%（+5%） |
| BarrelWalker | 第 1 次攻击意图段数 +2（+1） |

## 第三幕

编队照原作第 4 章（云取山、雾峰、巡逻和第 4 章下的沙漠）的出怪。第二幕的死亡玩偶在“门与诅咒玩偶”里再次出现，照原作木曾驹关卡。

### 遭遇

| 房型 | 遭遇 | 编队 | 原作出处 |
|---|---|---|---|
| 弱怪 | 灯胆跳虫与三爪镖 `BalbFleaAndTriClawWeak` | 灯胆跳虫 + 三爪镖 | MC04ExpMtKumotori01_R01 |
| 弱怪 | 异议之门 `BigotryGateWeak` | 异议之门 | MC04ExpMtKumotori01_R03 |
| 弱怪 | 花冠利刃与三爪镖 `CorollaKnifeAndTriClawWeak` | 花冠利刃 + 三爪镖 | MC04ExpMtKumotori01_R04 |
| 普通 | 三爪镖与灯胆跳虫 `TriClawAndBalbFleasNormal` | 灯胆跳虫 + 三爪镖 + 灯胆跳虫 | MC04ExpMtKumotori01_R02 |
| 普通 | 异议之门小队 `BigotryGateSquadNormal` | 灯胆跳虫 + 异议之门 + 三爪镖 | MC04ExpMtKumotori01_R05 |
| 普通 | 花冠利刃小队 `CorollaKnifeSquadNormal` | 花冠利刃 + 三爪镖 + 灯胆跳虫 | MC04ExpKirigamine01_01_R05 |
| 普通 | 两扇异议之门 `BigotryGatesNormal` | 异议之门 ×2 | MC04ExpKirigamine02_01_R05 |
| 普通 | 成对的花冠利刃 `CorollaKnivesNormal` | 花冠利刃 ×2（第二把 _02） | MC04ExpKirigamine02_02_R05 |
| 普通 | 门与花冠 `BigotryGateAndCorollaKnivesNormal` | 花冠利刃 + 异议之门 + 花冠利刃（第二把 _02） | MC04ExpKirigamine02_05_R04 |
| 普通 | 巨肌魔臂与三爪镖 `MolArmsAndTriClawNormal` | 巨肌魔臂 + 三爪镖（三角镖） | MC04BExpDay03Desert01_R05 |
| 普通 | 两只巨肌魔臂 `MolArmsPairNormal` | 巨肌魔臂 ×2（第二只错开一步） | MC04BExpDay05Desert01_R05 |
| 普通 | 门与诅咒玩偶 `BigotryGateAndMarionetteKillersNormal` | 诅咒玩偶 + 异议之门（胜利之门） + 诅咒玩偶 | MC04BExpDay01Kisokoma01_R03 |
| 精英 | 两只磐石异蟹 `ShrimpCrabsElite` | 磐石异蟹 ×2 | MC04ExpKirigamine02_01_BOSS01 |
| 精英 | SlimySerpent `SlimySerpentElite` | 触手巨蛇 | MC04BExpDay04Desert01_S01 |
| 精英 | GiraffeHalo `GiraffeHaloElite` | 长颈怪环 | MC04BExpDay01Kisokoma01_BOSS01 |
| Boss | ArachneLine `ArachneLineBoss` | 阿拉克尼巨蛛 | MC04ExpPatrol02_BOSS01 |
| Boss | PillarGuardian `PillarGuardianBoss` | 三柱守卫 | MC04BWilDay04MilitaryFacility01_BOSS01 |

### 新怪物

| 怪物 | 原生 HP | 招式循环 |
|---|---|---|
| 灯胆跳虫 BalbFlea | 52–56（56–60） | 按原作出招表（第三批）：冲撞 9（10）、闪光 13（14，全体、精偏）、攻击力上升 100%、回复（回满） |
| 三爪镖 TriClaw | 58–62（62–66） | 按原作出招表（第三批）：飞镖 15（17）、连爪 4（5）×5（精偏）、攻击力上升 100%、回复（最大 DP 的 78%）；巨肌魔臂与三爪镖里是三角镖：连爪 4×5、回满 |
| 异议之门 BigotryGate | 100–106（106–112） | 按原作出招表（三种变种）：冲击 4×3、Disaster Shot 14（16）+ 灾厄 20（30）层、Strong Impulse 19（22）+ 精神下降（原作全体、精偏）、攻击力上升 20%、回复（最大 DP 的 85%，胜利之门 68%） |
| 花冠利刃 CorollaKnife | 78–82（83–87） | 按原作出招表（第三批）：斩击 10（11）、Rotation Cut 13（15）+ 30%（高难37.5%）基础防御下降、Rampage Slice 6（7）×2（全体、精偏）、攻击力上升 100%；没有 hp 表和回复，两把同场时第二把用 _02 |
| 巨肌魔臂 MolArms | 120–126（128–134） | 按原作出招表（第三批）：双臂猛击 2×5、Deceive Shot 4×3 + 知性下降 3 回合、全体攻击 12（13，精偏）、攻击力上升 100%、回复（最大 DP 的 83%）；两只同场时第二只错开一步 |
| 磐石异蟹 ShrimpCrab（精英） | 120–126（128–134） | 按原作出招表（两种变种）：钳击 26（30）、Mirage Pulse（封印，攻击下降、防御下降各20%（高难25%）基础）、Steel Shield（防御力上升 75%）、回复（最大 DP 的 70%） |
| 触手巨蛇 SlimySerpent（精英） | 250–260（266–276） | 按原作出招表（第二批）：尾击 7（8）×2（全体）、Destruction Rush 17（19，全体、精偏）+ OD 下降 5%（10%）、Disease Plant 15（17）+ 30%（高难37.5%）基础攻击下降、攻击力上升 100%、回复（最大 DP 的 20%） |
| 长颈怪环 GiraffeHalo（精英） | 240–250（256–266） | 按原作出招表（第二批）：长颈鞭 13（15）、环光弹 7（8）×2（全体）、Judgment Ray 3（4）×5（全体），命中即施加幻惑 2 层、攻击力上升 100%、回复（回满 DP） |
| 阿拉克尼巨蛛 ArachneLine（Boss） | 420（450） | 按原作出招表（第二批）：八足连斩 13（15）×2、Absorb Whip 16（18）×2，吸取 DP、Piercing Flash 4×8（全体、精偏）+ 30%（高难37.5%）基础防御下降、攻击力上升 20%、回复（最大 DP 的 37%） |
| 三柱守卫 PillarGuardian（Boss） | 460（490） | 按原作出招表：柱击 8（9）×2 + 持续 DP 伤害 4 回合、Destruction Spear 4（5）×4、Paralysis Burst 4（5）×5，命中即施加眩晕（原作全体）、攻击力上升 100%、回复（最大 DP 的 71%） |

原生 HP 合计：弱怪 100–144，普通 156–270，精英 240–260，Boss 420、460。

### 30A 融合

| 怪物 | 融合效果（首次，之后每次） |
|---|---|
| 灯胆跳虫 | 伤害减免 +4%（+2%） |
| 三爪镖 | 第 1 次攻击意图段数 +1（+1） |
| 异议之门 | 最大偏导护盾 +4（+2），HBR 下 ×100 |
| 花冠利刃 | 伤害增幅 +7%（+3%） |
| 巨肌魔臂 | 每第 3 次攻击意图额外造成 10（+5）点伤害 |
| 磐石异蟹 | 伤害减免 +5%（+2%） |
| 触手巨蛇 | 最大生命值 +12（+6），HBR 下 ×100 |
| 长颈怪环 | 伤害增幅 +10%（+4%） |
| 阿拉克尼巨蛛 | 伤害增幅 +14%（+6%） |
| 三柱守卫 | 最大偏导护盾 +12（+6），HBR 下 ×100 |

## 30A 幻影

新怪都能被 30A 召唤成幻影，融合效果接进现有的几类汇总：伤害增幅／减免、最大生命值、最大偏导护盾由 `PhantomFusionEffectHelper` 汇总；“第 1 次攻击意图段数”和“每第 3 次攻击意图额外伤害”在 `PhantomHelper` 生成攻击段时读取。最大生命值和最大偏导护盾在 HBR 下 ×100。

幻影的攻击意图按意图重放伤害，不执行招式本体。28 只新怪和第一幕原有的 7 只都按原作出招表，带附加效果的攻击招实现 `IHbrgoPhantomAttackResults`，`PhantomHelper` 重放后传入被打的敌人、其中伤害打过格挡的敌人，以及打进 DP 和生命的伤害（吸取用），每次意图一次：

- 第一批：Laser Bind、棱光射线、Disaster Shot、Strong Impulse、柱击、Paralysis Burst；
- 第二批：Destruction Ball、Energy Drain、Illusion Spin、Confusion Spike、Rage Magnum、Destruction Rush、Disease Plant、Judgment Ray、Absorb Whip、Piercing Flash；
- 第三批：Creepy Ball、Erosion Slice、Illusion Spin（α）、Curse Stamp、Rotation Cut、Deceive Shot。

11-05 版用的旧接口 `IHbrgoPhantomAttackEffects`（牵线、缠腕、诅咒牵线、闪光等）现在没有怪实现，`PhantomHelper` 里的分支留着没删。

原作全体招在重放时打所有敌人，重炮（DP 最低）打外壳 DP 最低的敌人。各效果打到敌人时的规则见「原作出招表」两节。

非攻击招式（蓄力、Ruin Bullet、整备、晶面护盾、攻击力上升、Mirage Pulse、Steel Shield、各怪的回复）照原样执行，目标是敌人。第四批的 HealHopper 修复β 回幻影这一边（自己、别的召唤物和召唤者的偏导护盾），GoldHopper 幻影不逃跑，见「原作出招表（第四批）」。

## 2026-11-15 用户指定的敌方加强（需求六至九）

仅修改HBR数值模式下的控制附加、行动锁、减益强度／玩家计时与普通外壳Break时间；旧模式的控制仍要求通过格挡，束缚／眩晕仍失去[E]，普通外壳仍3／4／5回合。没有改变怪物的出招表、概率权重、伤害、生命／DP倍率或破坏率。

特殊Boss的公共攻降／防降入口：真正敌方施加者分别以25%／50%为基础，再走现有属性公式；FlatHand、柴犬、两只Red Crimson、SkullFeather、手冢咲原有时长与整场效果不变。Red Crimson、SkullFeather的玩家幻影重放仍用15%。

HBR模式普通外壳依房型取2／3／4回合，旧模式仍为3／4／5；Hello World的初始Break奖励继续相加。SkullFeather专属时长、手冢咲永久Break、柴犬三层DP、幻影不进Break、DP归零不眩晕等独立规则不变。

## 原作出招表（第一批，2026-11-07）

针嘴鸟、钻石眼魔、异议之门、磐石异蟹、三柱守卫改成按原作的出招表出招，方案见 [hbr-monster-mechanics-proposal.md](hbr-monster-mechanics-proposal.md)。第二批、第三批（2026-11-08）把其余 23 只也改了，见后两节；28 只新怪现在都按原作出招表。第四批（2026-11-08）又把第一幕原有的 7 只改了，手冢咲等 7 个先不改。HP 不变（HBR 下仍 ×700）。

- 构建器 `Core/Models/Monsters/HbrgoMoveTable.cs`：
  - 每只怪填原作的 dp 表和 hp 表。同一步有几个选项时按原作权重随机，用原版怪物 AI 的随机数，联机同步；只有一个选项的步不抽随机数。
  - 「一次」的步骤只走一遍。条件出招（针嘴鸟蓄力中才放 Ruin Bullet）不满足时跳过这一步。
  - DP 被打空：当前意图照常放出，下一招起从 hp 表第 1 步开始。没有 hp 表的异议之门 dp 表照常往下走。2026-11-09 起打空 DP 不再眩晕，见下「打空 DP 不再眩晕」；之前是先眩晕一回合、被打断的那招不再放。
  - Break 结束、外壳回来：回到 dp 表第 1 步，用过的「一次」跳过。外壳回来的那个回合，怪物放的是 Break 中已经定好的招（意图不中途换）。
  - 当前表、步数、用过的「一次」和变种存成怪物的存档属性，蓄力是能力。
- 变种：两扇异议之门的第二扇用 BigotryGateSmall_02，「门与诅咒玩偶」的门用胜利之门 BigotryGateDefault_01，其余用 BigotryGateSmall_01；两只磐石异蟹分别用 _01 和 _02。
- 全体招式（Murder Blade、Disaster Ray、Strong Impulse、Paralysis Burst）第一批先照单体打（原版怪物攻击本来就打全部玩家角色），招式上标了 `HbrgoMoveTarget.All`；第二批做了全体入口，这四招已切过去（见下一节）。精偏招式按原作标进 `HbrEnemySkillProfiles`。
- 2026-11-15 用户改定：Break 时长为普通2、精英3、Boss4回合。按现有流程，Break 中普通怪放1招 hp 表、精英2招、Boss3招，均算上外壳回来那回合。
- 伤害取方案的 `calibrated.json`：每回合平均伤害和 11-05 版持平。
- 回复 DP 按「原作回复量 ÷ 原作最大 DP」乘这只怪的最大外壳。Break 中没有外壳，不回。

### 新效果

| 能力 | 原作 | 模组角色、HBR | 模组角色、旧模式 | 原版角色 | 被 30A 召唤成幻影时 |
|---|---|---|---|---|---|
| 蓄力 | Charge | 自身；被眩晕或放出 Ruin Bullet 后移除（2026-11-09 起打空 DP 不再眩晕，也就不再清掉蓄力） | 同左 | 同左 | 幻影自己蓄力 |
| 攻击力上升 | AttackUp | 最终伤害 +20%／+100%，到持有者下一回合结束；意图显示加成后的数 | 同左 | 同左 | 幻影自己 |
| 防御力上升 | DefenseUp | 受到的攻击伤害 −75%，记进防御力下降的加算区 | 单独乘 ×0.25 | 同旧模式 | 幻影自己 |
| 精神下降 | SpiritDownRate | 精神 ×0.5，2 回合 | 受到的攻击伤害 +25% | 同旧模式 | 敌人受到的攻击伤害 +25% |
| 封印 | ImprisonRandom | 持有者回合内不能手动打出 2 费及以上（含 X 费）的牌 | 同左 | 同左 | 敌人下一招不是「不带附加效果的单段攻击」就跳过，显示为眩晕 |
| 束缚、眩晕 | BindRandom、StunRandom | 2026-11-15改为下回合能量重置后失去[EE]；各份独立计时，但多份只扣一次 | 仍失去[E]，原计时与份数不变 | 同旧模式 | 敌人眩晕一回合，之后接着放原来的招；每个敌人每场一次 |
| 持续 DP 伤害 | ContinuousDpDamageRate | 回合开始每份失去最大 DP 的5%，共4次，只扣DP；不限份数 | 同左 | 不施加（没有 DP） | 敌人外壳每回合失去最大外壳的 5%，打空照常 Break |
| DP 破坏（命令） | DpBreakRandom | 偏导护盾清零 | 同左 | 无效 | 敌人当前 DP 减半 |

- Disaster Shot 的灾厄用现有能力，只在 HBR 数值下施加；旧模式和原版角色不施加（它不降属性，图标挂着会误导）。
- 2026-11-15用户改定：HBR敌方封印、束缚、眩晕命中即施加，格挡完全吸收也生效；仍不对召唤物施加。Mirage Pulse照常附加。30A幻影重放仍要求伤害打过格挡；吸取仍读取实际打过格挡的伤害记录。
- 精神下降和以后的知性下降共用 `HbrStatRateDownPower`。知性下降第一批没做。
- 图标取原作 `Assets/UI/Texture/BattleIcon/`，来源写在 `images/powers/*.sources.json`。原作升降共用一张底图，所以升降都加了程序画的箭头角标：攻击力上升、防御力上升是橙红色向上箭头，精神下降和已有的攻击力下降、防御力下降（2026-11-08 按用户「也补上」补的）是蓝色向下箭头，箭头以外的像素不变。

### 2026-11-14 敌方状态份数与计时

此段更新HBR模式；旧模式等效适配及幻影的特殊规则保留。

| 来源／状态 | 档案与实际时机 | 多份行为 |
|---|---|---|
| 钻石眼魔棱光射线、花冠利刃Rotation Cut、阿拉克尼Piercing Flash、触手巨蛇Disease Plant的攻降／防降 | None／EnemyTurnEnd 2 | 不限份数、各自计时 |
| BarrelWalker Rage Magnum、磐石异蟹Mirage Pulse的攻降／防降 | None／EnemyTurnEnd 4 | 不限份数、各自计时 |
| 蓄力 | None／Eternal，放出蓄力招或被眩晕时清除 | 多份不额外加强 |
| 封印、束缚 | None／EnemyTurnEnd 2，施加敌方回合结束也算1次 | 开关类不加重 |
| 玩家持有的病毒、幻惑 | 2026-11-15改为None／PlayerTurnEnd 2 | 病毒强度、幻惑层数相加；敌方持有保留EnemyTurnEnd 2 |
| 眩晕 | None／EnemyTurnStart 1 | 多份束缚和眩晕共同只扣一次[EE] |
| 混乱 | 2026-11-15玩家持有改为None／PlayerTurnEnd 3，覆盖接下来3个自身回合；敌方持有保留EnemyTurnEnd 3 | 每回合第一张单体攻击牌仅改目标一次 |
| 持续DP伤害 | None／持有者回合开始触发4次，每份5%最大DP | 两份各扣一次，合计10%；没有DP不施加 |

2次退出条件在施加敌方回合末减为1，玩家下一回合仍有效；到下一个敌方回合结束才移除。4次再延长两轮。2026-11-15用户改定，六只表怪敌方施加的攻降／防降基础取原作上限：钻石眼魔、花冠利刃、阿拉克尼、触手巨蛇30%，BarrelWalker25%，磐石异蟹两类各20%；原多一层的高难档分别为37.5%、31.25%、25%，再进入现有属性公式。计时与不限份数政策不变。30A幻影重放仍按原层数换算，保留默认时长和两份政策。

病毒两份降低60%最大DP，四份最低降到0；超出的DP失去，恢复上限不返还。幻惑每次落空先从最早到期的一份扣1层。各份都有独立保存的强度、退出条件与剩余次数。图标右上显示剩余次数，右下显示强度；开关类不显示强度。2026-11-15起HBR敌方控制状态命中即施加，不看格挡与原作概率；30A幻影侧的通过格挡条件不变。

### 备用效果（还没有怪用）

- **持续伤害**（ContinuousDamageRate）：默认每份15%最大生命，持有者回合开始结算，共4次。先扣偏导护盾或高维外壳，DP不足部分扣生命；无视格挡，不走六维属性、技能或掷骰公式。每份独立、不限份数，百分比和次数可由以后接入的招式指定。施给敌人同样先扣外壳，打空照常Break。
- **能量流失**（ContinuousSpDamage等）：持有者回合开始、原生能量重置之后失去指定能量，默认[EE]、共4次。每份各自扣除，不限份数；次数1表示下回合扣一次。只给玩家，不给敌人和召唤物。本次不做把SP／能量设成指定值。
- 持续DP伤害、持续伤害以OwnerTurnStart次数保存，保留4次实际触发；否则若在施加敌方回合末先减一次，会错误只触发3次。原作来源None／EnemyTurnEnd 4保留在参考说明中。能量流失的实际消费时点延后到同一回合的原生能量重置之后。
- 两张图标原样复制原作64×64 RGBA，无改像素：`hbr_continuous_damage_power.png`、`hbr_energy_drain_power.png`；各自 `.sources.json` 记录bundle、path_id、原始路径和SHA-256。能量图标沿用原作SP字样。

## 原作出招表（第二批，2026-11-08）

鸦头马、象牙异蛛、深渊重锤S、线圈巨轮γ、石柱像、BarrelWalker、触手巨蛇、长颈怪环、阿拉克尼巨蛛也改成按原作的出招表出招（用户原话「其它的怪也做一下（？」）。构建器、Break（2026-11-15改为普通2、精英3、Boss4回合）、回复 DP 的算法和第一批相同；HP 不变；伤害取方案的 `calibrated.json`（每回合平均伤害和 11-05 版持平），27 招逐招对过。9 只都有 hp 表，Break 后从 hp 表第 1 步开始。剩下 14 种普通怪和弱怪在第三批改完，见下一节。

### 全体攻击入口

用户定的范围：「怪物的全体招打玩家一方的全部单位，包括 30A 的幻影和其他召唤物；幻影放全体招时打所有敌人；动画、意图预览、幻影重放都要对」。

- 怪物一侧：攻击都走 `HbrgoStaticMonster.PerformAttackOn(伤害, 段数, 范围)`，`PerformAttack` 等于范围 `Players`。仍是原版的 `AttackCommand`，只在原版取目标的 `AttackCommand.GetPossibleTargets` 后接一个补丁（`Core/Patches/Combat/HbrEnemyAttackPatches.cs` 的 `HbrAttackTargetingPatch`），给标了范围的这一次攻击换目标列表：
  - `Players`：原版行为，全部玩家角色，不含召唤物。没标范围的攻击（其余怪物、原版怪物）都是这个。
  - `AllUnits`：全部玩家角色，加上玩家一方活着、可被攻击的召唤物（30A 的幻影、亡灵契约师的 Osty 等）。蛍的炮台本来就不可被攻击（`HotaruCannonTargetabilityPatch`），不在内。
  - `MinimumDp`：活着的玩家里偏导护盾 DP 最低的一个；没有偏导护盾的原版角色算 0。只有 BarrelWalker 的重炮用。
  - 动画、受击特效、每段重新取活着的目标、伤害记录和钩子都还是原版流程；意图照原版显示对本地玩家的伤害。
- 第一批留了标记的四招（Murder Blade、Disaster Ray、Strong Impulse、Paralysis Burst）一起切到 `AllUnits`。Strong Impulse 的精神下降跟着给范围内的单位；Paralysis Burst 的眩晕只给打过格挡的玩家。
- 幻影一侧：`PhantomHelper` 重放攻击意图时读这招的原作目标：全体招打所有敌人（同 30A「横扫」的打法），重炮在没指定目标时打外壳 DP 最低的敌人（Break 中没有外壳，算 0）。
- 状态给召唤物的规则：攻击力下降、防御力下降、精神下降、灾厄、持续 DP 伤害这类数值减益照样给；作用于出牌、能量或超频的状态（封印、束缚、眩晕、幻惑、混乱、OD 下降）只给玩家角色。

### 新效果（第二批）

| 能力 | 原作 | 模组角色、HBR | 模组角色、旧模式 | 原版角色 | 被 30A 召唤成幻影时 |
|---|---|---|---|---|---|
| 幻惑 | IllusionRandom | 各份层数相加，每层使一张攻击牌伤害落空（其他效果照常），先扣最早到期的一份；2026-11-15改为PlayerTurnEnd 2，各份独立 | 旧模式仍取较高层数并按持有者2回合计时 | 同左 | 敌人下一次攻击落空 |
| 混乱 | ConfusionRandom | 2026-11-15改为PlayerTurnEnd 3（覆盖随后3个自身回合），每回合第一张单体攻击牌的目标在全部敌人和自己之间随机（联机同步的战斗随机数）；选中自己时，牌对原目标不造成伤害，改把牌面原始伤害当作施加者的攻击打在自己身上（先扣格挡和 DP），牌的其他效果照常 | 同左 | 同左 | 敌人下一次攻击一半概率打自己，伤害为意图数值（按固定资源换算），攻击后移除 |
| OD 下降（命令） | OverDrivePointDown | 超频计量 −5%（高难 −10%）个等级，每级 100 点，扣到 0 为止 | 同左 | 无效（没有超频） | 召唤者获得同样的量，每次重放一次 |
| 吸取（命令） | HealDpByDamage | 按这一招打进 DP 和生命的伤害回复自己的 DP，×4 | ×0.5 | ×0.5 | 回幻影自己的 DP |

- 2026-11-15起HBR敌方幻惑、混乱命中即施加，不看格挡；30A幻影侧保留passedTargets。长颈怪环Judgment Ray仍维持原必中行为，见下一节。
- 吸取按格挡结算处记下的「格挡没挡住的部分」求和；30A 幻影替召唤者挡下的伤害同时记在召唤者名下。Break 中没有外壳，吸不回 DP。
- 图标取原作 `IconIllusion`、`IconConfusion`。原作不分升降，原样复制（`images/powers/hbr_illusion_power.png`、`hbr_confusion_power.png`，来源写在各自的 `.sources.json`）。
- 线圈巨轮γ的出处有两处：招式照原作 γ `TargetWireFrameKiller_Day2E01`（原作只在 `MC04ExpMtKumotori01_E01` 单独出场）；编队保持 `MC03ExpMtKinpu02_E02`（原作那一场是线圈巨轮β `TargetWireFrameDefault_MC03` 加两只杀手跳虫）。β 开战的先制剧情技打自己一方，不搬。
- 深渊重锤S 的 Rage Wave 去掉了 11-05 版“目标有 DP 时再追加”（原作对 DP 的倍率是 1.0）；第一幕的 AbyssKnocker 当时没改，第四批也照原作去掉了。
- 招式名：原作有名字的用原作名（Destruction Ball、Energy Drain 等，Rage Wave 改回原作的写法）；没有名字的沿用 11-05 版能对上的旧名（践踏、掘取、线圈射击、散射、崩落、炮击、重炮、尾击、长颈鞭、环光弹、八足连斩），石柱像的精偏全体招新起名「震波」，攻击力上升和回复照第一批。

## 原作出招表（第三批，2026-11-08）

提线木偶、死亡玩偶、猫角兽、猫角兽γ、旋轮、章鱼尾、章鱼尾γ、线圈巨轮、杀手跳虫、杀手重锤、灯胆跳虫、三爪镖、花冠利刃、巨肌魔臂也改成按原作出招表出招，28 只到此全部改完。规则和前两批相同（Break 2／3／4 回合（2026-11-15用户修订）、每回合平均伤害和 11-05 版持平、HP 不变、全体招走第二批的入口），数值取方案的 `calibrated.json`。

### 必中和概率

2026-11-15用户改定：HBR敌方控制效果命中即施加，格挡全部吸收也生效，不抽原作状态概率。目标仍必须在这次范围内且攻击后存活；封印、束缚、眩晕、幻惑、混乱不给召唤物，病毒仍要求有DP。

- 猫角兽两种Creepy Ball、钻石眼魔Laser Bind、三柱守卫Paralysis Burst、线圈巨轮两种Illusion Spin、石柱像Confusion Spike、章鱼尾两种Erosion Slice统一使用命中的存活单位。
- 长颈怪环Judgment Ray、磐石异蟹Mirage Pulse、针嘴鸟Ruin Bullet已有必中／命令行为不变。
- 30A幻影重放保留原passedTargets条件；原本必中的Judgment Ray仍命中即施加。吸取依旧只计算打过格挡的伤害。
- 留档中的原作概率只用于核对来源，不再决定敌方附加条件；套件同时验证完全格挡后施加、死者和范围外排除、无DP及召唤物限制，并验证幻影侧没有增强。

### 新效果（第三批）

| 能力 | 原作 | 模组角色、HBR | 模组角色、旧模式 | 原版角色 | 被 30A 召唤成幻影时 |
|---|---|---|---|---|---|
| 病毒 | VirusRandom | 每份偏导护盾上限 −30%，相加最低0；2026-11-15改为PlayerTurnEnd 2，到期恢复上限但失去的DP不返还 | 旧模式仍按持有者2回合计时 | 不施加（没有 DP） | 敌人高维外壳上限 −30%，同样失去超出的 DP；Break 中没有外壳，不施加 |
| 知性下降 | WisdomDownRate | 2026-11-14 用户定保持需求二十五的知性 −80%，各份相加；EnemyTurnEnd 3 | 从卡牌获得的格挡 −25% | 同旧模式 | HBR敌方格挡按实际知性下降总强度降低；旧模式仍 −25% |

- 2026-11-15起，Erosion Slice病毒命中即施加，不看格挡；知性下降的Deceive Shot同样命中即施加。
- 病毒只给玩家角色和有外壳的敌人。怪物的 Erosion Slice 是单体招，只打玩家角色，所以 30A 幻影自己的外壳不会中病毒。病毒挤掉的 DP 记作 DP 损失（精神力奖励照常结算）。
- 图标：病毒用原作 `IconVirus`，原样复制；知性下降用原作 `IconWisdom`，加和精神下降同一个蓝色向下箭头（同一段画法，箭头以外像素不变）。

**知性下降的数值（2026-11-14 用户定）**：保持需求二十五的 −80%，不回到2026-11-08暂定的 −25%。HBR强度固定80%，None／EnemyTurnEnd 3，各份相加；旧非HBR模式、原版角色的格挡 −25%等效适配保留。

- 原作是知性 −80%、3回合。HBR模式下知性直接决定格挡量，因此一份使格挡只剩两成，两份可使格挡归零；这是用户在了解影响后选择的结果。
- 原作和模组的作用差异不再通过削弱HBR强度补偿。不要把旧模式的 `WisdomFactor=0.75` 当作HBR的强度来源；HBR由 `BasePercent=80` 及实际实例总强度决定。

### 变种、错开和原作数据的几处特别

- 遭遇设置的变种：提线木偶弱怪战用教学关 `MarionetteSmallVirtual_first`（没有 hp 表）；成对的猫角兽用 α `CatHornSmallVirtual_first`；门与诅咒玩偶的两只用诅咒玩偶 `MarionetteKillerCurse`；巨肌魔臂与三爪镖的三爪镖用三角镖 `TriClawDefault`；两把花冠利刃同场时（成对的花冠利刃、门与花冠）第二把用 `CorollaKnifeSmall_02`。
- 两只巨肌魔臂同表：第二只从 dp 表第 2 步开始，第 1 步的「一次」记为用过，之后不会回来。循环段里两只的攻击力上升和全体攻击错开一回合。
- 章鱼尾、章鱼尾γ 的 hp 表原作从第 2 步编号，这里按顺序当第 1～3 步；第 2 步（原作第 3 步）三个选项 100:50:50。
- 死亡玩偶 dp 表第 6 步原作写了两行同样的 AttackSingle02，照原样抄，结果总是这一招。
- 训练机关卡在原作里用另一个标签，出招表相同、只有 DP 不同：死亡玩偶 `MarionetteKillerVirtual`（回复 21%）、杀手跳虫 `CrestHopperKillerVirtual`（回复 20%）、杀手重锤金峰山 `DarkKnockerKillerFire2`（没有回复）。照方案各用一套：回复按 `MarionetteKiller2` 35%、`CrestHopperKiller2` 25%。
- 招式名：原作有名字的用原作名（Creepy Ball、Erosion Slice、Curse Stamp、Rotation Cut、Rampage Slice、Deceive Shot，「通常攻击」「连击」「全体攻击」「连续攻击」「修复α」也照原作）；没有名字的沿用 11-05 版能对上的旧名（双角突刺、横扫、尾鞭、线圈射击、散射、冲撞、闪光、飞镖、斩击、双臂猛击），三爪镖的五连新起名「连爪」，没有名字的全体攻击叫「全体攻击」。
- 11-05 版里原作没有的机制都去掉了：牵线、诅咒牵线的攻击下降，猫角兽的威吓，杀手重锤的蓄力和重击，三爪镖的回旋镖，花冠利刃的磨刃，巨肌魔臂的怒吼。

### 28 只用的原作变种

| 怪物 | 原作变种（括号里是遭遇设置的变种） |
|---|---|
| 提线木偶 | `MarionetteDefaultVirtual`（弱怪战：`MarionetteSmallVirtual_first`） |
| 猫角兽 | `CatHornDefaultVirtual` β（成对的猫角兽：`CatHornSmallVirtual_first` α） |
| 旋轮 | `WireSlasherDefaultDark`（五种属性的表相同） |
| 章鱼尾 | `OctopusTailDefaultVirtual` |
| 线圈巨轮 | `TargetWireFrameSmallVirtual` α |
| 针嘴鸟 | `NeedleBirdSmallVirtual_MC02` |
| 鸦头马 | `CrowHorseDefaultVirtual` |
| 象牙异蛛 | `IvoryScooperDefaultVirtual` |
| 死亡玩偶 | `MarionetteKiller2`（门与诅咒玩偶：`MarionetteKillerCurse`） |
| 杀手跳虫 | `CrestHopperKiller2` |
| 杀手重锤 | `DarkKnockerKillerVirtual` |
| 猫角兽γ | `CatHornKillerVirtual` |
| 章鱼尾γ | `OctopusTailKillerVirtual` |
| 深渊重锤S | `AbyssKnockerKillerVirtual_MC03` |
| 线圈巨轮γ | `TargetWireFrameKiller_Day2E01`（编队仍是 `MC03ExpMtKinpu02_E02`） |
| 石柱像 | `PileGolemEdgeSmall` |
| 钻石眼魔 | `DiamondEyeballDefaultVirtual_MC03_01` |
| BarrelWalker | `BarrelWalkerDefaultFire_MC04_02` |
| 灯胆跳虫 | `BalbFleaDefault` |
| 三爪镖 | `TriClawSmall` 恶之回旋镖（巨肌魔臂与三爪镖：`TriClawDefault` 三角镖） |
| 异议之门 | `BigotryGateSmall_01`（两扇门的第二扇：`BigotryGateSmall_02`；门与诅咒玩偶：`BigotryGateDefault_01`） |
| 花冠利刃 | `CorollaKnifeSmall_01`（两把同场的第二把：`CorollaKnifeSmall_02`） |
| 巨肌魔臂 | `MolArmsSmall`（两只同场时第二只错开一步） |
| 磐石异蟹 | `ShrimpCrabDefault_01`、`ShrimpCrabDefault_02` |
| 触手巨蛇 | `SlimySerpentDefault` |
| 长颈怪环 | `GiraffeHaloDefault_01` |
| 阿拉克尼巨蛛 | `ArachneLineDefault` |
| 三柱守卫 | `PillarGuardianDefault` |

## 原作出招表（第四批，2026-11-08）

第一幕原有的 7 只——SmallHopper、CrestHopper、HealHopper、GoldHopper、Knocker、ClayKnocker、AbyssKnocker——也改成按原作出招表出招（用户原话「也照原作改」）。规则和前三批相同：Break 2／3／4 回合（2026-11-15用户修订）、HP 不变、全体招走第二批的入口、回复 DP 按原作比例。这 7 只的原作技能都不带状态。

**用户 2026-11-08 定：这 7 个先不改**——手冢咲、Red Crimson（两个）、FlatHand、DeathSlug、SkullFeather、奈良良柴犬、萤的浮游炮，仍按各自现在的循环出招。

### 7 只用的原作变种

原作数据 2026-11-08 从 hbr.quest 抓取，17 份详情并进了 `D:\stsmod\outputs\monster-mechanics-hbrquest-20261107\enemies\`（`fetched-at.txt` 记了一行），解码和数值换算在 `D:\stsmod\outputs\monster-batch4-20261108\`（`decoded.md`、`calibrate4.py`、`calibrated4.json`）。

| 怪物 | 原作 | 用的变种 | 依据 |
|---|---|---|---|
| SmallHopper | 小型跳虫 | `CrestHopperSmall`（DP 2400） | 三只杂鱼 MC01_12_Battle01b |
| CrestHopper | 嵴冠跳虫 | `CrestHopperDefaultVirtual`（DP 4000） | MC02_10_Battle02、MC02_11_Battle01、MC02_14_Battle02。嵴冠跳虫群 MC01_12_Battle01event 的 `CrestHopperDefault_MC01` 出招表相同、DP 4400，用同一套 |
| HealHopper | 治愈跳虫 | 第一幕 `CrestHopperHealVirtual`（DP 4000）；第二幕训练机三场 `CrestHopperHealVirtual_MC03`（DP 2000） | 第一幕 MC02_10_Battle01、MC02_10_Battle02、MC02_11_Battle01、MC02_12_Battle02；第二幕 MC03ExpTraining01Virtual_S01、_R02、_R05。两个变种出招表不同，由遭遇设置 |
| GoldHopper | 黄金跳虫 | `CrestHopperDefaultGold01`（α） | GoldOmen 是模组自己的弱怪战，没有原作关卡；取原作第 1 章探索的黄金跳虫α。β `CrestHopperDefaultGold02` 出招表相同，α／β 外观照旧随机 |
| Knocker | 重锤 | `DarkKnockerSmallVirtual`（DP 2000） | 它的两场都没有原作重锤：「重锤与治愈」的原作 MC02_12_Battle02 是两只黏土重锤，「大锤小锤」是模组自己的精英战。取第 2 章训练机森林的重锤，`DarkKnockerSmall` 出招表和 DP 相同 |
| ClayKnocker | 黏土重锤 | `DarkKnockerDefaultVirtual`（DP 3600） | 「大锤小锤」没有原作关卡；取第 2 章 MC02_12、MC02_18、MC02_19 各场的黏土重锤，`DarkKnockerDefault` 相同 |
| AbyssKnocker | 深渊重锤 | `AbyssKnockerDefaultVirtual_MC02`（DP 44200） | 精英战没有原作关卡；取第 2 章训练机森林的 Boss。第 1 章洞窟的 `AbyssKnockerDefaultMC01`（DP 24000）和 `AbyssKnockerDefault` 出招表相同 |

没有原作关卡号的是模组自己的 6 个遭遇：SmallHopper 弱怪战、CrestHopper 弱怪战、GoldOmen、CrestHopper 与两只 HealHopper、大锤小锤、AbyssKnocker 精英战。它们按上表取第 1、2 章里同一只怪的变种。

### 招式和数值

招式名照前三批：原作的「通常攻击」沿用模组的「普通攻击」，其余用原作名。

| 怪物 | 原作技能 | 目标 × 段数 | 数值（高难） | 附带 |
|---|---|---|---|---|
| SmallHopper | `CrestHopper_DefaultAttack`「普通攻击」 | 单体 ×1 | 3（4） | |
| | `CrestHopper_SkillAttack`「连击」 | 单体 ×8 | 1×8（1×8） | |
| | `CrestHopper_Heal1`「修复α」 | 自身 | | 回 DP 25% |
| CrestHopper | 普通攻击 | 单体 ×1 | 6（8） | |
| | 连击 | 单体 ×8 | 1×8（1×8） | |
| | 修复α | 自身 | | 回 DP 15% |
| HealHopper | 普通攻击 | 单体 ×1 | 3（4） | |
| | 连击 | 单体 ×8 | 1×8（1×8） | |
| | `CrestHopper_HealAll1`「修复β」 | 友方全体 | | 每个友方回自己最大 DP 的 10% |
| | `CrestHopper_HealAll2`「修复Ω」 | 友方全体 | | 每个友方回满 DP（10000 ÷ 2000） |
| GoldHopper | `CrestHopperDefaultGold_DefaultAttack`「普通攻击」 | 单体 ×1 | 7（8） | |
| | `CrestHopperDefaultGold_Escape`「逃掉了……」 | 自身 | | 逃跑 |
| Knocker | `DarkKnocker_Attack01`「普通攻击」 | 单体 ×1 | 5（6） | |
| | `DarkKnocker_Attack02`「连续攻击」 | 单体 ×2 | 3×2（3×2） | |
| | `DarkKnocker_AttackAll01`「全体攻击」 | 全体 ×1 | 7（8） | |
| ClayKnocker | 普通攻击 | 单体 ×1 | 6（7） | |
| | 连续攻击 | 单体 ×2 | 3×2（4×2） | |
| | 全体攻击 | 全体 ×1 | 8（9） | |
| AbyssKnocker | `AbyssKnocker_Attack01`「普通攻击」 | 单体 ×1 | 7（8） | |
| | `AbyssKnocker_Attack02`「连续攻击」 | 单体 ×2 | 4×2（5×2） | |
| | `AbyssKnocker_AttackAll01`「Rage Wave」 | 全体 ×1，精偏 | 8（9） | |
| | `AbyssKnocker_Buff01`「回复」 | 自身 | | 回 DP 6.8% |

重锤、黏土重锤的全体攻击原作是体偏，和杀手重锤一样。

出招表（「／」后是同一步的其他选项，比例是权重）：

- SmallHopper：dp ① 普通攻击 → ② 普通攻击／连击 50:50 → ③ 普通攻击 → ④ 连击 → ⑤ 连击；hp ① 普通攻击 → ② 普通攻击／连击 40:60 → ③ 普通攻击 → ④ 修复α
- CrestHopper：dp ① 普通攻击 → ② 连击 → ③ 普通攻击／连击 50:50 → ④ 普通攻击 → ⑤ 连击 → ⑥ 连击；hp ① 普通攻击 → ② 普通攻击／连击 40:60 → ③ 连击 → ④ 修复α
- HealHopper 第一幕：dp ① 普通攻击 → ② 普通攻击／连击 50:50 → ③ 修复β；hp ① 普通攻击／连击 40:60 → ② 修复β
- HealHopper 第二幕训练机：dp ① 普通攻击 → ② 普通攻击／连击 50:50；hp ① 修复Ω
- GoldHopper：dp ① 普通攻击／逃掉了…… 40:60 → ② 同 ① → ③ 逃掉了……；没有 hp 表
- Knocker：dp ① 普通攻击 → ② 普通攻击／连续攻击 50:50 → ③ 全体攻击 → ④ 普通攻击 → ⑤ 全体攻击 → ⑥ 全体攻击；hp ① 普通攻击 → ② 普通攻击／连续攻击 40:60 → ③ 全体攻击
- ClayKnocker：dp ① 普通攻击 → ② 全体攻击 → ③ 普通攻击／连续攻击 50:50 → ④～⑦ 全体攻击；hp ① 连续攻击 → ② 全体攻击 → ③ 普通攻击／连续攻击 50:50 → ④ 全体攻击
- AbyssKnocker：dp ① 普通攻击 → ② Rage Wave → ③ 普通攻击／连续攻击 50:50 → ④ 同 ③ → ⑤ Rage Wave；hp ① Rage Wave → ② 普通攻击／连续攻击 50:50 → ③ Rage Wave → ④ 普通攻击／连续攻击 40:60 → ⑤ 回复

Break 中普通怪放 2 招 hp 表、精英 3 招，所以 SmallHopper、CrestHopper 的修复α，ClayKnocker 的第 4 步和 AbyssKnocker 的回复实际碰不到；HealHopper 的修复β、修复Ω 碰得到。

### 数值怎么算

照方案 `calibrate.py` 的口径：原作 dp 表走一遍的平均威力，对齐 11-05 版每回合平均伤害，普通、高难分开，每段至少 1。

| 怪物 | 11-05 版每回合平均 | 现在（取整后） |
|---|---|---|
| SmallHopper | 4（5） | 5.5（6.0） |
| CrestHopper | 7.5（10） | 7.17（8.0） |
| HealHopper | 0（不攻击） | 第一幕 2.83（3.33），第二幕 4.25（5.0） |
| GoldHopper | 7（8），每回合 | 7（8），只在出手的回合 |
| Knocker | 6（7） | 6.08（7.0） |
| ClayKnocker | 7.5（8.75），蓄力回合算 0、不算累积的力量 | 7.43（8.5） |
| AbyssKnocker | 7.5（8.75），不含对有 DP 目标的追加 | 7.6（8.8） |

三处特别：

- **SmallHopper**：连击 8 段，按比例每段只有 0.6（高难 0.7），只能取 1，所以整轮平均是 5.5（6.0），比 11-05 版的 4（5）高。高的部分在第 4、5 步；前三回合平均 3.83（4.67），和原来差不多。
- **HealHopper**：11-05 版不攻击，没法按自己的平均算。它的普通攻击、连击和小型跳虫是同一个原作技能、同样威力，血量档次也一样（14–18 对 12–16），所以伤害照 SmallHopper。
- **GoldHopper**：只有一种攻击。原作出招表里大半回合是逃跑，按整轮平均会把一击放大到 26（30），所以按出手的回合持平，仍是 7（8）。

### 和 11-05 版不同的地方

- **SmallHopper**：原来每回合普通攻击 4（5）。现在 5 步循环，见上；多了 hp 表和修复α。开局基准（HP 9800、DP 3000，`HbrEnemyResourceAdapter`）不变；普通攻击仍是 `ATTACK_MOVE`，开局的体偏受击曲线照旧。
- **CrestHopper**：原来普通攻击 5（6）、连击 2×5（2×7）交替。现在 6 步循环，连击 1×8；多了修复α。嵴冠跳虫群的 `IsSwarmMember` 照旧把 HP 降到 26–30（28–32）。
- **HealHopper**：原来每回合修复β：每个活着的同伴（含自己）回固定 4（5）DP（HBR ×100 = 400／500），再加 4（5）格挡，从不攻击。现在：
  - 会攻击，见上；
  - 修复β 每三回合一次，回每个友方自己最大 DP 的 10%（原作每人 400，是治愈跳虫自己 DP 4000 的十分之一），不给格挡；Break 中的友方和已死的不回；
  - 第二幕训练机三场用修复Ω：外壳在时只攻击，被打空 DP 进 Break 后，从下一招起的每个 hp 回合给所有友方回满 DP（自己在 Break 中不回）。
  - 回复量和原来比：模组 DP 按每点原生 HP 200 生成，所以大个子的友方回得比原来多，小个子少。例如重锤（原生 40）原来回 400，现在约 800；治愈跳虫自己（16）原来 400，现在约 320。旧模式的 DP 只有生命的两成，10% 只有 1 点上下，原来是 4 点加 4 格挡。
- **GoldHopper**：原来每回合普通攻击 7（8），从 Break 里恢复两次之后才逃。现在照原作的时机：第 1、2 回合各 60% 逃跑，第 3 回合一定逃。2026-11-09 起打空 DP 不再眩晕，逃跑照常（之前眩晕会把逃跑推迟一回合）。额外金币奖励照旧只在打死时给，逃掉不给。作为敌人出场时 HP 1、DP 10 照原作取固定值，带 99 层原版无实体，每一击只算 1，一共 11 击，见下「GoldHopper 的无实体和 HP／DP」。
- **Knocker**：原来每回合普通攻击 6（7）。现在 6 步循环，三步是全体攻击，打玩家一方全部单位（含 30A 幻影和召唤物）。
- **ClayKnocker**：原来普通攻击 6（7）、连续攻击 5×2（6×2）、蓄力（力量 +2，越打越强）、重击 14（16）循环。蓄力和重击原作没有，去掉了；现在 7 步循环，后四步是全体攻击 8（9）。
- **AbyssKnocker**：原来普通攻击、连续攻击 6×2（7×2）、回复（固定 10 DP 加 10 格挡）、Rage Wave 12（14），目标有 DP 时再追加 5（7）。现在 Rage Wave 是原作的全体、精偏攻击 8（9），原作对 DP 的倍率是 1.0，追加去掉了；回复只在 hp 表第 5 步，按原作 3000 ÷ 44200 回 6.8%，不给格挡。

### 第一幕第一场（SmallHopper 弱怪战）前后对比

SmallHopper 弱怪战随机出两只 SmallHopper，或者 HealHopper 加 SmallHopper。下表是每个敌人回合的期望伤害；HBR 列是开局校准下（状态值 E150、对方防御 100，每段向下取整）打到身上的数字，不算格挡。

| 回合 | 两只 SmallHopper 出招 | 原生 | HBR | 11-05 版 | HealHopper + SmallHopper 出招 | 原生 | HBR | 11-05 版 |
|---|---|---|---|---|---|---|---|---|
| 1 | 普通攻击 ＋ 普通攻击／连击 | 8.5 | 1150.5 | 8（1084） | 普通攻击 ＋ 普通攻击 | 6 | 814 | 4（542） |
| 2 | 普通攻击／连击 ＋ 普通攻击 | 8.5 | 1150.5 | 8（1084） | 普通攻击／连击 ＋ 普通攻击／连击 | 11 | 1487 | 4（542） |
| 3 | 普通攻击 ＋ 连击 | 11 | 1487 | 8（1084） | 修复β ＋ 普通攻击 | 3 | 407 | 4（542） |
| 4 | 连击 ＋ 连击 | 16 | 2160 | 8（1084） | 普通攻击 ＋ 连击 | 11 | 1487 | 4（542） |
| 5 | 连击 ＋ 普通攻击 | 11 | 1487 | 8（1084） | 普通攻击／连击 ＋ 连击 | 13.5 | 1823.5 | 4（542） |

- 两只 SmallHopper：前三回合合计 28（原 24），五回合 55（原 40）；高难前三回合 32（原 30），五回合 60（原 50）。按校准，每只 SmallHopper 要 4 张打击，这一场大约打 3～4 回合，所以多出来的主要是第 3、4 回合的连击。
- HealHopper 加 SmallHopper：前三回合 20（原 12），五回合 44.5（原 20），因为 HealHopper 开始攻击了；高难前三回合 24（原 15）。
- 对一张原版防御（HBR 500 格挡）：普通攻击 407 被整个挡住，剩 93；连击 8 段各 135，共 1080，挡掉 500 后掉 580 生命。原来一下 542，挡完掉 42。
- 出招节奏：原来每回合同一招、意图恒为 4（5）；现在第 2 回合起意图会在单发和 8 段之间变，第 4、5 回合是连击。
- 计算脚本和全表在 `D:\stsmod\outputs\monster-batch4-20261108\first-fight.md`。

### 同场的同种怪错开

同一场里同种的怪，第二只从 dp 表第 2 步开始，第三只从第 3 步（照两只巨肌魔臂的做法，`StartDpTableAt`）：

- 三只杂鱼、嵴冠跳虫群：三只依次从第 1、2、3 步开始（嵴冠跳虫群仍带降血标记）；
- SmallHopper 弱怪战抽到两只 SmallHopper 时，第二只从第 2 步开始；
- CrestHopper 与两只 HealHopper、猫角兽γ与治愈跳虫：第二只 HealHopper 从第 2 步开始；
- 重锤与治愈：第二只 Knocker 从第 2 步开始。

前三批原来只有两只巨肌魔臂错开（异议之门、花冠利刃、磐石异蟹靠不同的原作变种区分）。用户 2026-11-08 定（原话「按建议办」），其余 17 场同表的同种怪也已错开，只改起始步骤，招式、数值和变种都不变：

- 第一幕 6 场：成对的猫角兽、木偶剧场（两只提线木偶）、木偶与猫角兽（两只猫角兽）、两条章鱼尾、线圈巨轮阵（两只线圈巨轮）、三连旋轮；
- 第二幕 9 场：两具死亡玩偶、杀手跳虫与玩偶、杀手跳虫群、两台杀手重锤、重锤与死亡玩偶、成对的猫角兽γ、章鱼尾γ（两只章鱼尾γ）、死亡玩偶与猫角兽γ（两只猫角兽γ）、线圈巨轮γ精英战（两只杀手跳虫）；
- 第三幕 2 场：三爪镖与灯胆跳虫（两只灯胆跳虫）、门与诅咒玩偶（两只诅咒玩偶）。

两只的场次第二只从第 2 步开始，三连旋轮和杀手跳虫群三只依次从第 1、2、3 步开始。诅咒玩偶的前 5 步是「一次」：第二只的第 1 步记为用过，开场从第 2 步走到第 5 步，之后和第一只一样在第 6～9 步循环（和巨肌魔臂同样的做法）。现在三幕登记的遭遇里，同表的同种怪都从不同的步骤开始（共 23 场，SmallHopper 弱怪战抽到两只 SmallHopper 时也一样），套件逐场核对。

### HealHopper、GoldHopper 被 30A 召唤成幻影时

- 幻影的攻击照旧按意图重放，全体攻击打所有敌人；hp 表的招式（修复α、修复Ω、回复）幻影碰不到，幻影不进 Break。
- HealHopper 幻影的修复β 回的是幻影这一边：自己和别的召唤物的外壳，以及模组角色的偏导护盾，各回自己上限的 10%；原版角色没有 DP，不回；敌人不受影响。原来是给同一边每个单位回固定 DP 再加 4 格挡。
- GoldHopper 幻影不逃跑：出招表里的逃跑选项对玩家一方的 GoldHopper 无效，第 1、2 步只剩普通攻击，第 3 步跳回第 1 步，所以每回合都攻击，和原来一样。幻影不带无实体，HP 和 DP 照幻影原有的规则（幻影规则读的原生 HP 54–58 没动）。
- 默认幻影是 SmallHopper，它现在按上面的 5 步循环出招。重放时连击的 8 段各自结算，按段加的加成会加 8 次。
- 30A 融合效果都没动。

### GoldHopper 的无实体和 HP／DP（用户 2026-11-08 定）

用户原话「但coldhopper给99层无实体 然后dp和hp跟原作一样」：上一版「按数值模型把原生 HP 降到 22–26」作废，改成下面这样。逃跑时机、攻击、奖励和遭遇都不变。

- 作为敌人出场时带 99 层原版的无实体（`IntangiblePower`）：每次受到的伤害最多 1，失去生命也最多 1。层数每个敌人回合结束减 1，一场里用不完。
- HP 1、DP 10 照原作（`CrestHopperDefaultGold01`／`02`），是固定的绝对值：不乘 ×700，不吃敌方血量、敌方高维外壳和两项每幕成长设置，不吃难度（高难的 HP、外壳 ×1.5）和多人血量加成；HBR、旧模式、原版角色都一样。敌方高维外壳设成 0% 时别的敌人没有外壳，GoldHopper 照旧有 10 DP；Break 结束后外壳回来也是 10。
- 做法：GoldHopper 实现新接口 `IHbrgoFixedEnemyResources`（`HbrgoStaticMonster.cs`）。敌人生成时的血量换算 `EnemyHealthScalingHelper.ApplyInitialHealth` 和外壳上限 `HighDimensionalShellPower.CalculateAmount` 遇到它直接用固定值，其他怪不受影响。无实体在 `AfterAddedToRoom` 里只给敌人一侧加一次（存档属性 `HasAppliedStartingIntangible`，照 Red Crimson 的做法）。
- 30A 幻影：不带无实体（原版的入场回调本来只对敌人一侧调用，GoldHopper 自己也再判一次），HP 和 DP 照幻影原有规则。幻影规则读的是原生 HP，所以原生 HP 54–58（高难 56–60）保持不动，只给幻影用。

#### 实际几下打死

每一击 1 点：前 10 击各打掉 1 DP，第 10 击打空 DP，照常进 Break，不眩晕，逃跑照常（2026-11-09 之前会眩晕一回合、逃跑跟着推迟）；外壳在 Break 时移除，第 11 击打掉仅有的 1 HP。**一共 11 击**，HBR、旧模式、原版角色都一样，套件逐击核对（伤害走完整的原版伤害钩子）。

- 多段攻击每一段算一击（原版攻击命令每段各结算一次伤害），一张 1×8 的牌就是 8 击；全体攻击打到它也是 1。
- 中毒等持续伤害、荆棘和反击、失去生命、只打 DP 的伤害（棺木、持续 DP 伤害，套件里 500 点只打掉 1）、按 DP 算的伤害、跳过基础曲线的绝对伤害，都一样每次 1。
- 打空 DP 后没打死、也没逃的话，Break（3 回合）结束外壳以 10 DP 回来。
- 要拿额外金币，就得在它逃走前打满 11 击（或用下表的捷径）：第 1、2 个敌人回合各有 60% 逃。2026-11-09 起打空 DP 不再眩晕，逃跑不会因此推迟。

#### 绕过或改变「每击 1」的效果

| 效果 | 怎么作用 | 对 GoldHopper |
|---|---|---|
| 移除（罕见技能，选一个敌人） | `BreakImmediately` 直接清空外壳 | 一张牌打掉全部 10 DP（进 Break，不眩晕），再 1 击打死（套件跑了真实的 Break） |
| Rapture-弓 | 每次强化攻击另加对 DP 的伤害，这部分也被无实体封顶为 1 | 每击打掉 2 DP：5 击打空，第 6 击打死 |
| 浮游炮—胜负 Lv.2 | 造成 DP 伤害后追加 1 点只打 HP 的伤害，跳过 DP | HBR：胜负打中一次（打掉 1 DP），追加的 1 点直接打掉 HP，不用打空 DP（按代码，套件没跑炮的整条链）。旧模式这 1 点先被外壳 ×0.75，再过炮的灵魂之力和取整，结果没核实 |
| 末日（棺木等施加） | 敌人回合结束时 HP ≤ 末日层数就死，不看 DP | HP 只有 1，1 层末日就够；前提是它这回合没先逃 |
| 终将降临之世（稀有，3 费） | 移除敌人全部能力后直接杀死 | 打出就死 |
| 病毒（30A 幻影给敌人） | 外壳上限降到 70% | DP 上限 10 → 7 |
| 旧模式下 1 点的攻击 | 外壳在时先 ×0.75 再封顶 | 只打掉 0.75 DP（HBR 下是 1）；全用 1 点攻击要 14 下才打空 |
| 不到 1 的只打 HP 的追加：兜割（吸收 DP 的 1/3，强化后 1/2）、终局失去胜负后（DP 伤害的 25%） | HBR 下算成 0，旧模式 ×0.75；无实体不动不到 1 的数，原版生命是整数 | 没有效果 |

能一下打掉 1 以上 DP 的只有「破壁」和「Rapture-弓」；能跳过 DP 直接打 HP 的只有浮游炮—胜负 Lv.2；直接杀死的是末日和终将降临之世。

#### 和上一版比

上一版（同日，候选 `C2126D93…`）按数值模型把原生 HP 降到 22–26（高难 24–28），HBR 下 HP 15,400–18,200、DP 4,400–5,200，已经撤掉。那一版的估算脚本还在 `D:\stsmod\outputs\monster-batch4b-20261108\goldhopper-hp\`，不再适用。

## 打空 DP 不再眩晕（2026-11-09）

用户选「1」（经「全局统筹」转来）：普通高维外壳打空 DP 不再眩晕，特殊 Boss 不动。

- 普通、精英、Boss于2026-11-15改为分别得2、3、4层Break 和破坏率，Break 结束外壳回来；这一回合照常放出当前意图。
- 出招表怪物：当前意图不变，下一招起走 hp 表第 1 步；外壳回来时回到 dp 表第 1 步，规则不变。没有 hp 表的（如异议之门、GoldHopper）dp 表照常往下走。蓄力不再被打空 DP 清掉，蓄力那一回合打空 DP，Ruin Bullet 照常放出。
- GoldHopper 打空 DP 后不再推迟逃跑。
- **2026-11-14 更新：特殊 Boss 打空 DP 也不再眩晕。** 手冢咲的偏导护盾仍触发鬼神状态、永久 Break 和破坏率；柴犬分层 DP 和普通外壳挂在两个 Red Crimson、FlatHand、DeathSlug、SkullFeather 身上时均不替换当前意图。原有 Break 层数、破坏率、外壳恢复、柴犬唤醒及强制结束回合规则照常。旧的眩晕描述分支已移除。萤的浮游炮不是敌人，不受影响。
- 卡牌和幻影带来的眩晕（救世主的极限冲击、幻影的 Laser Bind、Paralysis Burst、Mirage Pulse 等）不变。

套件 `hbr-act-rosters` 现验证普通及特殊 Boss 都不因 DP 归零而眩晕，同时验证普通外壳的2／3／4回合 Break、破坏率和恢复，以及偏导护盾、分层 DP 的独立规则。主动技能施加的眩晕仍单独结算。

## 立绘

立绘取自本机解包库里街机模式的敌人卡图（`Assets/Contents/Image/Arcade/Enemy/Season*/`，512×704），用脚本抠出：

- 卡图共用一张城市背景，但天空每张不同。背景只在卡框和底部色带可靠，所以用全部 94 张卡的逐像素众数还原卡框和色带，再用 OpenCV GrabCut 按每张卡自己的边缘配色分出生物。
- 底部半透明色带按“观察值 = 斜率 × 场景 + 截距”逐通道反算；参数用所有卡图跨过色带上沿的上万对像素回归得到，粉色色带拟合误差约 1。色带上的装饰线和边缘接缝用修补填平，落在地面上的影子按“变暗的地面颜色”剔除。
- 有同姿势剪影 Mask 的用 Mask 定轮廓：针嘴鸟、象牙异蛛；杀手跳虫、杀手重锤、深渊重锤S、钻石眼魔；巨肌魔臂、阿拉克尼巨蛛。石柱像、BarrelWalker 的 Mask 和卡图姿势对不上，只用 GrabCut。
- 被卡框裁掉的肢体在边缘淡出。

每张图的原始资源路径、哈希和抠图参数记在 `images/monsters/hbrgo_act{1,2,3}/sources.json`。BarrelWalker 和巨肌魔臂取自 Season4，其余取自 Season1。六个新 Boss 的地图图标在 `images/map/boss_nodes/`（crow_horse、ivory_scooper、diamond_eyeball、barrel_walker、arachne_line、pillar_guardian），和 DeathSlug 等一样是 1254×1254 的居中剪影，描边版与本体相同。新贴图还没有 `.import`，要等打包时由 Godot 导入。

顶栏和对局记录还要按遭遇 ID 读一对图标：`images/ui/run_history/<遭遇>_boss.png` 和 `_boss_outline.png`，模组旧 Boss 的这对图就是地图节点图的逐字节副本。1.3.0.5 缺了六个新 Boss 的这对图，开新局时顶栏读图抛异常，打断了对局界面的初始化，随后地图 `SetMap` 报空引用。现已照旧 Boss 的做法补齐 12 张。

2026-11-07 幻影图鉴改成深色底以后，白底下看不出的背景残留露了出来。按用户的决定清了三张立绘的透明通道，只降低 alpha，RGB 一个像素都没改：

- TargetWireFrame：右上的米白碎片和尖端亮点、下方的褐色地面影子、眼睛旁和爪间透过来的护栏与地面，以及弧外缘的浅色细线；
- BarrelWalker：右上的淡紫建筑、本体和右环之间的地面、左下的地面块和左环断开的下半截；
- 三柱守卫：右上的灰粉碎屑、柱脚和叉尖下的地面楔块。

BarrelWalker、三柱守卫的地图图标是用立绘缩放出来的，按同样的比例同步清理；`run_history` 的副本跟着更新，仍然逐字节相同。章鱼尾γ、象牙异蛛、猫角兽γ、石柱像的地面色带和碎屑这次没动。清理脚本、修改前的原图和对比图在 `D:\stsmod\outputs\monster-alpha-cleanup-20261107\`，`sources.json` 里记了新旧哈希。

## 验证

- stable、beta 严格编译 0 警告 0 错误（候选 DLL `EB9F0812…`）。
- 套件 [hbr-act-rosters](../validation/hbr-act-rosters/README.md) stable、beta 各 3540 项：三幕登记表规模，原版密林／巢穴／荣耀池子实际追加的遭遇和怪物，51 个遭遇的编队和 HP 预算，28 种怪的 HP 和招式循环，全部招式在 HBR／旧模式和两档难度下的实际效果，幻影补效果，融合效果和幻影攻击段，本地化键，场景和贴图哈希，×700 HP。
- 回归：敌方技能 650，跨幕成长 stable／beta 各 161，敌方资源 169，敌方回复 63，DP 恢复 stable／beta 各 91，资源倍率 81，检查数都和改动前相同。
- 日志和报告：第一幕在 `D:\stsmod\outputs\act-one-roster-20261105\`，第二、三幕和三幕合并后的套件在 `D:\stsmod\outputs\act-two-three-roster-20261105\`。

- 2026-11-07 第一批原作出招表（候选 `6355B033…`，beta 编译 `C4F713EF…`）：
  - stable、beta 严格编译 0 警告 0 错误，stable 重编逐字节复现。
  - `hbr-act-rosters` stable、beta 各 5516 项（原 3552）：5 只的出招表逐行对照留档的原作数据，出招表随机、一次、条件、变种、打空 DP 前后的切换，每招在模组角色 HBR、模组角色旧模式、原版角色三种情况下的实际结算，新效果的伤害、能量、出牌和 DP 结算，幻影一侧，格挡没挡住的记录。
  - 打包会话同款整套回归 64 次运行全部通过，其余套件检查数和上一轮相同；beta-compat 加 `--install`，stable、v0.111.0 各 585 个补丁类、8444 条引用、0 个问题。
  - 日志和报告在 `D:\stsmod\outputs\monster-batch1-20261107\`。

- 2026-11-08 第二批原作出招表和全体攻击入口（候选 `CF56522E…`，beta 编译 `130C30AC…`）：
  - stable、beta 严格编译 0 警告 0 错误，stable 重编逐字节复现。
  - `hbr-act-rosters` stable、beta 各 9956 项（原 5576）：14 只的出招表逐行对照留档的原作数据（快照 17 个变种），第二批的开场、循环、权重和打空 DP 前后的切换，每招在三种情况下的实际结算和攻击范围，幻惑、混乱、OD 下降、吸取的结算，全体和 DP 最低的目标列表（含 30A 幻影和 Osty，不含炮台和已死的召唤物），幻影一侧（含真实的重放伤害循环），格挡记录的嵌套和数量。
  - 打包会话同款整套回归 64 次运行全部通过，其余套件检查数和第一批相同。`hbr-combo` 第一次跑失败：它用反射按 5 个参数调 `PhantomHelper.PerformAttackIntent`，而这批给它加了参数；已恢复原签名，全体重放另起 `PerformAttackIntentOn`。
  - beta-compat 加 `--install`，stable、v0.111.0 各 587 个补丁类（多的 2 个是这批的两个补丁）、8478 条引用、0 个问题。
  - 日志和报告在 `D:\stsmod\outputs\monster-batch2-20261108\`。

- 2026-11-08 第三批原作出招表和 Judgment Ray 必中（候选 `E02F109E…`，beta 编译 `378B0BD7…`）：
  - stable、beta 严格编译 0 警告 0 错误，stable 重编逐字节复现。
  - `hbr-act-rosters` stable、beta 各 17221 项（原 9956）：28 只的出招表逐行对照留档的原作数据（快照 36 个变种，记了概率状态的原作命中率），第三批的开场、循环、权重、变种和打空 DP 前后的切换，每招在三种情况下的实际结算和攻击范围，病毒、知性下降的结算，幻影一侧，遭遇设置的变种和两只巨肌魔臂的错开，原有怪 Knocker 的目标和 Break 不变。
  - 整套回归 64 次运行全部通过，其余套件检查数和第二批相同；beta-compat 加 `--install`，stable、v0.111.0 各 587 个补丁类、8481 条引用、0 个问题。
  - 日志和报告在 `D:\stsmod\outputs\monster-batch3-20261108\`。

- 2026-11-08 第四批原作出招表（候选 `766663D0…`，beta 编译 `B2B9E73B…`）：
  - stable、beta 严格编译 0 警告 0 错误，stable 重编逐字节复现。
  - `hbr-act-rosters` stable、beta 各 19838 项（原 17221）：35 只的出招表逐行对照留档的原作数据（快照 44 个变种），第四批的循环、权重、HealHopper 两个变种、打空 DP 前后的切换和 GoldHopper（没有 hp 表）眩晕后接着逃跑，每招在三种情况下的实际结算和攻击范围，修复β／Ω 的友方回复（Break 中和已死的不回，不给格挡，玩家的 DP 不动）和幻影一侧（回自己、别的召唤物和召唤者的偏导护盾），GoldHopper 第 3 回合一定逃、逃跑不给奖励、幻影不逃，遭遇设置的变种和同种怪错开（含 SmallHopper 弱怪战两种开局）。原来拿 Knocker 当「不在出招表里的怪」的两项检查改用手冢咲。
  - `hbr-enemy-skills`：Hopper 开局基准的格挡检查拆成独立一段，放进常跑的那段，659 项（原 650）：普通攻击 3 打到身上 407，被原版防御的 500 格挡全挡住；连击 1×8 每段 135，挡掉 500 后掉 580 生命。旧的四张打击预算照旧跳过（它的破坏率预期早就过时）。
  - `hbr-enemy-dp-recovery` stable、beta 各 67 项（原 91）：HealHopper、AbyssKnocker 改成原作比例、不给格挡，少的是旧格挡那几项。
  - 整套回归 64 次运行全部通过，其余套件检查数和第三批相同；beta-compat 加 `--install`，stable、v0.111.0 各 587 个补丁类、8481 条引用、0 个问题。
  - 日志和报告在 `D:\stsmod\outputs\monster-batch4-20261108\`。

- 2026-11-08 第四批补充：前三批 17 场错开、GoldHopper 无实体和原作 HP／DP（候选 `AD4A0CAD…`，beta 编译 `B07E9148…`）：
  - stable、beta 严格编译 0 警告 0 错误，stable 重编 DLL、PDB 都逐字节复现。
  - `hbr-act-rosters` stable、beta 各 20054 项（第四批 19838）：17 场逐场的起始步骤、第二只诅咒玩偶用过的「一次」，三幕全部登记遭遇里同表的同种怪都不同步（23 场）；GoldHopper 在三种情况、两档难度、三组设置、第 1／3 幕、单人和两人的原生血量下都是 HP 1、DP 10（旁边的 Knocker 照常换算），幻影照幻影规则、不带无实体，入场的 99 层无实体只加一次，逐击 11 击，绕过「每击 1」的效果逐项实测（见上面的表）。招式结算那组检查原来给每只怪塞 1000 DP，现在上限低于 1000 的（GoldHopper）塞满上限。
  - `hbr-enemy-resources` 181 项（原 169）：「模组普通怪」的血量换算样本从 GoldHopper 换成 Knocker，另加 GoldHopper 固定值 12 项（HBR 和旧模式、改过的血量／外壳／成长设置、第 2／3 幕、高难、2 人和 4 人）。
  - 整套回归 64 次运行全部通过，除上面两个套件外检查数和上一轮相同，其中现有的无实体套件 `coffin-intangible` 79／80、`hbr-damage-caps` 127／127 照旧；beta-compat 加 `--install`，stable、v0.111.0 各 587 个补丁类、8481 条引用、0 个问题。
  - 中间版（GoldHopper 降到 22–26，候选 `C2126D93…`，beta `AFCAB2BE…`）也跑过同一套（整套回归 64 次全过，beta-compat 0 个问题），日志在 `D:\stsmod\outputs\monster-batch4b-20261108\`，已被这一版取代。
  - 日志和报告在 `D:\stsmod\outputs\monster-batch4c-20261108\`。

没有在游戏里测：立绘的位置和大小、意图图标上的数字、真实战斗里的出招、地图上的 Boss 图标、幻影召唤的完整流程、存档和多人。第一批另外没测：真实战斗里的回合顺序（玩家回合开始定意图、敌人回合开始 Break 减 1）、眩晕和少能量的表现、封印时手牌变灰、新图标的导入和显示。第二批另外没测：全体招打到幻影和召唤物时的动画和受击特效、幻影全体重放的表现、混乱改目标后的出牌表现、幻惑落空时的伤害显示、OD 下降后计量条的刷新、吸取回 DP 的显示、新图标的导入和显示。第三批另外没测：病毒时偏导护盾上限和 DP 条的刷新、知性下降时格挡数字、遭遇变种和两只巨肌魔臂错开在真实战斗里的出招。第四批另外没测：修复β／Ω 回 DP 的显示、GoldHopper 逃跑的动画和逃掉后战斗的结束、同种怪错开在真实战斗里的出招、默认幻影 SmallHopper 的新出招和连击吃加成的表现；GoldHopper 的无实体图标、每击 1 的伤害数字和 10 DP 的 DP 条显示，浮游炮—胜负 Lv.2 在真实战斗里的追加伤害。


## 2026-11-15 追加机制 M10–14

这批只对HBR模式的真正敌人生效，旧模式和30A幻影继续使用原来的招式。原出招表的行、权重、一次标记和变种均保留，新招作为表外行动覆盖当前已选招式，不另改生命、DP或普通攻击倍率。原作图标的逐文件来源和SHA保存在 `images/powers/*.sources.json`。

| 敌人 | 新机制 | 原表推进 |
|---|---|---|
| 三柱守卫 | 对玩家的每次攻击按该目标OD等级乘 `1+0.5×等级`；任一存活玩家OD满级，当前招改为全单位单段100，高难125的临终之门。召唤物不继承召唤者OD。 | 替换并消耗原招；OD下降恢复此前已选的同一招，不重掷AI。 |
| BarrelWalker | 入场倒数4；第4、8、12…回合改为全单位单段40的终焉加农，之后重置4。Break暂停倒数；实际DP击破立即重置4并取消已预告的加农。 | 暂停；加农后恢复此前已选招，连同权重结果和一次标记保存。 |
| 钻石眼魔 | 第3、7、11…回合确认DP，第4、8、12…回合分割爆破。实际DP比例达到75%／50%／25%时伤害47／23／15，否则8，全单位单段。Break跳过确认与爆破、取消已确认的爆破，但计时继续。 | 两种追加行动均暂停；HP表转换是唯一允许重新选择HP招的状态切换。 |
| 磐石异蟹 | 原钢盾改名挑衅之盾，保留防升75基础并获得2敌方回合挑衅。玩家单体攻击牌只可选存活挑衅者，多个挑衅者可任选；混乱候选限挑衅者与自己。其他异蟹真实死亡后仅触发一次狂暴，获得永久攻升100基础。 | 原招位置不变；群攻、非攻击牌、药水不受挑衅限制。 |
| 猫角兽α／γ | 原回复DP招在至少3只敌人存活时替换为力量产生器，为所有存活敌人给予200基础攻升、敌方回合结束2。施加属性读取猫角兽，敌人死亡即时更新意图。 | 替换并消耗原回复招；不足3只仍回复DP。 |

意图刷新不每帧扫描，不消耗随机数。OD只在等级跨档时通知，DP与死亡使用已有事件；执行前同步复核，原生 `Monster.PerformMove` 与回合／攻击命令继续负责行动。真正Break切换HP表时最多选一次新HP招。暂停行动在执行和回合末保持原游标，恢复预选普通招时提交其原先已经得到的游标，不多抽一次。暂停招ID、前后游标、一次掩码与计时均走原生SavedProperties保存。

托管检查入口见 [hbr-enemy-flavor](../validation/hbr-enemy-flavor/README.md)，报告绑定本阶段不可变候选DLL哈希。尚未运行Godot界面、意图动画、图标导入、完整游戏存档与双端联机。
