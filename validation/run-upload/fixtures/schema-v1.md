# hbrgo-run/1 对局记录格式

- 状态：草案 v1，2026-11-08，由 Claude「HBR 装备」会话起草。模组按这份格式记录和上传，「hbrgo.com 对局数据站」会话按这份格式接收、存储和展示。
- 示例：同目录 [example-run-v1.json](example-run-v1.json)。单人 31O，打到第 6 层后放弃，含两场战斗。为了控制篇幅，示例地图只有 7 行，数值只作演示。
- 发送地址、删除申请方式由网站会话写在同目录的 `endpoint.md`。没定之前，模组里先用常量占位。
- 后续修改只能按第 2.1 节的规则加字段。需要改已有字段时，先在通道里商量。

## 1. 外层信封（已定）

```json
{
  "schema": "hbrgo-run/1",
  "uploadId": "6f1c2a7e-4b8d-4c1f-9a3e-2d5b7c9e0f14",
  "installId": "b2e4d6f8-1a3c-4e5f-8a7b-9c0d1e2f3a4b",
  "modVersion": "1.3.0.12",
  "gameVersion": "v0.111.0",
  "uploadedAt": "2026-11-08T12:58:09Z",
  "run": { }
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `schema` | string | 固定为 `hbrgo-run/1` |
| `uploadId` | uuid | 每局一个。这局结束、记录封存时生成，重试时沿用。网站按它去重，重复收到也返回成功 |
| `installId` | uuid | 玩家第一次同意上传时随机生成，存在本机的模组设置里，设置界面可以复制，也可以重新生成（第 13 节）。和 Steam 账号、电脑都没有关系。玩家申请删除时凭它查找数据 |
| `modVersion` | string | 模组 manifest 里的 `version` |
| `gameVersion` | string | 游戏 build id，例如 `v0.107.1`、`v0.111.0` |
| `uploadedAt` | time | 这次发送的时间，每次重试都会更新 |
| `run` | object | 对局内容，见第 3 节起 |

uuid 统一写成小写、带连字符的 v4 格式。

## 2. 通用约定

- 编码：UTF-8 JSON。对象键用 camelCase。枚举值用小写 snake_case，例如 `rest_site`、`turn_start`。
- 六维、六感的名字在对象里作为键出现，例如 `strength`、`leadershipCharm`。在别处作为值引用时，用同一个键名，例如 `"attribute": "leadershipCharm"`。
- 时间：UTC，ISO 8601，精确到秒，以 `Z` 结尾，不写本地时区。时长一律用整数秒。
- 游戏对象 id：沿用游戏的 ModelId 字符串，格式是 `类别.条目`，例如 `CARD.STRIKE_HBRGO_SAKI`、`RELIC.ARCANE_SCROLL`、`POTION.WEAK_POTION`、`EVENT.NEOW`、`ENCOUNTER.CREST_HOPPER_WEAK`、`MONSTER.CREST_HOPPER`、`CHARACTER.HBRGO_SAKI_31O`、`ACT.OVERGROWTH`、`POWER.DESTRUCTION_RATE_POWER`、`ENCHANTMENT.…`。这和游戏自己的 `.run` 历史文件是同一套 id。模组装备用模组自己的模板 id，例如 `SBooster_0101`。
- 坐标：`[col, row]`，两个整数，和游戏地图坐标一致。
- 数值：照游戏里的数值原样记录，不做换算。本模组的 HP、DP、伤害都是放大后的整数，比如初始 HP 3500。六维和装备加成可能带小数。
- 卡牌 CardRef：`{ "id", "upgrade" }`，有附魔时加 `"enchantment": id`。战斗里的卡再加 `uid`，见 7.4。`cardChoices` 里的卡再加 `rarity`，见 6.1。
- 缺省：拿不到或不适用的字段，可以省略，也可以写 `null`，两者含义相同。空数组可以省略。
- 文字字段：选项文字、显示名、`label` 都是记录时的游戏语言，每条最多 200 字，超出部分截掉。

### 2.1 扩展规则

1. `hbrgo-run/1` 之内只做加法：新增可选字段、新的事件类型、新的枚举值。已有字段不删除，也不改名称、类型或含义。
2. 网站遇到不认识的字段、事件类型或枚举值时，要跳过它们（最好原样保存），不能因此拒收整条记录。
3. 需要不兼容的改动时，换成 `hbrgo-run/2`，网站按 `schema` 分开处理。
4. 记录逻辑每改一次，`run.recorder.version` 加 1。`run.recorder.features` 列出这一局实际记录了哪些部分。某一部分不在 `features` 里，意思是“这个版本没记录”，不是“这局没发生”。

`features` 在 v1 的取值：

| 值 | 含义 |
|---|---|
| `map` | 记录了 `acts[].map` 和 `acts[].path` |
| `floors` | 记录了每层的房间、选择和结算 |
| `combat` | 记录了战斗逐回合的事件 |
| `hbr` | 记录了 HBR 数值：开战快照，以及每层的等级、六感和装备变化 |
| `shop_offers` | 记录了商店货架，而不只是买了什么 |
| `event_offers` | 记录了事件每一页提供了哪些选项 |
| `names` | 带了 `names` 显示名表 |
| `integrity` | 带了 `integrity` 完整性标记，见 3.2 |

## 3. `run` 顶层

| 字段 | 类型 | 说明 |
|---|---|---|
| `recorder` | object | 记录器信息，见 3.1 |
| `rules` | object | 记录时的模组规则常量。v1 只有 `rollTiers`，是掷骰各档位的下限：`{ "first": 50, "second": 76, "third": 91, "max": 100 }` |
| `startedAt` | time | 开局时间 |
| `endedAt` | time | 结束时间 |
| `playSeconds` | int | 游戏自己统计的游玩时长 |
| `character` | id | 本机玩家的角色 |
| `ascension` | int | 进阶等级 |
| `seed` | string | 种子 |
| `gameMode` | string | 游戏的原始值，例如 `standard`、`daily`、`custom` |
| `modifiers` | id[] | 自定义模式的修正 |
| `outcome` | enum | `win`、`loss`、`abandoned` |
| `killedBy` | object 或 null | 失败时写 `{ "encounter": id }` 或 `{ "event": id }`，其余情况为 null |
| `floorReached` | int | 最后到达的楼层，等于最后一条 `floors[].floor` |
| `integrity` | object | 完整性标记，网站据此过滤，见 3.2 |
| `multiplayer` | object 或 null | 单人局为 null，联机局见第 9 节 |
| `locale` | string | 游戏语言，例如 `zhs`、`eng`。文字字段和 `names` 都用这个语言 |
| `acts` | Act[] | 每一幕，见第 4 节 |
| `final` | object | 结束时的状态，见第 8 节 |
| `names` | object | 可选。本局出现过的 id 对应的显示名，尽量覆盖。缺少的 id 由网站直接显示 id |

### 3.1 `recorder`

| 字段 | 类型 | 说明 |
|---|---|---|
| `version` | int | 记录器版本，从 1 开始 |
| `features` | string[] | 见 2.1 |
| `partial` | bool | 记录不完整时为 true。v1 记录器只记从开局起的完整一局，总是 false：中途才打开上传的那一局不记录（第 13 节）。退出时有战斗中断的局照常是完整记录，那场战斗记为 `interrupted`（第 13 节）。字段留给以后的版本 |
| `fromFloor` | int | 从第几层开始有记录。v1 总是 1 |
| `sessions` | int | 这局分几次玩完。退出游戏后再继续，算新的一次 |
| `notes` | string[] | 记录器发现的问题，写给人看的，没有固定格式 |

### 3.2 `integrity`

完整性标记。模组按它决定上不上传，网站也可以按同样的规则再查一遍。

| 字段 | 类型 | 说明 |
|---|---|---|
| `modifiers` | string[] | 这一局检测到的修改器类模组的简短标签。v1 的名单：`loadout`（Loadout）、`free_loadout`（Free Loadout）、`god_mod`（STS2 God Mod）。名单是模组里的常量，以后可以加 |
| `consoleUsed` | bool | 这一局用控制台改动过对局：本机输入的，或联机队友发来的网络命令，执行成功、又不在白名单里的才算。白名单只有游戏自带、只查看信息或调整日志这类不改动对局的命令（名单见附）；其余都算，包括本模组的 hbrgosakimod 命令、其他模组注册的命令和没见过的命令名 |
| `otherGameplayMods` | bool | 除了本模组和它的依赖（BaseLib、STS2-RitsuLib）以外，还开着别的 `affects_gameplay` 为真的模组。只记是非，不记名字。一局分几次玩完时，任何一次开着就算 |

上传规则：

1. `modifiers` 不为空，或 `consoleUsed` 为 true：不上传。
2. 过早结束：`outcome` 是 `loss` 或 `abandoned`，并且 `floorReached` ≤ 5，不上传。楼层数和游戏顶栏显示的一致，先古之民算第 1 层。5 层是模组里的常量，用户已于 2026-11-08 确认。
3. 只有 `otherGameplayMods` 为 true：照常上传，网站默认不计入统计。
4. 不上传的对局，本机也不保留，记录直接删除。中途用控制台改动了对局，或者开着修改器，这一局立即停止记录，已经记下的内容也删除。

所以网站实际收到的记录里，`modifiers` 总是空数组，`consoleUsed` 总是 false。这两个字段照样保留，方便网站再核对，也方便以后调整规则。

## 4. Act（幕）

| 字段 | 类型 | 说明 |
|---|---|---|
| `index` | int | 第几幕，从 0 开始 |
| `id` | id | 例如 `ACT.OVERGROWTH` |
| `map` | Map | 进入这一幕时的地图 |
| `mapChanges` | object[] | 可选。这一幕里地图被事件或遗物改动后，被改动节点的新状态：`{ "floor": 7, "nodes": [Node, …] }`，按发生顺序排列 |
| `path` | coord[] | 实际走过的节点，按顺序排列，和 `floors[].at` 一一对应 |
| `floors` | Floor[] | 每一层的记录，见第 5 节 |

Map：

| 字段 | 类型 | 说明 |
|---|---|---|
| `cols`、`rows` | int | 地图网格的列数和行数 |
| `start` | coord | 起点，也就是先古之民所在的节点 |
| `boss` | coord | Boss 节点 |
| `secondBoss` | coord 或 null | 双 Boss 时的第二个 Boss 节点 |
| `nodes` | Node[] | 全部节点，包括没走的 |

Node 的格式是 `{ "at": [col, row], "type": 节点类型, "next": [coord, …] }`。`next` 是从这个节点往上能走到的节点，全部节点的 `next` 合起来就是地图的全部连线。

节点类型：`ancient`、`monster`、`elite`、`boss`、`unknown`（问号）、`shop`、`treasure`、`rest_site`。问号节点实际进了什么房间，看 `floors[].rooms`。

## 5. Floor（层）

走过一个节点就记一条。

| 字段 | 类型 | 说明 |
|---|---|---|
| `floor` | int | 全局楼层序号，从 1 开始，跨幕累计，按走过的顺序编号 |
| `actFloor` | int | 在这一幕里的序号，从 1 开始 |
| `at` | coord | 节点坐标 |
| `pointType` | enum | 节点类型，取值同第 4 节 |
| `seconds` | int | 可选，在这一层停留的秒数 |
| `rooms` | Room[] | 这个节点里实际进入的房间，按顺序排列。问号节点可能先进事件再进战斗，这时有两条 |
| `choices` | Choices | 本机玩家在这一层的选择和得失，见第 6 节 |
| `hbr` | FloorHbr | 这一层的 HBR 变化，见 6.8 |
| `stats` | object | 离开这一层时的结算，见下表 |

Room：

| 字段 | 类型 | 说明 |
|---|---|---|
| `type` | enum | `monster`、`elite`、`boss`、`event`、`shop`、`treasure`、`rest_site` |
| `model` | id | 战斗房写遭遇 id `ENCOUNTER.…`，事件房写事件 id `EVENT.…`，其他房间可以省略 |
| `monsters` | id[] | 战斗开始时的怪物 |
| `turns` | int | 游戏记录的回合数 |
| `combat` | Combat | 这场战斗的记录，见第 7 节。退出后重打过的，这里放最后一次 |
| `earlierAttempts` | Combat[] | 可选。退出游戏前没打完的那几次，按时间顺序排列 |

stats，全部是本机玩家的数值：

| 字段 | 类型 | 说明 |
|---|---|---|
| `hp`、`maxHp`、`dp`、`maxDp`、`gold` | int | 离开这一层时的值 |
| `goldGained`、`goldSpent`、`goldLost`、`goldStolen` | int | 这一层的金币得失 |
| `damageTaken`、`hpHealed`、`maxHpGained`、`maxHpLost` | int | 这一层的 HP 变化，口径和游戏的 `.run` 历史一致 |

## 6. Choices（选择和得失）

这一节的字段全部可选，没发生就省略。所有数组都按发生顺序排列。

### 6.1 `cardChoices`：卡牌奖励和选牌

`{ "source", "offered": CardRef[], "picked": CardRef[], "skipped": bool, "alternative"?: string }`

- `source`：`reward`（战斗奖励）、`event`、`shop`、`other`。
- `offered`、`picked` 里的卡带 `rarity`：游戏 CardRarity 的小写，`basic`、`common`、`uncommon`、`rare`、`ancient`、`event`、`token`、`status`、`curse`、`quest`。拿不到时省略。
- `picked` 为空并且 `skipped` 为 true，表示跳过。
- `alternative`：用别的东西代替卡牌时，记下所选替代项的 id。
- 目标是每打开一次选牌界面记一条。v1 记录器取自游戏自己的每层历史，历史里不区分同一层的几次选牌界面，所以 v1 每层按来源合成一条：同一层有两次卡牌奖励时，`offered` 是两次的牌合在一起，`picked` 是拿到的全部牌。

### 6.2 `relicChoices`、`potionChoices`

`{ "source"?, "offered": id[], "picked": id 或 null }`

Boss 遗物之类的多选一放在 `relicChoices`。先古之民的选项放在 6.3。战斗掉落的药水放在 `potionChoices`。

### 6.3 `ancientChoices`：先古之民

数组，在先古之民处每做一次选择记一项：`{ "offered": [Option, …], "picked": Option }`。Option 是 `{ "key", "text"? }`，`key` 是游戏的选项 TextKey，多数等于遗物的条目名，例如 `ARCANE_SCROLL`。

### 6.4 `eventChoices`：事件

每做一次决定记一条：`{ "event": id, "page": string, "offered": [Option, …], "picked": Option }`。

Option 是 `{ "key", "text", "locked"?: bool, "vars"?: object }`：

- `key`：选项标题的本地化键，例如 `HBRGO_SAKI_MOD_EVENT_GYM_HBRGO_SAKI.pages.INITIAL.options.CHOICE.title`。
- `text`：当时显示的文字。
- `locked`：当时这个选项不能选。
- `vars`：游戏记下的选项变量，例如金额。

多页事件每页各有一条。`offered` 需要 `event_offers`。

### 6.5 `rest`：休息处

数组，每做一次选择记一项：`{ "choice": string, "card"?: CardRef, "healed"?: int }`。

- `choice`：游戏的选项 id，例如 `HEAL`、`SMITH`，也包括遗物或模组加的其他选项。
- `card`：锻造等针对卡牌的选项选中的牌，记录升级后的样子。
- `healed`：实际回复的 HP。

### 6.6 `shop`：商店

```text
{
  "offered": {
    "cards":   [ { "card": CardRef, "price": int, "onSale"?: bool } ],
    "relics":  [ { "id": id, "price": int } ],
    "potions": [ { "id": id, "price": int } ],
    "removalPrice": int
  },
  "bought": [ { "kind": "card" | "relic" | "potion" | "removal", "card"?: CardRef, "id"?: id, "price": int } ]
}
```

- `removal` 是删卡服务，`card` 是被删掉的牌。
- `offered` 是进店时的货架，需要 `shop_offers`。
- 模组装备商店的货架和购买记在 6.8 的 `equipmentOffers` 和 `equipment`。

### 6.7 卡牌、遗物、药水的变动

| 字段 | 类型 | 说明 |
|---|---|---|
| `cardsGained` | CardRef[] | 从任何途径加入牌组的卡 |
| `cardsRemoved` | CardRef[] | 删掉的卡 |
| `cardsUpgraded` | CardRef[] | 升级后的样子 |
| `cardsDowngraded` | CardRef[] | 降级后的样子 |
| `cardsTransformed` | `{ "from": CardRef, "to": CardRef }[]` | 变化 |
| `cardsEnchanted` | `{ "card": CardRef, "enchantment": id }[]` | 附魔 |
| `relicsGained` | `{ "id", "source"? }[]` | `source` 取 `combat`、`elite`、`boss`、`treasure`、`shop`、`event`、`ancient`、`other` |
| `relicsRemoved` | id[] | 失去的遗物 |
| `potionsGained`、`potionsUsed`、`potionsDiscarded` | id[] | `potionsUsed` 包括战斗里用的。战斗里的使用顺序看战斗事件 |
| `questsCompleted` | id[] | 完成的游戏任务 |

### 6.8 `hbr`：这一层的 HBR 变化（需要 `hbr`）

| 字段 | 类型 | 说明 |
|---|---|---|
| `levelBefore`、`levelAfter` | int | 战斗等级的变化。普通战 +1，精英 +2，Boss +3 |
| `senseGained` | int | 这一层获得的未分配六感点数 |
| `senseAllocations` | object[] | 玩家每次分配六感点数的增量，例如 `{ "madness": 1 }` |
| `senseChanges` | object[] | 事件、卡牌等直接改动六感：`{ "attribute", "delta", "source" }` |
| `equipmentOffers` | `{ "item": Item, "price" }[]` | 装备商店的货架 |
| `equipment` | object[] | 装备操作，按顺序：`{ "action", "item"?: Item, "itemId"?, "slot"?, "index"?, "price"? }` |
| `other` | object[] | 模组其他系统的记录，格式是 `{ "type": string, … }`，以后按需要扩展 |

`equipment` 的 `action` 取值：`drop`（掉落）、`buy`、`sell`、`alchemy`（炼金得到）、`equip`、`unequip`、`plus`（＋槽开放或更换）、`upgrade`（芯片升级）、`discard`。

Item（装备）：`{ "itemId"?, "template", "kind", "stars", "name", "rolls": int[], "plus": [ { "stat", "level" } ] }`

- `itemId`：模组在这一局里给每件装备的编号，整局不变，用来把买、装、卖串起来。货架上还没买的装备没有 `itemId`。
- `kind`：`booster`、`chip`、`ring`、`earring`、`bracelet`、`necklace`、`charm`。
- `stars`：星级，也就是模板的稀有度。
- `rolls`：每个随机属性组抽到的档位，是模组内部的编号。网站可以按模板表解读，也可以只做展示。
- `plus`：＋槽的属性和等级。`stat` 用六维的键名，或者 `dp`、`critRatePercent`。

## 7. Combat（战斗）

| 字段 | 类型 | 说明 |
|---|---|---|
| `attempt` | int | 同一场战斗的第几次。退出游戏再继续时战斗会从头重打，次数从 1 开始 |
| `truncated` | bool | 事件太多被截断时为 true，见 7.6 |
| `creatures` | object | 参战单位表，见 7.1 |
| `hbr` | HbrSnapshot | 本机玩家开战时的 HBR 数值，见 7.2 |
| `rounds` | Round[] | 每一轮，见 7.3 |
| `result` | object | 战斗结果，见 7.5 |

### 7.1 `creatures`

键是单位代号，值是 `{ "kind", "id", … }`：

| 代号 | kind | 说明 |
|---|---|---|
| `p` | `player` | 本机玩家，`id` 是角色 |
| `a1`～`a3` | `ally` | 联机队友，数字是座位号，只记角色 `id` |
| `s0`… | `summon` | 我方召唤物，例如幻影、浮游炮，另有 `owner` 字段写主人的代号 |
| `e0`… | `enemy` | 敌人，按出场顺序编号。中途召唤的接着往后编，编号不重复使用。另有 `hbrParameter`（敌人的能力参数）；中途加入的还有 `joinedRound` |

### 7.2 HbrSnapshot（开战时，本机玩家）

| 字段 | 类型 | 说明 |
|---|---|---|
| `battleLevel` | int | 战斗等级 |
| `stats` | object | 六维：`strength`、`dexterity`、`intelligence`、`vitality`、`spirit`、`luck`，是算上六感和装备之后的战斗数值 |
| `senses` | object | 六感：`madness`、`leadershipCharm`、`talent`、`gentleness`、`mentalStrength`、`simplicity` |
| `unspentSensePoints` | int | 还没分配的六感点数 |
| `critRatePercent` | number | 暴击率加成，单位是百分数，0.2 表示 0.2% |
| `equipment` | object | 见下 |

`equipment` 包含：

- `equipped`：`[ { "slot", "index", "item": Item } ]`。`slot` 取值同 Item 的 `kind`，`index` 是同类槽位的第几个，从 0 开始。
- `bonus`：装备给的六维、`dp`、`critRatePercent` 合计。
- `effects`：装备特效的合计。键是效果名：`attackRoll`、`skillRoll`、`powerRoll`、`dpDamage`、`hpDamage`、`overdriveGain`、`destructionGain`、`skillDamage`、`destructionFlat`、`damageTaken`、`bossDamageTaken`、`energyPerTurn`、`perfectStrike`。数值单位和模组内部一致：百分比效果记百分数，掷骰效果记加的点数，能量记点数。

当前规则下，六维 = 100 + 4 ×（等级 − 1）+ 2 × 六感 + 装备。写在这里方便核对，统计时以记录里的数值为准。

### 7.3 Round（轮）

一轮包括我方回合和敌方回合。

| 字段 | 类型 | 说明 |
|---|---|---|
| `round` | int | 从 1 开始 |
| `start` | object | 我方回合开始、抽牌之前各单位的状态（State），键是单位代号。敌人带 `intents` |
| `events` | Event[] | 这一轮按发生顺序排列的全部事件 |
| `end` | object | 敌方回合结束时（战斗在这一轮内结束的，就是结束时）各单位的状态，只有 `hp`、`block`、`dp`。这一轮没打完时省略 |

State：

| 字段 | 类型 | 说明 |
|---|---|---|
| `hp`、`maxHp`、`block` | int | |
| `dp`、`maxDp` | int | 有 DP 时才写。玩家的 DP 来自偏导护盾，敌人的 DP 来自高维外壳 |
| `energy`、`stars` | int | 玩家的能量和星 |
| `od` | int | 玩家的 OD 计量，也就是超频遗物的充能 |
| `piles` | object | 玩家各牌堆的张数：`{ "hand", "draw", "discard", "exhaust" }` |
| `powers` | `{ "id", "amount" }[]` | 身上的能力，包括破坏率 `POWER.DESTRUCTION_RATE_POWER`，100 表示 100% |
| `intents` | Intent[] | 敌人这一轮的意图 |

Intent：`{ "type", "move"?, "damage"?, "hits"?, "cards"? }`

- `type`：`attack`、`buff`、`debuff`、`debuff_strong`、`defend`、`escape`、`heal`、`hidden`、`summon`、`sleep`、`stun`、`status_card`、`card_debuff`、`death_blow`、`unknown`，对应游戏的 IntentType。
- `move`：敌人的招式 id，例如 `ATTACK_MOVE`。
- `damage`、`hits`：攻击意图显示的单段伤害和段数，按对本机玩家计算。
- `cards`：往牌堆里塞状态牌的张数。

### 7.4 Event（事件）

所有事件都有 `type` 字段。单位用 7.1 的代号表示。

战斗里的卡写成 `{ "uid", "id", "upgrade" }`。`uid` 是这场战斗里每张卡的编号：同一张卡从抽到、打出到下一次抽到，编号都不变；战斗中新生成的卡拿到新的编号。别的事件指向某张卡时，用 `cardUid` 写它的编号。

| type | 字段 | 说明 |
|---|---|---|
| `turn_start`、`turn_end` | `side`：`player` 或 `enemy` | 回合边界。一方的 `turn_start` 之后、下一个 `turn_start` 之前的事件属于这一方的回合 |
| `draw` | `by`、`card` | 抽到一张牌，按抽牌顺序排列 |
| `play` | `by`、`card`、`target`、`energy`、`stars`?、`auto`?、`repeat`?、`dealt`? | 打出一张牌，见下方说明 |
| `roll` | `by`、`cardUid`?、`label`、`base`、`final`、`dice`、`bonuses`、`tier`、`forcedCritical`?、`critMin`、`absCritMin`、`discarded` | 掷骰，见下方说明 |
| `potion` | `by`、`potion`、`target`? | 使用药水，按使用顺序排列 |
| `damage` | `from`?、`to`、`src`?、`amount`、`blocked`、`hpLoss`、`dpLoss`?、`overkill`?、`killed`? | 一段伤害，见下方说明 |
| `block` | `to`、`src`?、`amount` | 获得格挡 |
| `power` | `to`、`from`?、`src`?、`power`、`delta`、`amount` | 能力层数变化，`amount` 是变化后的层数 |
| `move` | `by`、`move` | 敌人执行招式 |
| `spawn` | `who`、`id` | 中途加入的单位 |
| `death` | `who` | 单位死亡 |
| `exhaust`、`discard` | `by`、`card` | 消耗、弃牌。回合结束时的自动弃牌不记 |
| `generate` | `by`、`card`、`pile` | 战斗中生成卡牌。`pile` 取 `hand`、`draw`、`discard`、`exhaust` |
| `shuffle` | `by`、`count` | 洗牌，`count` 是洗回抽牌堆的张数 |
| `select` | `by`、`cardUid`?、`optionUids`、`pickedUids` | 出牌过程中的选择，例如从手牌里选一张 |

`play`：

- `target`：目标的代号，没有目标时为 null。
- `energy`、`stars`：实际花费。
- `auto`：自动打出时为 true。
- `repeat`：同一次出牌的第几次重复，从 0 开始，只在重复打出时出现。
- `dealt`：这次出牌造成伤害的合计，`{ "total", "hp", "dp", "blocked", "kills" }`。

`roll`：

- `cardUid`：触发掷骰的卡。
- `label`：模组记录的掷骰来源文字。
- `base`、`final`：原始骰点和算上加成后的骰点。
- `dice`：骰子的来源，例如“基础骰”。
- `bonuses`：骰点加成，`[ { "amount", "source", "percent"? } ]`。
- `tier`：结果档位，取 `none`、`first`、`second`、`third`、`critical`。各档下限见 `run.rules.rollTiers`。
- `forcedCritical`：被效果强制判为暴击时为 true。
- `critMin`：当时的暴击门槛，会受充能影响。`absCritMin` 是绝对暴击门槛。
- `discarded`：掷多个骰取一个时丢掉的骰，`[ { "base", "final", "tier" } ]`。

`damage`：

- `amount`：这段伤害的总量。
- `blocked`：被格挡的部分。
- `dpLoss`、`hpLoss`：实际扣掉的 DP 和 HP。
- `overkill`：超出剩余 HP 的部分。
- `killed`：这段伤害打死了目标。

`src`（来源）只有一个键，可能是 `{ "cardUid": uid }`、`{ "potion": id }`、`{ "relic": id }`、`{ "power": id }` 或 `{ "move": string }`。拿不到来源时省略。

顺序：`play` 在出牌开始时记录，后面跟着这张牌引起的 `roll`、`damage`、`draw` 等事件。`play` 里的 `dealt` 在出牌结束后补上。

### 7.5 `result`

| 字段 | 类型 | 说明 |
|---|---|---|
| `outcome` | enum | `win`、`loss`、`escape`（敌人逃跑或我方逃离）、`abandoned`（放弃整局）、`interrupted`（退出游戏，下次继续时这场战斗重打） |
| `rounds` | int | 打了几轮 |
| `player` | object | 结束时本机玩家的 `{ "hp", "maxHp", "dp", "maxDp" }` |
| `dealt` | object | 本机玩家这场造成的伤害合计 `{ "total", "hp", "dp", "blocked", "kills" }` |
| `taken` | object | 本机玩家这场受到的伤害合计 `{ "total", "hp", "dp", "blocked" }` |

做统计时用 `room.combat`。`earlierAttempts` 里 `outcome` 为 `interrupted` 的几次只作参考。

### 7.6 截断

一场战斗的事件超过 20000 条时，之后只记回合边界和 `result`，并把 `truncated` 设为 true。

## 8. `final`（结束时的状态）

| 字段 | 类型 | 说明 |
|---|---|---|
| `floor` | int | 结束时所在的楼层 |
| `hp`、`maxHp`、`dp`、`maxDp`、`gold` | int | |
| `deck` | `{ "id", "upgrade", "enchantment"?, "floorAdded" }[]` | 最终牌组 |
| `relics` | `{ "id", "floorAdded" }[]` | 最终遗物 |
| `potions` | id[] | 最终药水 |
| `hbr` | object | 格式同 7.2，另外 `equipment` 里多一个 `inventory`，列出持有的全部装备，包括没装备的 |

## 9. 联机局

- `multiplayer` 写成 `{ "playerCount", "localSeat", "allies": [ { "seat", "character" } ] }`。队友只有座位号和角色，不带任何账号信息。游戏内部的玩家编号在联机时就是平台账号，所以模组一律换成座位号。
- `floors[].choices`、`stats`、`hbr` 和 `final` 只写本机玩家。
- 战斗：`creatures` 里会有 `a1` 等队友。本机玩家的事件完整记录。队友只记本机屏幕上能看到的结果：打出的牌（卡牌 id 和目标）、使用的药水、对敌人和我方造成的伤害、能力变化。不记队友的抽牌、手牌、掷骰明细，也不记队友在战斗之外的任何选择。

## 10. 不会出现在记录里的内容

- Steam ID、Steam 名字、好友，以及任何平台账号或联机玩家编号。
- 电脑上的路径、系统用户名、电脑名、硬件信息、本地时区。
- 模组列表和其他模组的信息。
- 存档原文、模组内部的存档字段、地图涂鸦、聊天内容。
- 显示名和文字字段只来自游戏和模组的本地化文本，不含玩家输入的内容。
- `integrity` 里只有已知修改器的简短标签和两个是非值，不含任何模组的名字或列表。
- 网站收到请求时能看到 IP，只在内存里用来限流，不保存，也不显示给任何人。

## 11. 大小

- 一局通常几百 KB（未压缩），很长的对局可能到几 MB。模组发送时用 gzip 压缩，整个正文是一个 gzip 成员。
- 网站的上限（见 `endpoint.md`）：传输的正文最多 4 MiB，解压后最多 16 MiB，超过返回 413。模组在入队前检查，压缩后超过 4 MiB 或未压缩超过 16 MiB 的记录不会入队，只在本地日志里记一笔。

## 12. 发送方式（按 endpoint.md）

- 地址：`POST https://runs.hbrgo.com/api/v1/runs`。子域名还要用户确认，模组里基址是一个常量，定下来以后只改这一处。
- 请求头：`Content-Type: application/json; charset=utf-8`、`Content-Encoding: gzip`、`User-Agent: HBRGOSakiMod/<modVersion>`。不带 cookie、密钥或账号信息。
- 201，或者 200 且 `duplicate` 为 true：发送成功，从队列删掉。
- 400、409、413、415、422：这条永远发不上去，删掉，不再重试，只在本地日志里记一笔。422 是网站按 3.2 的规则拒收（`modified_run`、`early_end`）。
- 404、405、408、429、5xx，以及超时、连不上：都当作暂时失败，留在本地队列。404 也算在内，免得网站上线前就把本地记录删光。429 和 503 带 `Retry-After` 时，至少等这么多秒再发。
- 重试时机：下次启动游戏时，以及下一局结束时。同一条连续失败时，等待时间从 1 分钟起逐次加倍，最长 6 小时。
- 超时：连接 10 秒，整个请求 30 秒。发送在后台进行，不会卡住游戏。

