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
Hotaru's explicit currentHP1 exception with maximum3000 and full6000DP,
Deflector's existing2DP/HP and phantom20percent DP ratios,
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
not load a full gameplay save. The real Harmony guard is installed on native
AncientEventModel.BeforeEventStarted and the Hotaru/Neow entry is invoked: it
skips the native HP reset/heal, preserving1/3000 and6000DP. Repeated entry
preserves later73HP. Direct prefix controls cover pre-finished and non-Hotaru
branches without executing their native event bodies. The complete Neow event
synchronizer and selection UI are not executed. This is a managed model
regression, not a Godot/in-game test. No real
save/config IO, packaging, network transport or Steam upload occurs.

Hotaru's current HP is deliberately not multiplied by the general100 resource
scale: real `InitializeStartingHealth` must set1, a repeated call must retain73,
and `PreserveLoadedHealth` followed by initialization must retain stored7.
Maximum HP3000 and DP6000 keep their existing scaled values. The obsolete
`InitialScaledCurrentHp` property is not part of the fixture contract.

For an explicit local GodotSharp reference, the checked invocation is:

```powershell
dotnet build validation/hbr-resource-scale/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:GodotSharpAssemblyPath=C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hbr-resource-scale/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll
```

The pre-correction DLL is intentionally red: actual Hotaru initialization sets100
rather than1. Local evidence is preserved in
`outputs/hotaru-initial-hp/baseline-stable.json`; the corrected HP-only candidate
passes81 checks in `outputs/hotaru-initial-hp/candidate-stable.json`. These are
managed regression reports rather than gameplay-save or rendered-game evidence.

The combined HP and enemy-recovery final candidate also passes81 checks in
`outputs/hotaru-initial-hp/candidate-final-stable.json` with SHA256
`2CA73801C76A4E78C621A0F0858AFF70132EE724AA16A2F5ECE5B35ECB9FDFB9`.
This rerun uses the same managed model and guarded-ancient-entry boundaries; it
does not establish a complete Neow event flow or an in-game test.
