# Native Thorns trigger and raw-unit regression

This fixture executes the actual native ThornsPower.BeforeDamageReceived body with the production source adapter. It captures terminal command inputs: raw stacks2 remain2 skill units (33.33% when displayed), rather than the superseded fixed-resource command200. Native target, dealer, null card source, choice context, Unpowered|SkipHurtAnim flags and stored/displayed stack counters remain unchanged. The powered-attack/Omnislice exception, legacy mode, detached/canonical variable guards and unknown Unpowered amounts are covered.

The terminal capture does not execute the canonical numerical command scope. Consequently this fixture does not resolve the new skill damage, sample criticals, render percentage descriptions or run HP mutations. Those boundaries are covered by the [effect-skill regression](../hbr-effect-skill-damage/README.md), which exercises the command scope and calculation while retaining original reaction flags. Previous fixed200 reports describe the earlier, superseded policy.

Build and run from the checkout root:

```powershell
dotnet build validation/hbr-thorns/Checks.csproj -c Release -p:SkipModOutputCopy=true
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hbr-thorns/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/effect-skill-damage/thorns-gates-stable.json
```

For beta, use the existing verified outputs/hotaru-overcharge/beta-profile, compile with BetaNativeChecks=true, NativeAssemblyPath and NativeRitsuAssemblyPath overrides and separate build directories, then pass that profile and --isolate-telemetry to the same bootstrap. Checked telemetry isolation edits only a separate output copy. This is a managed trigger/storage check, without a game process, rendered UI, saves, packaging or installed-file changes.
