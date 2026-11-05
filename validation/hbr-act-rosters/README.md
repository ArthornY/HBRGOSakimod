# Act roster checks

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
  order, from the real `GenerateMoveStateMachine`.
- Every move body, in HBR and legacy mode at both ascensions: the recorded
  attack (damage × hits), raw `Move` block, self Strength, the debuff on the
  target (HBR attack/defense down through the mod adapter, native Weak/Vulnerable
  in legacy mode, with the expected strength), fixed DP recovery (×100 in HBR
  mode) and extra damage only against a target with DP. `PerformAttack`,
  animations, the native block command and the extra damage command are
  recorded leaves.
- Phantom side effects: exactly the attack+debuff monsters implement
  `IHbrgoPhantomAttackEffects`, which reapplies the debuff once for its own move
  and nothing for the others.
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

Not covered: in-game rendering and sprite placement, intent icons and their
displayed numbers, enemy AI rolls inside a real combat, phantom summoning end to
end, map generation, saves and multiplayer.

## Results

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
