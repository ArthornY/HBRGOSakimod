# Coffin direct-DP damage caps regression

Candidate `B436B5839ED67193FD2B9B4E78FB9B75855BA28F16383A928B608C90C98E2433` passes 79 checks on stable and 80 on beta v0.111.0. Beta adds an assertion for the native cap hook's additional CardPlay argument. Reports are `outputs/coffin-intangible/candidate-stable.json` and `candidate-beta.json`.

The frozen pre-change DLL is `outputs/coffin-intangible/baseline/HBRGOSakiMod.dll`, SHA256 `100E63797656C1E459980CB0ECEA057DF467C5C29B1944191BDE13ACD09B9C84`. `baseline-stable.json` records the real bug: OnCoffinBlock with resolved block100 and native Intangible1 spends100 DP (1000→900), with an actual DP100/HP0 receipt, instead of spending1. Native Doom still receives100.

This fixture links the existing stat-power host and overcharge bootstrap/strict telemetry isolator unchanged. It initializes mutable player/cannon/power state, then invokes the real `HotaruMyCoffinPower.OnCoffinBlock`. Highest-HP target selection, `DamageAndDoom`, native cap listeners, `HighDimensionalShellPower.DamageDpOnly`/`TryAbsorb`/production exact-DP amount writes, `NotifyDirectDpDamage`, the complete `FinishAfterDamageGiven` dispatch and native Doom `PowerCmd.Apply` execute. DP values are read after actual shell mutation; receipts are observed without being manufactured or edited. No new CreatureCmd.Damage command is issued.

Cases verify:

- Native Intangible1 spends exactly1 DP, leaves HP unchanged, and reports actual DP1/HP0. Its real cap hook receives the selected target, owner dealer, original source card, Unpowered flags and beta CardPlay. The genuine `Hook.AfterModifyingDamageAmount` notification is observed while DP is still1000, with only the effective Intangible modifier and the original card.
- No cap spends the full block100. A pure cap0 test Power prevents all DP spending and receives one effective-modifier notification. Two pure negative caps -1/-2 normalize to0 before comparison, so only the first actual reduction is notified. These are ordinary cap-listener test powers, not replacements for native Intangible or production cap results.
- Another target's native Intangible retains its target-owner gate and produces no effective-modifier notification. No target DP causes no HP loss or DP receipt. Excess block1500 against DP500 spends only500 with no HP overflow.
- Strength999, Weak, Vulnerable and HBR amplification do not recalculate the already-resolved block100. Doom remains based on the original block, including100 under Intangible/cap0 and1500 in the DP-overflow case. Legacy mode also honors native Intangible1. Actual receipts retain dealer, highest-HP target, card, CardPlay, cannon and Unpowered reaction attribution.

Only logging/StringName, Flash and the shell-break attachment/removal/stun lifecycle are isolated. Break isolation happens after real DP reaches0; ordinary positive-DP cases use the real refresh path, which naturally exits without a game room. No DP subtraction, cap result, damage amount, Doom amount, cap notification or receipt is replaced. The supplied block is the consumer's input: a complete block-gain command is not executed. There is no game process, rendering, complete room/combat lifecycle, save/clone suite, package or installed-file modification.

Run from this checkout:

```powershell
$candidate = (Resolve-Path outputs/coffin-intangible/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/coffin-intangible/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/coffin-intangible/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/coffin-intangible/candidate-stable.json
```

Beta uses the existing verified runtime profile and separate fixture build output:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/coffin-intangible/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/coffin-intangible/bin/Beta/Checks.dll $candidate $digest $profile $godot outputs/coffin-intangible/candidate-beta.json --isolate-telemetry
```

The bootstrap verifies candidate/profile hashes. Stable native SHA256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. Beta source native SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`, while its output-local telemetry-isolated copy is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`. The shared isolator requires the exact Sentry initialization call+return, zeros only that five-byte call, verifies all other bytes are identical and records proof in the report. Original profile/runtime assemblies remain unchanged.
