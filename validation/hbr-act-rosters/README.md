# Act roster checks

2026-11-15需求六至九：HBR敌方控制招式完全格挡后仍附加（死者、范围外、召唤物与DP限制保留），幻影passedTargets不变；六类表怪普通／高难强度进入现有属性公式，特殊Boss25%／50%且玩家幻影15%；行动锁共同只扣[EE]；玩家幻惑／病毒／混乱按自身回合结束2／2／3计时；普通外壳2／3／4回合逐次真实倒数并回壳，Hello World继续相加。原始概率快照只验证出处，不再用来限制敌方附加。

2026-11-14更新：本宿主安装通用独立效果应用与原生实例查找补丁，强度快照和Amount时限分开断言。HBR下的封印／束缚／眩晕／幻惑／病毒／混乱按新原作时钟和独立份数校验，旧非HBR适配保留；知性下降按用户选定80%。原有敌方Buff断言同步需求二十五的两份快照及E缩放，幻影混乱的HBR技能伤害保留原始威力到通用公式入口。所有原作出招表、攻击范围与概率／必中断言继续执行，资源档案改读冻结原作DP／HP快照。另见 `validation/hbr-enemy-status-clock` 的真实原生回合分发与多份状态检查。

Covers the act rosters added on 2026-11-05: 28 monsters and 51 encounters across
acts one to three, plus the CrestHopper swarm HP flag. Design notes and every
number are in [docs/hbr-act-rosters.md](../../docs/hbr-act-rosters.md); the
expected values live in [RosterSpecs.cs](RosterSpecs.cs).

- Act one: Marionette, CatHorn, WireSlasher, OctopusTail, TargetWireFrame,
  NeedleBird, CrowHorse, IvoryScooper; 15 encounters.
- Act two: MarionetteKiller, CrestHopperKiller, DarkKnockerKiller, CatHornKiller,
  OctopusTailKiller, AbyssKnockerKiller, TargetWireFrameKiller, PileGolemEdge,
  DiamondEyeball, BarrelWalker; 19 encounters.
- Act three: BalbFlea, TriClaw, BigotryGate, CorollaKnife, MolArms, ShrimpCrab,
  SlimySerpent, GiraffeHalo, ArachneLine, PillarGuardian; 17 encounters.

The fixture links the existing `hbr-fragile` host (real `PowerCmd.Apply`, HBR
mode switch, hero and combat state) and installs the production
`HbrPercentDebuff*` application patches, the exact-DP `SetAmount` bridge and
the act-one to act-three pool patches. Canonical monster and encounter models
are injected with the native `ModelDb.Inject`, which runs their real
constructors.

What it checks:

- Registries: each act's mod pools match the native act's pool sizes (Overgrowth
  4/12/3/3, Hive 4/10/3/3, Glory 3/9/3/3 weak/normal/elite/boss) and hold
  nothing else; no duplicates; every entry is an HBR encounter; no encounter is
  registered for two acts.