## 13. 模组这边的行为（网站解读数据时需要知道）

- 第一次打开带这个功能的模组版本时，主菜单上弹出提示，按钮是「允许」「拒绝」，直接关掉算拒绝。选之前不记录也不发送；选择保存在本机，之后可以随时在设置里改。在设置里从关改成开时，再弹同一份说明确认。模组里的 `ConsentVersion` 升级时（收集范围有大的变化，例如格式大版本升级）会重新询问。联机时每个玩家各自选择。
- 开关默认关闭。关闭时不记录，也不发出任何请求。
- 设置里可以「重新生成匿名 ID」：之后上传的记录用新的 `installId`，和之前上传的对不上。本机还没发出的记录保持原来的 ID。
- 中途打开开关：打开时如果有一局正在进行（包括存档里还没打完、之后才继续的局），这一局不记录、不上传，从下一次新开的一局开始记录。继续存档时本机没有这一局从开局起的记录（例如记录被清空过），也一样不记录。所以网站收到的都是从开局起的完整一局，`partial` 总是 false，`fromFloor` 总是 1。
- 中途关闭开关：立即停止记录和上传，正在发送的请求会被取消；这一局已经记下的内容丢弃，不上传，之后再打开开关也不再记这一局。开关打开期间已经结束、还没发出的记录留在本地队列，关着的时候一律不发，再次打开开关后才会发送；也可以用「清空本地未上传数据」删掉。
- 只上传看到结局的对局，也就是胜利、失败或放弃。游戏崩溃后这局再也没有继续，或者存档被删除的，记录会在下一局开始时丢弃。
- 退出后再继续（本机有这一局从开局起的记录）：记录接着写，`sessions` 加 1，照常上传。退出时正在打的战斗记为 `interrupted`，放进这个房间的 `earlierAttempts`，继续后重打的那场 `attempt` 加 1。
- 本地队列上限：最多 50 局，每局最多保留 30 天，超出的从最旧的删。这两个数是模组里的常量。

