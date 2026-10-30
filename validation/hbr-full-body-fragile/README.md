# Full-body shot and Fragile percentage checks

Runs the actual card's HBR and legacy OnPlay methods. Native power commands
exercise its unchanged Fragile grant, threshold conversion, independent saved
effects, Scars enhancement, current destruction cap and fractional destruction.
Attack execution is captured at the native builder leaf to inspect the requested
raw damage. Separate real shared-hook cases verify that 50 percent Fragile
supplies 5000 base combat damage, with 150 percent destruction applied once to
make 7500 before normal other bonuses. Checks also cover off-form halving and
requiring the chosen enemy to be in Break despite the bow rune's global query.

```powershell
dotnet build validation/hbr-full-body-fragile/Checks.csproj -c Release `
  -p:ModAssemblyPath='<absolute candidate DLL>'

dotnet validation/hbr-full-body-fragile/bin/Release/net9.0/Checks.dll `
  '<absolute candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

The report records the exact candidate hash. Godot native calls and hook-listener
enumeration reuse the existing Fragile fixture's controlled leaves. Rune
availability is controlled; the actual bow query and card target gate run. This
does not execute a game process, render UI, package, or upload.
