# Anchor block skill regression

Candidate `2CF1FE4A167353373B7975C908035684958419C37C5DA6FCA08B1F030733C588` passes 41 checks on stable and 41 on beta v0.111.0. Reports are `outputs/anchor-block-skill/candidate-stable.json` and `candidate-beta.json`.

The pre-change DLL is frozen at `outputs/anchor-block-skill/baseline/HBRGOSakiMod.dll`, SHA256 `75E18B88D67C59E7867A26AD58B36797F33DC051F0E4799939425E064991047F`. `baseline-stable.json` executes real native Anchor.BeforeCombatStart and gains only10 actual block at INT100, instead of1000.

This focused fixture links the existing stat-power minimal host, overcharge bootstrap and strict telemetry isolator without modifying them. The real no-argument native `Anchor.BeforeCombatStart` calls the original typed `CreatureCmd.GainBlock(Creature,BlockVar,CardPlay,bool)` boundary. The production identity/recipient/HBR prefix resolves only Anchor's own original BlockVar, then invokes the real decimal GainBlock with original Unpowered/CardPlay/fast arguments. Native `Hook.ModifyBlock` and real `Creature.GainBlockInternal` execute: actual block is read from the creature after the complete command, with no terminal block recorder or mutation replacement.

Cases verify raw10 is a200% block skill using the block denominator5, actual1000 at INT100 and1500 at INT150. The second stat input comes from the real saved manual's Talent25; repeated grants add1500 each without multiplying the original variable or previous block. Legacy mode still gains10. A different target, a cloned/noncanonical Anchor variable and another native relic's HornCleat BlockVar remain unmapped. An explicit CardPlay and fast=true retain identity/value through the decimal command. Every grant observes exactly one original native block-modifier Hook call, preserves original Unpowered flags, and leaves Anchor's stored BlockVar at10.

Real native Relic.DynamicDescription with LocManager/SmartFormat displays200%, not the damage-denominator166.67%, while keeping raw10 unchanged. The real merchant helper scope renders unowned Anchor at200%, then canonical formatting returns to native after disposal; owned legacy descriptions also remain native. No shop node or rendered tooltip is created.

Only StringName/logging, relic Flash, CombatHistory.BlockGained log recording and energy-icon pool/prefix lookup are isolated leaves. The history log requires metadata absent from this minimal host and is not part of block calculation or mutation. Native waits/VFX naturally bypass their engine work under the shared host's native TestMode; no GainBlock, block Hook or GainBlockInternal body is replaced. The fixture does not execute complete combat-start scheduling, a complete card play, all possible block modifiers, game rendering, save/clone suites, packaging or installed-file changes.

Run from this checkout:

```powershell
$candidate = (Resolve-Path outputs/anchor-block-skill/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/anchor-block-skill/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/anchor-block-skill/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/anchor-block-skill/candidate-stable.json
```

Beta uses the existing verified runtime profile and separate build output:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/anchor-block-skill/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/anchor-block-skill/bin/Beta/Checks.dll $candidate $digest $profile $godot outputs/anchor-block-skill/candidate-beta.json --isolate-telemetry
```

The bootstrap verifies candidate/profile hashes. Stable native SHA256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. Beta source native SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; its output-local telemetry-only copy is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`. The shared isolator verifies the exact Sentry initialization call+return, zeros only that five-byte call and proves every other byte is unchanged. Original runtime/profile assemblies stay untouched.
