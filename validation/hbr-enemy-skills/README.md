# Enemy skill and actor regression

## Current focused resource checks (2026-11-14)

The SmallHopper calibration now uses native14 -> HP8000 / DP4800 (total12800 unchanged, original37.5% DP share). The focused `--hopper-calibration-only` entry exercises native HP/DP spending and the production damage pipeline against an independently calculated hit trajectory, with current skill-coefficient destruction gain6 per complete single-hit attack. It also runs the current opening attack/Defend checks. Both stable0.107.1 and beta0.113.0 pass257 checks in the development candidates recorded under `outputs/31b-monsters-20261114/{stable,beta}/hopper-calibration-report.json`; the final report hashes, not this historical sentence, identify the tested DLLs.

This is deliberately a focused entry, not a green claim for the older complete actor suite. The complete suite's untouched enemy support-block assertion expects3000 where the current shared formula returns2424. The frozen pre38 candidate `C70FEEBC...` reproduces the same failure after18 checks; baseline proof is `outputs/31b-monsters-20261114/enemy-skills-pre38-baseline.json`. The resource work does not change that support formula, so this separate stale assertion has not been silently rewritten or counted as passing.

`--hopper-calibration-only` installs the real shared arithmetic and DP hooks and runs its native resource driver; it does not install the older synthetic canonical-damage terminal, which this focused driver never calls. Preview and RNG-counter inspection support both pinned native APIs. It is managed validation, without Godot, complete room gameplay or live multiplayer.

## Historical full-suite records

Current candidate `100E63797656C1E459980CB0ECEA057DF467C5C29B1944191BDE13ACD09B9C84` passes 650 checks in the stable actor/map/scope/skill segment with the obsolete Hopper card-budget calibration excluded. The focused act-carryover entry passes 161 checks on both stable and beta v0.111.0. These are managed native-model checks, not a complete game run or full-suite beta result.

| Report under `outputs/enemy-parameter-act-carryover/` | Result |
| --- | --- |
| `candidate-stable-segment.json` | 650 pass; existing actor/map/scope/single-coefficient checks plus new carryover and real native Hook checks, Hopper calibration excluded |
| `candidate-focused-stable.json` | 161 pass; native history/getters/current-act setter |
| `candidate-beta.json` | 161 pass; same focused checks against verified beta runtime |
| `baseline-stable.json` | Expected carryover failure: prior floor17 is E246, then old next-act E210 instead of E306 |
| `baseline-existing-suite.json` | Existing Hopper calibration fails after 484 prior checks |
| `candidate-existing-suite-failure.json` | Same existing Hopper failure after the same 484 prior checks, with new carryover entry explicitly skipped for comparison |

The frozen pre-change DLL is `outputs/enemy-parameter-act-carryover/baseline/HBRGOSakiMod.dll`, SHA256 `8026D7C86D6D7DB3E59A3B3B138FEFF48E1519860439751D2D36AC79FDE18C94`. Both the baseline and candidate fail the unchanged historical first Hopper HP12/four-Strike assertion with HP0 and `DestructionRatePower.Amount = 19`; the historical calibration still assumes a fixed +2 destruction gain, while production already uses skill-coefficient growth. This carryover change does not alter that production mechanism or rewrite the old budget expectations.

Build and run the green stable segment from this checkout:

```powershell
$candidate = (Resolve-Path outputs/enemy-parameter-act-carryover/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/hbr-enemy-skills/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:GodotSharpAssemblyPath=$godot"
dotnet validation/hbr-enemy-skills/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/enemy-parameter-act-carryover/candidate-stable-segment.json --skip-hopper-calibration
```

Use the same stable-compiled fixture binary for the focused native-history checks. The bootstrap calls `RunActCarryover` directly, so it never JITs old Counter/preview APIs in the legacy suite when loading beta:

```powershell
dotnet validation/hbr-enemy-skills/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/enemy-parameter-act-carryover/candidate-focused-stable.json --act-carryover
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet validation/hbr-enemy-skills/bin/Release/net9.0/Checks.dll $candidate $digest $profile $godot outputs/enemy-parameter-act-carryover/candidate-beta.json --act-carryover --isolate-telemetry
```

