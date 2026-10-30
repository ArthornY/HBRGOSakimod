# Fragile attack coefficient checks

The managed fixture runs actual Piercing Shot and Strong Shot card bodies with a
controlled `AttackCommand.Execute` leaf. It verifies per-target versus greatest
enemy Fragile percentage, both independent instances, exact snapshots, expired
effects, pre-Break use, final Scars multiplication, upgrades, three-hit behavior,
legacy flat bonuses and contextual numeric/coefficient descriptions.
Strong Shot requests one all-target attack command containing three hits; the
fixture captures both the command count and authored hit count.

At 6 permille per 1 percentage point of Fragile, 30% adds 18 percentage points to
the skill coefficient, or 1.08 native damage units. This is an additive skill
coefficient increase; it is independent of the card's initial damage value.
Both existing bonus upgrades retain their double-strength 12-permille rule.

```powershell
dotnet build validation/hbr-fragile-attacks/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/hbr-fragile-attacks/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

The JSON report records the exact candidate and localization SHA256 hashes.
It does not start Godot, render a UI, package the mod or upload to Steam.
