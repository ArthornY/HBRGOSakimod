# Native Weak / Vulnerable percentage effects

**Historical contract:** this fixture records the earlier native-power
replacement implementation. The current implementation preserves vanilla
Weak/Vulnerable and uses independent HBR attack/defense debuffs. Use
[`../hbr-separated-debuffs`](../hbr-separated-debuffs/README.md) for the current
regression checks. The old totals and reports below apply only to their recorded
candidate, not the current source.

Build once per native runtime; the beta fixture compiles against beta game/Ritsu
references and enables the native `CardPlay` hook signatures:

```powershell
dotnet build validation/hbr-percent-debuffs/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-percent-debuffs/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<dependency profile>' '<GodotSharp.dll>' '<report.json>'
```

For beta, add `-p:BetaNativeChecks=true`, `-p:NativeAssemblyPath=<beta sts2.dll>`,
and `-p:NativeRitsuAssemblyPath=<beta monolithic Ritsu compile reference>` when building. Like the production project, this fixture retains the existing monolithic compile reference; its runtime profile uses the current five Ritsu assemblies. Keep its
`OutputPath` / `IntermediateOutputPath` separate from stable. Both runs must use
the same candidate hash. Existing profile manifests are checked before loading.

The fixture reuses the real Fragile native combat setup without running its old
test suite. Production patches and native generic/non-generic `PowerCmd` calls,
Artifact, independent counters, expiry, shared damage hooks, Double card play,
native save-property bags with JSON and actual packet writer/reader roundtrips,
and mutable clone methods execute. Native Weak and
Vulnerable types remain intact. A controlled native StringName/logging boundary
and hook-listener enumeration replace engine-only leaves. Profile beta telemetry
initialization is isolated in the already verified output-only test copy.

Stable registers the existing declared Fragile saved properties with the native
type cache, as normal mod registration does. Beta runs its real `AssemblyInfo`
and `ModelIdSerializationCache` initialization, then its debug custom-type cache
registration. No save method, packet-name map, or packet bit width is faked.

Description checks execute native `LocManager`/`LocTable`/SmartFormat, actual card
upgrade phases, target scopes, and the production description patches. Set
`HBR_NATIVE_LOCALIZATION_DIRECTORY` to the provided native Chinese table snapshot
directory (default: the local `D:/stsmod/sts/localization/zhs` reference). This
checks text formatting, not beta rendering or physical input. A separate fixture
run is not a complete multiplayer synchronization test.

`MechanicChanges.cs` additionally executes real Replay card plays across all
three percentage debuffs (one to two to four to eight instances), ordinary
application limits, Artifact and Narara prevention, and callback changes/removal
after the pre-play snapshot. It checks Fragile's actual Double card behavior,
Sea Breeze's defense-down base bonus, native relic/Cruelty/Scars ordering,
copied/doubled/restored snapshots, and the Contest cannon's virtual view.

`GhostShotChanges.cs` uses a real Ghost power and Tezuka move identity through
the complete native damage hook and actual-damage wrapper. Independent enemy
and phantom arithmetic checks the percentage-derived skill coefficient before
attribute conversion, separate durations/raw counters, Scars and Sea Breeze
snapshots, Contest views, and resolved/absolute damage bypasses. Its observation
patch counts real coefficient evaluations without changing their arguments or
results. Legacy formulas and the formatted 6‰ description are checked too.

The frozen candidate `6D0700B5D301138E8EEDD30A627CEB80CD7F481C1FE38EA7E785887974B0E9E4`
passes **532 checks on stable and 532 on beta**, using the same DLL. Reports are
local at `outputs/ghost-strong-shot/validation/{stable,beta}/report.json`.

It does not render Godot UI, boot a game, load a complete save, or validate
network/save-file migration. Source-only checks do not package or upload.