v1 记录器暂不记录的部分（以后加上时只做加法）：

- 商店货架（`shop_offers`）、事件每页的全部选项（`event_offers`）、装备商店货架（`equipmentOffers`）、地图中途改动（`mapChanges`）。
- `cardChoices` 每层按来源合成一条，见 6.1；`relicChoices`、`potionChoices`、`ancientChoices` 同样每层一条。`eventChoices` 只有选中的那项（`picked`），没有 `offered`。
- 战斗事件的 `src` 只会是 `cardUid`、`move` 或 `potion`；`discard` 和 `select` 事件暂不记录。
- 联机局的队友只记出牌、药水、伤害、格挡和能力变化。

## 14. 待确认

已定（2026-11-08，全局统筹转达）：

1. 鉴权：不鉴权，网站只校验格式和限流。
2. 联机局：只记本机屏幕上看得到的结果，不记队友的抽牌、手牌和掷骰明细（第 9 节）。
3. 关闭开关：立即停止记录和上传；开关打开期间记下、还没发出去的不删，但关着的时候一律不发；另有「清空本地未上传数据」按钮。
4. 本地队列：最多 50 局、30 天，超出的从最旧的删（第 13 节）。
5. 首次打开时的提示、「重新生成匿名 ID」、完整性标记和过滤规则（3.2、第 13 节），全局统筹 2026-11-08 转达。
6. 过早结束的标准：前 5 层以内死亡或放弃的对局不上传，用户已于 2026-11-08 确认。
7. 第二轮规则（用户 2026-11-08 回复「2和5不用管」，全局统筹转达）：说明文字补 Cloudflare 那句；控制台只算改动对局的命令（3.2）；中途打开开关的那一局不上传，从下一局开始记录（第 13 节）。

