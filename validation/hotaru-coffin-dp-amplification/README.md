# Coffin tier5 maximum-DP amplification regression

This focused fixture executes real `HotaruCannonSupportUpgradeHelper.OnCombatStart`, real Millsage upgrade/retirement predicates, the real Deflector maximum-DP getter and initializer, and native `PowerCmd.Apply`/`ModifyAmount` with the production exact-percentage/stacking adapters. It links the existing stat host, hash-verified overcharge bootstrap and beta telemetry isolator without modifying those fixtures.

Stable and beta v0.111.0 each pass117 checks. Both HBR and legacy modes use100 maximum DP per1 amplification percentage point:6000 ->60%,53301 ->533.01%,99 ->0.99%,100 ->1%. Maximum0 attaches no power. Current DP0/1/5999/6000 gives the same60% award from maximum6000 and is never spent. The real predicates reject level4, permanently lost Coffin, missing Deflector, dead owner and owner outside combat. An existing native0.375% bank stacks with533.01% into the same native power instance at533.385%.

Only the unrelated tier3 `CreatureCmd.Heal` leaf is captured. Input HP1/5000 leaves enough missing health to avoid tier3 overflow restoration, so DP preservation checks isolate the tier5 award while keeping the real tier3/tier5 predicates. Character/player-combat metadata allows the native combat-history path to execute normally. No tier predicate, DP getter, reward amount, native application or exact percentage storage is replaced. Shared host logging/StringName leaves remain isolated. This does not run a game process, mutate HP through healing, repeat the clone/save suite, render UI, package or change installed files.

Stable invocation from this checkout:

```powershell
$candidate = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
dotnet build validation/hotaru-coffin-dp-amplification/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hotaru-coffin-dp-amplification/bin/Release/net9.0/Checks.dll $candidate $digest libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/coffin-dp-amplification/candidate-stable.json
```

Beta uses the existing verified runtime profile:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hotaru-coffin-dp-amplification/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hotaru-coffin-dp-amplification/bin/Beta/Checks.dll $candidate $digest $profile C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/coffin-dp-amplification/candidate-beta.json --isolate-telemetry
```

The bootstrap verifies the candidate SHA256 and runtime manifest; beta isolates only the checked telemetry initializer in an output copy and records source/copy proof without editing input DLLs. Both green reports use `1A4FE507A5F29EAC7CA13C70FCCDF60E8A0F207586F8FFE82BD0651D58E28886`. The preserved `5FEA6EFA86681C4E90C98F2FC201C392A47E1712E2804EE259715692D1C3AB76` baseline is intentionally red in `outputs/coffin-dp-amplification/baseline-stable.json`: maximum6000 awards6000 rather than60 percentage points.
