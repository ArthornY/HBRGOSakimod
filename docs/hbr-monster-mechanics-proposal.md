# 三幕新怪机制改造方案（第二版）

2026-11-07。只是方案，没有改代码。对象是 11-05 加的 28 种新怪（见 [hbr-act-rosters.md](hbr-act-rosters.md)）。用户的反馈是「怪的机制感觉太千篇一律了」。

第一版的机制多数是按外形和名字设计的。用户指出 hbr.quest 写了敌人技能，这一版改成照原作每只怪的技能和出招表来做。和第一版的差别见文末。

同日按用户补充修订，原话「其实可以新增效果的，如果mod里没有」：原作效果在模组或原版里没有对应的，照原作新增，不再拿近似的代替。见第三节。

同日第一批 5 只（针嘴鸟、钻石眼魔、异议之门、磐石异蟹、三柱守卫）已按本方案落地，第六节 6 条按「全局统筹」给用户的建议定。实现规则和与方案的出入写在 [hbr-act-rosters.md](hbr-act-rosters.md) 的「原作出招表」一节。

2026-11-08 第二批 9 只（鸦头马、象牙异蛛、深渊重锤S、线圈巨轮γ、石柱像、BarrelWalker、触手巨蛇、长颈怪环、阿拉克尼巨蛛）也已落地，补了幻惑、混乱、OD 下降、吸取和全体攻击入口（第六节第 1 条按用户定的范围：怪物全体招打玩家一方全部单位，幻影全体招打所有敌人）。写在 hbr-act-rosters.md 的「原作出招表（第二批）」一节。

2026-11-08 第三批 14 种普通怪和弱怪也已落地，28 只全部按原作出招表；补了病毒、知性下降和遭遇设置的变种，两只巨肌魔臂错开一步。长颈怪环 Judgment Ray 的幻惑改回原作必中（「打过格挡才附加」只管按概率附加的）。2026-11-14 用户定知性下降保持需求二十五的 −80%、3个敌方回合结束计时；2026-11-08 的 −25%意见已被取代。旧非HBR模式的格挡 −25%等效适配保留。

2026-11-08 第四批：第一幕原有的 7 只（SmallHopper、CrestHopper、HealHopper、GoldHopper、Knocker、ClayKnocker、AbyssKnocker）也照原作出招表改了（用户原话「也照原作改」），按各自遭遇的原作关卡取变种；HealHopper 第一幕、第二幕训练机两个变种由遭遇设置，同场的同种怪错开。写在 hbr-act-rosters.md 的「原作出招表（第四批）」一节。

**用户 2026-11-08 定：这 7 个先不改**——手冢咲、Red Crimson（两个）、FlatHand、DeathSlug、SkullFeather、奈良良柴犬、萤的浮游炮。

同日用户又定（原话「按建议办」）：前三批 17 场同表的同种怪也错开。GoldHopper 先按数值模型把原生 HP 降到 22–26，随后用户改成「但coldhopper给99层无实体 然后dp和hp跟原作一样」：作为敌人带 99 层原版无实体，HP 1、DP 10 取原作的固定值，每击只算 1，一共 11 击；降血那版作废。写在 hbr-act-rosters.md 的「GoldHopper 的无实体和 HP／DP」。

## 2026-11-15 用户修订（怪物需求六至九）

本节为当前HBR数值规则，取代下文早期方案的相应意见；旧模式控制、1点行动锁与3／4／5回合Break保留。HBR敌方封印／束缚／眩晕／幻惑／混乱／病毒命中即施加，不再要求打过格挡；范围、存活、玩家／召唤物、DP限制不变，30A幻影侧不加强。束缚和眩晕下一次能量重置共同只失去[EE]，不足扣到0，多份不重复扣。

六只表怪的敌方攻降／防降用原作上限：通常30%、Rage Magnum25%、Mirage Pulse两类各20%，高难再×1.25；时钟和不限份数不变。特殊Boss公共入口以攻降25%、防降50%为基础，幻影保留15%。病毒、幻惑、混乱在玩家身上按自身回合结束2／2／3次退出，敌方持有者计时不变。

HBR普通外壳Break改为普通2、精英3、Boss4回合；旧模式仍为3／4／5。按当前不眩晕流程，hp表分别可放1／2／3招，含回壳当回合。Hello World继续加在新基数上；SkullFeather专属时长、手冢咲永久Break、柴犬分层DP、幻影不Break均不改。没有执行HP×1.5、伤害×1.5、破坏率等其余加强提案。

