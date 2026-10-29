# HBR Strength and Dexterity stat boundaries

This managed fixture loads the exact candidate DLL by SHA256 and exercises real
native Strength, Dexterity and Vigor methods, production Harmony adapters, native
damage/block hooks and saved-manual stat reads.

Coverage includes allied Strength STR/DEX +1, allied Dexterity INT +1, negative
layers and stat floors, enemy Strength's local VIT/SPR ignore, enemy Dexterity's
local STR/DEX ignore, per-act 1/2/3/4/8 scaling, HP/DP and physical/spirit weights,
unchanged snapshots and save data, repeated previews, independent phantom buffs,
owner-sourced cannon stats, Happiness Lv.3, real active Longing virtual Dexterity,
temporary native duration markers, Vigor and disabled-mode native rules. Native
LocManager/LocTable/SmartFormat renders all four stat-power descriptions with the
current act's actual bound value, preserving original strings outside HBR runs.

Rounding checks retain internal attribute precision until all final multipliers,
Soul Power and caps have resolved, then compare floored native previews, the
production actual-damage wrapper, and the captured raw-damage ledger. Explicit
fixture multiplier/cap powers supply fractional boundary inputs.

Build and run from the repository root:

```powershell
dotnet build validation/hbr-stat-powers/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-stat-powers/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

`bin/Release/net9.0/report.json` records all assertions and the candidate/native
assembly hashes. StringName, logging and hook-listener enumeration are isolated
leaves. This does not start the game, render UI, load saves, execute full card,
cannon or temporary-power command lifecycles, package, or upload.
