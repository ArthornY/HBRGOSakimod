# Enemy resource compatibility checks

The HBR enemy reference stores `enemy_stat`, `max_hp` and `max_dp` separately.
`HbrEnemyResourceProfile` contains explicit STS adaptation coefficients; it is
not a reconstruction of each original enemy's resource table. In particular,
HP and DP are not derived from player VIT or SPR.

The current compatibility profile keeps the test branch's existing resource
values. Its HP coefficient is 1000, and its independently named DP coefficient
is 1000. The STS DP reference still follows current exact HP, including phase
replacement and later maximum-HP changes. Separate profile coefficients expose
the resource conversion boundary without changing that established behavior.
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

The 72 checks execute native Creature entry, both production phase transpilers
at installation, actual TestSubject phase replacement, the production HP helper,
ordinary/special DP calculators, Narara's initialization gate and exact saved
resource reads. HBR enablement, monster entry setup and phantom difficulty
suppression are controlled leaves. Native healing notifications are replaced
with native internal healing. No Godot process, actual game save/config IO or
engine combat runs. This does not verify rendering, full combat load/replay,
summon animation ordering or individual original HBR monster skill/resource data.
