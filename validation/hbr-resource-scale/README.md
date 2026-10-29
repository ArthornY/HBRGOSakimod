# Player HP and fixed HP/DP scale regression

Build the mod with `SkipModOutputCopy=true`, then build this fixture with
`dotnet build validation/hbr-resource-scale/Checks.csproj -c Release`.
If needed, pass `-p:GodotSharpAssemblyPath=<GodotSharp.dll path>`.
Run its `Checks.dll` with four arguments: candidate mod DLL, expected SHA256,
runtime dependency directory containing `sts2.dll`, and matching GodotSharp DLL.
The shared bootstrap checks the supplied hash before loading the candidate;
`bin/Release/net9.0/report.json` records both candidate and native DLL hashes.

These cases exercise real mutable character/player/creature/relic models and
native internal HP setters. They cover the fresh original35/10/30 HP baselines,
Hotaru currentHP100, Deflector's existing2DP/HP and phantom20percent DP ratios,
once-only native teammate and phantom initialization, real SavedProperties
relic round trips, legacy mode, Hotaru fresh/loaded guards, fixed signed/fractional
amounts and saturation, fixed Recovery6=600 independent of Intelligence, and
actual derived/percentage resource APIs that must not scale twice.

Phantom growth cases keep roster and fusion metadata in original units, apply
two successive leaf-fusion metadata effects, check actual resource differences
and owner-aware descriptions, update off-field stored health twice, and run the
production temporary-level bonus refresh/expiry ledger through repeated calls.
The temporary-level cases control the terminal GainMaxHp/SetMaxHp commands,
DP refill notification, and UI refresh leaves; they exercise native HP setters
and production bonus/expiry accounting, rather than full command hooks.

Logging, Godot StringName construction and relic Flash are controlled leaves.
The save cases serialize relic progress attached to existing stored HP; they do
not load a full gameplay save. Neow's event synchronizer and selection UI are not
executed. This is a managed model regression, not a Godot/in-game test. No real
save/config IO, packaging, network transport or Steam upload occurs.
