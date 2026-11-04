# Overcharge growth and Happiness amplification regression

The fixture links the minimal host from `../hbr-stat-powers/Program.cs` without changing that fixture. It invokes the real custom `HotaruOverchargePower.Apply` and native `PowerCmd.Apply`/`ModifyAmount`; it never sets `_damagePercent` or replaces cloning/storage. Cases cover 100%, 150%, mixed 250%/400%, native clone, `SavedProperties` JSON and packet round trips.

Growth tests execute real `Millsage.GrowHappiness` and `FireHappiness(growBeforeDamage: true)`, using a real minimal player combat state with empty card piles and two enemy creatures. In both HBR and legacy modes, two consecutive growths must emit one reaction per living enemy, retain the exact growth/percentage command base, and keep the next growth sequence. Direct primary firing retains floor-halving and stored-damage halving. Tier 2 reaction hits keep their existing growth reward.

The sole damage execution leaf is the native `CreatureCmd.Damage` method. Its replacement executes production cannon `Begin`, the full native damage hooks through the production actual-damage wrapper, native integer HP-result rounding, production receipt binding/taking, and `FinishAfterDamageGiven`; it synthesizes only the final HP receipt. Reaction attribution is read from the actual pending command, never manufactured. Logging/StringName and the visual `Flash`/`RefreshIntent` leaves are isolated. Tests use a null card source and do not claim complete HP-command execution, non-null card attribution, rendered gameplay, complete saves, or live multiplayer.

Build and run in PowerShell from this checkout:

```powershell
dotnet build validation/hotaru-overcharge/Checks.csproj -c Release -p:SkipModOutputCopy=true
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hotaru-overcharge/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hotaru-overcharge/candidate-stable.json
```

The bootstrap verifies the candidate hash and every runtime-profile manifest entry when a profile is supplied. For a beta profile, compile with `-p:BetaNativeChecks=true`, `-p:NativeAssemblyPath=<profile>/sts2.dll`, and `-p:NativeRitsuAssemblyPath=<profile>/STS2-RitsuLib.dll`, use separate output/intermediate directories, and pass that profile instead of `libs` at runtime.

Before this fix, the unchanged 1.3.0.0 staged DLL produces two initial HBR reaction hits of 321 each for raw growth 1, but each receipt halves the next increase: `2 -> 1 -> 0`. The following growth adds zero and emits no new overcharge commands. The fixed DLL preserves `2 -> 3` for natural growth; manual growth plus its primary firing preserves `1 -> 2`.

For the beta profile's managed module initializer, pass the optional final argument `--isolate-telemetry`. This uses the same checked PE isolation as `validation/beta-compat`: require exactly a call to `SentryAutoInit.Init` followed by return, replace only that five-byte call with NOPs in a separate output copy, and verify every other byte is unchanged. The report records source/copy hashes and the output includes `telemetry-isolated/proof.json`. Original profile and installed files stay untouched. HBR reaction results are additionally checked against the independent attribute/Soul formula and must be positive.


The original completed growth-regression reports are `outputs/hotaru-overcharge/candidate-stable.json` and `outputs/hotaru-overcharge/candidate-beta.json` (288 checks each). The unchanged staged 1.3.0.0 baseline is intentionally red in `outputs/hotaru-overcharge/baseline-stable.json`; it reaches the real damage receipts and then fails because the second growth emits no reaction commands.

Exact beta invocation using the verified derived profile (which adds the installed `System.IO.Hashing.dll` dependency) is:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hotaru-overcharge/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hotaru-overcharge/bin/Beta/Checks.dll $mod $hash $profile C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hotaru-overcharge/candidate-beta.json --isolate-telemetry
```

Output profiles and reports are generated local evidence, not committed runtime binaries. Profile manifests record exact source paths and SHA256 values. The beta report distinguishes the unmodified native source hash from the telemetry-isolated runtime hash.
The Happiness tier 4 extension passes 488 checks on stable and beta against candidate `E6615360E4D8FFAA20A5ED9FD26A6498AAAC9C892BB2091EF21EA1A86F41E661`. The first 288 successful assertion strings exactly match the original growth report; the extra 200 assertions verify the new receipt-based award. Final reports are `outputs/happiness-damage-amplification/candidate-stable.json` and `candidate-beta.json`.

Tier 4 cases run the real cannon `Damage`/`ReactionDamage` entry, upgrade/ownership/retirement predicates, cannon scope, actual Hook calculation, `RecordFinalDamage`, `RecordDpAbsorption`, `BindReceipts`, one-time receipt take, `FinishAfterDamageGiven`, `AfterCannonDamage` and native `PowerCmd.Apply`/`ModifyAmount` with production precision adapters. Only the existing terminal damage leaf supplies controlled HP/DP loss inputs for these cases; it does not fabricate the cannon receipt, edit its actual-loss fields, replace the award calculation or directly set the exact amplification bank. Physical native HP/DP mutation remains outside this managed fixture.

Both modes verify actual losses 1000 -> 1%, 2500 -> 2.5%, HP 600 + DP 400 -> 1%, pure DP 1000 -> 1%, and HP 1 -> 0.001% without flooring. The issued raw amount is 9999 and the separately calculated damage is larger than the controlled terminal loss, proving the award follows actual receipts. Existing 0.375% stacks with 2.5% to 2.875% on the same native power instance; separate target receipts of 600 and 400 stack to 1%. A genuine reaction receipt retains the tier 2 growth reward without halving its next growth. Fully blocked zero-loss receipts, locked tier 4, permanent cannon retirement and a dead cannon grant no amplification. The native receipt dealer receives the buff; the damaged target does not.

The pre-change DLL is frozen at `outputs/happiness-damage-amplification/baseline/HBRGOSakiMod.dll`, SHA256 `4EFF0AC617C85F76EFC0E218321A2063ECE5EA097C2D3F70354B9D5D0BB1407C`. Its `baseline-stable.json` passes all original 288 checks, then fails the real first tier 4 award: 1000 actual HP loss yields 1000% instead of 1%.

Run the extension with the same build commands above and these pinned candidate/report paths:

```powershell
$mod = (Resolve-Path outputs/happiness-damage-amplification/candidate/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet validation/hotaru-overcharge/bin/Release/net9.0/Checks.dll $mod $hash libs $godot outputs/happiness-damage-amplification/candidate-stable.json
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet validation/hotaru-overcharge/bin/Beta/Checks.dll $mod $hash $profile $godot outputs/happiness-damage-amplification/candidate-beta.json --isolate-telemetry
```

This extension adds no new clone/save suite, Godot execution, gameplay claim, package, or installed-file changes. The beta runtime uses the same verified profile and checked output-local telemetry isolation described above.
