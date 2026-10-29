# Mod fixed HP/DP effect regression

Build the mod separately with `SkipModOutputCopy=true`, then build this fixture:

```powershell
dotnet build validation/hbr-mod-fixed-resources/Checks.csproj -c Release -p:GodotSharpAssemblyPath=<GodotSharp.dll>
```

Run its `bin/Release/net9.0/Checks.dll` with candidate mod DLL, expected SHA256,
`libs` runtime dependency folder, and GodotSharp DLL. The shared bootstrap checks
the candidate hash before loading it. The ignored `report.json` records both
candidate and native DLL hashes and each completed check.

The real mod consumption methods execute for BindingRedemption's total600
allocation (200 missing DP followed by400 HP recovery), Overdraw's raw15 and
upgraded10 DP costs, DP potion raw20, UnbrokenRemnant raw10 and its once-per-combat
guard, and Overwork player1/phantom3 maximum-DP loss. Both HBR and disabled-mode
values are exercised. Native DynamicVar upgrades and cloning retain raw units;
actual percentage-DP recovery remains in resolved resource units. Real resource
argument binding is checked against the HBR card templates, including potion and
selected card numeric amounts. No display values are written into raw ledgers.

Controlled host leaves are Godot StringName/logging/Flash/power notifications,
active-combat state, empty card piles and custom-target registration. Heal and
energy commands are captured, and CanReceivePowers prevents Unbroken's subsequent
Strength application from requesting a game host. Overwork is given the accepted
callback receipt normally recorded by BeforeCardPlayed; its full real
AfterCardPlayed resource consumer runs. Exact DP restoration and SetAmount run
with the production native-amount patch. Full SmartFormat rendering, native
hand-event ordering, complete combat/saves, Godot rendering, packaging and Steam
are outside this fixture.
