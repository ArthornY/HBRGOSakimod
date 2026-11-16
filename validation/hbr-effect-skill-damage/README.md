# Explicit effect skill damage regression

## Native hand-end self-damage (2026-11-15)

The narrow entry `HBR_NATIVE_HAND_TICK_ONLY=1` executes the real native hand-end handlers for Burn, Decay, Infection, Toxic and Wither. These are environmental damage on the player's HP scale: native raw 2/2/3/5/3 becomes 200/200/300/500/300. Their raw DamageVars, native self-dealer, original card source and flags remain intact. The beta Infection call's last `null` is CardPlay; its card-source overload supplies the holder as dealer. Previously that unintentionally selected the holder's skill curve and critical roll.

`NativeHandTickChecks.cs` verifies actual handlers/adapters, all five native descriptions, high holder attributes and attack buffs, dual swords/Magic Sword, no critical RNG, no combo expansion or attack-up consumption, Wither's real FakeUpgrade (raw6 → 600), legacy raw3 and unchanged sourced Kusarigama/FirePotion skills. The full suite's old Burn actor-skill/damage-display expectations were corrected as well.

The same fixed stable DLL `3101AE461EE5597083F46B83FD7471FED10326F7C6DE66819CEF8B5406578DE2` passes **121 checks on stable 0.107.1 and 121 on original beta 0.113.1**. Actual old AE76 and delivered DCEF Infection handlers are independent negative controls: each records raw3, holder dealer and **1928** damage plus a critical sample at the controlled baseline, rather than environmental300. This proves the route defect; it does not reconstruct the other player's exact attributes or lethal damage total.

Run `outputs/card-turn-stall-1131-20261115/run-hand-tick-checks.ps1 -Profile stable`, then `-Profile beta`; add an explicit old `-Candidate`, `-ExpectedSha256` and `-NegativeControl` to reproduce the prior bug. Final reports and the explicit index live under that stage's `infection/`. The existing whole fixture has 23 nullable-flow warnings in unrelated older cases; this thin entry uses ordinary validation compilation, with no changes suppressing those warnings or widening production source scope.

Scene/SFX/Wormy VFX leaves and the canonical resource terminal are isolated. The actual numeric pipeline runs, but synthetic terminal results do not mutate HP/DP or execute native after-damage events. Thus these reports establish real native hand-handler attribution, calculation and formatting, not a complete rendered combat or separate block/resource-cap integration proof. No Godot/package/install/save changes are made.

## Manual glossary static wording (2026-11-07)

The previous run listed 27 power entries that still showed "（对应数值）": 24 in HBR and 24 in legacy, 3 of them legacy only. Their hover templates read one live instance. `HbrManualGlossaryStaticText.Rewrite`, called from `HbrManualGlossaryCatalog.AddEntry`, now gives the manual the rule instead, and `HbrManualGlossaryPresentation.FormatEntry` appends its notes after the formatter's own. Combat hovers and localization are unchanged.

- Card amounts are written as "base（升级后 upgraded）", for example Breath After Miss's "4（升级后6）点格挡". Where a power stacks per play, a note says the amounts add up.
- Live state is named in words: "（剩余回合数）", "（剩余次数）", "（待结算的点数）", "（剩余条数）", "（当前破坏率）".
- Energy uses the manual's energy icons, as the power hovers repeat them.
- Assembled texts are expanded without the live parts: the reward lines start with "每份效果", Coffin shows all three forms, and Savior lists its memory-garden effects, except effect 2, which the hover also omits.
- Attack Down, Defense Down, HBR Fragile and Boost Up's form damage read the static texts their canonical hovers already show (`hbrReferenceDescription`, `canonicalDescription`).

Four entries keep the placeholder. Their hover text and the actual settlement disagree, so the wording is not settled yet:

- HBR Breathing Shelter: the hover states 12 block, which settles as 1200.
- HBR Six Sense Remnants: the hover states +1, which the card states as +1000 damage and +100 block.
- My Happiness, both modes: the redemption text omits the per-stack multiplier.
- Legacy Savior: Sea Breeze's shared text describes the HBR rule. In legacy mode it doubles an existing Vulnerable's bonus when damage resolves, 10 → 15 → 20.

The suite now writes the whole manual of each mode beside its report (`manual-glossary-hbr.txt`, `manual-glossary-native.txt`), for comparing runs. `ManualGlossaryCases` also does the following:

- checks every rewritten entry in both modes;
- checks that only the four entries above keep the placeholder and no raw variable is left;
- compares every card amount with the real card before and after its upgrade;
- records the canonical hovers of all 27 powers;
- runs the real block hook for Breathing Shelter, damage hook for Sea Breeze and Rapture Bow's follow-up, and the applied defense down for HBR Sea Breeze;
- reads the owned hovers of Fate and Unbroken Remnant in both modes, and the HBR card text of Six Sense Remnants.

My Happiness's per-stack multiplier was confirmed from the source only.

Candidate `2C1CE83835319E75B256400F7E0F2040BE1E3576C0E84A511CBFE6F5CEFB4A5F` passes **765 checks on stable and 765 on v0.111.0**, 114 of them new. Against the dumps of candidate `8BD9854E505D57D48B788F09CC38D88EA88311190B3E246779234200A2B8164B`, 21 HBR and 22 legacy entries changed, and the other entries, titles and categories of all 283 are identical. The 27 canonical hovers are identical too. Reports and dumps: `D:/stsmod/outputs/manual-placeholders-20261107/`. The manual was not opened in game.

## Manual glossary coefficients (2026-11-06)

The HBR texts of Eat Passersby, Pain Resonance and Hanging Shot read `{DamageSkill}`, which the manual glossary's formatter did not know. Run through the production entry builder on candidate `F34166FE72A0D15FE37D2F4DE75BD66B08AA5A2C4BDC37ECA6DBF53D095679E9`, the manual showed "造成（对应数值）%倍率伤害" with the note "具体数值由施加此效果的卡牌或能力决定。". `HbrManualGlossaryPresentation` now writes the variable as their card-hover tips do, "（层数×16.67）". No other localization entry uses `{DamageSkill}`, and the legacy texts are unchanged.