Without skip flags, the old full suite remains attempted and reports its existing Hopper failure. `--skip-act-carryover` excludes only the new carryover cases for the baseline/candidate old-suite comparison; it does not mask or modify the Hopper assertion. The optional fifth argument selects the report path; the original four-argument invocation still writes `report.json` alongside the executable. The local bootstrap replaces only this project's old linked bootstrap. It verifies candidate/profile hashes and links the existing strict telemetry isolator unchanged. Beta source SHA256 `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9` becomes output-local telemetry-only copy `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`; the proof verifies only Sentry's exact five-byte initialization call changed. Original runtime/profile assemblies are not edited.

The fixture uses actual saved manual progress, native Player/Creature models
and stat accessors. It verifies that enemies do not expose player six stats,
that their map parameter E is independent of player stat allocations, and that
players, phantoms and cannons retain their existing inheritance. It exercises
the three explicitly assigned RedCrimsonBoss ray/beam moves and the fixed
vitality fallback, including an additional living ally and an unrelated monster
with the same move name. Endpoint cases check the incoming maximum and 1 floor;
legacy mode, Unpowered and the STS enemy block support bridge have separate cases.

Enemy E is `150 + 60*act + 6*(sum(max(0,completedActHistory.Count-1)) + localFloor-1) + currentRoomBonus`. The current coordinate row+1 takes precedence over native ActFloor; a missing coordinate uses ActFloor clamped to at least1. Completed buckets are read directly from existing native `RunState.MapPointHistory`, with empty/null/missing buckets contributing zero. Current/future history is not added to the local floor again. There is no assumed17-floor act length, saved cache or cumulative write on reads.

Two different completed-act length layouts, 17/22/9 and 8/13/19, exercise act1→2→3→4. The first layout gives first-floor E306/492/600 in subsequent acts; the second gives252/384/552. Cases include a current history bucket not yet present, an empty current bucket before history append, coordinate precedence over ActFloor99, absent coordinates with inconsistent current history counts, repeated reads retaining the identical history reference and counts, null/missing histories, and ordinary/elite/boss classification. Only the current room's +30/+50 applies, and a prior enemy's temporary calamity reduction does not enter progress.

One17-entry scenario executes the real native `RunState.CurrentActIndex = 1` setter. It clears the current coordinate and resets ActFloor to0 while retaining the completed history reference and17 node entries; the production getter then returns E306 from E246 plus the act60 increment. The other scenarios install native collection structures and entry instances directly. They do not execute AppendNode, complete room transitions, event/encounter lifecycle or save deserialization. Native history counts map-node entries, including the row0 Ancient; extra Rooms within one node do not add floor growth. Stable segment checks additionally run carried E through native incoming preview, the production actual wrapper and the support-block Hook. Beta focused checks cover only the native history/getter/setter paths.

Existing per-floor/room/actor tests continue to use independently derived incoming/outgoing/block formulas. Their synthetic total-history disagreement now belongs to the current act's bucket, preserving the coordinate/ActFloor precedence test without accidentally creating previous-act growth. Player stat allocations remain independent of E; absent run/combat falls back to150. Native row0 is floor1, row1 floor2/E156. The row0 Hopper case below is a historical numeric benchmark, not a forced first-fight exception.
Preview calculations enter actual native `Hook.ModifyDamage`, then the
production `ModifyDamageInternal` replacement and complete common pipeline.
Actual calculations enter the production StrongestShield wrapper, which marks
the native Hook call as actual, and reach the same pipeline. This checks numeric
intent/actual agreement for each selected move. HBR native preview and actual
outputs are floored once, after all multipliers and damage caps. Independent
incoming-reference calculations and direct skill-adapter expectations retain
their fractional precision; legacy damage keeps its native fractions. Rendering,
enemy AI execution are outside the fixture. The Hopper cases below additionally
exercise native block/HP mutation and the production DP absorption boundary.

The canonical `CreatureCmd.Damage` method has the real production critical
scope prefix/postfix/finalizer installed. Its terminal HP work is replaced with
an asynchronous driver that consumes targets once and calls the production
actual wrapper. A separate Harmony owner explicitly orders this controlled
terminal after the production prefix. Tests verify no early target enumeration,
no enemy critical scope/RNG usage, lazy player sampling, sample reuse, nested
command isolation, and cleanup after a controlled fault inside the actual
wrapper. The native RNG itself is used; no specific lucky/unlucky roll is assumed.

Actor/scope regressions use empty controlled hook listeners. Hopper calibration
uses live shell, destruction and native Vulnerable listeners. Logging and
Godot StringName construction are controlled leaves. Thus these checks do not
validate every modifier combination, UI preview context, a complete native
damage task, replay/save restoration or original per-enemy skill data. No Godot
process, real game save/config IO or packaging is performed.

