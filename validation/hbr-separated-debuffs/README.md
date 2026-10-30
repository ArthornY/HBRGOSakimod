# Independent HBR debuffs and untouched vanilla powers

This fixture replaces the native-replacement assumptions in the historical
`hbr-percent-debuffs` fixture. It verifies separate `HbrAttackDownPower` and
`HbrDefenseDownPower` models while native Weak/Vulnerable retain their stacking,
damage formulas, card descriptions, relic interactions and native application
boundary, including while HBR numerical mode is enabled.

The independent attack/defense debuffs use the final requested
`15% × 1.25^(modifiers−1)` basis; Fragile retains its `30%` basis. Independent
formula vectors cover the INT/LUCK weights and snapshot precision, and actual
Double verifies Fragile's unchanged basis alongside the two new powers.

```powershell
dotnet build validation/hbr-separated-debuffs/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-separated-debuffs/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<dependency profile>' '<GodotSharp.dll>' '<report.json>'
```

For beta, build the fixture against beta `NativeAssemblyPath` and
`NativeRitsuAssemblyPath` references, with `BetaNativeChecks=true` and separate
`OutputPath`/`IntermediateOutputPath`. Both runtime runs must load the same frozen
candidate SHA256. Profile manifests are validated before assembly loading.

The fixture executes actual native `PowerCmd` application, original power damage
methods, shared Hook damage calculations, Artifact, owner turn-end duration,
independent percent slots, real Soul Intimidation/Double/Replay `OnPlay`, actual
SmartFormat text, intrinsic saved-property JSON and packet roundtrips, and native
mutable cloning. Native Weak/Vulnerable coexistence is checked separately from
the new percent calculations and effects. Direct `PowerCmd.Apply<WeakPower>` is
never remapped even when its source is a mod card; migrated mod effects must use
the explicit adapter.

The frozen candidate
`EAF31A28793A7C0BF2C77E01042ECD5044F3E84525A7BEEAE9BFBDDC17D564E9`
passes **122 checks on stable and 122 on beta**, using the same stable-built
candidate DLL. Reports are at
`outputs/separate-debuffs/validation/{stable,beta}/report.json`.

The save checks use actual registered inherited properties and actual
`SavedProperties.FillInternal`, JSON, `PacketWriter` and `PacketReader`.
They caught the inherited-private-setter reflection issue before the final
candidate; the protected setters now restore exact snapshots on both runtimes.

The shared Fragile fixture supplies combat setup and controlled leaves for Godot
StringName/logging and hook-listener enumeration. Beta uses its existing isolated
test telemetry copy. These managed checks do not start the game, render Godot UI,
load a complete save file, or run a multiplayer session.
