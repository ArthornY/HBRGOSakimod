# Unified damage amplification

This focused fixture executes the canonical `HotaruDamageAmplificationPower`
through actual native `PowerCmd.Apply` and `ModifyAmount`, and installs the
production exact-percent and mixed-model-family stacking patches. The old
`PhantomDamageInheritancePower` ID remains a compatibility subtype, while current
sources create the canonical power.

It checks exact fractional stacking and native integer proxies, old/new ID
stacking in either order, full native damage preview and actual wrapper,
summoner/phantom isolation, outgoing inheritance summary, actual Transcend Self
`OnPlay`, old JSON property migration, real registered `SavedProperties` JSON and
packet roundtrips, independent cloning and attachment, large-value saturation,
and exact combat snapshot identity/index preservation.

```powershell
dotnet build validation/hbr-merged-damage-buff/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-merged-damage-buff/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<dependency profile>' '<GodotSharp.dll>' '<report.json>'
```

For beta, build against beta `NativeAssemblyPath` and
`NativeRitsuAssemblyPath`, set `BetaNativeChecks=true`, and use separate build
output/intermediate directories. Both runs must load the same frozen candidate
hash. The bootstrap validates profile manifests before loading assemblies.

The merge-only candidate
`209B5E9ECC779B0DDACC1D4B52C58CBB7EE6DD28E1C3D0023A0ED993E007B8F9`
passes **107 checks on stable and 107 on beta**. Reports are at
`outputs/merged-damage-buff/validation/{stable,beta}/report.json`.

The final combined damage-amplification and combo candidate
`1AFC9B1BA3956A6D72898AC076DEA47216F9EF649DBA753D38EFE6BA10AB8EDD`
also passes **107 checks on stable and 107 on beta**, loading the same
stable-built candidate DLL on both runtimes. Its reports are kept separately at
`outputs/hbr-combo/merge-validation/{stable,beta}/report.json` so that the
merge-only evidence remains available.

The shared Fragile fixture supplies combat setup and controlled leaves for Godot
StringName/logging and hook listener enumeration. These managed checks do not
start the game, render Godot UI, load a complete save file, or run a multiplayer
session. No package or upload is produced.