- Pools: on blank native Overgrowth, Hive and Glory models with their native
  encounter list emptied, the real `AllEncounters`, `AllWeakEncounters`,
  `AllRegularEncounters`, `AllEliteEncounters`, `AllBossEncounters` and
  `AllMonsters` getters (with every act's pool patch installed) yield exactly
  that act's registry contribution, in order.
- Each new encounter: room type and weak flag, the exact ordered lineup from its
  real `GenerateMonsters`, mutable monster copies, `AllPossibleMonsters` equal to
  the monsters it generates, native HP budget at ascension 0 (act one weak 20–60,
  normal 36–110, elite 80–130, boss 100–320; act two 40–110, 80–190, 140–220,
  280–420; act three 80–170, 140–280, 200–320, 350–560), and boss map icons.
  Every new monster fights in an encounter of its own act.
- Each new monster at ascension 0 and 10: native HP range and its move cycle
  order, from the real `GenerateMoveStateMachine` (move-table monsters: see
  below).
- Every move body, in HBR and legacy mode at both ascensions: the recorded
  attack (damage × hits), raw `Move` block, self Strength, the debuff on the
  target (HBR attack/defense down through the mod adapter, native Weak/Vulnerable
  in legacy mode, with the expected strength), fixed DP recovery (×100 in HBR
  mode) and extra damage only against a target with DP; every attack keeps the
  native targets (player creatures). `PerformAttackOn` (every monster attack,
  with its target scope), animations, the native block command and the extra
  and single-target damage commands are recorded leaves.
- Phantom side effects: exactly the attack+debuff monsters implement
  `IHbrgoPhantomAttackEffects`, which reapplies the debuff once for its own move
  and nothing for the others.
- Since the third move-table batch every new monster follows its original
  tables, so the fixed-cycle checks above (cycle order, move bodies, the old
  phantom interface) cover none of them; the move-table checks below do.
- Fusion: each monster maps to its own new fusion type (≥100), first and second
  fusion values, the aggregate it feeds (damage bonus/reduction, max HP, max DP)
  and its description line, including the value.
- Phantom segments: the real `PhantomHelper.BuildAttackSegments` gives the first
  attack intent the extra segments of WireSlasher, TargetWireFrameKiller,
  BarrelWalker and TriClaw (and only the first), and adds the bonus segment of
  TargetWireFrame, DarkKnockerKiller, AbyssKnockerKiller and MolArms on every
  third attack intent only, for one and two fused copies. Only the Moment of
  Reign roster lookup (`GetFusionEffectData`) is replaced.
- Files: localization keys derived from the real `ModelDb.GetEntry` (names, move
  titles, encounter titles and loss lines), default visuals scenes, RGBA cutouts
  under `images/monsters/hbrgo_act{1,2,3}/` and their `sources.json` hashes (one
  record per new monster).
- Resources: the new monsters use the shared ×700 HBR HP profile, and native HP
  in legacy mode.

Move-table monsters ([MoveTableChecks.cs](MoveTableChecks.cs)): NeedleBird,
DiamondEyeball, BigotryGate, ShrimpCrab and PillarGuardian (first batch,
2026-11-07), then CrowHorse, IvoryScooper, AbyssKnockerKiller,
TargetWireFrameKiller (the original γ), PileGolemEdge, BarrelWalker,
SlimySerpent, GiraffeHalo and ArachneLine (second batch, 2026-11-08), then
the fourteen normal and weak monsters (third batch, 2026-11-08), then the seven
original act-one monsters SmallHopper, CrestHopper, HealHopper, GoldHopper,
Knocker, ClayKnocker and AbyssKnocker (fourth batch, 2026-11-08; their specs are
`FourthBatch` in RosterSpecs.cs) follow their original hbr.quest move tables.
[hbrquest-move-tables.json](hbrquest-move-tables.json)
holds the rows of the forty-four original variants they copy, trimmed from the
archived responses, with the original chance of each chance-based status
(full-response SHA-256 per enemy; when the archive under
`D:/stsmod/outputs/monster-mechanics-hbrquest-20261107/` is present the suite
re-hashes it).

- Tables: every dp/hp step, option, weight, "once" flag and condition equals
  the snapshot rows (original orders may start above 1, as OctopusTail's hp
  tables do); each table move keeps its original skill, target (single, all,
  self, lowest DP, every ally) and spirit or vitality defense; every move returns to the
  table state; a monster has no move outside its variants' tables; hits,
  statuses, debuffs and DP heal shares (each variant's own) match the original
  skills; original chance values remain archival metadata. Since 2026-11-15
  enemy control statuses attach regardless of block, while phantom replay
  retains its existing passed-target rule.
- Walk, on the real state machine with a seeded RNG: the same seed gives the
  same 40 moves; single-option steps draw no number and weighted steps draw one;
  4000 rolls per sampled step land within 3 points of the weights; Ruin Bullet
  is skipped without Charge and fired with it; PillarGuardian's seven once
  steps open the fight and then 8-10 loop; the gate and crab variants open with
  their own steps, and two gates release Strong Impulse on turns 4 and 2. Second
  batch: each opening; SlimySerpent's seven and GiraffeHalo's eight once steps,
  then their loops; BarrelWalker's four-step loop; ArachneLine's 100:100 hp
  step rolls even odds. Third batch: each opening and variant (the tutorial
  puppet, the α cat horn, the cursed doll, both claws, both knives),
  MarionetteKiller's doubled step 6, the once-only openings and loops,
  OctopusTail's 100:50:50 hp step, and the second MolArms one step ahead.
  Fourth batch: each loop (SmallHopper five steps, CrestHopper six, both
  HealHopper variants, Knocker six, ClayKnocker seven, AbyssKnocker five) and
  its 50:50 or 40:60 steps; GoldHopper escapes 60% on its first two turns and
  always on the third, while a GoldHopper on the player side (a 30A phantom)
  skips the escape rows and attacks every turn.
- Break: ordinary shells now give 2/3/4 turns by room type, plus the existing
  permanent-deck Hello World bonus. Real turn-start expiry restores the shell
  without replacing the queued intent. DP depletion does not stun ordinary or
  special bosses. Dedicated Tezuka permanent Break, SkullFeather duration,
  Narara layered DP and phantom no-Break rules retain their own behavior.
  Each second-batch monster
  switches to its hp table and back, skipping used once steps; third-batch
  monsters with an hp table switch to it, the tutorial puppet and the knives
  (none) keep their dp tables. Fourth-batch monsters switch to their hp tables
  (HealHopper's MC03 variant straight into Repair Ω) and restart the dp table
  when the shell returns.
- Move bodies for a mod character with HBR numerics, a legacy mod character and
  an original character (no Electronic Soldier Manual or Deflector Protection),
  at ascension 0 and 10: attack damage and hits, and the target scope of the
  original skill (single: player creatures; all: every player-side unit;
  lowest DP); HBR enemy Bind, Stun, Illusion and Confusion through full block
  (legacy keeps its original pass-through condition); Spirit Down, Seal, Calamity (HBR only), continuous DP damage (not on an
  original character), DP break, OD down (5% or 10% of an Overclock level; none
  on an original character), Absorb (x4 HBR, x0.5 otherwise, from the damage
  past block), enemy Seal and Virus on contact (Virus lowers the maximum DP to 70%;
  none on an original character), Wisdom Down, attack/defense down (HBR powers
  or native Weak/Vulnerable), Charge, Attack Up, Defense Up and the DP heal
  share; variant heals and TriClawDefault's 4 x5 claws; GoldHopper's escape
  (`CreatureCmd.Escape` recorded) without its bonus reward; a monster outside
  the tables (TezukaSaki) keeps the native player targets.
- Repair β and Repair Ω (HealHopper): every living unit on the healer's side,
  the healer included, regains 10% (all) of its own maximum DP; a unit in Break
  or dead regains nothing; no block; the player's DP is untouched. A phantom
  HealHopper restores itself, another summon's shell and, for a mod character,
  10% of the summoner's maximum Deflector Protection; enemies are untouched.
- Settlement in the same three cases through the production damage pipeline:
  Attack Up doubles (or adds a fifth to) the hit and keeps the higher value;
  Defense Up cuts 75% and, in HBR, adds to defense down; Spirit Down halves HBR
  spirit or adds 25% damage taken; Seal blocks 2-cost and X-cost cards played
  by hand; Bind/Stun cost two energy in HBR (one in legacy) at the next turn start (`PlayerCmd.LoseEnergy`
  recorded) and do not stack; continuous DP damage takes 4 x 5% of maximum DP
  and never HP; Illusion zeroes the next attack card's damage per layer (skill
  cards spend nothing) and lasts two of the holder's turns; Confusion retargets
  the first single-target attack card each turn among the enemies and the
  holder (400 seeded turns land near 1 in 2), a self-hit deals the card's
  printed damage to the holder as the enemy's attack and none to the target,
  area cards are untouched; OD down stops at an empty gauge and skips summons;
  Absorb restores nothing without a shell; Virus lowers the maximum DP to 70%,
  takes the DP above it, sums independent HBR copies (legacy refresh), and
  after two player turn ends gives the maximum back but not the DP (not on an original
  character); Wisdom Down takes a quarter of HBR intelligence or, without
  attributes, of card block, for three turns.
- Phantom side: replayed attacks (`IHbrgoPhantomAttackResults`, which the real
  `PhantomHelper` replay calls) stun an enemy once per combat, past block only;
  defense down, Calamity, Spirit Down and DP drain land on the attacked enemy;
  phantom Ruin Bullet halves one enemy's DP, Mirage Pulse skips a non-plain
  enemy move, Steel Shield and Attack Up buff the phantom. Second batch: Illusion
  and Confusion land past block only, a dazed enemy's next attack misses, a
  confused enemy's next attack either lands or hits itself for its intent
  (half the time over 200 seeded attacks); OD down gives the summoner the share
  once per replay; Disease Plant, Rage Magnum and Piercing Flash debuff every
  attacked enemy; Energy Drain and Absorb Whip restore the phantom's own DP;
  Judgment Ray's illusion lands on every attacked enemy, blocked or not. Third
  batch: Creepy Ball skips the non-attack move of an enemy hit past block;
  Erosion Slice lowers a shelled enemy's maximum (not a blocked one or one in
  Break) until it ends; Deceive Shot, Curse Stamp and Rotation Cut land on every
  attacked enemy.
- Target lists, through the production `HbrAttackTargetingPatch` on the real
  `AttackCommand.GetPossibleTargets`: native and `Players` attacks hit player
  creatures only; `AllUnits` adds the living, hittable summons (a 30A phantom,
  Osty) but not a Hotaru cannon (production `HotaruCannonTargetabilityPatch`)
  or a dead summon; `MinimumDp` picks the living player with the least Deflector
  Protection DP, a player without it counting as 0. The phantom replay reads
  each move's original target and picks the enemy with the least shell DP; the
  real replay damage loop hits every enemy with each segment for an all-unit
  move and only the chosen enemy otherwise, and the original five-argument
  `PerformAttackIntent` still replays a single target.
- The block receiver patch's postfix records damage past block (DP or HP) and
  how much, adding up over hits; a phantom block taking its summoner's hit
  credits both, a summon hit directly only itself; nothing outside a scope.
- Encounters set the gate, crab, puppet, cat horn, cursed doll, claw and knife
  variants, and MolArmsPairNormal starts the second MolArms at step 2. Fourth
  batch: the three act-two training fights use HealHopper's MC03 variant and
  the act-one fights its default one; same-type monsters start one dp step
  apart (three small hoppers and the CrestHopper swarm at steps 1-3, two heal
  hoppers or two knockers at steps 1-2, and in SmallHoppersWeak a second
  SmallHopper at step 2 over 64 seeded rolls of its opener). Since the user's
  decision of 2026-11-08 the 17 earlier fights with same-type monsters on one
  table do the same (the second at step 2, a third at step 3; the second cursed
  doll's once step 1 spent), and over every registered encounter of the three
  acts no two same-type monsters on one table share a starting step (23 fights).
- GoldHopper (user, 2026-11-08: 99 native Intangible and the original's HP 1
  and DP 10). With a mod character on HBR numerics, a legacy mod character and
  an original character, at ascension 0 and 10, in acts 1 and 3, with the
  enemy HP setting at 50/100/300% (300% with 50% growth per act), the enemy
  shell at 0/20/100% (100% with 50% DP growth per act) and a native roll of 58
  or the two-player 116, `EnemyHealthScalingHelper.ApplyInitialHealth` leaves
  it at HP 1/1 and `HighDimensionalShellPower.CalculateAmount` at 10, while a
  Knocker beside it still scales (and has no shell at 0%). The native range
  stays 54-58 (56-60) for the 30A phantom rule; a phantom keeps its HP and the
  phantom DP rule and gets no Intangible. As an enemy the real
  `AfterAddedToRoom` applies 99 Intangible once; ten hits of any size, attack
  or HP loss, through the full native damage hooks deal 1 each and empty the
  shell (a pending break), and with the shell gone the eleventh hit's 1 kills
  it: 11 hits. Effects around "1 per hit", measured: Intangible cuts an HP loss
  of 5000 to 1; HP-only follow-ups below 1 (Magic Sword Enhancement's 1/3 or
  1/2, the endgame cannon's 25%) arrive as 0 on HBR numerics or x0.75 otherwise
  and take no HP; a 1-damage hit spends 1 DP on HBR numerics and 0.75 otherwise;
  500 DP-only damage spends 1; Virus lowers the maximum to 7; 1 Doom already
  dooms it; the production `BreakImmediately` (Remove DP) takes all 10 DP at
  once without stunning it, and the Break's end brings the shell back at 10; with
  Rapture-Bow each hit spends 2 DP (five break it). The move-body checks start
  each shell at 1000 DP, or full when the maximum is lower (GoldHopper's 10).

Not covered: in-game rendering and sprite placement, intent icons and their
displayed numbers, enemy AI rolls inside a real combat, phantom summoning end to
end, map generation, saves and multiplayer. For the move-table monsters: the
full turn order of a real combat (roll at player turn start, Break tick at enemy
turn start), the stun and energy-loss visuals, and card greying under Seal;
since the second batch also the all-unit attack animation and hit effects on
summons, the phantom's all-enemy replay, the confused card's retargeted play
and the illusion miss as shown in a real combat.

## Results

2026-11-08, the 17 earlier fights staggered and GoldHopper with 99 Intangible
and the original's HP 1 / DP 10: candidate
`AD4A0CAD4B1DCDA7706F583267CB9DF273EC81B8E91980F3421F503820D127F3` passes 20054
checks on stable and 20054 on beta v0.111.0 (`beta-latest` profile). Reports:
`D:/stsmod/outputs/monster-batch4c-20261108/validation/roster-{stable,beta}.json`.

2026-11-08, the 17 earlier fights staggered and GoldHopper's lower HP (replaced
the same day by the entry above): candidate
`C2126D9360D6B3B6EC41C956CFF45234197B3305FCDF2E14E3BEB2B6AE16405F` passes 19859
checks on stable and 19859 on beta v0.111.0 (`beta-latest` profile). Reports:
`D:/stsmod/outputs/monster-batch4b-20261108/validation/roster-{stable,beta}.json`.

2026-11-08, fourth move-table batch (the seven original act-one monsters):
candidate `766663D0372B2DFFF996493BED800AD42B70452E7FF1E0719FE3C84792CA6CB3`
passes 19838 checks on stable and 19838 on beta v0.111.0 (`beta-latest`
profile). Reports: `D:/stsmod/outputs/monster-batch4-20261108/validation/roster-{stable,beta}.json`.

2026-11-08, third move-table batch and Judgment Ray's must-hit illusion:
candidate `E02F109EA3F18695EF72E717E3F37F2C6AF71B47781015BE8087AB2CB81DF9A3`
passes 17221 checks on stable and 17221 on beta v0.111.0 (`beta-latest`
profile). Reports: `D:/stsmod/outputs/monster-batch3-20261108/validation/roster-{stable,beta}.json`.

2026-11-08, second move-table batch and the all-unit target scope: candidate
`CF56522EF67C2CF9C239F3B187BEE93AE46685904C11CDDC368E949B10802FEC` passes
9956 checks on stable and 9956 on beta v0.111.0 (`beta-latest` profile).
Reports: `D:/stsmod/outputs/monster-batch2-20261108/validation/roster-{stable,beta}.json`.

2026-11-07, first move-table batch: candidate
`6355B033C5CA9CEF01C424FA0ECD2352B3003ACC58E3E33868CA001F5D2824EF` passes
5516 checks on stable and 5516 on beta v0.111.0 (`beta-latest` profile).
Reports: `D:/stsmod/outputs/monster-batch1-20261107/validation/roster-{stable,beta}.json`.

1.3.0.5 crashed when a new run started. The six new bosses had map nodes but no
run-history icon pair, which the top bar loads by encounter ID through native
`ImageHelper`. The load threw while the run UI initialized, so `NMapScreen.SetMap`
then hit a null. The suite now checks that pair for every boss, using the same
native path. With the 12 icons added (byte copies of each node image, as for the
older bosses), candidate
`A751620CF30CFDB61419DE22FE6574D9F7E452C06728991F70276F4E6E546A4B` passes 3552
checks on stable and 3552 on beta v0.111.0. With one icon removed, the suite fails
on that icon's check. Reports:
`D:/stsmod/outputs/boss-run-history-icons-20261105/roster-{stable,beta}.json`.

Candidate `EB9F0812E3618DF5356578602C87F46ADCC1E86CF6473951A22BD06D8FA766F8`
passes 3540 checks on stable and 3540 on beta v0.111.0 (`beta-latest` profile).
Reports: `D:/stsmod/outputs/act-two-three-roster-20261105/roster-{stable,beta}.json`.
The earlier act-one-only version of this suite passed 897 checks on candidate
`EA503E7DD019A3580579185F3397A5A43100C87065CA6AF66AEBCC394EA2BA6E`
(`D:/stsmod/outputs/act-one-roster-20261105/`).

## Running

Stable, from the checkout root (PowerShell):

```powershell
dotnet build validation/hbr-act-rosters/Checks.csproj -c Release -p:SkipModOutputCopy=true
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hbr-act-rosters/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/act-rosters/stable.json
```

Beta: build with `-p:BetaNativeChecks=true`, `NativeAssemblyPath` and
`NativeRitsuAssemblyPath` pointing at a verified beta profile, and a separate
`OutputPath`/`IntermediateOutputPath` (for example `bin/Beta/`, `obj/Beta/`);
then run `bin/Beta/Checks.dll` with that profile directory instead of `libs`.
The bootstrap verifies the candidate hash and every `profile-manifest.json`
entry before loading.
