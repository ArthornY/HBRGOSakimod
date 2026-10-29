# Enemy skill and actor regression

Build the mod with `SkipModOutputCopy=true`, then build this fixture with
`dotnet build validation/hbr-enemy-skills/Checks.csproj -c Release`. Supply
`-p:GodotSharpAssemblyPath=<GodotSharp.dll path>` if its NuGet root is unavailable.
Run the resulting `Checks.dll` with the same four arguments as the sibling
enemy-resource fixture: built mod DLL, expected SHA256, runtime dependency
directory containing `sts2.dll`, and matching GodotSharp DLL path. The entry
point checks the hash before loading the mod; `report.json` records that hash
and all assertion names.

The fixture uses actual saved manual progress, native Player/Creature models
and stat accessors. It verifies that enemies do not expose player six stats,
that their map parameter E is independent of player stat allocations, and that
players, phantoms and cannons retain their existing inheritance. It exercises
the three explicitly assigned RedCrimsonBoss ray/beam moves and the fixed
vitality fallback, including an additional living ally and an unrelated monster
with the same move name. Endpoint cases check the incoming maximum and 1 floor;
legacy mode, Unpowered and the STS enemy block support bridge have separate cases.

Enemy growth uses E150 at act index0/local floor1, +60 per act, +6 for
each additional local floor, and +30 for elite/+50 for boss rooms. Both a
native SmallHopper and an ordinary actor are checked at explicit early/later
floor, act, elite and boss points. These cases verify independent incoming,
outgoing and block formulas through the real native preview Hook and production
actual-damage wrapper. Native encounter models supply the room classifications;
native MapCoord, ActFloor and history collections exercise coordinate precedence,
absent-coordinate/native-ActFloor fallback, floor/act clamps and missing encounter/current
room fallback. Player six-stat allocations are verified to change the actual
matching stat while leaving E independent; absent run/combat falls back to150.
Native coordinate row0 is floor1; row1 is floor2/E156. If Neow occupies row0,
the first monster at row1 therefore starts at E156. The explicit row0 Hopper
calibration below remains an opening numeric benchmark, not a forced first-fight
exception. Total history length does not substitute for native ActFloor, because
act lengths and multiplayer progression can differ.

Preview calculations enter actual native `Hook.ModifyDamage`, then the
production `ModifyDamageInternal` replacement and complete common pipeline.
Actual calculations enter the production StrongestShield wrapper, which marks
the native Hook call as actual, and reach the same pipeline. This checks numeric
intent/actual agreement for each selected move. Rendering, displayed rounding,
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
Defend100INT grants500 block against542.857... incoming damage. This defender
has no DP: native DamageBlockInternal resolves block first, and the actual
native LoseHpInternal truncates the remaining42.857... to42 HP lost. This
fixture does not attribute that truncation to a CreatureCmd cast. These profile numbers
are mod design values, not original HBR Hopper data.

The single-coefficient cases execute the real native StrikeIronclad and mod
StrikeHbrgoSaki `OnUpgrade` mutations. Raw6 becomes9 (the existing100% becomes150%),
then native preview and the production actual wrapper both produce5785.71 at
attribute100/E150. Equal-stat hero, main phantom and cannon actors share3857.14
for raw6. With STR400/DEX100, HP reference300 gives12428.57 while active DP uses
reference200 and gives8142.85. STR500/DEX500 gives21000 beyond the old endpoint,
so these cases distinguish the new linear rule from the old overflow curve.

A real Millsage state supplies the cannon's default50% Soul Power. The fixture
constructs a matching pending intent and enters the production `Begin`/`Close`
command scope around the actual hook: the owner-sourced result1928.57 confirms
that Soul Power applies once. Real cannon `PreviewDamage` returns its display
floor1928 without consuming RNG. A production critical scope is given a fixed
replay sample0; preview stays3857.14, while actual damage first lowers E150 to100
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