`ManualGlossaryCases` in `ManualGlossaryChecks.cs` checks the three entries through `HbrManualGlossaryCatalog.AddEntry` in HBR and legacy runs. It also runs the real `Build` over all 283 entries (250 powers, 22 keywords, 10 relic rules, Block) and records every entry that still shows the placeholder, with the variables that produce it: 24 in HBR, none of them these three. Candidate `21B522555DFFF4D0CE7B195835FC06957184A72BC32A658A7399AB1E5BA30E21` passes **643 checks on stable and 643 on v0.111.0**, 9 of them new. Reports: `D:/stsmod/outputs/manual-glossary-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. The manual itself was not opened in game.

## 31A bond: Strength and Dexterity (2026-11-06)

The user then set the bond, "1，并且把增伤换成力敏", with native numbers changed in the same proportion. Two kinds turn every 100 HBR Block, or every native Block, into one layer; four kinds count layers as Block and also keep Dodge at turn start; six kinds only double the cap from 40 to 80. Evasion keeps the rule below.

The 2.5% damage bonus per layer is gone. With the relic, the current layers give Strength and Dexterity: 2 each per layer under HBR, 1 each per 5 layers natively. The bonus is read on demand, never stored as Strength or Dexterity powers, following See You Again's extra Dexterity. Under HBR `HbrStatPowerHelper` adds it to the Strength and Dexterity layers it turns into attributes. Natively, `DodgePower` adds flat damage and Block under native Strength's and Dexterity's eligibility, and returns nothing under HBR. Doubling or rewriting Strength and Dexterity powers therefore neither keeps nor loses it, and the live Dodge tip states the current amount.

At the host's attributes 100 against Small Hopper (parameter 150), 10/20/40/80 layers give 20/40/80/160 HBR Strength and Dexterity. A native-6 attack goes from 3857 to 4714/5571/7285/10714 (the old bonus gave 4821/5785/7714/11571), and native-5 Block from 500 to 600/700/900/1300. Natively they give 2/4/8/16: Strike 6 becomes 8/10/14/22 and Defend 5 becomes 7/9/13/21.

`DodgeChecks.cs` now checks the 1:1 conversion, the turn-start tiers, and parity with real Strength and Dexterity of the same amount at every layer count: powered and Unpowered damage and Block, and HBR attributes. It plays the real Double Powers on real Strength 3 and 10 layers (attributes 123 → 146, then 128 after 9 layers are spent and 122 without the real Strength), and runs the Dexterity → Block → Dodge loop until the cap ends it: 7 Block gains under HBR and 6 natively, after which Block grows by a fixed 900 or 13 per gain. The texts are checked in both modes. Candidate `8BD9854E505D57D48B788F09CC38D88EA88311190B3E246779234200A2B8164B` passes **651 checks on stable and 651 on v0.111.0**. Reports: `D:/stsmod/outputs/dodge-stats-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play and rendered tips were not tested.

## 31A collection and Dodge (2026-11-06)

The first change of the day; the section above replaces its 200-Block conversion, its six-kind turn-start rule and its damage bonus. The user found the 31A bond "有点抽象". The eight production Dodge patches ran first on candidate `7439504E495AFD570232982C3789B1A34A2F93DB6A95E91C0A84E8F4C89F049A`, with the collection's count fixed by a prefix on `ThirtyOneAKeywordRules.GetPermanentDistinctCount`, against the host's Small Hopper (parameter 150, attributes 100):

- With two kinds, an HBR Block gain of 500 (native 5 at Intelligence 100) became 40 Dodge layers and no Block; even 199 became 40 layers. Block was gained normally only at the cap.
- Dodge compared its layers with the whole hit: 40 layers could not evade Small Hopper's native 6, 814 under HBR, and evaded only hits up to 40.
- With four kinds, 40 layers counted as 40 Block (300 became 340).
- The 2.5% damage bonus per layer applied as usual: 3857 became 7714 at 40 layers.

