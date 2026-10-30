# HBR Calamity checks

This focused fixture loads an exact candidate DLL by SHA256 and reuses the
native setup from `hbr-fragile`; it does not rerun that fixture's full suite.
It exercises real generic/non-generic `PowerCmd.Apply`, `ModifyAmount`, native
Artifact receiver hooks, captured Narara immunity, native removal/clone, actual
holder-side turn-start callbacks, HBR stat helpers, cannon attack previews and
native cannon `GainBlock` scopes and block writer.

The new custom `HbrCalamityPower` has a unique `HBR_CALAMITY_POWER` ID and icon.
Its Amount is reduction strength, its saved RemainingTurns is an independent
two-turn clock. Accepted positive applications keep the stronger effective
strength and refresh the clock, including weaker or equal incoming effects.
Artifact and immunity can reject a weaker refresh before either state changes.
Explicit nonpositive amount changes do not refresh the clock. The fixture also
checks that native Doom and the original native Calamity buff retain additive
behavior and are not replaced.

All six allied attributes and enemy E are checked, including zero floors,
phantoms' independent debuffs, owner-plus-local cannon reduction exactly once,
preview state restoration and read-only progression snapshots. The actual
Happiness Lv3 preview observes its real outgoing stat result: the extra
Strength must be combined before flooring, so base100 + two Strength bonuses
of10 - owner Calamity105 - local Calamity10 gives5; owner Calamity200 gives0.

```powershell
dotnet build validation/hbr-calamity/Checks.csproj -c Release `
  -p:ModAssemblyPath='<absolute candidate DLL>'

$env:HBR_NATIVE_PROFILE='stable-0.107.1-current-dependencies'
dotnet validation/hbr-calamity/bin/Release/net9.0/Checks.dll `
  '<absolute candidate DLL>' '<candidate SHA256>' '<stable dependency profile>' `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

For beta, build the fixture against matching native override signatures while
keeping the same stable-compiled mod DLL:

```powershell
dotnet build validation/hbr-calamity/Checks.csproj -c Release `
  -p:ModAssemblyPath='<same stable-compiled mod DLL>' `
  -p:NativeAssemblyPath='<isolated beta sts2.dll>' `
  -p:NativeRitsuAssemblyPath='<matching monolithic beta STS2-RitsuLib.dll>' `
  -p:BetaNativeChecks=true

$env:HBR_NATIVE_PROFILE='beta-0.111.0-current-dependencies'
dotnet validation/hbr-calamity/bin/Release/net9.0/Checks.dll `
  '<same stable-compiled mod DLL>' '<candidate SHA256>' '<beta dependency profile>' `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

The final candidate SHA256
`47E96800843E79CAB8F3F0BF77BB746CA5232B5163783393672A025533152989`
passes 116 checks on stable0.107.1 and 116 on beta0.111.0, with current dependency
profiles (BaseLib3.4.7; Ritsu0.6.10 facade and forwarded assemblies). Each build
has zero warnings and errors. `bin/Release/net9.0/report.json` records each
assertion and candidate/native hashes; save it separately between runs.

Only Godot StringName/logging, hook-listener enumeration and the sound/VFX
leaves are isolated. Combat arithmetic, PowerCmd, Artifact, history, turn
countdown, stat adaptation and cannon block writing are real. The saved-property
roundtrip checks metadata and values, not a complete native save-file load.
Beta uses the previously verified telemetry-isolated DLL with SHA256
`0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`;
the five-byte Sentry module-initializer call is the only removed call, with all
production APIs unchanged. See `../hbr-fragile/README.md` and the beta audit
boundary report for the original DLL hash and byte verification. These checks
do not start Godot, render UI, verify multiplayer, package or upload.