实现与验证见[hbr-act-rosters.md](hbr-act-rosters.md#2026-11-15-用户指定的敌方加强需求六至九)；下文原始数值保留为方案历史，不代表当前规则。

## 一、原作数据

### 来源

- 每只敌人的详情：`https://master.hbr.quest/v1/cn/enemies/<label>.json`，2026-11-07 抓取。里面有技能（每个技能分成几段效果：类型、斩突打、目标、威力、数值、持续回合）和两张出招表。
- 关卡到敌人：`https://master.hbr.quest/v1/cn/battles.json`，按 hbr-act-rosters.md 的「原作出处」关卡号查出每只怪的原作变种。**28 种都找到了，没有缺数据的。**
- 敌人技能的说明文字是空的。效果的含义取自两处：
  - hbr.quest `skills.json` 里我方技能的同类效果说明，比如灾厄、封印、混乱、眩晕；
  - appmedia 的状态异常页（2026-11-07 查），给出束缚、封印、混乱、幻惑、病毒、眩晕的效果；
  - game8 的「厄」说明页，即灾厄。
- 留档在 `D:\stsmod\outputs\monster-mechanics-hbrquest-20261107\`：
  - `enemies\`：44 份详情，含 28 种用到的全部变种，另加旋轮的光、雷、冰三种属性；
  - `enemies.json`、`battles.json`、`fetched-at.txt`；
  - `decode.py` 生成 `decoded.md`，逐只列出技能和出招表；
  - `calibrate.py` 生成 `calibrated.md`、`calibrated.json`，是下文的逐招数值。

### 原作怎么出招

从数据里能看到这些规则：

- **两张表**：dp 表在 DP 还在时用；hp 表在 DP 被打空、处于 Break 时用。
- **按步走**：表按步骤一步一步走，走完回到第 1 步。同一步有几个选项时按权重随机。权重和不一定是 100，按比例算。
- **一次（once）**：标了的步骤只走一次。第三幕的长颈怪环、三柱守卫、触手巨蛇、巨肌魔臂、三角镖、诅咒玩偶，前半张表全是「一次」。开场走完以后，它们只在最后几步里循环。
- **条件**：只有针嘴鸟一处，`IsEnemyCharge()`：蓄过力才放 Ruin Bullet。
- **先制**：只有线圈巨轮β 开战时的剧情技，见线圈巨轮γ。
- **hp 表的收尾**：多数 hp 表最后一步是回复 DP，推测原作靠这一步结束 Break。异议之门、花冠利刃没有 hp 表；象牙异蛛、杀手重锤的 hp 表里没有回复。
- **防御权重**：每个攻击都写了受击方体力和精神的权重，2:1 或 1:2。这正好对应模组的体偏、精偏（`HbrEnemySkillProfiles.Get`）。全体攻击多数是精偏，下文逐招标出，没标的是体偏。
- **威力**：技能威力是整招的合计，按段数平分。

## 二、机制库

先列这批怪共用的机制，第四节逐只引用。每条写三件事：

- **结算**：HBR 和旧模式下怎么算；
- **幻影**：被 30A 召唤成幻影时怎么表现；
- **新东西**：要不要新写能力或图标。

原版已有的意图图标够用：攻击、增益、减益、强减益、防御、回复、未知。下面不再逐条写意图。

幻影的现状：

- 攻击意图按意图重放伤害，不执行招式本体；
- 非攻击招式照原样执行，目标是敌人；
- 攻击附带的效果靠 `IHbrgoPhantomAttackEffects` 补上；
- 塞状态牌会被 `PhantomPlayerBenefitContext` 换成玩家收益；
- 幻影的 DP 被打空时不进 Break（`IsDefaultBreakSuppressed`）。

### M1 两张出招表（dp／hp）

DP 在时走 dp 表。DP 被打空时，按现有规则先眩晕一回合，然后从 hp 表第 1 步开始（2026-11-09 用户决定普通外壳打空 DP 不再眩晕：当前意图照常放出，下一招起走 hp 表，见 `hbr-act-rosters.md`「打空 DP 不再眩晕」）。Break 结束、重新获得高维外壳时，回到 dp 表第 1 步，标了「一次」的步骤跳过。没有 hp 表的怪，Break 前后都照 dp 表往下走，不重置。

- **结算**：
  - 两种模式一样，判断依据都是有没有 DP。
  - 钩子现成：`IHighDimensionalShellObserver` 的 `AfterHighDimensionalShellBreak`、`OnHighDimensionalShellApplied`，Narara、SkullFeather 已经在用。
  - 现有代码在打空 DP 的眩晕之后，会恢复被打断的那招。这批怪要改成转到 hp 表，否则被打断的 dp 招会在 Break 里放出来。
  - 2026-11-15修订：Break 为普通2／精英3／Boss4回合，打空DP不眩晕；普通怪放1招 hp 表、精英2招、Boss3招（含回壳当回合）。所以原作 hp 表只用到前几步；最后的「回复 DP」由 Break 结束时重新获得外壳代替。
- **幻影**：幻影不进 Break，一直走 dp 表。
- **新东西**：建议写一个通用的出招表构建器，用 `RandomBranchState`、`ConditionalBranchState` 拼状态机，FlatHand、DeathSlug 已经用过这两个类。28 只是同一种结构，每只只填表；校验套件可以直接拿表和留档的原作数据比对。存档要记当前用哪张表、走到第几步、哪些「一次」用过了。

### M2 加权随机

同一步有几个选项时，按原作权重随机。意图在上一回合结束时定好，和原版随机怪一样。

- **结算**：和数值模式无关。
- **幻影**：用幻影自己的随机数，联机同步。
- **新东西**：不用。

### M3 开场段和循环段

标了「一次」的步骤组成开场段，只走一遍；之后在剩下的步骤里循环。第三幕的几只就是这种结构：开场打一套组合，之后固定在 2 到 4 步里转，通常是「攻击力上升 → 大招」。

- **结算**：两种模式一样。
- **幻影**：每次召唤从头走开场段。
- **新东西**：构建器支持「一次」就行。

### M4 蓄力和条件出招

只有针嘴鸟用：Charge01 给自己挂「蓄力」，下一步 Ruin Bullet 的条件是蓄力中。

原作里眩晕能打断敌人蓄力（appmedia）。模组打空 DP 本来就会眩晕一回合，所以规则定为：被眩晕时清掉蓄力，Ruin Bullet 的条件不满足，这一步跳过。

- **结算**：两种模式一样。打断靠打空 DP，或者别的眩晕来源。
- **幻影**：幻影也要先蓄力，才放 Ruin Bullet。幻影的 Ruin Bullet 怎么打敌人，见第三节的效果对照。
- **新东西**：新能力「蓄力」1 个，图标用原作 `IconCharge`。

### M5 全体攻击和目标

- **单体（Single）**：照现在的 `PerformAttack`。它走原版怪物攻击，目标是全部玩家角色（`AttackCommand` 取 `PlayerCreatures`），不含召唤物。所以单人局里，单体和全体打的是同一个人。
- **全体（All）**：所有玩家角色，再加玩家一方的召唤物，比如 30A 的幻影、亡灵契约师的 Osty。这样原作的单体、全体才有区别；要不要这样做，要用户定。
- **DP 最低（MinimumDp）**：只有 BarrelWalker 的 `AttackSingle02_Fire`。打 DP 最低的玩家；多人时才有区别。
- **结算**：伤害照现有换算。体偏、精偏按原作标在 `HbrEnemySkillProfiles.Get` 里。
- **幻影**：幻影的全体招式要不要打所有敌人，和上面同一个问题一起定。现有幻影只打一个目标；30A 已有「横扫」（`PHANTOM_NEXT_ATTACK_ALL_ENEMIES_POWER`），可以借它的打法。
- **新东西**：一个「打玩家一方全部单位」的攻击入口。

### M6 回复 DP 和吸取

- **回复 DP**（HealDp）：回复量按「原作回复量 ÷ 原作最大 DP」的比例，乘模组里这只怪的最大 DP，超过 100% 就是回满。比如异议之门回 85%，长颈怪环回满。
- **吸取**（HealDpByDamage）：按这一招实际打掉的 DP 和生命，回复自己的 DP。原作是伤害的 400%。
- **结算**：
  - 回复按比例算，两种模式都不再另外 ×100。
  - 吸取在 HBR 下照原作 400%。旧模式先按 50%：旧模式的 DP 只有生命的两成，照搬会一下回满，50% 和 HBR 下的效果差不多。
  - Break 中不能回 DP 是模组现有规则，照旧。
- **幻影**：回的是幻影自己的 DP，按幻影 DP 上限的比例；吸取作为攻击附带效果补上。
- **新东西**：不用。

### M7 攻击力上升、防御力上升

原作是临时的百分比增益，持续 2 回合（到下一个敌人回合结束），实际只影响下一招：

- 攻击力上升：异议之门、阿拉克尼巨蛛 +20%，其余 +100%；
- 防御力上升：磐石异蟹的 Steel Shield，受到伤害 −75%。

原版力量是永久的；HBR 下怪物的力量又是「忽略目标的体力和精神」，和原作对不上。所以新写两个能力。

- **结算**：
  - 攻击力上升乘在最终伤害上，两种模式一样。意图直接显示加成后的数字，玩家看得到下一招翻倍。
  - 防御力上升在 HBR 下进防御力下降的共享加算区，记成负值；旧模式单独一个乘区。
- **幻影**：幻影给自己上。重放攻击时要读攻击力上升，`PhantomHelper` 生成攻击段时加一步。
- **新东西**：新能力 2 个，图标用原作的 `IconBuffAttack`、`IconBuffDefense`。详见第三节。

### M8 给玩家的状态

见第三节的对照表。原作的封印、束缚、幻惑、混乱、病毒、眩晕都按概率附加，并受运影响。尖塔里怪物的减益一般是确定的，看意图就知道。

建议改成：**这一招有伤害打进 DP 或生命（没被格挡完全挡住）才附加**。多段和全体攻击只要有一段打进去就算；不带伤害的招式照常附加。这样玩家可以靠格挡躲掉，比随机更好读。要用户定。

## 三、效果对照

用户补充：「其实可以新增效果的，如果mod里没有」。所以原作效果在模组或原版里有对应的，就直接用；没有的照原作新增，不再拿近似的东西代替。

### 3.1 总表

| 原作 | 原作效果 | 用现有／新增 | 模组里用什么 |
|---|---|---|---|
| AttackSkill（单体） | 单体攻击 | 现有 | `PerformAttack` |
| AttackSkill（全体） | 打全体 | 新增，待定 | 打玩家一方全部单位的攻击入口（M5）；不做就和单体一样 |
| AttackSkill（DP 最低） | 打 DP 最低的角色 | 新增 | 按 DP 选目标（M5） |
| HealDp | 回复自己的 DP | 现有 | `HighDimensionalShellPower.Restore`，按比例（M6） |
| HealDpByDamage | 按伤害回 DP | 现有 | 同上，招式里按实际伤害算（M6） |
| AttackDown | 攻击力下降 | 现有 | `HbrAttackDownPower`，旧模式原版虚弱 |
| DefenseDown | 防御力下降 | 现有 | `HbrDefenseDownPower`，旧模式原版易伤 |
| Misfortune | 灾厄（日文「厄」） | 现有 | `HbrCalamityPower`「灾厄」 |
| DpBreakRandom | DP 破坏 | 现有 | 偏导护盾的 DP 直接清零 |
| OverDrivePointDown | OD 下降 | 现有 | 超频计量直接减少 |
| Charge | 蓄力 | 新增 | 「蓄力」 |
| AttackUp | 攻击力上升 | 新增 | 「攻击力上升」 |
| DefenseUp | 防御力上升 | 新增 | 「防御力上升」 |
| SpiritDownRate | 精神下降 | 新增 | 「精神下降」 |
| WisdomDownRate | 知性下降 | 新增 | 「知性下降」，和精神下降共用一套 |
| ImprisonRandom | 封印 | 新增 | 「封印」 |
| BindRandom | 束缚 | 新增 | 「束缚」 |
| StunRandom | 眩晕 | 新增 | 「眩晕」，和束缚共用一套 |
| IllusionRandom | 幻惑 | 新增 | 「幻惑」 |
| ConfusionRandom | 混乱 | 新增 | 「混乱」。原版也有「混乱」，但效果是抽到的牌费用随机，和原作不同，不用 |
| VirusRandom | 病毒 | 新增 | 「病毒」 |
| ContinuousDpDamageRate | 持续 DP 伤害 | 新增 | 「持续 DP 伤害」 |
| 先制剧情技 | 线圈巨轮β 开战打自己一方 | 不搬 | |

新增的能力 12 个（另有两项目标选择，见 M5）。束缚和眩晕、精神下降和知性下降各共用一套代码，实际写 10 套。图标全部能用原作解包库 `Assets/UI/Texture/BattleIcon/` 里的，不用另画；意图都用原版已有的。

玩家分三种情况：

- **模组角色、HBR 数值**：有六维属性、偏导护盾（DP）、超频（OD）。
- **模组角色、旧模式**：有偏导护盾和超频，六维属性不参与结算。
- **原版角色**（铁甲战士等）：三样都没有，一直按旧模式结算。

针对 DP、OD 的效果（DP 破坏、病毒、持续 DP 伤害、OD 下降）对原版角色不生效，这些招只剩伤害。灾厄照现有规则，只在 HBR 数值下降属性。其余新效果三种情况都有结算，见下。

### 3.2 用现有的几项

- **攻击力下降、防御力下降**：层数按原作百分比：15% 及以下 1 层，20% 2 层，高难多 1 层。旧模式用同一个数作为虚弱、易伤的回合数。模组每份固定 2 回合，原作 4 回合的那几招按 2 回合算。幻影：照样施加给敌人。
- **灾厄**：
  - 模组的「灾厄」就是原作这个效果，图标同为原作 `IconMisfortune`，日文叫「厄」。
  - 原作技能里的 2～3 是持续回合：我方技能写「灾厄状态 2 回合」，威力就是 2。
  - 每次降多少，原作数据里没写。game8 的厄说明页评论区有玩家写「380−厄20＝360」，即状态 −20（不是官方数据）。所以给 20 层，六项属性各 −20，大约让伤害、格挡各降一成多。
  - 高难 30 层：模组灾厄固定 2 回合，原作高难的 3 回合用层数补。
  - 旧模式和原版角色：照现有规则，不降属性。
  - 幻影：给敌人同样层数，状态值 E −20。
- **DP 破坏**：模组角色的偏导护盾 DP 直接清零。幻影：敌人当前 DP 减半；直接清零会立刻 Break 加眩晕，等于幻影两回合打出一次 Break。
- **OD 下降**：模组角色的超频计量 −5，高难 −10。原作每级 OD 是 100%，模组每级 100 点（单人），可以直接对上。幻影：敌人没有 OD，反过来让玩家获得同样点数。

### 3.3 新增的效果

每条写：是什么、对应原作哪个效果、出现在哪；HBR 和旧模式怎么结算；幻影怎么表现；图标；工作量。工作量分三档：

- **小**：一个能力类，加本地化、图标和校验；
- **中**：还要接进伤害、属性、出牌或 DP 的现有流程，或者玩家、敌人两边都用；
- **大**：要改现有的公共系统。

#### 蓄力

- **是什么**：原作 `Charge`。给自己挂「蓄力」，下一招 Ruin Bullet 只在蓄力中才放（原作条件 `IsEnemyCharge()`）。被眩晕时移除，包括打空 DP 的眩晕；放出 Ruin Bullet 后移除。出现在针嘴鸟。
- **结算**：两种模式一样。
- **幻影**：幻影也要先蓄力，才放 Ruin Bullet。
- **图标**：原作 `IconCharge`；意图用原版「增益」。
- **工作量**：小。

#### 攻击力上升

- **是什么**：原作 `AttackUp`。攻击伤害 +20% 或 +100%，到它下一回合结束移除，实际只影响下一招。
  - +20%：异议之门、阿拉克尼巨蛛；
  - +100%：灯胆跳虫、BarrelWalker、花冠利刃、长颈怪环、诅咒玩偶、巨肌魔臂、石柱像、三柱守卫、触手巨蛇、三爪镖。
- **结算**：两种模式都乘在最终伤害上。意图直接显示加成后的数字，玩家看得到下一招翻倍。
- **幻影**：幻影给自己上；`PhantomHelper` 生成攻击段时读这个加成。
- **图标**：原作 `IconBuffAttack`；意图用原版「增益」。
- **工作量**：中。要接进敌人的伤害、意图数字和幻影重放。

#### 防御力上升

- **是什么**：原作 `DefenseUp`。受到的伤害 −75%，到它下一回合结束。出现在磐石异蟹的 Steel Shield。
- **结算**：
  - HBR：进防御力下降的共享加算区，记成 −75%，和玩家给它的防御力下降相加；
  - 旧模式：受到的攻击伤害 ×0.25，单独一个乘区。
- **幻影**：幻影给自己上。
- **图标**：原作 `IconBuffDefense`；意图用原版「防御」。
- **工作量**：中。两种模式各接一处。

#### 精神下降、知性下降

两个共用一套「属性按比例下降」。

- **是什么**：
  - 精神下降：原作 `SpiritDownRate`，精神 −50%，2 回合。出现在异议之门的 Strong Impulse（全体）。
  - 知性下降：原作 `WisdomDownRate`，知性 −80%，3 回合。出现在巨肌魔臂的 Deceive Shot。
- **结算**：
  - HBR：读属性时按比例扣，和灾厄走同一个读属性入口。
    - 精神只用在挨打时的防御权重，所以结果是挨打更痛。体力、精神相同时，精偏攻击的防御降三分之一，体偏降六分之一。
    - 模组的知性除了管施加减益的效果，还直接决定格挡量（格挡 = 500 × 倍率 × 知性 ÷ 100）。知性下降期间格挡只剩两成，比原作重（原作的知性不管防御），见第六节。
  - 旧模式、原版角色：没有六维属性，按 HBR 下的结果直接改数。精神下降：受到的攻击伤害 +25%，取体偏、精偏之间；知性下降：从卡牌获得的格挡 −80%。
- **幻影**：敌人只有一个状态值 E，没有六维属性，按上面「没有六维属性」那条结算：精神下降让敌人挨打 +25%，知性下降让敌人获得的格挡 −80%。
- **图标**：原作 `IconSpirit`、`IconWisdom`。原作这两张是属性图标，升降共用，模组要加一个向下的角标。意图用原版「减益」。
- **工作量**：中。读属性入口加一项比例；没有属性时另写两条改数。

#### 封印

- **是什么**：原作 `ImprisonRandom`：一回合不能用技能，只能普攻，原作描述是「封印强攻击」。模组：下回合费用 2 及以上的牌（含 X 费）不能打出；便宜的牌相当于普攻，照常能出。出现在猫角兽、猫角兽γ 的 Creepy Ball，磐石异蟹的 Mirage Pulse。
- **结算**：两种模式一样。
- **幻影**：原作封印对敌人也是只剩普攻。敌人下一招如果不是「不带附加效果的单段攻击」，就跳过。
- **图标**：原作 `IconImprison`；意图用原版「减益」。
- **工作量**：中。出牌限制照原版缠结、昏眩的写法；敌人一侧要判断哪招算普攻。

#### 束缚、眩晕

两个共用一套。

- **是什么**：
  - 束缚：原作 `BindRandom`，一回合不能行动，可以换到后排等它结束。出现在钻石眼魔的 Laser Bind。
  - 眩晕：原作 `StunRandom`，一回合不能行动，概率 30%。出现在三柱守卫的 Paralysis Burst（全体）。
  - 模组：下回合开始时少 1 点能量。
  - 为什么是 1 点：原作只停三名前卫里的一个人。尖塔里玩家就是整支队伍，整回合不能动太重，按「三人少一人」折成三分之一的行动。见第六节。
- **结算**：两种模式一样。
- **幻影**：敌人一侧原作就是一回合不能行动，用模组现有的敌人眩晕（`CreatureCmd.Stun`）。为免幻影一直控住同一个敌人，同一个目标每场最多一次。
- **图标**：原作 `IconBind`、`IconStun`；意图用原版「减益」。
- **工作量**：小。下回合扣能量；幻影一侧调现有眩晕。

#### 幻惑

- **是什么**：原作 `IllusionRandom`：攻击按概率落空，失误率 50%（长颈怪环 70%）；判定在攻击开始，多段一起落空，持续 2 回合。模组：每层让你打出的下一张攻击牌伤害全部落空，牌上别的效果照常；落空一次减一层，最多留 2 回合。
  - 线圈巨轮、线圈巨轮γ 的 Illusion Spin：1 层；
  - 长颈怪环的 Judgment Ray：全体、必中、失误率更高，2 层。
- **结算**：两种模式一样，两条伤害路线都要拦。
- **幻影**：敌人的下一次攻击落空。
- **图标**：原作 `IconIllusion`；意图用原版「减益」。
- **工作量**：中。

#### 混乱

- **是什么**：原作 `ConfusionRandom`：3 回合内，行动按概率打到自己。出现在石柱像的 Confusion Spike。模组：
  - 3 回合内，每回合你打出的第一张单体攻击牌，目标改为在全体敌人和你自己之间随机选；
  - 打到自己时，按这张牌的原始伤害当作敌人攻击结算，走现有的敌方换算，先扣格挡和 DP。不按玩家的出伤算，否则 HBR 下一张打击就是几千点。
- **结算**：两种模式一样。
- **幻影**：敌人的下一次攻击，一半概率改为打它自己。
- **图标**：原作 `IconConfusion`；意图用原版「减益」。
- **工作量**：中。要改出牌目标，打自己时走受伤流程。

#### 病毒

- **是什么**：原作 `VirusRandom`：DP 上限 −30%，2 回合；到期上限恢复，但 DP 不会跟着回来（appmedia）。出现在章鱼尾、章鱼尾γ 的 Erosion Slice。
- **结算**：模组角色在两种模式下都有偏导护盾：DP 上限 −30%，超出新上限的 DP 去掉；2 回合后上限恢复，DP 不回。原版角色没有 DP，不生效。
- **幻影**：同一个能力挂在敌人身上，压高维外壳的上限。
- **图标**：原作 `IconVirus`；意图用原版「减益」。
- **工作量**：中。偏导护盾和高维外壳两边都要接上限修改。

#### 持续 DP 伤害

- **是什么**：原作 `ContinuousDpDamageRate`：每回合失去 DP 上限的 5%，4 回合，只扣 DP，不碰生命。出现在三柱守卫的 `PillarGuardian_AttackSingle01`。
- **结算**：模组角色在自己回合开始时失去偏导护盾上限的 5%，DP 为 0 就不扣。按比例算，两种模式一样。原版角色没有 DP，不生效。
- **幻影**：同一个能力挂在敌人身上，扣高维外壳；扣到 0 照常 Break。
- **图标**：原作 `IconContinuousDpDamage`；意图用原版「减益」。
- **工作量**：小到中。

### 3.4 附加条件

见 M8：原作按概率附加的封印、束缚、眩晕、幻惑、混乱、病毒，改成这一招有伤害打进 DP 或生命才附加。

## 四、逐只方案

格式：

- 每只先写原作 label、原作名、出处关卡、原作 DP／HP。
- 表里是原作技能 label、目标和段数、尖塔数值（括号里是高难）、附带效果。
- 「回 DP」是占最大 DP 的比例（M6）。
- 下面两行是出招表，「/」后面是同一步的其他选项，比例是权重。技能名省掉怪物前缀（`Attack01` 即 `CatHorn_Attack01`）。
- 数值规则：每只怪每回合平均伤害和 11-05 版持平，普通、高难分开算；按原作威力的比例分到各招；攻击力上升后的那一招按加成算进平均。死亡玩偶、磐石异蟹按这个规则单发会过高，封顶在 11-05 版最大一击的 1.25 倍。

★ 是第一批推荐（第五节）。

### 精英和 Boss

#### ★ 针嘴鸟 NeedleBird（第一幕精英）

原作 `NeedleBirdSmallVirtual_MC02`（[训练机]针嘴鸟，MC02_20_Battle01），DP 27800／HP 31600。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `NeedleBird_AttackSingle01` | 单体 ×1 | 14（17） | |
| `NeedleBird_AttackAll01`「Murder Blade」 | 全体 ×1，精偏 | 16（19） | |
| `NeedleBird_Charge01` | 自身 | | 蓄力 |
| `NeedleBird_AttackSingle02_Braek`「Ruin Bullet」 | 单体，无伤害 | | DP 破坏；条件：蓄力中 |
| `NeedleBird_Buff01` | 自身 | | 回 DP 3% |

- dp 表：① AttackSingle01 → ② Charge01 → ③ Ruin Bullet（条件）→ ④ AttackSingle01 / AttackAll01 60:40 → ⑤ AttackSingle01 / AttackAll01 40:60
- hp 表：① AttackSingle01 / AttackAll01 40:60 → ② Charge01 → ③ Ruin Bullet（条件）→ ④ AttackSingle01 / AttackAll01 60:40 → ⑤ Charge01 → ⑥ Ruin Bullet（条件）→ ⑦ Buff01
- 蓄力那一回合打空它的 DP，眩晕会清掉蓄力，Ruin Bullet 就放不出来。

#### 鸦头马 CrowHorse（第一幕 Boss）

原作 `CrowHorseDefaultVirtual`（[训练机]鸦头马，MC02_15_Battle01），DP 21600／HP 32400。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `CrowHorse_Attack01` | 单体 ×1 | 12（14） | |
| `CrowHorse_Attack02`「Destruction Ball」 | 单体 ×1 | 15（18） | OD 下降 |
| `CrowHorse_AttackAll01`「Extinction Ray」 | 全体 ×1，精偏 | 13（16） | |
| `CrowHorse_Heal01` | 自身 | | 回 DP 14% |

- dp 表：① Attack01 → ② Attack01 / Attack02 50:50 → ③ AttackAll01 → ④ Attack01 / Attack02 50:50 → ⑤ Attack01 / AttackAll01 50:50 → ⑥ Attack02 / AttackAll01 50:50
- hp 表：① Attack02 → ② Attack01 / AttackAll01 40:60 → ③ Attack02 / AttackAll01 60:40 → ④ Attack01 / Attack02 40:60 → ⑤ Heal01
- dp 表里没有回复，只有 Break 里才回。

#### 象牙异蛛 IvoryScooper（第一幕 Boss）

原作 `IvoryScooperDefaultVirtual`（[训练机]象牙异蛛，MC02_21_Battle02），DP 16000／HP 50000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `IvoryScooper_Attack01` | 单体 ×1 | 7（9） | |
| `IvoryScooper_Attack02`「Energy Drain」 | 单体 ×3 | 3×3（4×3） | 吸取 |
| `IvoryScooper_AttackAll01`「Destroy Bash」 | 全体 ×1，精偏 | 9（10） | |

- dp 表：① Attack01 → ② Attack02 → ③ Attack01 / AttackAll01 50:50 → ④ 同 ③ → ⑤ Attack02 → ⑥ AttackAll01
- hp 表：① AttackAll01 → ② Attack01 / AttackAll01 50:50 → ③ Attack02 / AttackAll01 50:50 → ④ Attack01 / AttackAll01 50:50 → ⑤ Attack02
- 没有回复技能，只靠 Energy Drain 吸 DP。Break 中不能回 DP，这时 Energy Drain 只剩伤害。

#### 深渊重锤S AbyssKnockerKiller（第二幕精英）

原作 `AbyssKnockerKillerVirtual_MC03`（[训练机]深渊重锤S，MC03_18_Battle01），DP 11000／HP 18400。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `AbyssKnocker_Attack01`「通常攻击」 | 单体 ×1 | 13（15） | |
| `AbyssKnocker_Attack02`「连续攻击」 | 单体 ×2 | 8×2（9×2） | |
| `AbyssKnocker_AttackAll01`「Rage Wave」 | 全体 ×1，精偏 | 15（17） | |
| `AbyssKnocker_Buff01`「回复」 | 自身 | | 回 DP 27% |

- dp 表：① AttackAll01 → ② Attack01 / Attack02 50:50 → ③ AttackAll01 → ④ AttackAll01 → ⑤ Attack01 / Attack02 50:50 → ⑥ Buff01
- hp 表：① AttackAll01 → ② AttackAll01 → ③ Attack01 / Attack02 60:40 → ④ AttackAll01 → ⑤ Attack01 / Attack02 40:60 → ⑥ Buff01
- 11-05 版的 RageWave「目标有 DP 时再追加」去掉：原作这招对 DP 的倍率是 1.0。第一幕原有的 AbyssKnocker 不在这次范围内（2026-11-08 第四批也照原作去掉了）。

#### 线圈巨轮γ TargetWireFrameKiller（第二幕精英）

原作 `TargetWireFrameKiller_Day2E01`（线圈巨轮γ），DP 7000／HP 13000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `TargetWireFrame_Attack01` | 单体 ×1 | 12（13） | |
| `TargetWireFrame_Attack02`「Illusion Spin」 | 单体 ×1 | 16（18） | 幻惑 |
| `TargetWireFrame_AttackAll01` | 全体 ×1，精偏 | 14（16） | |
| `TargetWireFrame_Heal02` | 自身 | | 回 DP 71% |

- dp 表：① AttackAll01 → ② Attack01 / Attack02 40:60 → ③ Attack01 / Attack02 40:60 → ④ AttackAll01 → ⑤ Attack01 / Attack02 40:60 → ⑥ Heal02
- hp 表：① AttackAll01 → ②～④ 每步 Attack01 / Attack02 40:60 → ⑤ Heal02
- **出处对不上**：模组的 `TargetWireFrameKillerElite` 标的是 `MC03ExpMtKinpu02_E02`。那一场其实是线圈巨轮β（`TargetWireFrameDefault_MC03`）加两只杀手跳虫。γ 只在 `MC04ExpMtKumotori01_E01` 单独出场。
  - β 的技能和第一幕的线圈巨轮相同，回复是 700 的 Heal01；开战先制一招 `Event_AttackAll_Stab`，打的是自己一方，威力 2000，属于剧情演出，不搬。
  - 建议招式用 γ，编队保持 E02，roster 的出处两处都写。

#### 石柱像 PileGolemEdge（第二幕精英）

原作 `PileGolemEdgeSmall`（断壁石像，MC04ExpMtKumotori01_S01），DP 14000／HP 29000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `PileGolemEdge_AttackAll01` | 全体 ×1 | 10（11） | |
| `PileGolemEdge_AttackAll02` | 全体 ×1，精偏 | 11（13） | |
| `PileGolemEdge_AttackSingle01`「Confusion Spike」 | 单体 ×1 | 12（14） | 混乱 3 回合 |
| `PileGolemEdge_AttackUp` | 自身 | | 攻击力 +100% |
| `PileGolemEdge_Heal` | 自身 | | 回满 DP（107%） |

- dp 表：① AttackSingle01 / AttackAll01 50:50 → ② AttackUp → ③ AttackSingle01 → ④ AttackAll01 / AttackAll02 50:50
- hp 表：① AttackUp → ② AttackSingle01 → ③ AttackAll01 / AttackAll02 50:50 → ④ AttackUp → ⑤ AttackSingle01 → ⑥ Heal
- 招牌是「攻击力上升 → Confusion Spike」：24（28）加混乱。

#### ★ 钻石眼魔 DiamondEyeball（第二幕 Boss）

原作 `DiamondEyeballDefaultVirtual_MC03_01`（[训练机]钻石眼魔S，MC03ExpTraining02Virtual_BOSS01），DP 32800／HP 19500。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `DiamondEyeball_AttackSingle01`「Laser Bind」 | 单体 ×1 | 13（15） | 束缚 |
| `DiamondEyeball_AttackSingle02` | 单体 ×1 | 17（19） | 防御力下降 2 层（高难 3） |
| `DiamondEyeball_AttackAll01`「Disaster Ray」 | 全体 ×5，精偏 | 3×5（3×5） | |
| `DiamondEyeball_Buff01` | 自身 | | 回 DP 2% |

- dp 表：① AttackSingle01 → ② AttackSingle01 / AttackSingle02 60:40 → ③ AttackAll01 → ④ AttackSingle01 / AttackSingle02 40:60 → ⑤ AttackSingle01 / AttackSingle02 60:40
- hp 表：① AttackAll01 → ② AttackSingle01 / AttackSingle02 60:40 → ③ AttackAll01 → ④ 同 ② → ⑤ AttackAll01 → ⑥ 同 ② → ⑦ Buff01
- 和攻略站对幻影ダイヤモンドアイ的描述一致：DP 在时，束缚和防御力下降的单体攻击为主；Break 后隔一回合一次全体五连。

#### BarrelWalker（第二幕 Boss）

原作 `BarrelWalkerDefaultFire_MC04_02`（烈焰步行火炮，MC04ExpMtKumotori01_BOSS01），DP 31000／HP 47000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `BarrelWalker_AttackSingle01_Fire` | 单体 ×1 | 14（16） | |
| `BarrelWalker_AttackSingle02_Fire` | DP 最低的 ×1 | 17（20） | |
| `BarrelWalker_AttackAll01_Fire`「Rage Magnum」 | 全体 ×8，精偏 | 2×8（2×8） | 防御力下降 1 层（高难 2） |
| `BarrelWalker_AttackUp` | 自身 | | 攻击力 +100% |
| `BarrelWalker_Heal` | 自身 | | 回 DP 48% |

- dp 表：① AttackSingle01 → ② AttackSingle01 / AttackAll01 40:60 → ③ AttackUp → ④ AttackSingle02
- hp 表：① AttackSingle01 / AttackAll01 40:60 → ② AttackUp → ③ AttackSingle02 → ④ AttackSingle01 / AttackAll01 30:70 → ⑤ Heal
- dp 表四步一轮，每轮都是「攻击力上升 → 炮击 DP 最低的人」，炮击 34（40）。

#### ★ 磐石异蟹 ShrimpCrab（第三幕精英，两只）

原作 `ShrimpCrabDefault_01` 和 `ShrimpCrabDefault_02`（侏儒寄居兽，MC04ExpKirigamine02_01_BOSS01，两只同场），DP 21500／HP 8500。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `ShrimpCrab_AttackSingle01` | 单体 ×1 | 26（30），封顶 | |
| `ShrimpCrab_AttackSingle02`「Mirage Pulse」 | 单体，无伤害 | | 封印，攻击力下降、防御力下降各 1 层（高难各 2） |
| `ShrimpCrab_DefenseUp`「Steel Shield」 | 自身 | | 防御力 +75% |
| `ShrimpCrab_Heal` | 自身 | | 回 DP 70% |

- _01 dp 表：① AttackSingle01 → ② AttackSingle02 → ③ DefenseUp → ④ AttackSingle01 → ⑤ DefenseUp
- _01 hp 表：① AttackSingle01 → ② DefenseUp → ③ AttackSingle01 → ④ AttackSingle02 → ⑤ Heal
- _02 dp 表：① AttackSingle02 → ② AttackSingle01 / DefenseUp 50:50 → ③ AttackSingle01 / AttackSingle02 50:50
- _02 hp 表：① AttackSingle02 → ② AttackSingle01 / DefenseUp 50:50 → ③ 同 ② → ④ AttackSingle02 → ⑤ Heal
- 两只用不同的表，第一回合一只打、一只放 Mirage Pulse。攻击回合少，所以单发偏高。

#### 触手巨蛇 SlimySerpent（第三幕精英）

原作 `SlimySerpentDefault`（触手巨蛇β，MC04BExpDay04Desert01_S01），DP 76000／HP 20000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `SlimySerpent_AttackAll01_MC04B` | 全体 ×2 | 7×2（8×2） | |
| `SlimySerpent_AttackAll02_MC04B`「Destruction Rush」 | 全体 ×1，精偏 | 17（19） | OD 下降 |
| `SlimySerpent_AttackSingle01_Default_MC04B`「Disease Plant」 | 单体 ×1 | 15（17） | 攻击力下降 2 层（高难 3） |
| `SlimySerpent_AttackUp02` | 自身 | | 攻击力 +100% |
| `SlimySerpent_Heal` | 自身 | | 回 DP 20% |

- dp 表（①～⑦一次）：① AttackSingle01 / AttackAll01 50:50 → ② AttackUp02 → ③ AttackAll02 → ④ AttackSingle01 → ⑤ AttackAll01 / AttackAll02 50:50 → ⑥ AttackSingle01 / AttackAll01 40:60 → ⑦ AttackAll01 / AttackAll02 30:70；之后循环 ⑧ AttackUp02 → ⑨ AttackAll02 → ⑩ AttackSingle01
- hp 表：① AttackUp02 → ② AttackAll02 → ③ AttackSingle01 / AttackAll01 50:50 → ④ AttackUp02 → ⑤ AttackAll02 → ⑥ Heal

#### 长颈怪环 GiraffeHalo（第三幕精英）

原作 `GiraffeHaloDefault_01`（长颈怪环，MC04BExpDay01Kisokoma01_BOSS01），DP 7500／HP 25000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `GiraffeHalo_AttackSingle01` | 单体 ×1 | 13（15） | |
| `GiraffeHalo_AttackAll01` | 全体 ×2 | 7×2（8×2） | |
| `GiraffeHalo_AttackAll02`「Judgment Ray」 | 全体 ×5 | 3×5（4×5） | 幻惑 2 层（原作必中） |
| `GiraffeHalo_AttackUp` | 自身 | | 攻击力 +100% |
| `GiraffeHalo_Heal` | 自身 | | 回满 DP（200%） |

- dp 表（①～⑧一次）：① AttackSingle01 / AttackAll01 40:60 → ② AttackSingle01 / AttackUp 40:60 → ③ AttackAll02 → ④ AttackSingle01 / AttackAll01 30:70 → ⑤ AttackUp → ⑥ AttackAll02 → ⑦ AttackSingle01 / AttackUp 40:60 → ⑧ AttackAll01；之后循环 ⑨ AttackUp → ⑩ AttackAll02
- hp 表：① AttackAll01 / AttackAll02 50:50 → ② AttackSingle01 / AttackUp 30:70 → ③ AttackAll02 → ④ AttackAll01 / AttackUp 50:50 → ⑤ AttackAll02 → ⑥ Heal
- 开场八步之后，每两回合一次加成过的 Judgment Ray：6×5（8×5），带幻惑。

#### 阿拉克尼巨蛛 ArachneLine（第三幕 Boss）

原作 `ArachneLineDefault`（阿拉克尼巨蛛，MC04ExpPatrol02_BOSS01），DP 41000／HP 56000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `ArachneLine_AttackSingle01` | 单体 ×2 | 13×2（15×2） | |
| `ArachneLine_AttackSingle02`「Absorb Whip」 | 单体 ×2 | 16×2（18×2） | 吸取 |
| `ArachneLine_AttackAll01`「Piercing Flash」 | 全体 ×8，精偏 | 4×8（4×8） | 防御力下降 2 层（高难 3） |
| `ArachneLine_AttackUp` | 自身 | | 攻击力 +20% |
| `ArachneLine_Heal` | 自身 | | 回 DP 37% |

- dp 表：① AttackSingle01 → ② AttackAll01 / AttackSingle02 60:40 → ③ AttackAll01 / AttackSingle02 40:60 → ④ AttackSingle01 / AttackUp 40:60 → ⑤ AttackAll01 → ⑥ AttackUp → ⑦ AttackSingle02 → ⑧ Heal
- hp 表：① AttackAll01 → ② AttackSingle01 / AttackUp 70:30 → ③ AttackAll01 → ④ AttackSingle01 / AttackUp 60:40 → ⑤ AttackSingle02 → ⑥ AttackSingle01 / AttackUp 40:60 → ⑦ AttackSingle02 → ⑧ AttackUp / Heal 50:50 → ⑨ AttackSingle02
- 原作没有召唤，第一版的「召唤刃蛛」去掉。

#### ★ 三柱守卫 PillarGuardian（第三幕 Boss）

原作 `PillarGuardianDefault`（三柱守卫，MC04BWilDay04MilitaryFacility01_BOSS01），DP 21000／HP 60000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `PillarGuardian_AttackSingle01` | 单体 ×2 | 8×2（9×2） | 持续 DP 伤害 4 回合 |
| `PillarGuardian_AttackSingle02`「Destruction Spear」 | 单体 ×4 | 4×4（5×4） | |
| `PillarGuardian_AttackAll01`「Paralysis Burst」 | 全体 ×5 | 4×5（5×5） | 眩晕 |
| `PillarGuardian_AttackUp` | 自身 | | 攻击力 +100% |
| `PillarGuardian_Heal` | 自身 | | 回 DP 71% |

- dp 表（①～⑦一次）：① AttackSingle01 / AttackSingle02 60:40 → ② AttackSingle01 / AttackSingle02 70:30 → ③ AttackSingle01 / AttackUp 50:50 → ④ AttackAll01 → ⑤ AttackSingle01 / AttackSingle02 80:20 → ⑥ AttackUp → ⑦ AttackAll01；之后循环 ⑧ AttackSingle01 / AttackSingle02 80:20 → ⑨ AttackUp → ⑩ AttackAll01
- hp 表：① AttackSingle01 / AttackSingle02 30:70 → ② AttackSingle01 / AttackSingle02 20:80 → ③ AttackSingle02 / AttackUp 50:50 → ④ AttackAll01 → ⑤ Heal
- 循环段每三回合一次加成过的 Paralysis Burst：8×5（10×5）。

### 普通怪和弱怪

#### 提线木偶 Marionette（第一幕）、死亡玩偶 MarionetteKiller（第二幕）

两种共用 `Marionette_AttackSingle01`「通常攻击」（威力 250）、`Marionette_AttackSingle02`「通常攻击」（300）、`Marionette_Buff01`「回复」。第三幕「门与诅咒玩偶」里的诅咒玩偶另有一套技能，见下。

| | 原作 | AttackSingle01 | AttackSingle02 | 回 DP |
|---|---|---|---|---|
| 提线木偶 | `MarionetteDefaultVirtual`（[训练机]提线木偶，DP 3600） | 6（7） | 8（9） | 17% |
| 死亡玩偶 | `MarionetteKiller2`（金峰山，DP 1700）、`MarionetteKillerVirtual`（训练机，DP 2800），出招表相同 | 11（13） | 13（14），封顶 | 35% |

- 提线木偶 dp 表：① AttackSingle01 → ② AttackSingle02 → ③ AttackSingle01 / AttackSingle02 50:50 → ④ Buff01 → ⑤ AttackSingle02 → ⑥ AttackSingle02
- 提线木偶 hp 表：① AttackSingle01 → ② AttackSingle01 / AttackSingle02 40:60 → ③ AttackSingle02 → ④ Buff01
- 弱怪战 `MarionetteWeak` 的原作是教学关的 `MarionetteSmallVirtual_first`（[原型机]玩偶，MC01_01_Battle01）：dp 表 ① AttackSingle01 → ② AttackSingle01 → ③ AttackSingle02 → ④ Buff01，没有 hp 表。建议由遭遇设置变种标记，照 CrestHopper 的 `IsSwarmMember` 写法。
- 死亡玩偶 dp 表：① AttackSingle01 → ② Buff01 → ③ AttackSingle02 → ④ AttackSingle01 / AttackSingle02 50:50 → ⑤ Buff01 → ⑥ AttackSingle02
- 死亡玩偶 hp 表：① AttackSingle02 → ② AttackSingle01 / AttackSingle02 40:60 → ③ AttackSingle01 → ④ Buff01
- 11-05 版的「牵线（攻击下降）」原作没有，去掉。

**诅咒玩偶**：原作 `MarionetteKillerCurse`（诅咒玩偶，MC04BExpDay01Kisokoma01_R03，DP 2600／HP 4300）。用同一个类，由遭遇设置变种。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `MarionetteKillerCurse_AttackSingle01` | 单体 ×1 | 8（8） | |
| `MarionetteKillerCurse_AttackSingle02`「Curse Stamp」 | 单体 ×1 | 9（10） | 灾厄 20 层（高难 30） |
| `MarionetteKillerCurse_AttackUp` | 自身 | | 攻击力 +100% |
| `MarionetteKillerCurse_Heal` | 自身 | | 回满 DP（192%） |

- dp 表（①～⑤一次）：① AttackSingle02 → ② AttackUp / AttackSingle01 50:50 → ③ AttackSingle01 / AttackSingle02 40:60 → ④ AttackUp → ⑤ AttackSingle01 / AttackSingle02 30:70；之后循环 ⑥ AttackUp → ⑦ AttackSingle01 / AttackSingle02 30:70 → ⑧ AttackUp → ⑨ AttackSingle02
- hp 表：① AttackSingle02 → ② AttackUp / AttackSingle01 50:50 → ③ AttackSingle01 / AttackSingle02 30:70 → ④ Heal

#### 猫角兽 CatHorn（第一幕）、猫角兽γ CatHornKiller（第二幕）

共用 `CatHorn_Attack01`（单体）、`CatHorn_Attack02`「Creepy Ball」（单体，带封印）、`CatHorn_AttackAll01`（全体 ×2，精偏）、`CatHorn_Heal01`。

| | 原作 | Attack01 | Attack02 | AttackAll01 | 回 DP |
|---|---|---|---|---|---|
| 猫角兽 | `CatHornDefaultVirtual`（[训练机]猫角兽β，DP 5400） | 6（7） | 8（9） | 3×2（4×2） | 13% |
| 猫角兽γ | `CatHornKillerVirtual`（[训练机]猫角兽γ，DP 4000） | 9（10） | 12（13） | 5×2（6×2） | 18% |

下面的「A1/A2」都是 Attack01 / Attack02 两选一，后面是权重。

- 猫角兽 dp 表：① A1/A2 50:50 → ② AttackAll01 → ③ A1/A2 50:50 → ④ A1/A2 50:50 → ⑤ AttackAll01 → ⑥ Heal01
- 猫角兽 hp 表：① AttackAll01 → ② A1/A2 50:50 → ③ Attack02 → ④ AttackAll01 → ⑤ A1/A2 50:50 → ⑥ Heal01
- 「成对的猫角兽」（MC01_05_Battle01）原作是 α：`CatHornSmallVirtual_first`，DP 3000。
  - dp 表：① Attack01 → ② A1/A2 60:40 → ③ AttackAll01 → ④ A1/A2 60:40 → ⑤ AttackAll01 → ⑥ Heal01
  - hp 表：① A1/A2 60:40 → ② AttackAll01 → ③ A1/A2 60:40 → ④ Heal01
  - 同样由遭遇设置变种。
- 猫角兽γ dp 表：① AttackAll01 → ② A1/A2 40:60 → ③ AttackAll01 → ④ AttackAll01 → ⑤ A1/A2 40:60 → ⑥ Heal01
- 猫角兽γ hp 表：① A1/A2 40:60 → ② Attack02 → ③ AttackAll01 → ④ A1/A2 40:60 → ⑤ AttackAll01 → ⑥ Heal01

#### 旋轮 WireSlasher（第一幕）

原作 `WireSlasherDefaultDark`（黑暗旋轮，MC02_25）。MC02_25 几场按属性配队，有暗、光、雷、火、冰五种；五种的技能和出招表除属性外完全相同。模组没有属性，统一一个类。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `WireSlasher_AttackSingle01_Dark`「通常攻击」 | 单体 ×1 | 6（7） | |
| `WireSlasher_AttackSingle02_Dark`「连击」 | 单体 ×7 | 1×7（1×7） | |
| `WireSlasher_AttackAll01_Dark`「全体攻击」 | 全体 ×1 | 7（9） | |
| `WireSlasher_Buff01`「回复」 | 自身 | | 回 DP 9% |

- dp 表：① AttackSingle01 → ② AttackAll01 → ③ AttackSingle01 / AttackSingle02 50:50 → ④ AttackSingle02 → ⑤ Buff01 → ⑥ AttackAll01 → ⑦ AttackAll01
- hp 表：① AttackAll01 → ② AttackSingle01 / AttackSingle02 40:60 → ③ Buff01

#### 章鱼尾 OctopusTail（第一幕）、章鱼尾γ OctopusTailKiller（第二幕）

共用 `OctopusTail_Attack01`（单体）、`OctopusTail_Attack02`「Erosion Slice」（单体，带病毒）、全体 ×1（精偏；章鱼尾是 `AttackAll01`，γ 是威力相同的 `AttackAll01_Killer`）、`OctopusTail_Heal01`。

| | 原作 | Attack01 | Attack02 | 全体 | 回 DP |
|---|---|---|---|---|---|
| 章鱼尾 | `OctopusTailDefaultVirtual`（[训练机]章鱼尾β，DP 8400） | 6（6） | 7（9） | 7（8） | 8% |
| 章鱼尾γ | `OctopusTailKillerVirtual`（[训练机]章鱼尾γ，DP 6200） | 8（9） | 11（12） | 9（10） | 11% |

下面的「A1/A2」是 Attack01 / Attack02 两选一。

- 章鱼尾 dp 表：① A1/A2 50:50 → ② 全体 → ③ A1/A2 50:50 → ④ A1/A2 50:50 → ⑤ 全体 → ⑥ 全体
- 章鱼尾γ dp 表：① 全体 → ② A1/A2 40:60 → ③ 全体 → ④ A1/A2 40:60 → ⑤ A1/A2 40:60 → ⑥ 全体
- hp 表（两种相同）：原作从第 2 步编号。② A1/A2 50:50 → ③ 全体 / Attack01 / Attack02 50:25:25 → ④ Heal01

#### 线圈巨轮 TargetWireFrame（第一幕）

原作 `TargetWireFrameSmallVirtual`（[训练机]线圈巨轮α，MC02_11_Battle01、MC02_14_Battle02），DP 6200／HP 7600。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `TargetWireFrame_Attack01` | 单体 ×1 | 6（6） | |
| `TargetWireFrame_Attack02`「Illusion Spin」 | 单体 ×1 | 8（9） | 幻惑 |
| `TargetWireFrame_AttackAll01` | 全体 ×1，精偏 | 7（8） | |
| `TargetWireFrame_Heal01` | 自身 | | 回 DP 11% |

- dp 表：① Attack01 → ② AttackAll01 → ③ Attack01 / Attack02 60:40 → ④ Attack01 / Attack02 60:40 → ⑤ AttackAll01 → ⑥ Heal01
- hp 表：① Attack01 / Attack02 50:50 → ② 同 ① → ③ AttackAll01 → ④ 同 ① → ⑤ Heal01

#### 杀手跳虫 CrestHopperKiller（第二幕）

原作 `CrestHopperKiller2`（金峰山，DP 2400）、`CrestHopperKillerVirtual`（训练机，DP 3000），出招表相同。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `CrestHopper_DefaultAttack`「通常攻击」 | 单体 ×1 | 6（8） | |
| `CrestHopper_SkillAttack`「连击」 | 单体 ×8 | 1×8（1×8） | |
| `CrestHopper_Heal1`「修复α」 | 自身 | | 回 DP 25% |

- dp 表：① DefaultAttack → ② SkillAttack → ③ DefaultAttack / SkillAttack 50:50 → ④ SkillAttack → ⑤ DefaultAttack → ⑥ SkillAttack → ⑦ SkillAttack
- hp 表：① SkillAttack → ② DefaultAttack / SkillAttack 40:60 → ③ SkillAttack → ④ Heal1

#### 杀手重锤 DarkKnockerKiller（第二幕）

原作 `DarkKnockerKillerVirtual`（训练机，DP 2800）、`DarkKnockerKillerFire2`（烈焰重锤，金峰山「重锤与死亡玩偶」，DP 3300），出招表相同。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `DarkKnocker_Attack01`「通常攻击」 | 单体 ×1 | 8（8） | |
| `DarkKnocker_Attack02`「连续攻击」 | 单体 ×2 | 5×2（5×2） | |
| `DarkKnocker_AttackAll01`「全体攻击」 | 全体 ×1 | 11（12） | |

- dp 表：① Attack01 → ② AttackAll01 → ③ Attack01 / Attack02 50:50 → ④ AttackAll01 → ⑤ Attack02 → ⑥ Attack01 → ⑦ AttackAll01 → ⑧ AttackAll01
- hp 表：① AttackAll01 → ② Attack01 / Attack02 50:50 → ③ AttackAll01 → ④ Attack02
- 原作没有回复，也没有蓄力。11-05 版的「蓄力 → 重击」去掉。

#### 灯胆跳虫 BalbFlea（第三幕）

原作 `BalbFleaDefault`（灯胆跳虫，云取山、雾峰几场），DP 3500／HP 5600。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `BalbFlea_AttackSingle01` | 单体 ×1 | 9（10） | |
| `BalbFlea_AttackAll01` | 全体 ×1，精偏 | 13（14） | |
| `BalbFlea_AttackUp` | 自身 | | 攻击力 +100% |
| `BalbFlea_Heal` | 自身 | | 回满 DP（143%） |

- dp 表：① AttackSingle01 → ② AttackAll01 → ③ AttackSingle01 / AttackUp 50:50 → ④ AttackAll01 → ⑤ 同 ③ → ⑥ AttackAll01
- hp 表：① AttackSingle01 / AttackUp 50:50 → ② AttackAll01 → ③ 同 ① → ④ AttackAll01 → ⑤ Heal
- 攻击力上升之后紧跟全体攻击，翻倍后 26（28）。

#### 三爪镖 TriClaw（第三幕）

原作分两种：

- `TriClawSmall`（恶之回旋镖，DP 6400／HP 3600）：云取山、雾峰的五场；
- `TriClawDefault`（三角镖，DP 3200／HP 2000）：「巨肌魔臂与三爪镖」（MC04BExpDay03Desert01_R05）。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `TriClaw_AttackSingle01`（三角镖用 `_MC04B` 版） | 单体 ×1 | 15（17） | |
| `TriClaw_AttackSingle02`（三角镖用 `_MC04B` 版） | 单体 ×5，精偏 | 4×5（恶之回旋镖 5×5，三角镖 4×5） | |
| `TriClaw_AttackUp` | 自身 | | 攻击力 +100% |
| `TriClaw_Heal` | 自身 | | 回 DP 78%；三角镖回满（156%） |

- 恶之回旋镖 dp 表：① AttackSingle01 / AttackUp 50:50 → ② AttackSingle01 / AttackSingle02 50:50，两步循环
- 恶之回旋镖 hp 表：① 同 dp ① → ② 同 dp ② → ③ Heal
- 三角镖 dp 表（①～④一次）：① AttackUp / AttackSingle02 50:50 → ② AttackSingle01 / AttackSingle02 50:50 → ③ AttackUp → ④ AttackSingle01 / AttackSingle02 50:50；之后循环 ⑤ AttackUp → ⑥ AttackSingle01 / AttackSingle02 30:70 → ⑦ AttackSingle02 → ⑧ AttackUp → ⑨ AttackSingle02
- 三角镖 hp 表：① AttackUp / AttackSingle02 50:50 → ② AttackSingle01 / AttackSingle02 40:60 → ③ Heal
- 第一版的「回旋镖」只是从名字来的，原作没有回程伤害，去掉。

#### ★ 异议之门 BigotryGate（第三幕）

原作三种，技能相同，出招表错开：

- `BigotryGateSmall_01`（怪异之门，DP 5900／HP 2500）：多数遭遇；
- `BigotryGateSmall_02`（同上）：「两扇异议之门」（MC04ExpKirigamine02_01_R05）里的第二扇；
- `BigotryGateDefault_01`（胜利之门，DP 7300／HP 3500）：「门与诅咒玩偶」（MC04BExpDay01Kisokoma01_R03）。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `BigotryGate_AttackSingle01` | 单体 ×3 | 4×3（4×3） | |
| `BigotryGate_AttackSingle02`「Disaster Shot」 | 单体 ×1 | 14（16） | 灾厄 20 层（高难 30） |
| `BigotryGate_AttackAll01`「Strong Impulse」 | 全体 ×1，精偏 | 19（22） | 精神下降 |
| `BigotryGate_AttackUp` | 自身 | | 攻击力 +20% |
| `BigotryGate_Heal` | 自身 | | 回 DP 85%（胜利之门 68%） |

下面的「S1/S2」是 AttackSingle01 / AttackSingle02 各 50%。

- Small_01：① AttackSingle01 → ② S1/S2 → ③ AttackUp → ④ AttackAll01 → ⑤ S1/S2 → ⑥ S1/S2 → ⑦ AttackUp → ⑧ AttackAll01 → ⑨ Heal
- Small_02：① AttackUp → ② AttackAll01 → ③ AttackSingle01 → ④ S1/S2 → ⑤ AttackUp → ⑥ AttackAll01 → ⑦ S1/S2 → ⑧ S1/S2 → ⑨ Heal
- Default_01：① AttackSingle02 → ② S1/S2 → ③ AttackUp → ④ AttackAll01 → ⑤ S1/S2 → ⑥ S1/S2 → ⑦ AttackUp → ⑧ AttackAll01 → ⑨ Heal
- 三种都没有 hp 表，Break 中照 dp 表往下走。
- 两扇门同场时，Strong Impulse 一扇在第 2 回合放，一扇在第 4 回合放，正好错开。

#### 花冠利刃 CorollaKnife（第三幕）

原作 `CorollaKnifeSmall_01` 和 `_02`（花冠利刃，DP 4400／HP 6200）。`_02` 只在两把同场时用在第二把。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `CorollaKnife_AttackSingle01` | 单体 ×1 | 10（11） | |
| `CorollaKnife_AttackSingle02`「Rotation Cut」 | 单体 ×1 | 13（15） | 防御力下降 2 层（高难 3） |
| `CorollaKnife_AttackAll01`「Rampage Slice」 | 全体 ×2，精偏 | 6×2（7×2） | |
| `CorollaKnife_AttackUp` | 自身 | | 攻击力 +100% |

下面的「S1/S2」是 AttackSingle01 / AttackSingle02 两选一，后面是权重。

- _01：① S1/S2 70:30 → ② S1/S2 60:40 → ③ S1/S2 30:70 → ④ AttackUp → ⑤ AttackAll01
- _02：① AttackUp → ② AttackAll01 → ③ S1/S2 70:30 → ④ S1/S2 60:40 → ⑤ S1/S2 30:70
- 没有 hp 表，也没有回复。

#### 巨肌魔臂 MolArms（第三幕）

原作 `MolArmsSmall`（触手鼹鼠α，沙漠两场），DP 6000／HP 15000。

| 原作技能 | 目标 × 段数 | 数值 | 附带 |
|---|---|---|---|
| `MolArms_AttackSingle01` | 单体 ×5 | 2×5（2×5） | |
| `MolArms_AttackSingle02`「Deceive Shot」 | 单体 ×3 | 4×3（4×3） | 知性下降 |
| `MolArms_AttackAll01` | 全体 ×1，精偏 | 12（13） | |
| `MolArms_AttackUp` | 自身 | | 攻击力 +100% |
| `MolArms_Heal` | 自身 | | 回 DP 83% |

下面的「S1/S2」是 AttackSingle01 / AttackSingle02 两选一，后面是权重。

- dp 表（①～⑤一次）：① S1/S2 40:60 → ② AttackSingle01 / AttackAll01 50:50 → ③ S1/S2 40:60 → ④ AttackUp → ⑤ AttackSingle02 / AttackAll01 50:50；之后循环 ⑥ S1/S2 30:70 → ⑦ AttackUp → ⑧ AttackAll01
- hp 表：① AttackUp → ② AttackAll01 → ③ S1/S2 40:60 → ④ S1/S2 30:70 → ⑤ S1/S2 20:80 → ⑥ Heal
- 「两只巨肌魔臂」原作两只同一个表，循环段会同一回合放加成过的全体攻击，合计 48（52）。要不要让第二只晚一步，见第七节。

## 五、第一批推荐

先写通用出招表构建器（M1～M3），再做下面 5 只：

1. **针嘴鸟**（第一幕精英）：唯一的条件出招（蓄力 → Ruin Bullet），两张表都用到。打空 DP 正好打断蓄力，和原作「眩晕打断蓄力」对上。要新写蓄力和 DP 破坏。
2. **钻石眼魔**（第二幕 Boss）：攻略站的行动描述和数据一致。束缚、防御力下降、全体五连，DP 在时和 Break 后的节奏明显不同。要新写束缚（和眩晕共用一套，工作量小）。
3. **异议之门**（第三幕弱怪、普通）：三种变种的表错开，两扇门同场时大招错开放。灾厄用已有能力；没有 hp 表，正好验证「Break 中照 dp 表走」。要新写攻击力上升、精神下降。
4. **磐石异蟹**（第三幕精英）：两只同场、用不同的表。Mirage Pulse 一招叠封印和攻防下降，Steel Shield 防御力上升 75%。要新写封印、防御力上升。
5. **三柱守卫**（第三幕 Boss）：开场段加循环段，攻击力上升、持续 DP 伤害、眩晕都有。要新写持续 DP 伤害；攻击力上升、眩晕和前面共用。

理由：

- 这 5 只覆盖出招表的全部结构：两张表、加权随机、一次、条件、没有 hp 表、同场变种。
- 新效果写 7 套：蓄力、攻击力上升、防御力上升、精神下降（知性下降同一套）、封印、束缚和眩晕、持续 DP 伤害，外加 DP 破坏一个命令。
- 剩下的 23 只多数只是填表。

后两批：

- **第二批**：其余 9 只精英和 Boss，即鸦头马、象牙异蛛、深渊重锤S、线圈巨轮γ、石柱像、BarrelWalker、触手巨蛇、长颈怪环、阿拉克尼巨蛛。补上幻惑、混乱、OD 下降、吸取。
- **第三批**：14 种普通怪和弱怪，补上病毒（章鱼尾）和遭遇设置的变种标记（提线木偶、猫角兽、诅咒玩偶、三爪镖、花冠利刃）。巨肌魔臂的知性下降在第一批已经随精神下降写好。

## 六、要用户定的

1. **全体攻击两边都生效吗**：怪物的全体攻击也打 30A 的幻影和其他召唤物；幻影的全体招式也打所有敌人。不这样做的话，单人局里单体、全体没有区别。
2. **概率改成条件**：原作按概率附加的封印、束缚、幻惑、混乱、病毒、眩晕，改成「这一招没被格挡完全挡住才附加」，可以吗？
3. **Break 时长**：沿用模组的 3／4／5 回合（hp 表只用前 1～3 步），还是改成原作那样，走到 hp 表的回复才结束？后者会让 hp 表长的 Boss（阿拉克尼 9 步）Break 很久。
4. **伤害口径**：每回合平均伤害和 11-05 版持平。攻击回合少的怪，单发会变高，比如磐石异蟹单发 26（高难 30）；攻击力上升后的一击会翻倍。这个口径行不行？
5. **线圈巨轮γ 的出处**：招式用 γ，编队保持原来的 E02（原作那场其实是 β 加两只杀手跳虫），出处两处都写，可以吗？
6. **两处强度**：
   - 束缚、眩晕原作是一回合不能行动，方案按「三人少一人」折成下回合少1点能量；2026-11-15用户已改为失去[EE]，共同只扣一次。
   - 知性下降照原作 −80%。模组的知性直接管格挡，三回合里格挡只剩两成，比原作重。要不要减轻？（2026-11-08曾暂定 −25%；2026-11-14 用户改定保持需求二十五的 −80%、3个敌方回合末计时，旧意见已取代。）

## 七、工作量和风险

全部做完，大约要：

- 通用出招表构建器：两张表切换、一次、条件、加权；打空 DP 后转 hp 表，存档记表和步数。工作量大。
- 新效果 12 个，实际 10 套，各自的工作量见第三节：
  - 小：蓄力；束缚和眩晕；持续 DP 伤害（小到中）；
  - 中：攻击力上升、防御力上升、精神下降和知性下降、封印、幻惑、混乱、病毒。
- 新命令 2 个：DP 破坏、OD 下降。
- 全体攻击入口：如果第六节第 1 条定了要做，工作量大（目标、动画、幻影重放）。
- 图标：12 张全部取自原作 BattleIcon，精神、知性两张加下降角标，不用另画。
- 改写 28 只的招式，数值见 `calibrated.md`。
- 校验套件 `hbr-act-rosters` 从「核对固定循环」改成「核对出招表和留档的原作数据一致」，再加各效果在三种情况（模组角色 HBR、模组角色旧模式、原版角色）下的实际结算。

HBR 换算沿用现有规则。风险主要在这几处：

- **幻影一侧**：全体攻击、一次、条件、攻击力上升都要接进重放；封印、束缚、眩晕、幻惑、混乱打到敌人身上各有一条规则。
- **联机同步**：加权随机要用怪物自己的随机数。
- **存档**：当前表、步数、用过的「一次」、蓄力，以及新效果的层数和剩余回合。
- **数值尖峰**：攻击力上升让下一招翻倍。两只巨肌魔臂同表，原作也是同一回合一起放；嫌太狠的话，第二只从第 2 步开始。
- **控制效果的强度**：封印、束缚、眩晕、幻惑、混乱照原作新增，比第二版初稿的近似版重，和第六节第 2、6 条一起定。
- **原版角色**：DP、OD 类效果对他们不生效，这些招只剩伤害。逐招伤害按平均算，已经把蓄力、Ruin Bullet 这类回合算成 0。
- **打空 DP 后的眩晕**：现有代码会恢复被打断的那招，这批怪要改成转 hp 表，别的怪不受影响。

没有搬的原作内容：

- 属性弱点和抗性（斩突打、五属性）：模组的六感抗性是另一套。
- 原作的 HP、DP 绝对值和比例：模组统一按 ×700 和 20% 壳。
- 原作每只的攻击倍率 `atk_rate`、破坏率系数 `d_rate` 和上限：模组按房型统一。
- 线圈巨轮β 的开战剧情技。

### 30A 融合

融合效果先不动。第一版建议的三条改动作废，因为原作里没有回旋、荆棘、人工制品这些机制：

- 三爪镖「下回合再造成一次伤害」；
- 旋轮「被召唤时获得荆棘」；
- 巨肌魔臂「被召唤时获得人工制品」。

## 八、和上一版的差别

- **同日修订**：用户补充「其实可以新增效果的，如果mod里没有」。第三节改成「用现有／新增」总表，加了每个新效果的说明：
  - 原来拿近似效果代替的，改成照原作新增：精神下降（原来用防御力下降）、知性下降（原来用脆弱）、封印（原来用缠结）、束缚（原来用昏眩）、眩晕（原来塞晕眩）、混乱（原来用原版混乱加计时）。
  - 原版角色没有 DP、OD 时，原来给的替代（失忆、不可格挡、脆弱、中毒）去掉，改成不生效。
  - 攻击力上升、防御力上升、精神、知性在原作解包库里都有图标，12 个新效果都用原作图标。
  - 灾厄仍用模组已有的。按原作数据改了理解：技能里的 2～3 是持续回合，不是等级。
  - 要用户定的加了第 6 条。
- **原作资料**：上一版说「hbr.quest 和解包库都没有敌人技能」，这是错的。当时只查了列表接口 `enemies.json` 和我方的 `skills.json`，没查每只敌人的详情接口。这一版的技能、出招表全部取自详情接口。
- **机制来源**：上一版的 16 条机制（M1～M16）多数按外形和名字设计，原作里没有。这些都去掉了：
  - 复起一次、回旋、三层 DP、逃离、硬壳、锁定、弹药和卡弹、三柱计数、召唤刃蛛、蛛网；
  - 元素盾（抗性锁定）、失衡、荆棘和架势、群猎、诅咒牌、半血转阶段；
  - 原版增益：仪式、激怒、人工制品、覆甲、壁垒、再生。
- **保留但改了的**：
  - 蓄力：原来是「受到一定伤害就打断」，现在照原作：蓄力后放 Ruin Bullet，被眩晕才打断，只有针嘴鸟用。
  - 条件和随机出招：原来是自己设计的条件，现在照原作的表和权重。
- **主数据字段**：上一版引用的这几条，都来自同族其他变种的记录，模组对应的 label 没有：
  - 异议之门的三条 DP（`extra_gauge.dp`）；
  - 象牙异蛛、猫角兽α 的元素盾；
  - 巨肌魔臂的负面状态抗性。
- **新增**：
  - dp／hp 两张表和 Break 的对应；
  - 开场段和循环段、同场变种错开；
  - 全体攻击和 DP 最低的目标；
  - 效果对照表，写了原作含义和来源；
  - 数值换算规则和逐招数值，用原作图标。
- **11-05 版招式里的设计成分去掉**：
  - 深渊重锤S 的 RageWave、针嘴鸟破甲穿刺的「目标有 DP 时再追加」：原作这些攻击对 DP 的倍率都是 1.0。
  - 各怪的「蓄力、威吓、硬化、装填」这类力量加格挡回合：原作是攻击力上升、回复 DP，或者根本没有。
- **第一批推荐**：从「异议之门、钻石眼魔、针嘴鸟、BarrelWalker、巨肌魔臂、三爪镖」改成「针嘴鸟、钻石眼魔、异议之门、磐石异蟹、三柱守卫」，并且先写通用出招表。
- **要用户定的**：换成第六节这 6 条。上一版的 4 条（打断难度、状态牌张数、召唤物奖励、异议之门逃走）随机制一起作废。
- **新发现**：roster 里线圈巨轮γ 的出处对不上，见第四节。

## 参考

- 敌人详情：`https://master.hbr.quest/v1/cn/enemies/<label>.json`，2026-11-07 抓取，44 份留档在 `D:\stsmod\outputs\monster-mechanics-hbrquest-20261107\enemies\`。
- 关卡和敌人列表：`https://master.hbr.quest/v1/cn/battles.json`、`https://master.hbr.quest/v1/cn/enemies.json`，同日抓取。
- 我方技能说明：`https://master.hbr.quest/v1/cn/skills.json`。
- 状态异常说明：[appmedia 状态异常一览](https://appmedia.jp/?p=75248970)，2026-11-07 查。
- 厄（灾厄）：[game8 厄说明](https://game8.jp/heavenburnsred/485592)，2026-11-07 查；「−20」出自评论区，不是官方数据。
- 钻石眼魔的行动概述：[appmedia](https://appmedia.jp/?p=75170115)、[game8](https://game8.jp/heavenburnsred/425632)。
- 原作图标：本机解包库 `HeavenBurnsRedEditor\extracted`，`Assets/UI/Texture/BattleIcon/`。


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
