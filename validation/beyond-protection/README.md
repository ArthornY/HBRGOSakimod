# Beyond Protection conversion boundaries

This fixture loads the candidate mod DLL by its exact SHA256 and executes the
actual card, power, DP conversion/settlement ledger and native health setters.
It verifies five-per-mille damage conversion, explicit percentage-bank reloads
and legacy `ConvertedDp` fallback, Exhaust, unchanged upgraded draw2, odd/zero DP,
health caps, one-time refunds and actual HP gained with the temporary maximum.

The 31O case runs the production maximum-HP redirect and real owned-main-phantom
lookup. It verifies that the phantom heals once and its summoner does not heal.
The production native maximum-HP synchronization patches remain installed.

From the repository root:

```powershell
dotnet build validation/beyond-protection/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet validation/beyond-protection/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

`bin/Release/net9.0/report.json` records the checked assemblies and each passing
assertion. The Heal command's presentation and post-heal hook traversal are
isolated, but the supplied amount is recorded and native `HealInternal` performs
the real HP write/clamp. Draw is captured; StringName/logging/relic flash are
isolated. The fixture does not render cards, move an Exhaust card through native
piles, load full native saves, run the game, package, or upload.