Under HBR one layer now stands for 100 points (`HbrResourceValueHelper.Fixed`), the scale of player HP, active Block and fixed resources. Two kinds convert every 200 Block into one layer. A hit is evaded when layers × 100 cover it, spending the hit ÷ 100 rounded up: 9 layers evade 814 and spend all 9, 40 keep 31. Four kinds count each layer as 100 Block (300 becomes 4300). Layers, the 40/80 caps, turn-start halving and the damage bonus are unchanged, legacy runs keep every native number, and Dodge without the relic (Hotaru's cannon upgrade) evades the same way.

Texts follow the rule: Dodge gains an `hbrDescription` under both of its keys; the relic's `hbrDescription` is swapped in by `HotaruRelicDescriptionPatch` as Strike Dummy's is; its short hover text `hbrCompactDescription` is registered in `HbrCompactTextKeys`; and the Dodge tip under an HBR holder's relic states the HBR rule. The manual's glossary reads the same `hbrDescription`.

`DodgeChecks.cs` runs the real `GainBlockInternal`, `DamageBlockInternal` and `Block` getter through those patches and records every value before asserting. It then checks the texts through `HotaruRelicDescriptionPatch`, the relic's hover tips, the short text at 1, 2, 4 and 6 kinds, and the glossary's entry builder, in HBR and legacy runs. Candidate `F34166FE72A0D15FE37D2F4DE75BD66B08AA5A2C4BDC37ECA6DBF53D095679E9` passes **634 checks on stable and 634 on v0.111.0**, 39 of them new. Reports: `D:/stsmod/outputs/dodge-hbr-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play and rendered tips were not tested.

## Pain Resonance, Defy Fate Strike and Hanging Shot (2026-11-06)

Three trigger effects of the same kind as Eat Passersby. Their real code ran first on candidate `C4F6975708D42FD5C0AE46B481810887E2787DBEB7DA7655D72EEB3AA831DC62`, against the host's Small Hopper (parameter 150, attributes 100):

- `PainResonancePower` dealt its stacks to a random enemy as Unpowered damage without a declared skill: a literal 6 under HBR, 9 upgraded. Each hit now declares the stacks as its holder's skill on that enemy, six units to 100%: 6 stacks resolve 3857, 9 stacks 5785, and legacy runs keep 6. The card uses `HbrSkillVars.DamagePower<PainResonancePower>(100m)` with its stacks unchanged; its new `.hbr` face says "造成100%倍率伤害" (150% upgraded), and the power gains an `hbrDescription` as Eat Passersby did.
- Defy Fate Strike already cost its phantom 100 HP per hit under HBR (`PhantomHelper.LoseHp` through `HbrResourceValueHelper.Fixed`) and 1 in legacy runs. Only its two `.hbr` faces said 1; they now say 100.
- `HangingShotPower` already dealt its applier's powered attack: 15 stacks resolve 9642, a 250% skill, and 12053 against DP after the native ×1.25; legacy runs keep 15. Only its text said "等同于层数". Under HBR the live power now states its applier's coefficient from its stacks, and its canonical tip inside an HBR card's hover states the per-stack 16.67%.

`TriggerTextChecks.cs` records these real values before asserting, then checks the power texts, card faces, the upgrade and the Hanging Shot hover tip in HBR and legacy runs. Candidate `7439504E495AFD570232982C3789B1A34A2F93DB6A95E91C0A84E8F4C89F049A` passes **595 checks on stable and 595 on v0.111.0**, 30 of them new. Reports: `D:/stsmod/outputs/trigger-texts-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play and rendered tips were not tested.

## Eat Passersby (2026-11-06)

`EatPassersbyPower` dealt its stacks to every enemy as Unpowered damage without a declared skill, so HBR runs took a literal 3 per Block gain (5 upgraded). Like Thorns and the shared relics, it now declares the stacks as its holder's skill, six units to 100%: three stacks are 50%, five 83.33%. One command covers every enemy with one critical sample. Without HBR numerics the declaration stays inactive and the stacks deal native damage.

Texts follow the same rule. The card uses the new `HbrSkillVars.DamagePower<T>` (stacks unchanged), so its `.hbr` face states "造成50%倍率伤害". The live power states its coefficient from its stacks; its canonical tip inside an HBR card's hover states the per-stack 16.67%, reading the card-hover scope that `HbrVigorSkillDescriptionPatch` now exposes as `IsHbrCardHover`. Legacy runs keep every native text.

`EatPassersbyChecks.cs` runs the real `AfterBlockGained` (one enemy, upgraded stacks, two enemies, legacy) and the real card face, upgrade and hover tips with the production `CardDescriptionLocPatch`. Candidate `C4F6975708D42FD5C0AE46B481810887E2787DBEB7DA7655D72EEB3AA831DC62` passes **565 checks on stable and 565 on v0.111.0**, 25 of them new. Reports: `D:/stsmod/outputs/eat-passersby-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play and rendered tips were not tested.

## Six Sense block overflow and High Song replay (2026-11-06)

A 1.3.0.11 run stopped after Magic Sword Mode ("卡结算了"). Mimic Moonlight's replay stacks are a power, so Double Powers doubled them and every later manual card was replayed. Each Double Powers then ran twice, and seven plays in one turn multiplied every power by 16384. Magic Sword Mode's Simplicity block (units × Intelligence, Intelligence growing with Dexterity, then Dual Swords ×1.5 and percentage block boosts) passed int range. `CreatureCmd.GainBlock` caps the creature at 999,999,999 Block, but then records the gain as `(int)amount`, and that cast faulted the whole play, leaving the card on screen. The same replay ran High Song a second time: it chose another song and tried to remove itself again.

Fixes:

- `BlockGainLimitPatch` caps every `Hook.ModifyBlock` result at the native 999,999,999, the most one gain can add. `LargeHealthSacrificeBlockBoundaryPatch` did this for Sacrifice alone.
- `IntegralStatAmountHelper.Normalize` bounds every power's amount to the native ±999,999,999 before `ApplyInternal`'s cast and `ModifyAmount`'s int sum, as it already did for Falling Continually. Doubling a capped power through an amplifier used to wrap that sum to the negative cap.
- `SixSenseCardCapability.AfterCardPlayedLate` logged a fault in the Six Sense effects and let the card finish its play. The card effects were removed on 2026-11-09, and this guard with them.
- `HighSongHbrgoSaki.OnPlay` returns when the card is no longer in a combat pile, which is the replay after the first run replaced it.

`SixSenseOverflowChecks.cs` installs the production `BlockGainLimitPatch` and `IntegerStatAmountPatch`, skips only the two combat-history recorders (their argument casts still run in the commands), and checks:

- A 3-unit skill block (what Simplicity gave) at 999,999,999 Dexterity, which resolves past int range, gains block at the cap through the real `GainBlock`; an ordinary one still gains 300. Until 2026-11-09 this ran the real Simplicity effect, removed with the six-sense card effects.
- Real `PowerCmd.ModifyAmount` doubling capped Dexterity through a stand-in 125% receiving modifier stays at the cap; 600,000,000 doubles to the cap and 40 to 80; a 3,000,000,000 Strength `PowerCmd.Apply` is stored at the cap.
- A replayed High Song outside combat piles neither throws nor adds a song; a played one is in the play pile, so its first run still chooses.

Candidate `6FD9E5812486A21C8A53E46DA617CE47B3D17F3D3E63D78F70CBB9A68157F219` passes **540 checks on stable and 540 on v0.111.0**, 15 of them new. As a negative control, the same checks without `BlockGainLimitPatch` fail with the reported `OverflowException` from `Decimal.ToInt32` in `CreatureCmd.GainBlock`. These checks turn the combat-in-progress flag back on (v0.111.0 needs a full turn state), because an earlier case leaves it off. Reports: `D:/stsmod/outputs/six-sense-overflow-20261106-r2/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play, a real Moonlight replay chain and the card's on-screen path after a fault were not tested.

## Madness self-damage (2026-11-06, removed 2026-11-09)

Madness's card effect, and this self-damage with it, was removed with the six-sense card attributes on 2026-11-09; `MadnessSelfDamageChecks.cs` was deleted. The record below describes the 2026-11-06 candidate.

The user fixed Madness's self-damage at 100 under HBR. `SixSenseCombatHelper.ResolveCardEffects` declared the native 1 as the player's own effect skill: a 16.67% coefficient on their attributes, with Vulnerable and Weak applied. This fixture measures that route at 642 with Madness 0 and 2071 at Madness 150 (Strength 400). The 1 now converts like Corrupted's literal HP, through `HbrResourceValueHelper.Fixed`: 100 under HBR, 1 without. The command keeps the player as target and dealer, no card and its Unpowered flag, so it is still damage that Block and DP take first. Burn and the other status cards still deal their holder's skill, as decided earlier.

`MadnessSelfDamageChecks.cs` runs the real card-effect method for a played Strike:

- 100 at Madness 0 and 150, and with the player's own Vulnerable and Weak. No skill is declared and no critical sample is drawn.
- Native 1 with HBR disabled.
- The Six Sense page's real description builder, the manual glossary formatter on the HBR entry and the card hover entry all state 100 and no coefficient. With HBR disabled the page keeps the native 1.

Candidate `1725BC8EEB3E57A9E4DE3D272B2E36E1201F50C9F60B08509C87473EA1EF7C8A` passes **525 checks on stable and 525 on v0.111.0**, 33 of them new. The v0.111.0 run loads the telemetry-isolated copy `0C0C1CC3DD3FFF8E…`. Reports: `D:/stsmod/outputs/madness-self-damage-20261106/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game combat, Block and DP absorption and rendered text were not tested; this fixture's terminal command changes no HP, Block or DP.

## Six-sense card attributes and enemy resistances removed (2026-11-09)

The user decided to keep only the six-sense points (「要不给六感机制删了」, 「就这么删」, 「萤也一起删」; Narara's nullification 「这条先关掉」). `SixSenseRemovalChecks.cs` checks, against the real candidate:

- Structure: the attribute patch, the resistance patches, the card badge, hand focus and resistance-row nodes no longer exist; the starting-inventory patch still initializes HBR numerics and the fifteen starting points and assigns nothing to cards. `SixSenseCardCapability` stays registered but is no default capability, holds no field and overrides only its save and load: an old `{"attribute":3}` loads and nothing is written back. The retired powers (resistance, the three effect counters, their accumulator, Godspeed, Haste, Break Insight) declare no hook and the six-sense ones are hidden; no source applies them or Narara's nullification. The six resistance cards are in `RetiredCardTypes`, registered in the deprecated pool; the manual leaves out the retired powers. Source checks for the cannon paths: Contest's hit and preview count debuff kinds, Millsage reports power changes to Six Sense Remnants, Longing Lv.6 and Light Points give missing debuffs, the main phantom reads its attack count, and no roll bonus or damage multiplier comes from a card attribute.
- Points: card-type pairs are unchanged; a level of 10 raises only its own stat by 20; `SixSenseCombatHelper.Level` still reads it; the fifteen starting points go five per pair, once; a victory raises one sense of each pair; the memory garden gives a point at 30 and 60 total points and not at 29.
- Replacements: native Destruction Rate is a flat 0.5% per stack even beside an old fully broken resistance power, HBR stays 1%. The missing-debuff rule (native Weak/Vulnerable/Fragile and HBR attack down/defense down/Fragile through the real `PowerCmd.Apply`) gives distinct missing debuffs, stops at three, leaves an existing one alone, and counts kinds like Triple Scars. The main phantom counts one per attack on each enemy (not per hit), ignores the summoner's attacks and unpowered hits, stops at six, saves the count as enemy id and count, and its minions keep none. Six Sense Remnants rewards an enemy's first gain of each debuff kind, per enemy, not buffs or the player's side, and a kind gained before the power was held never counts later. Trace Old Scar's power discounts the chosen card type by 1 down to 0 and names it. An older Narara nullification is inactive, caps no damage and lowers no roll. The card variables of the replaced cards and their upgrades.
- Text: the six-sense keywords keep only the point rules; the manual relics, the replaced cards, the retired cards and the three emptied memory garden upgrades say what they do now; the phantom texts state +15% per attack, at most six; Destruction Rate drops the 110% sentence; the real manual build lists no retired power and its seven six-sense entries mention no roll or resistance; the six-sense page and choice texts.

- Card faces (`SixSenseCardFaceCases`, after the production `CardDescriptionLocPatch` is installed): the replaced and retired cards' real faces in both modes, with base and upgraded values, format every variable and mention no resistance; Six Sense Remnants' hover. The faces are kept in the report's cases.

Candidate `953C64FBE0A8F033F7AB447FE2B0FA56A60EBB3D6F50ACC67FF1CDDA14FF4583` passes **1080 checks on stable and 1080 on v0.111.0** (765 before): 358 are new (237 for the removal, 121 card faces); the Madness group (38) and the four fault-isolation checks left with the effects, and the overflow group now has 11. Reports: `D:/stsmod/outputs/six-sense-removal-20261109/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game play, rendered text in the game's fonts and the cannon actions themselves were not run here.

## Fate dice and the magic sword switch (2026-11-09)

31O's swordheart is kept between combats, the magic sword is a HUD switch, and its cost goes onto the fate dice (design v3, `D:\stsmod\outputs\deck-rethink-31o-20261108\magic-sword-fate-dice-design.md`; notes in `docs/hbr-fate-dice.md`). `FateDiceChecks.cs` runs the production rules on the host's player as 31O, with the record saved on the real manual:

- Swordheart from rolls: a raw multiple of seven gives one (7, 14, 77, 98), a 7 in the number gives none without 七之刻印, the raw roll counts and not the modified one, a critical success gives two and no multiple-of-seven one on top; 七之刻印 gives one for 17 and 27 and two for 14 and 77, only when fitted; 剑鸣 makes a critical success four; other characters gain none and carry no fate die.
- The critical range: 宽刃 98, 狂骰 95, both 93, 绝境 95 below half HP and not at exactly half, nothing from the bag.
- Cracks through the manual: a crack on a full set pushes the last fitted fragment to the bag, two fill two slots, the third is raw −3 and five are −9, never below 1; 修骰子 is offered with a crack and removes one; slots stop at five; an old save reads as empty slots for its act.
- Fragment effects: 整数's draw, 狂骰's self-damage at 10 or less, 鹰眼 only in bow form on a critical success, 镜面 triggering on repdigits, 托底 under 20, 幸运数字 only in dual swords (and no 7/77 rule without it), the three percent modifiers, 孤注 blocking advantage, 再来 once per combat and 先手 once per round.
- 镜面 (corrected the same day, 「全局统筹」: 「镜面的 ×2 只算进伤害和格挡，不算进给予的能力层数」): on a card's capability with the production `SetCurrentRollValueMultipliers`, raw 77 (tier ×1.2) makes the attack's damage and the skill's block ×2.4 while the Strength they give stays ×1.2; raw 76 and an unfitted 镜面 stay at ×1.2; a card rolled inside this one starts without it, the outer roll gets it back afterwards, and a reset clears it.
- Kept swordheart: seven kept, 剑心通明 doubles only this combat's maximum (7→14; forge 9→18) and swordheart costs once; it generates no swordheart and upgrades to Innate. The end of combat cuts 12 to 7 and keeps 4 as 4. Anoko keeps its defensive powers and generates Meditation without granting Clarity or filling swordheart. Meditation's real card body grants 1/2 swordheart outside magic sword, draws three and gains one energy in either mode.
- The manual's commands: fit and remove outside combat, the saved record, locked during combat, a claim once per rolled set, no purchase outside a merchant's room.
- Structure: 魔剑模式 no longer overrides the survival and dual-swords hooks, a full swordheart puts no card in hand, the magic sword no longer weakens the forms, 加速 and 主人公特典 are retired cards (the six-sense removal count of retired cards goes from 7 to 9).

Candidate `538424FD3410E4F86D55ABD177CE6C748F23A50A0BDA0AF1605786695F3F3CA7` passed **1174 checks on stable and 1174 on v0.111.0** (1116 before; the 58 new ones are these cases). The 镜面 correction adds 8: candidate `6A6FE4E4B92874230108CC18CEAA72736E78DD84802FB8F625A08E48036F4C3F` passes **1182 checks on stable and 1182 on v0.111.0**. Reports: `D:/stsmod/outputs/mirror-fragment-20261109/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. Reports: `D:/stsmod/outputs/magic-sword-fate-dice-20261109/validation/{stable,beta}/hbr-effect-skill-damage/report.json`.

Not run here: the HUD button, the reward screen, the rest site button, the manual page, the merchant's stock in a real room, co-op sync and Lucky Seven's hits in a real combat.

## 人造救世主 retired (2026-11-09)

The user removed the base character (「话说游戏里还保留着的人造救世主角色删掉吧一起」). It was already hidden from the native character select and its random roll; `HbrgoRetiredCharacters` and `RetiredArtificialSaviorCallSitePatch` take it out of the places that still listed or counted it. `RetiredCharacterChecks.cs` checks, against the real candidate:

- Only `HbrgoSaki` itself is retired (id `HBRGO_SAKI`); 31O, 30A, Hotaru and Ironclad are not. The class stays concrete and hidden, 31O stays selectable and in the library, and the `HBRGO_SAKI` texts (title, 「人造救世主卡牌」) stay for old saves.
- The filtered list drops it and keeps the order; `SelectableCharacters` is the real `ModelDb.AllCharacters` (stubbed with five characters) without it; stats left by old 人造救世主 runs do not count as a character played.
- The call-site patch targets, installed and read back with `PatchProcessor.GetCurrentInstructions`: the custom run's character buttons and modifier list (the 「人造救世主卡牌」 tickbox), the daily run's rolled characters and character-cards modifier (`NDailyRunScreen.RollModifiers` on stable, `ModifierModel.Pick2Good1Bad` on v0.111.0), Orobas's Sea Glass options, Kaleidoscope's Neow check, and the Daily Run and Orobas epoch gates (whose two played-character counts also skip its stats). Every original read of `ModelDb.AllCharacters` there is replaced.
- The 14 cards only it could get are in neither the 31O nor the 30A pool and keep their classes and texts; 模仿-邪眼海潮斬 was already retired with the six-sense resistances. An old 人造救世主 run opens the card library on the shared 31O row (`FromCharacter` and the filter mapping), and the unlocked characters still leave it out.

Not run here: the patched game methods themselves (the custom-run and daily screens, Orobas, Kaleidoscope, the post-run epochs) and old saves in the game.

## Enemy-side fixed damage (2026-11-05)

Shared and event relics that damage enemies now declare their native amount as the holder's skill, six units to 100% like Kusarigama: Festive Popper, Forgotten Soul, Letter Opener, Lost Wisp, Mercury Hourglass, Mr. Struggles, Parrying Shield, Screaming Flagon and Stone Calendar. Most pass their `DamageVar` to the typed adapter. Lost Wisp and Mr. Struggles pass a decimal, so `HbrNativeRelicEffectSkillDamagePatch` wraps that command. Parrying Shield's Block threshold is a fixed resource, 10 native and 1000 in HBR.

Poison, Constrict, Disintegration, Strangle and Demise now declare each tick's stacks as their applier's skill. `HbrNativeDamageOverTimePatch` finds each power's one decimal command in whichever async body holds it; v0.111.0 moved Poison's into a new `Trigger` method. An enemy applier resolves through the incoming curve like Galvanic, and a player-side one through its own attributes. Stacks applied by their holder, or with no applier, are a fixed HP loss of a hundred per stack instead. As the holder's own skill, the six-stack Disintegration a hero chooses from Knowledge Demon would deal 3857 per turn with this fixture's stats, more than a 3500 HP hero. `HbrNativePoisonPreviewPatch` computes `CalculateTotalDamageNextTurn`, which health bars and the top bar read, through the same scope without a critical sample. It calls the preview's own `Hook.ModifyDamage` overload, which gains a `CardPlay` on v0.111.0.

`NativeEnemyDamageChecks.cs` runs the real hooks:

- Mercury Hourglass, first-turn Festive Popper, empty-hand Screaming Flagon, Forgotten Soul on exhaust, Parrying Shield below and at 1000 Block, Lost Wisp after a power card, and Mr. Struggles on turns 1 and 3.
- Hero- and enemy-applied Poison with their previews, applierless Poison, enemy-applied Constrict, self-applied Disintegration, and hero-applied Demise and Strangle.
- Each result matches an explicitly declared skill of the same source, or the fixed loss. Disabled HBR keeps native amounts.

Text checks use native Simplified Chinese with the mod's tables:

- Converted relics state coefficients. Parrying Shield shows 1000 Block. Mr. Struggles uses `HBR_NATIVE_MR_STRUGGLES.description`.
- Poison reads "失去…倍率的生命", Constrict "受到…倍率伤害", and self-applied Disintegration its fixed 600 or 700.
- Powdered Demise and Tinker Time's Choking option state coefficients.
- The point unit after a closing color tag is gone, Kusarigama's included.

Letter Opener's and Stone Calendar's hooks and Mad Science's rider text are not executed; they use the same adapter and tagging.

Candidate `B2AA6DDCC5F795FB0562A7EBA4C2CEED4B39B1C55F14E480E9EA725E2E358982` passes **486 checks on stable and 486 on v0.111.0** (previously 350). The v0.111.0 run loads the telemetry-isolated copy `0C0C1CC3DD3FFF8E…`. Reports: `D:/stsmod/outputs/enemy-fixed-damage-20261105/validation/{stable,beta}/hbr-effect-skill-damage/report.json`. In-game combat, health bars and rendered text were not tested.

## Environmental unit (2026-11-05)

Dealer-less audited effects, currently the four event `Damage` fields, used the
outgoing player unit of 1000 per raw unit. Their 3/14/11/8 became
3000/14000/11000/8000, more than an HBR player's whole HP. They now use
`HbrDamageSkillAdapter.EnvironmentalPowerUnit`, the 100 player HP scale that fixed
HP losses also use, so the same events deal 300/1400/1100/800 before the normal
target defenses and caps.

Their option texts showed the amount as a skill coefficient
("失去[red]233.33%倍率[/red]点生命"), which means nothing without an attacker. Event
damage variables now display the environmental amount and keep the native point
unit ("失去[red]1400[/red]点生命"). Card, relic, potion and Thorns coefficients still
display percentages. The fixture formats each event's real option template and
checks both the HBR amount and native text with HBR disabled.

With the Galvanic case below, the fixture passes **350 checks** on stable and on
beta v0.111.0 against
`B45389DCF14964ACFF40BFA5EF1342DBAE50771965C959FE5B4978BBCA7C9C8A`, with zero
build warnings or errors. Reports are
`D:\stsmod\outputs\environmental-text-20261105\validation\{stable,beta}\hbr-effect-skill-damage.json`.
The unit change alone passed 342 checks against
`F3AFCF5C699D6C2A9C10DBD83D538D76C77B7FD25F3524130950145669BE71CB`
(`D:\stsmod\outputs\environmental-unit-20261105\`).

## Galvanic regression (2026-11-05)

Globe Head's Galvanic damages the player who plays a Galvanized card by its raw
stack count, with no dealer, so HBR runs took a literal 8. `HbrNativeGalvanicDamagePatch`
declares that command the way an enemy's Thorns is declared: the stacks become the
Galvanic owner's skill units on the enemy incoming curve. Native flags, the null
dealer and card, and the stack counters stay unchanged. Galvanized text uses the
same skill display. On a card, the card's owner decides the mode; for the cardless
hover tip that Galvanic builds, its enemy owner decides it.

The fixture runs the real native `GalvanicPower.AfterCardPlayed` for a Galvanized
Inflame. In HBR the hit equals an explicitly declared enemy-sourced skill with the
same raw units (1085 with the fixture's stats), rather than a literal 8 or a
dealer-less environmental amount. Both texts show 133.33%. With HBR disabled, the
damage and both texts stay at native 8. Before the environmental unit change, this
case first passed **342 checks** on stable and on beta v0.111.0 against
`F95CD70D0FB2AB2467245ED42AD4059AC3C807378557A2B96B89865801C610C8`, with zero build
warnings or errors. Reports are
`D:\stsmod\outputs\galvanic-skill-20261105\validation\{stable,beta}\hbr-effect-skill-damage.json`.

## Shared-host patch registration regression (2026-11-04)

The current fixture passes **322 checks** against the same frozen merged production
DLL `7B6226B14AE5080BCD191E46A8F4D078CF5BD67024B00F40008959F230D0569B`
in all four combinations:

| Linked stat host | stable | beta v0.111.0 |
| --- | ---: | ---: |
| overcharge-fix: absolute hook not installed by host | 322 | 322 |
| test-pack: absolute hook already installed by host | 322 | 322 |

All four fresh fixture builds have zero warnings and errors. Reports are
`outputs/effect-absolute-fix/fixed/{overcharge,testpack}-{stable,beta}.json` in
the overcharge-fix checkout. The tested `Program.cs` SHA256 is
`94BB8B97F6CAAA54AD16A28C0A8EF1C4649EC590631555BD0BE58828672BC85A`.

The previous fixture installed `HbrAbsoluteBaseDamageHookPatch` after invoking
the shared stat host's setup. The newer host already installs it for Strike
Dummy checks. Registering it a second time under another Harmony owner executes
two `BeginHook` prefixes: the second correctly disables the nested same-intent
receipt. Thus an unknown absolute preview incorrectly *in that test assembly*
entered skill conversion (3857 instead of 6). The production guard was correct.

Fresh pre-fix controls using the same historical production DLL
`D6990D1CFD5D1B904653CD3164DD1FA981B33C6BE98F9D145F42D9627B312E6E`
passed 315 checks with the old host, but failed at check103 with the newer host.
Their reports are `outputs/effect-absolute-fix/baseline/source-overcharge-stable.json`
and `source-testpack-stable.json`. With the fixed fixture, that same historical
DLL and newer host pass322 (`fixed/testpack-historical-d699-stable.json`).

Fixture setup now queries the actual runtime hook target and exact production
Prefix/Finalizer methods. It installs only when both are absent, reuses one
complete existing pair, and fails on partial or duplicate registration. A second
setup request must retain exactly one pair. Existing resolved6 versus newly
declared3857 expectations remain unchanged; additional controls require both
preview and actual damage to return to6 after the explicit declaration ends,
for both outer absolute and precalculated scopes. No production method or
damage formula is changed, and no expected result is relaxed.

These are managed command/hook tests with the terminal HP isolation described
below. No game, package or installed-file modification is performed.

## Earlier effect-skill coverage and frozen candidates

The earlier effect-skill/Vigor candidate `4C5E94B28D29CEF3831FC4D19CAC7DB986E991DB7D1A06066E0EFE1BA61E1E3A` passed315 assertions on both stable and beta v0.111.0. Its historical reports remain in `outputs/vigor-skill-coefficient/candidate-stable.json` and `candidate-beta.json`.

The preserved pre-change DLL is `outputs/effect-skill-damage/baseline/HBRGOSakiMod.dll`, SHA256 `89AE465F1CCFAB252D5C9327F58C2F953D1687BB2164417B0338F88331DF04B7`. Its real native Kusarigama third-attack handler produced literal 6 damage where the declared 100% skill must produce 3857 for actor stats 100 against enemy parameter 150. `baseline-stable.json` records this failure before any missing-new-helper check. `intermediate-resolved-stable.json` preserves a second genuine failure from candidate `21C85851BA1BE7FEF290EE86BF664571A716021C73F62538700D8061C377B8F8`: an outer matching precalculated context incorrectly left an explicit skill preview at 6 instead of 3857. Both behaviors pass in the final candidate.

Coverage includes:

- Raw skill units 6, 2 and 3 through the actual attribute calculation, native Weak/Vulnerable, persistent HBR amplification, an active Vigor counter and a ready Pen Nib. A powered Pen Nib control really doubles damage; the original Unpowered effect gains no such bonus and leaves its marker/counter unchanged. Preview never samples RNG; actual damage uses its own critical sample independently of a conflicting parent sample. Environmental damage without a dealer uses raw units times 1000, while undeclared Unpowered and legacy damage remain literal.
- Real native Kusarigama `AfterCardPlayed` three-attack counting, target RNG and original typed DamageVar adapter; real native Thorns `BeforeDamageReceived` with Thorns on both actors, original retaliation flags and no loop. Real native Burn `OnTurnEndInHand` also passes raw 2 through its typed adapter to exact skill damage 1285; FirePotion `OnUse` passes raw 20 through its direct-decimal consumer to exact skill damage 12857. Burn retains its native Unpowered|Move flags and Burn card source; FirePotion retains Unpowered, its native dealer and null card source. Raw variables/counters and command target/dealer/card/props remain unchanged. No native handler or typed adapter is replaced by a copied algorithm.
- Claimed canonical command scopes across an actual asynchronous continuation, synchronous/asynchronous faults, same-argument unknown nested commands, same-command AoE targets sharing one independent sample, non-null card attribution, and matching outer precalculated/absolute contexts. Unknown resolved damage stays resolved, while a new explicit declaration still converts in both preview and actual command paths.
- A real `HpOnlyDamageContext.Damage` invocation and its production canonical command patch distinguish already-resolved unknown HP damage from a declared raw skill: unknown amount 1 stays 1, while explicit raw 1 uses HP weighted-stat 140 (Strength 160, Dexterity 100) for damage 928 even with target DP 2000. It differs from the DP weighted-stat 120 result 785, keeps HP-only flags/card/dealer and closes pending/current contexts without spending terminal-isolated DP.
- All four event handlers' original IL calls to the patched typed adapter, followed by execution of those exact native adapters with owned native DamageVars: 3/14/11/8 become environmental 300/1400/1100/800 (raw units times the 100 player HP scale). Original DamageVar flags, null dealer/card and storage remain intact. The complete event choices/UI and subsequent event rewards are not executed. These events share the typed adapter; there is no separate event damage patch class.
- Native `LocManager`/SmartFormat formatting of Kusarigama, Burn and Thorns descriptions, including nested native diff formatting, canonical owner gates and an explicit unowned merchant display scope. The displayed 33.33% is rounded text; numerical conversion retains the raw 2/6 coefficient. Status registry/storage and exact native consumer metadata are checked for five typed adapters and three direct potion `OnUse` bodies. The other potion/status handlers and the surrounding `PotionCmd.Use` inventory/consumption lifecycle are not executed here. The merchant Harmony patch installs, but no shop node is instantiated.

The fixture links the existing stat-power minimal host, overcharge bootstrap and strict telemetry isolator without editing them. Production canonical numeric Damage Prefix/Postfix/Finalizer and the real actual-damage wrapper/full native Hook execute. A lowest-priority terminal prefix replaces only the native HP command body, returning synthetic damage results after calculation; HP/DP spending, target caps in the native damage body, the complete native `AttackCommand.Execute` body and full combat are outside this fixture; the Vigor extension below does execute native BeforeAttack/AfterAttack hooks and their real consumption. VFX, logging, energy icon pool/prefix lookup and Godot string construction are isolated host leaves; native descriptions and production skill tags/formatting are retained. No Godot rendering, game session, save/clone suite, package or installed-file changes are performed.

Run from the checkout using the pinned candidate (the default mod build output can also be passed with its verified hash):

```powershell
$candidate = (Resolve-Path outputs/vigor-skill-coefficient/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/hbr-effect-skill-damage/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hbr-effect-skill-damage/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/vigor-skill-coefficient/candidate-stable.json
```

Beta uses the existing verified runtime profile and a separate build output:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hbr-effect-skill-damage/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-effect-skill-damage/bin/Beta/Checks.dll $candidate $digest $profile $godot outputs/vigor-skill-coefficient/candidate-beta.json --isolate-telemetry
```

Candidate/profile hashes are checked before loading. Stable native SHA256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. Beta source native SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; its output-local isolated copy is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`. The isolator verifies the module initializer consists only of the Sentry initialization call and return, zeros just that call, proves every other byte is identical and writes proof into the report. Original runtime/profile assemblies remain unchanged. Rng counter assertions read the native public property on stable or its private backing field on beta; no RNG method is replaced.
The Miniature Cannon extension adds 43 assertions; the first 197 successful assertion strings exactly match the earlier report. The pre-change DLL is frozen at `outputs/miniature-cannon-skill/baseline/HBRGOSakiMod.dll`, SHA256 `E6615360E4D8FFAA20A5ED9FD26A6498AAAC9C892BB2091EF21EA1A86F41E661`. `baseline-stable.json` reaches its real native additive hook and fails at final damage 3860 instead of 5785: raw base 6 first becomes 3857.14, then native flat 3 was appended. The candidate converts the raw 3 addend to the independent 50% skill component 1928.57, combines the two components, and floors once to 5785.

The extension uses a real upgraded native Strike and real `MiniatureCannon.ModifyDamageAdditive`, retaining its raw ordinary `ExtraDamage` variable at 3. The complete original command executes through the actual wrapper and native Hook; no extra damage command is issued. The fixture's relic-inclusive listener prefix explicitly runs at `Priority.First`, before the linked stat host's power-only terminating prefix. An independent real `RunState.IterateHookListeners` assertion requires the cannon relic exactly once. A passive native hook postfix observes, without replacing or changing its return, exactly one actual calculation call returning 3. Reports call the listener projection `nativeListeners` and the passive observations `nativeAdditiveCalls`; neither is the native outer Hook's applied-modifier output.

Checks cover current attributes, native Weak/Vulnerable and HBR amplification after the base-plus-bonus sum; one shared successful critical sample gives base 9000 plus bonus 4500, with no second RNG draw. Raw zero still gets the independent additive skill; at enemy parameter 330, base and bonus each retain their own minimum 1, totaling 2. An active Ghost focused-breakthrough bonus of 0.36 units applies to the main base only. Non-upgraded, native non-powered and foreign-owner gates remain native. The ownership guard preserves its dealer-owner OR card-owner rule, and legacy mode still yields raw 6 + flat 3 = 9. Absolute world-space base 6 remains 6 while the extra component becomes 1928.57; its existing noncritical rule consumes no RNG even with a critical seed. Precalculated and unknown HP-only damage continue to skip the complete additive phase. The original additive stage is retained; this fixture does not add a new Madness-capability host.

Owned and explicit merchant-context native descriptions render the addend as 50%, preserve raw `ExtraDamage = 3`, and restore canonical native formatting after scope disposal. These remain managed calculation/formatting checks with the synthetic terminal HP leaf and telemetry isolation described above, not game execution or shop rendering. This extension changes only its Program and README, adds no package and touches no installed files.
The Vigor extension adds75 assertions. The first240 successful assertion strings exactly match the previous Miniature Cannon report. Its frozen baseline is `outputs/vigor-skill-coefficient/baseline/HBRGOSakiMod.dll`, SHA256 `18CBFD50AA21476E1557A9D3EFD3985E3796AEF0894EF223C9C4582CDC69D836`. `baseline-stable.json` first passes the complete prior240 checks and real native Vigor reservation/consumption checks, then fails at6857 rather than5785: the old implementation adds3000 world-space damage to base3857 instead of evaluating raw6+3 as one150% coefficient.

The extension constructs a real native AttackCommand and owned Strike, executes native `Hook.BeforeAttack`, the original Vigor additive hook through the actual command/Hook calculation, attaches the captured native command results with `AddResultsInternal`, and executes native `Hook.AfterAttack`. Three Vigor remain until AfterAttack; native consumption removes them and production reservation tracking clears. A passive observer requires exactly one effective native additive evaluation returning raw3. The canonical damage command retains input6, original flags/card/dealer, and an explicit powered-skill intent still matches its original raw6 while the numerical coefficient becomes9. No native Vigor amount, reservation, consumption or damage coefficient is directly replaced.

Checks cover actor attributes with Strength20, native Weak/Vulnerable and HBR amplification25% (final9944), one shared successful critical sample (13500), legacy6+3=9, Unpowered exclusion, zero-base plus Vigor3, and a single min1 for the combined coefficient at high enemy E. Absolute world base250 remains250 plus an independent noncritical Vigor3 component, consuming no RNG. Precalculated and unknown HP-only damage skip Vigor collection. A phantom consumes only its own Vigor3, preserving its summoner's9. Real cannon complete-action batch/attack scopes apply Vigor before one Soul factor and consume it once; a native owner-attack reservation prevents the cannon from stealing it, then the owner attack still receives and consumes its own Vigor. These execute the native hooks and production reservation machinery around a captured damage body, not full AttackCommand.Execute or a complete animated cannon phase.

Owned Vigor3 SmartDescription uses native LocManager/SmartFormat to display50% without rewriting its counter. Real native Patter.HoverTips constructs the generic six-points-to100% Vigor explanation in an explicit HBR card context, switches back to native text in legacy context, restores canonical native text after disposal, and switches to HBR again. This exercises the original card getter and HoverTip construction, with only `PowerModel.Icon` resource loading isolated to a null icon to avoid Godot ResourceLoader/native bindings. Tooltip descriptions, scope binding, factories and formatting are retained. No game rendering or tooltip UI is claimed; no new clone/save suite, package or installed-file changes are added.
