# Enemy resource compatibility checks

The HBR enemy reference stores `enemy_stat`, `max_hp` and `max_dp` separately.
`HbrEnemyResourceProfile` contains explicit STS adaptation coefficients; it is
not a reconstruction of each original enemy's resource table. In particular,
HP and DP are not derived from player VIT or SPR.

Profiles store the native reference HP, adapted reference HP and independent
reference DP (at the default shell setting) as three explicit fields:

| Profile | Native reference HP | Reference HP | Reference DP |
| --- | ---: | ---: | ---: |
| Disabled/legacy | 1 | 1 | 0.2 |
| Ordinary HBR enemy compatibility | 1 | 700 | 200 |
| SmallHopper opening calibration | 14 | 9800 | 3000 |
| Narara Shiba exception | 1 | 1000 | 200 |

All HBR-enabled enemies now scale native HP by 700, except Narara Shiba, whose
coefficient remains 1000. The same entry adapter covers normal, elite, boss,
native and custom enemies; it does not assign them all the same absolute HP.
Players, allies, pets and phantoms remain excluded. A repeated room-entry
notification never applies the coefficient twice.

SmallHopper's 14 is the midpoint of its ordinary native 12–16 HP range. The
native roll, difficulty range, multiplayer HP scaling/rounding, configurable HP
multiplier and act growth all run before or alongside the adapted HP coefficient;
the profile does not assign every Hopper a fixed 9800 HP. At default settings,
raw 12–16 HP becomes 8400–11200 HP and 2571–3428 DP. High-difficulty raw 17 becomes
11900 HP/3642 DP. These are local encounter design values, not original HBR rows.

This is an HP change, not a uniform DP-ratio change. Ordinary enemies keep an
independent reference of 200 DP per native HP; SmallHopper keeps its 3000 DP at
the 14-native-HP reference. The fixture explicitly checks native 1 -> 700 HP /
200 DP, native 2 -> 1400 HP / 400 DP, native 101 -> 70700 HP / 20200 DP, and
SmallHopper native 14 -> 9800 HP / 3000 DP. Reference conversion multiplies
before dividing so a rounded repeating ratio cannot incorrectly floor 400 to
399. Narara's intrinsic full-layer DP remains independent of the ordinary shell.

The STS DP reference still follows current exact HP, including phase replacement
and later maximum-HP changes. SmallHopper's reference DP therefore gives a fresh
maximum of 3030 after its adapted maximum HP increases from 9800 to 9900;
existing current DP is not rescaled. The configurable shell percentage acts
relative to its default 20% setting, so doubling that setting doubles reference
DP without editing the independent profile value. Wider reference intermediates
remain unclamped until the final resource calculation, including at 2^64 HP.
Normal shell percentage, full-resource simulation shields, Narara's initial
frozen layer maximum, ascension enhancement and configured per-act growth keep
their prior rules. No enemy is matched to an original HBR row by name.

Build the mod first with `SkipModOutputCopy=true`. Build this fixture with
`dotnet build validation/hbr-enemy-resources/Checks.csproj -c Release`, supplying
`-p:GodotSharpAssemblyPath=<GodotSharp.dll path>` if the NuGet root is unavailable.
Run its `Checks.dll` with four positional arguments: the built mod DLL, its
expected SHA256, the runtime dependency directory containing `sts2.dll`, and
the matching GodotSharp DLL path. A successful run writes `report.json` beside
the fixture executable, including the exact tested mod hash and case names.

The checks execute native Creature entry, both production phase transpilers
at installation, actual TestSubject phase replacement, the production HP helper,
ordinary/special DP calculators, Narara's initialization gate and exact saved
resource reads. Explicit source/output pairs cover native normal LivingFog,
elite Byrdonis and boss Queen, as well as custom normal GoldHopper, elite
AbyssKnocker and bosses RedCrimson/Narara. These category representatives use
controlled native HP inputs rather than asserting their actual encounter HP
tables. Custom elite cases also exercise multiplayer, fresh-entry and raw-phase
scaling. SmallHopper cases execute native HP sampling, same-seed
replay, same-side duplicate avoidance and multiplayer HP scaling/rounding;
they cover both difficulty ranges and independent HP/DP settings with act growth.
HBR enablement, monster entry setup, ascension selection and phantom difficulty
suppression are controlled leaves. Native healing notifications are replaced
with native internal healing. No Godot process, actual game save/config IO or
engine combat runs. This does not verify rendering, full combat load/replay,
summon animation ordering or individual original HBR monster skill/resource data.
