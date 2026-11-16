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

Since the fourth move-table batch (2026-11-08) HealHopper and AbyssKnocker follow their original hbr.quest tables: Repair β restores 10% (400 of the original 4000) of every living ally's own maximum DP, the healer included, and AbyssKnocker's recovery 3000/44200 of its own, both without block; Break and dead allies are still excluded and the player side is untouched. The checks expect those shares (two ascension-10 HealHoppers now take the 14700-maximum receiver from 7862 to 10802) and no block command; DeathSlug, FlatHand and SkullFeather keep their fixed recoveries. Candidate `766663D0372B2DFFF996493BED800AD42B70452E7FF1E0719FE3C84792CA6CB3` passes 67 checks on stable and on beta v0.111.0 (`D:/stsmod/outputs/monster-batch4-20261108/validation/{stable,beta}/hbr-enemy-dp-recovery/report.json`). The paragraph above describes the earlier fixed-value version.

The preserved pre-fix DLL `E7D8443019B9030620D68B82D8C5ED69364334BEE0CB33D7176008BB6E790F76` is intentionally red in `outputs/hbr-enemy-dp-recovery/baseline-stable.json`: the first real HBR HealHopper action changes teammate DP 100 -> 104 instead of 100 -> 500. Both candidate reports pass against the same DLL `2CA73801C76A4E78C621A0F0858AFF70132EE724AA16A2F5ECE5B35ECB9FDFB9`.