`HopperOpeningAttack` (2026-11-08) runs in the green segment, outside the excluded calibration: since the fourth move-table batch the opening SmallHopper follows the original CrestHopperSmall table, and the check reads the production table and move values (normal attack 3, combo 1 x8). At the opening E150 the normal attack floors to 407, which a native Defend's 500 block takes whole (93 left); each combo hit floors to 135, and the eight hits (1080) leave 580 HP loss after the 500 block. Candidate `766663D0372B2DFFF996493BED800AD42B70452E7FF1E0719FE3C84792CA6CB3` passes 659 checks in the stable segment (650 before plus these 9). The report's `hopperOpeningAttack` holds the numbers.

The following Hopper budget section documents the original historical calibration. Its fixed-destruction expectations are stale, and it is excluded from the green650-check segment; both frozen baseline and candidate reproduce its same failure as reported above.

The Hopper calibration reads the installed native unupgraded StrikeIronclad,
Bash and DefendIronclad variables and costs (6/1, 8/2 plus Vulnerable2, and5/1).
It uses the actual saved manual's level-one six stats100, opening Hopper E150,
and its mod resource profile native14 -> HP9800/DP3000. HP is native HP times700;
DP independently uses floor(native HP /14 times3000). Every normal native
HP roll12-16 requires exactly four Strikes; Bash followed by two Strikes kills
in three cards costing four energy. All these cases disable natural criticals.
With a normal three-energy turn the four-energy sequences span turns (e.g.
Bash+Strike, then Strike while Vulnerable remains); these are card/energy
budgets without draw, turn expiry or energy payment simulation. The shell hook executes and its old
0.75 reduction is ignored only in HBR mode. The production DP absorber discards
the breaking hit's overflow, then the real receipt adds2 destruction points;
subsequent damage uses real destruction and native Vulnerable multipliers.

The shell's break attachment/removal/stun is a controlled lifecycle leaf; its
absorption, exact DP writes, receipts, fixed gain and native HP mutations remain
production code. Bash damage precedes Vulnerable attachment, matching the native
OnPlay IL in the installed sts2.dll (A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52).
The report records the actual mod and native game DLL hashes. Difficulty8's
maximum native HP17 now has11900 HP and3642 DP; four Strikes or Bash plus two
Strikes also kill that case. Later difficulty DP/attack changes are not covered.
Before 2026-11-08 the calibration ended with Defend100INT granting500 block
against the old 4-damage attack's542 final incoming damage (42 HP loss); that
part moved to `HopperOpeningAttack` above with the new moves. These profile
numbers are mod design values, not original HBR Hopper data.

The single-coefficient cases execute the real native StrikeIronclad and mod
StrikeHbrgoSaki `OnUpgrade` mutations. Raw6 becomes9 (the existing100% becomes150%),
then native preview and the production actual wrapper both produce5785 at
attribute100/E150, while the direct adapter retains5785.71. Equal-stat hero,
main phantom and cannon actors retain3857.14 in the raw6 adapter and return3857
at the shared output. With STR400/DEX100, HP reference300 gives12428.57 in the
adapter while active DP uses reference200 and gives8142.85; the phantom's actual
DP hit is8142. STR500/DEX500 gives21000 beyond the old endpoint,
so these cases distinguish the new linear rule from the old overflow curve.

A real Millsage state supplies the cannon's default50% Soul Power. The fixture
constructs a matching pending intent and enters the production `Begin`/`Close`
command scope around the actual hook: the owner-sourced result1928 confirms
that Soul Power applies once, before final flooring. Real cannon `PreviewDamage`
also returns1928 without consuming RNG. A production critical scope is given a fixed
replay sample0; preview stays3857, while actual damage first lowers E150 to100
and then applies1.5, giving9000, or4500 with one cannon Soul factor. These are
native-model/hook and production-scope checks, not complete card-play or cannon
phase execution.

Hero, phantom and cannon actors' native block hooks are checked at INT100/200
(raw5 gives500/1000), with Unpowered amounts unchanged. Fixed resource recovery
still gives500 for raw5 at INT200. A controlled resolved-gain scope verifies
that actual-damage-derived cannon block skips the coefficient/stat bridge, and
the production absolute-base context likewise preserves a resolved250 attack.
These scope fixtures intentionally isolate the unit-conversion boundary; they
do not simulate the asynchronous block command or all later cannon modifiers.
Enemy support block retains its separate0.8..1.2/T300 bridge, and disabled mode
retains raw6 attack/raw5 block.
