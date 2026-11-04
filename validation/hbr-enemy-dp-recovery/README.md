# Enemy fixed DP recovery regression

This fixture executes the real recovery Move bodies of HealHopper, AbyssKnocker, DeathSlug, FlatHand and SkullFeather, with the production fixed-resource conversion and exact-DP native `PowerModel.SetAmount` bridge installed. It runs the actual `HighDimensionalShellPower.Restore` guards, remaining-capacity clamp and ledger changes. `CombatState.GetTeammatesOf` performs the real target selection.

The same candidate DLL passes 91 checks with stable and beta v0.111.0 native assemblies. Coverage includes HealHopper's authored 4/5 recovery becoming 400/500 only in HBR mode; two ascension-10 HealHoppers restoring DP 7862 -> 8862 out of an actual 14700 maximum while HP stays 34300; all four other fixed recoveries; legacy values; Break; dead-teammate exclusion; opposing-side exclusion; full-DP saturation; and direct already-resolved `Restore(10)` remaining 10. GainBlock calls retain their authored raw coefficients and `Move` flags, so their separate numerical pipeline receives no extra resource scaling.

Animation and Flash leaves are isolated. The final native GainBlock leaf is captured without running the native block engine. The integer Ascension selector is controlled to exercise the real authored 4/5 values. The shared stat-power host supplies managed combat objects and real HBR enablement. The linked overcharge bootstrap verifies candidate/profile hashes and isolates only beta's telemetry initializer in an output copy. No Godot rendering, full game combat, save editing, packaging or installed-file changes occur.

From the checkout, run stable checks in PowerShell:

```powershell
dotnet build validation/hbr-enemy-dp-recovery/Checks.csproj -c Release -p:SkipModOutputCopy=true
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hbr-enemy-dp-recovery/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hbr-enemy-dp-recovery/candidate-stable.json
```

For the hash-verified beta profile prepared during the overcharge checks:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hbr-enemy-dp-recovery/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-enemy-dp-recovery/bin/Beta/Checks.dll $mod $hash $profile C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hbr-enemy-dp-recovery/candidate-beta.json --isolate-telemetry
```

Reports include the exact candidate, source-native and isolated-native hashes, the runtime profile, the telemetry proof, executed checks and concrete recovery cases. Generated reports and runtime copies remain local evidence.

The preserved pre-fix DLL `E7D8443019B9030620D68B82D8C5ED69364334BEE0CB33D7176008BB6E790F76` is intentionally red in `outputs/hbr-enemy-dp-recovery/baseline-stable.json`: the first real HBR HealHopper action changes teammate DP 100 -> 104 instead of 100 -> 500. Both candidate reports pass against the same DLL `2CA73801C76A4E78C621A0F0858AFF70132EE724AA16A2F5ECE5B35ECB9FDFB9`.
