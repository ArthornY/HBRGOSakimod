# Card effect numbers and inspect coefficients

This managed fixture loads the exact candidate DLL by SHA256 and formats the
actual mod card templates with native `LocManager`, `LocTable`, `LocString` and
the game's registered SmartFormat extensions. It exercises production card
description and HBR numeric adapters, not a second implementation of the damage
formula.

The real `NCard.UpdateVisuals` Harmony prefix/postfix/finalizer is installed and
its target and cleanup callbacks are verified, without constructing Godot nodes.

Coverage includes ordinary numeric card faces, inspect-only `（100%）` suffixes,
base and upgraded Strike/Defend, current-owner intelligence, the ownerless
INT100 reference, already resolved native previews, target-aware calculation,
nested and throwing description scopes, and unchanged raw variable/upgrade/clone
storage. Registered HBR card template variants are checked for bound display
parameters and formatted in both ordinary and inspect contexts.

Build and run from the repository root:

```powershell
dotnet build validation/hbr-card-display/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-card-display/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

The fixture reads Chinese localization from the repository and merges local
native tables from `D:/stsmod/sts/localization/zhs` when present. Native runtime
formatter dependencies are read from the installed game's runtime directory,
overridable with `HBR_NATIVE_RUNTIME_DIRECTORY`.

`bin/Release/net9.0/report.json` records the exact mod/game/localization hashes,
checks and formatted examples. Godot StringName/logging and target registration
are isolated leaves; native preview values are prepared as the input that the
NCard display receives. This does not start Godot, render a card, play a game,
edit saves, package, or upload to Steam.