还要用户定：

1. 子域名名称（提议 `runs.hbrgo.com`）。
2. 网站上的数据保存多久，以及删除申请的联系方式（隐私说明页上还空着）。
3. 限流和大小上限是否合适（见 `endpoint.md` 第 5 节）。

## 附：模组的取数来源（实现备忘）

代码在 `HBRGOSakiMod/Core/RunUpload/`，其中 `Common/` 是不依赖游戏的格式、队列和发送逻辑，离线检查在 `validation/run-upload/`。

- 开局、读档、结束、进入幕和房间、战斗开始和结束：RitsuLib 的生命周期事件。
- 每层的选择和结算：游戏自己的每层历史（MapPointHistoryEntry、PlayerMapPointHistoryEntry），离开这一层时读取；商店购买取自 `Hook.AfterItemPurchased`，获得的药水取自 `Hook.AfterPotionProcured`，获得的遗物按这一层前后的遗物差。
- 地图：进入一幕时 RunState 的地图（MapPoint 的坐标、类型和 Children）。
- 战斗：游戏 `Hook` 的只读前置补丁。`BeforeSideTurnStart` 记回合开始，`BeforeHandDraw` 拍下一轮开始的快照（格挡已清、能量已回、还没抽牌），`AfterTurnEnd`（v0.111.0 叫 `AfterSideTurnEnd`）记回合结束，敌方回合结束时拍下一轮结束的快照；`AfterCardDrawn`、`BeforeCardPlayed`、`AfterCardPlayed`、`BeforePotionUsed`、`BeforeDamageReceived` 加 `AfterDamageReceived`（前后的 HP、DP 差）、`AfterBlockGained`、`AfterPowerAmountChanged`、`AfterDeath`、`AfterCreatureAddedToCombat`、`AfterCardExhausted`、`AfterCardGeneratedForCombat`、`AfterShuffle`；敌人出招取自 `MonsterModel.PerformMove`。
- 掷骰：模组的掷骰记录 `SoulOfAnchor.RecordRoll`（后置，读刚存下的那一条，档位已判定）；卡牌归属按这名玩家正在打出的那张牌。
- HBR：电子军人手册（等级、六维、六感、装备），六感分配取自 `ApplySixSenseAllocation`，装备操作取自 `ApplyEquipmentCommand`；偏导护盾（玩家 DP）、超频（OD）、高维外壳（敌人 DP）、敌人能力参数。
- `integrity`：修改器和其他玩法模组取自 `ModManager.Mods` 里本次加载成功的模组（清单的 id 和 `affects_gameplay`）；控制台取自 `DevConsole` 私有的 `ProcessCommand(Player, string cmdName, string[])`（本机输入和联机队友的网络命令都经过它），返回成功、命令名不在白名单里才算，比较不分大小写。白名单是模组里的常量，只放游戏自带、源码确认不碰对局的命令，v0.107.1 和 v0.111.0 相同：`achievement、art、bestiary、dump、getlogs、help、instant、leaderboard、log、open、sentry、trailer`。故意不放的三个：unlock（改进度存档里的已发现记录，本局记下的「新发现」会跟着变）、cloud（删 Steam 云存档后退出游戏）、multiplayer（测试场景会换掉这一局，还能用调试设置另开一局）。开着修改器时整局不记录；用控制台改动了对局时立即停止记录，`current-run.json` 换成只含这局键值和原因的排除标记，继续这局时也不再记录。
- 记不记录这一局：`Common/RunRecordingGate.cs`。新开局时开关开着才记；继续存档时只有本机存着这一局从开局起的记录才接着记，否则不记；一局进行中打开或关闭开关、清空本地数据，这一局都不再记。
- 首次提示：`NMainMenu._Ready` 后置挂一个等待节点，等主菜单可用、没有别的弹窗时弹出；回答保存在 `settings.json`。
- 记录只读取游戏状态，不写存档，也不参与联机同步。进行中的记录存在模组自己的 `user://HBRGOSakiMod/run-upload/current-run.json`，开关和安装 ID 在同目录的 `settings.json`，待发送的记录在 `queue/`。
