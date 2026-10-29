# Native fixed damage caps

Runs the production Harmony adapters against the actual native `HardToKillPower` and `HardenedShellPower` methods. The fixture does not reimplement their damage-cap formulas.

Coverage includes fixed 9 → 900 and 20 → 2000 thresholds, multiplayer-adjusted raw 40 → 4000, target identity, fully blocked hits, accumulated actual HP loss, immunity display and turn reset, badges, native cloning and repeated reads, disabled/ownerless/canonical values, and small/large health-relative main-phantom fusion caps. Native Intangible remains a literal 1-damage cap with its duration counter unchanged. It formats the actual Chinese description through native `LocManager`, `LocTable` and SmartFormat.

Pass the candidate DLL and its expected SHA256 explicitly:

```powershell
$candidate = 'D:\stsmod\worktrees\HBRGOSakiMod-hbr-numerics-test\outputs\damage-caps\bin\Release\HBRGOSakiMod.dll'
$digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash
dotnet build validation/hbr-damage-caps/Checks.csproj -c Release -p:ModAssemblyPath=$candidate
dotnet validation/hbr-damage-caps/bin/Release/net9.0/Checks.dll $candidate $digest ./libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll
```

`report.json` is written next to the executable with every successful assertion, any failure, and both native/mod assembly hashes. Godot logging, `StringName`, energy-icon leaves and the read-only phantom roster lookup are isolated. The fixture does not claim an in-game UI or save-loading test and never runs Godot, packages or uploads the mod.
