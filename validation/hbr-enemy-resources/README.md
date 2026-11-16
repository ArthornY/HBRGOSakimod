# Enemy HP / DP resource checks

The HBR enemy reference stores state values, HP and DP separately. These checks
validate the mod's resource adaptation: native STS2 HP selection remains the
starting point, while 33 table-monster classes use the HP/DP shares of their
42 original variants. Player Vitality and Spirit do not determine enemy HP/DP.

## Current profiles

`HbrEnemyResourceProfile` stores a native reference HP, reference maximum HP and
reference maximum DP at the default 20% shell setting.

| Profile | Native reference HP | Reference HP | Reference DP |
| --- | ---: | ---: | ---: |
| Disabled / excluded actor | 1 | 1 | 0.2 |
| Unlisted HBR enemy compatibility | 1 | 700 | 200 |
| Original table-monster variant | original HP + DP | 900 × original HP | 900 × original DP |
| SmallHopper opening calibration | 14 | 8000 | 4800 |
| Narara Shiba exception | 1 | 1000 | 200 |

Table-monster HP uses `floor(native HP × 900 × original HP / (original HP +
original DP))`. At default settings, fresh DP is `floor(adapted maximum HP ×
original DP / original HP)`. DP follows the already-rounded maximum HP, so the
sum of the two pools can have a rounding difference from native HP ×900.
The original absolute HP/DP totals are not assigned directly to the monsters.

`dp-hp-share.json` is an independent copy of the approved exact-integer source
rows. The fixture selects each actual production move-table label and compares
its profile with those integers; it does not use rounded six-digit shares.
MarionetteKiller, TriClaw and BigotryGate have different shares between variants.
Variants with equal shares are still checked separately.

SmallHopper is a separate reference with total 12,800 at native HP 14. Its ordinary
native 12–16 range now becomes HP 6,857–9,142 / DP 4,114–5,485; the high-difficulty
native 13–17 range becomes HP 7,428–9,714 / DP 4,456–5,828. Native 14 is HP 8,000 / DP 4,800.
GoldHopper remains fixed HP 1 / DP 10, independent of numeric mode, settings,
ascension or multiplayer. Unlisted native monsters and special bosses retain
their compatibility profiles; Narara still uses HP ×1000 and its frozen DP layers.
Allies, pets and phantoms are excluded from enemy resource redistribution.

## Settings, phases and precision

Native random HP selection and multiplayer HP rounding run before resource
adaptation. HP settings and per-act growth multiply the profile once. The normal
shell setting is relative to its default 20%: setting 40% doubles the profile DP
before independent DP act growth and ascension enhancement. A zero-DP setting
still gives zero normal shell. Full-resource simulation shields and Narara's
intrinsic frozen-layer lifecycle retain their existing special rules.

Room-entry scaling is idempotent. A phase replacement supplies a fresh raw HP
value, so it is converted once rather than multiplying the already-adapted HP.
A later maximum-HP change affects newly calculated maximum DP, but never
rewrites the saved current-DP ledger or an already-frozen Narara layer cap.

HP and ordinary DP multiply profile numerators and settings before the final
division/floor. This preserves exact boundaries such as SmallHopper 14 at 150% HP
being 12,000, Knocker 42 at 150% HP in the second act with 50% per-act HP growth
being 60,750, and DiamondEyeball 320 producing 270,928 DP with the high-difficulty shell multiplier.
No epsilon or tolerance is used. Normal values use decimal arithmetic; an
overflowing intermediate uses exact integer fractions and saturates at the
final resource cap 2^64. Maximum saved HP/current HP strings remain unchanged.

## Running the checks

Build the production candidate separately with `SkipModOutputCopy=true`. Build
this fixture with a matching native profile and an output directory specific to
that profile:

```powershell
dotnet build validation/hbr-enemy-resources/Checks.csproj -c Release `
  -p:ModAssemblyPath=<immutable-candidate.dll> `
  -p:NativeAssemblyPath=<profile>/sts2.dll `
  -p:NativeRitsuAssemblyPath=<profile>/STS2-RitsuLib.dll `
  -p:GodotSharpAssemblyPath=<GodotSharp.dll> -o <fixture-output>
dotnet <fixture-output>/Checks.dll <immutable-candidate.dll> <expected-SHA256> <profile> <GodotSharp.dll> <report.json>
```

The optional fifth runtime argument selects the report path; otherwise the
report is written beside the fixture executable. The bootstrap verifies the
candidate SHA and each file hash in `profile-manifest.json` before loading the
assemblies. Build and run stable/beta in sequence when reusing this project.

`report.json` records success or failure, all passed cases, errors, candidate
SHA and native profile. `resource-profile-values.json` records all 42 default
before/after rows plus actual bounds, configuration/multiplayer/phase,
exclusion, high-difficulty, saved-string and overflow evidence. The complete
human-readable table is in [the encounter documentation](../../docs/hbr-act-rosters.md#enemy-resource-table-20261114).
Current integration reports live under
`outputs/31b-monsters-20261114/{stable,beta}/resource-report.json`; their hashes
identify the exact candidate tested.

## Validation boundary

The suite executes native Creature entry, native HP sampling and duplicate
avoidance, multiplayer HP scaling, actual TestSubject phase replacement,
production health/phase helpers, ordinary and special DP calculators, Narara's
initialization gate, and exact saved-resource reads. It checks both native HP
bounds for every original variant, both difficulty ranges, one/two/four players,
configuration and act growth, repeated entry, ally/pet/phantom/disabled
exclusions, and final resource saturation.

HBR enablement, ascension selection, unrelated monster entry setup and phantom
difficulty suppression are controlled leaves. Native healing notifications are
replaced with native internal healing. No Godot process, game save/config IO,
UI rendering, full combat/replay, live multiplayer transport, summon animation,
packaging or upload runs. Passing these checks validates the affected native
resource paths, not a complete in-game playthrough or the monsters' skill AI.

The optional sixth bootstrap argument `--isolate-telemetry` copies the raw native assembly and suppresses only its verified Sentry module-initializer call. The raw compile/profile assembly stays unchanged; reports include its SHA and the proof that all remaining bytes match. This is a runtime-host isolation boundary, not a gameplay patch.
