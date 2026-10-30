# Fragile card percentages

This managed fixture reuses `hbr-card-display` setup and its actual production
card-description localization patch, native formatter and targeting scopes.
It verifies modifier counters become percentages without changing card storage:
Thunder 37.5%, Ice 46.9% / 91.6%, upgrades, clones, fixed two-turn text, target
INT/LUCK/E calculation, weapon switching and unchanged legacy descriptions.
The actual Ice `OnPlay` executes with isolated attack and power-application
leaves: below-threshold raw3, threshold raw6, upgraded raw9, and legacy separate
raw3/raw3 requests are captured before the independent production power fixture
checks their lifecycle. Scars multiplies the final resolved effect once,
including zero-grant and fractional raw-input normalization boundaries.
Ice's extra defense debuff uses the independent HBR model and 15% basis
(raw three displays 23.4%); legacy mode retains native Vulnerable. The actual
application capture asserts that those models stay separate.
Descriptions also cover DoublePowers effect/duration doubling, Weakness Strike
effect-only doubling, Piercing/Strong Shot's 6/12-permille coefficient bonuses,
and FullBody's 10/5-percentage-point destruction thresholds and confirmed
hundred-point damage scale.

Run from the repository root after a source-only candidate build:

```powershell
dotnet build validation/hbr-fragile-cards/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-fragile-cards/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

`bin/Release/net9.0/report.json` records the exact DLL/localization hashes and
formatted examples. It does not start Godot or demonstrate a rendered card or
an in-game combat; the independent Fragile combat fixture covers the native
power lifecycle beyond these raw requests.
