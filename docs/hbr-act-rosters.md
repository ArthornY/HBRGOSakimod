# 三幕怪物名单

2026-11-05 按原版前三幕的池子规模补齐模组遭遇，编队照原作主线的出怪：

| 幕 | 原版 | 弱怪 | 普通 | 精英 | Boss | 合计 | 原有 | 新增 |
|---|---|---|---|---|---|---|---|---|
| 第一幕 | 密林 | 4 | 12 | 3 | 3 | 22 | 7 | 15 |
| 第二幕 | 巢穴 | 4 | 10 | 3 | 3 | 20 | 1（DeathSlug） | 19 |
| 第三幕 | 荣耀 | 3 | 9 | 3 | 3 | 18 | 1（RedCrimson） | 17 |

模组遭遇是**加进**原版池子，不替换原版遭遇。每一幕的登记表在 `Core/Models/Encounters/HbrgoAct{One,Two,Three}EncounterRegistry.cs`，由 `Core/Patches/HbrgoAct*EncounterPoolPatch.cs` 按幕序号（0、1、2）追加到原版的全部、弱怪、普通、精英、Boss 和怪物列表。设置里的“强制模组精英／Boss”只影响精英和 Boss；弱怪、普通怪仍和原版混在一起抽。

## 通用规则

数值写尖塔原生量级，HBR 下照现有规则自动换算：最大 HP ×700，DP 按每点原生 HP 200 生成，攻击走敌方受击曲线（默认体偏），固定 DP 恢复 ×100，状态值和破坏率按地图位置和房型。怪物显示名沿用模组惯例，用原作的英文代号。

下表括号里是高难度（ToughEnemies 加血、DeadlyEnemies 加伤）的数值。

- 攻击下降、防御下降走模组的 `HbrPercentDebuffApplication`：HBR 下是独立的 HBR 能力，旧模式是原版虚弱、易伤。
- “目标有 DP 时再追加”的招式照 AbyssKnocker 的 RageWave 写：意图显示合计值，实际先打本体伤害，再对有 DP 的目标单独追加。

## 第一幕

### 遭遇

| 房型 | 遭遇 | 编队 | 原作出处 |
|---|---|---|---|
| 弱怪 | 一具提线木偶 `MarionetteWeak` | 提线木偶 | MC01_01_Battle01 |
| 普通 | 三只杂鱼 `SmallHoppersNormal` | SmallHopper ×3 | MC01_12_Battle01b |
| 普通 | 嵴冠跳虫群 `CrestHopperSwarmNormal` | CrestHopper ×3（降血，见下） | MC01_12_Battle01event |
| 普通 | 成对的猫角兽 `CatHornsNormal` | 猫角兽 ×2 | MC01_05_Battle01 |
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
| 提线木偶 Marionette | 24–28（26–30） | 牵线：5（6）伤害，给予 1 层攻击下降 → 普通攻击 7（8） |
| 猫角兽 CatHorn | 30–34（32–36） | 双角突刺 4（5）×2 → 横扫 9（10）→ 威吓：力量 +2 |
| 旋轮 WireSlasher | 20–24（22–26） | 回旋斩 6（7）→ 钢线弹射 2×3（2×4） |
| 章鱼尾 OctopusTail | 26–30（28–32） | 缠腕：4（5）伤害，给予 1 层防御下降 → 尾鞭 9（10） |
| 线圈巨轮 TargetWireFrame | 24–28（26–30） | 锁定：格挡 6（8）→ 线圈射击 11（12） |
| 针嘴鸟 NeedleBird（精英） | 82–86（86–90） | 连啄 3×4（3×5）→ 蓄力：力量 +3、格挡 10（12）→ 破甲穿刺 15（17），目标有 DP 时再追加 6（8） |
| 鸦头马 CrowHorse（Boss） | 150（160） | 践踏 13（15）→ 鸦群 3（4）×5 → 嘶鸣：力量 +3，给予 2 层防御下降 → 冲锋 24（27） |
| 象牙异蛛 IvoryScooper（Boss） | 180（192） | 掘取 12（14）→ 缠丝：7（8）伤害，给予 2 层攻击下降 → 碎地 5（6）×3 → 象牙壁：格挡 14（18），回复 25 DP（HBR 下 2500） |

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
| 精英 | 线圈巨轮γ `TargetWireFrameKillerElite` | 杀手跳虫 + 线圈巨轮γ + 杀手跳虫 | MC03ExpMtKinpu02_E02 |
| 精英 | PileGolemEdge `PileGolemEdgeElite` | 石柱像 | MC04ExpMtKumotori01_S01 |
| Boss | DiamondEyeball `DiamondEyeballBoss` | 钻石眼魔 | MC03ExpTraining02Virtual_BOSS01 |
| Boss | BarrelWalker `BarrelWalkerBoss` | BarrelWalker | MC04ExpMtKumotori01_BOSS01 |

