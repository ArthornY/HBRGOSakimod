# HBR cannon display checks

This focused fixture loads the exact candidate DLL by SHA256 and reuses the
native card-display setup. It executes the real cannon `IntentDescription`
methods, `Millsage` state, damage/block preview helpers, native `LocManager` /
`LocTable` / SmartFormat, and manual/rest description builders.

Coverage includes all five original cannons and both localization ID forms;
Coffin's 224 block at 112 Intelligence followed by `(80%)`; positive/negative
final cannon forms and the optional extra start attack; distinct Soul Power and
skill percentages; live and no-target descriptions; out-of-combat current
Intelligence, current Soul Power and the next upgrade's Soul preview; unchanged
saved combat/roll state; and disabled-HBR legacy wording.

Counters on real preview helpers verify that all 35 upgrade-only templates run
zero unused damage/block previews. Coffin evaluates one block value, Absolution
two damage values, and all five manual summaries together evaluate four damage
and two block values. Percentages are presentation only and never written back
into native-unit state.

```powershell
dotnet build validation/hbr-cannon-display/Checks.csproj -c Release `
  -p:ModAssemblyPath='<absolute candidate DLL>'

dotnet validation/hbr-cannon-display/bin/Release/net9.0/Checks.dll `
  '<absolute candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

`bin/Release/net9.0/report.json` records assertions, rendered strings, the DLL
hash, and both changed localization hashes. Hook-listener enumeration, logging
and Godot StringName construction are isolated leaves. The checks exercise
production preview formulas but do not construct Godot nodes, render the UI,
start the game, package, or upload.
