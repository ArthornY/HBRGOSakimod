# HBR Strength and Dexterity stat boundaries

This managed fixture loads the exact candidate DLL by SHA256 and exercises real
native Strength, Dexterity and Vigor methods, production Harmony adapters, native
damage/block hooks and saved-manual stat reads.

Coverage includes allied Strength STR/DEX +1% per layer and allied Dexterity INT
+1% per layer, with layers added before multiplying the unbuffed live stat.
Non-100 bases (150 STR, 180 DEX, 220 INT) distinguish percentage growth from
flat additions and per-layer compounding. Negative layers, -100/-101 floors,
live removal, fixed Calamity after percentage growth, enemy Strength's local
VIT/SPR ignore, enemy Dexterity's
local STR/DEX ignore, per-act 1/2/3/4/8 scaling, HP/DP and physical/spirit weights,
unchanged snapshots and save data, repeated previews, independent phantom buffs,
owner-sourced cannon stats, Happiness Lv.3, real active Longing virtual Dexterity,
temporary native duration markers, Vigor and disabled-mode native rules. Native
LocManager/LocTable/SmartFormat renders all four stat-power descriptions with the
current act's actual bound value, preserving original strings outside HBR runs.

Happiness Lv.3 combines two copies of positive Strength in the same percentage
sum: 20 layers on STR150 produce STR210, not STR216. Six virtual Longing
Dexterity layers combine with 25 native layers to make INT220 × 1.31 = 288.2.
Phantoms use their own native layers; cannons use their owner's layers once.

The exact stable-built candidate below passed **636 checks on both stable and
beta native profiles**, with zero fixture build warnings or errors:

- DLL: `outputs/percent-stats/stable/bin/Release/HBRGOSakiMod.dll`
- SHA256: `724860094E42C5D9F719203E5C72C6D925ED6776CBA781E79ED3337A9D5F9B91`
- Reports: `outputs/percent-stats/validation/{stable,beta}/report.json`

The local bootstrap validates the candidate hash and every dependency recorded in
the runtime profile manifest. Beta fixture compilation adapts native signatures;
the production candidate DLL is the same stable-built binary in both runs.

Rounding checks retain internal attribute precision until all final multipliers,
Soul Power and caps have resolved, then compare floored native previews, the
production actual-damage wrapper, and the captured raw-damage ledger. Explicit
fixture multiplier/cap powers supply fractional boundary inputs.

Build and run from the repository root:

```powershell
dotnet build validation/hbr-stat-powers/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-stat-powers/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<stable-profile>' `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll' `
  '<stable-report.json>'

dotnet build validation/hbr-stat-powers/Checks.csproj -c Release --no-restore `
  -p:BetaNativeChecks=true -p:ModAssemblyPath='<candidate DLL>' `
  -p:NativeAssemblyPath='<beta sts2.dll>' `
  -p:NativeRitsuAssemblyPath='<beta STS2-RitsuLib.dll>' `
  -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-stat-powers/bin/Beta/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<beta-profile>' `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll' `
  '<beta-report.json>'
```

The selected report path records all assertions, candidate/native/localization
hashes and the runtime profile. StringName, logging and hook-listener enumeration are isolated
leaves. This does not start the game, render UI, load saves, execute full card,
cannon or temporary-power command lifecycles, package, or upload.