### 新怪物

| 怪物 | 原生 HP | 招式循环 |
|---|---|---|
| 死亡玩偶 MarionetteKiller | 44–48（47–51） | 诅咒牵线：7（8）伤害，给予 1 层攻击下降 → 普通攻击 10（11） |
| 杀手跳虫 CrestHopperKiller | 48–52（51–55） | 普通攻击 7（8）→ 连击 3×3（3×4） |
| 杀手重锤 DarkKnockerKiller | 64–68（68–72） | 普通攻击 10（11）→ 蓄力：力量 +3 → 重击 18（20） |
| 猫角兽γ CatHornKiller | 58–62（62–66） | 双角突刺 6（7）×2 → 横扫 14（15）→ 威吓：力量 +3 |
| 章鱼尾γ OctopusTailKiller | 46–50（49–53） | 缠腕：6（7）伤害，给予 1 层防御下降 → 尾鞭 13（14） |
| 深渊重锤S AbyssKnockerKiller（精英） | 150–156（160–166） | 普通攻击 12（13）→ 连续攻击 8（9）×2 → 回复：格挡 14（16），回复 20 DP（HBR 下 2000）→ RageWave 22（25），目标有 DP 时再追加 8（10） |
| 线圈巨轮γ TargetWireFrameKiller（精英） | 90–94（96–100） | 锁定：格挡 12（14）→ 线圈射击 20（22）→ 散射 5（6）×3 |
| 石柱像 PileGolemEdge（精英） | 170–176（180–186） | 岩拳 14（16）→ 硬化：力量 +2、格挡 20（24）→ 崩落 7（8）×3 |
| 钻石眼魔 DiamondEyeball（Boss） | 320（340） | 凝视：10（11）伤害，给予 2 层防御下降 → 棱光射线 24（27）→ 晶面护盾：格挡 20（24），回复 30 DP（HBR 下 3000）→ 光束齐射 6（7）×4 |
| BarrelWalker（Boss） | 360（380） | 炮击 14（16）→ 连射 5（6）×4 → 装填：力量 +3、格挡 15（18）→ 重炮 30（34） |

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
| 普通 | 成对的花冠利刃 `CorollaKnivesNormal` | 花冠利刃 ×2 | MC04ExpKirigamine02_02_R05 |
| 普通 | 门与花冠 `BigotryGateAndCorollaKnivesNormal` | 花冠利刃 + 异议之门 + 花冠利刃 | MC04ExpKirigamine02_05_R04 |
| 普通 | 巨肌魔臂与三爪镖 `MolArmsAndTriClawNormal` | 巨肌魔臂 + 三爪镖 | MC04BExpDay03Desert01_R05 |
| 普通 | 两只巨肌魔臂 `MolArmsPairNormal` | 巨肌魔臂 ×2 | MC04BExpDay05Desert01_R05 |
| 普通 | 门与诅咒玩偶 `BigotryGateAndMarionetteKillersNormal` | 死亡玩偶 + 异议之门 + 死亡玩偶 | MC04BExpDay01Kisokoma01_R03 |
| 精英 | 两只磐石异蟹 `ShrimpCrabsElite` | 磐石异蟹 ×2 | MC04ExpKirigamine02_01_BOSS01 |
| 精英 | SlimySerpent `SlimySerpentElite` | 触手巨蛇 | MC04BExpDay04Desert01_S01 |
| 精英 | GiraffeHalo `GiraffeHaloElite` | 长颈怪环 | MC04BExpDay01Kisokoma01_BOSS01 |
| Boss | ArachneLine `ArachneLineBoss` | 阿拉克尼巨蛛 | MC04ExpPatrol02_BOSS01 |
| Boss | PillarGuardian `PillarGuardianBoss` | 三柱守卫 | MC04BWilDay04MilitaryFacility01_BOSS01 |

### 新怪物

| 怪物 | 原生 HP | 招式循环 |
|---|---|---|
| 灯胆跳虫 BalbFlea | 52–56（56–60） | 闪光：9（10）伤害，给予 1 层攻击下降 → 冲撞 14（15） |
| 三爪镖 TriClaw | 58–62（62–66） | 三连爪 6（7）×3 → 飞镖 16（18） |
| 异议之门 BigotryGate | 100–106（106–112） | 闭门：格挡 14（17）→ 冲击 18（20）→ 回响：7（8）×2伤害，给予 1 层防御下降 |
| 花冠利刃 CorollaKnife | 78–82（83–87） | 斩击 15（17）→ 花冠旋舞 5（6）×4 → 磨刃：力量 +3 |
| 巨肌魔臂 MolArms | 120–126（128–134） | 重拳 17（19）→ 双臂猛击 8（9）×2 → 怒吼：力量 +3、格挡 12（14） |
| 磐石异蟹 ShrimpCrab（精英） | 120–126（128–134） | 钳击 16（18）→ 硬壳：格挡 16（20），回复 20 DP（HBR 下 2000）→ 连钳 7（8）×3 |
| 触手巨蛇 SlimySerpent（精英） | 250–260（266–276） | 缠绕：12（14）伤害，给予 2 层攻击下降 → 尾击 26（29）→ 蜕皮：力量 +2、格挡 18（22）→ 毒牙乱咬 6（7）×4 |
| 长颈怪环 GiraffeHalo（精英） | 240–250（256–266） | 光环：10（12）伤害，给予 2 层防御下降 → 长颈鞭 24（27）→ 环光弹 5（6）×5 → 祈光：力量 +3、格挡 18（22） |
| 阿拉克尼巨蛛 ArachneLine（Boss） | 420（450） | 丝线束缚：12（14）伤害，给予 2 层攻击下降 → 八足连斩 6（7）×6 → 织网：格挡 25（30），回复 40 DP（HBR 下 4000）→ 断线 36（40） |
| 三柱守卫 PillarGuardian（Boss） | 460（490） | 柱击 20（23）→ 三柱共鸣：力量 +4，给予 2 层防御下降 → 震地 8（9）×4 → 守护：格挡 30（36），回复 50 DP（HBR 下 5000）→ 崩柱 40（45） |

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

幻影的攻击意图按意图重放伤害，不执行招式本体。“攻击 + 减益”的招式实现了 `IHbrgoPhantomAttackEffects`，由 `PhantomHelper` 在重放后补上减益，每次意图一次：

- 第一幕：牵线、缠腕、缠丝。
- 第二幕：诅咒牵线、缠腕（章鱼尾γ）、凝视。
- 第三幕：闪光、回响、缠绕、光环、丝线束缚。

非攻击招式（威吓、锁定、蓄力、嘶鸣、象牙壁、硬化、回复、晶面护盾、装填、闭门、磨刃、怒吼、硬壳、蜕皮、祈光、织网、三柱共鸣、守护）照原样执行。

## 立绘

立绘取自本机解包库里街机模式的敌人卡图（`Assets/Contents/Image/Arcade/Enemy/Season*/`，512×704），用脚本抠出：

- 卡图共用一张城市背景，但天空每张不同。背景只在卡框和底部色带可靠，所以用全部 94 张卡的逐像素众数还原卡框和色带，再用 OpenCV GrabCut 按每张卡自己的边缘配色分出生物。
- 底部半透明色带按“观察值 = 斜率 × 场景 + 截距”逐通道反算；参数用所有卡图跨过色带上沿的上万对像素回归得到，粉色色带拟合误差约 1。色带上的装饰线和边缘接缝用修补填平，落在地面上的影子按“变暗的地面颜色”剔除。
- 有同姿势剪影 Mask 的用 Mask 定轮廓：针嘴鸟、象牙异蛛；杀手跳虫、杀手重锤、深渊重锤S、钻石眼魔；巨肌魔臂、阿拉克尼巨蛛。石柱像、BarrelWalker 的 Mask 和卡图姿势对不上，只用 GrabCut。
- 被卡框裁掉的肢体在边缘淡出。

每张图的原始资源路径、哈希和抠图参数记在 `images/monsters/hbrgo_act{1,2,3}/sources.json`。BarrelWalker 和巨肌魔臂取自 Season4，其余取自 Season1。六个新 Boss 的地图图标在 `images/map/boss_nodes/`（crow_horse、ivory_scooper、diamond_eyeball、barrel_walker、arachne_line、pillar_guardian），和 DeathSlug 等一样是 1254×1254 的居中剪影，描边版与本体相同。新贴图还没有 `.import`，要等打包时由 Godot 导入。

## 验证

- stable、beta 严格编译 0 警告 0 错误（候选 DLL `EB9F0812…`）。
- 套件 [hbr-act-rosters](../validation/hbr-act-rosters/README.md) stable、beta 各 3540 项：三幕登记表规模，原版密林／巢穴／荣耀池子实际追加的遭遇和怪物，51 个遭遇的编队和 HP 预算，28 种怪的 HP 和招式循环，全部招式在 HBR／旧模式和两档难度下的实际效果，幻影补效果，融合效果和幻影攻击段，本地化键，场景和贴图哈希，×700 HP。
- 回归：敌方技能 650，跨幕成长 stable／beta 各 161，敌方资源 169，敌方回复 63，DP 恢复 stable／beta 各 91，资源倍率 81，检查数都和改动前相同。
- 日志和报告：第一幕在 `D:\stsmod\outputs\act-one-roster-20261105\`，第二、三幕和三幕合并后的套件在 `D:\stsmod\outputs\act-two-three-roster-20261105\`。

没有在游戏里测：立绘的位置和大小、意图图标上的数字、真实战斗里的出招、地图上的 Boss 图标、幻影召唤的完整流程、存档和多人。
