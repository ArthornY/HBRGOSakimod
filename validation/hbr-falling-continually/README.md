# Falling Continually: base100 plus debuff attributes

Focused managed regression for the final requested rule: preserve the block-sized base amount, then apply the normal debuff INT/LUCK/E curve. Frozen candidate `D6990D1CFD5D1B904653CD3164DD1FA981B33C6BE98F9D145F42D9627B312E6E` passes **131 checks on stable and 131 on beta**.

The authored counters remain Falling Continually 3→4 and Death to You 4→6. HBR application resolves:

`raw × 100 × (1 + max(0, (2×INT + LUCK)/3 − E) × 0.001)`

The helper reads current attributes once. Ordinary native power Given/Received modifiers follow; final native storage truncates and caps the result. Legacy owned cards retain their raw counts. This grants Falling layers, without granting block or running GainBlock modifiers.

| Raw counter | INT100 / LUCK100 / E150 | INT1000 / LUCK100 / E150 |
| --- | --- | --- |
| 3 | 300 | 465 |
| 4 | 400 | 620 |
| 6 | 600 | 930 |

Baseline `4F03A8992BF32B4C443E675C2DF5305CA4BC83391525B354D2E0B572D66D8FDD` reaches the real first card/native Apply and fails with **3 stored layers instead of 300**, after the two patch-install checks. This is a real behavior failure, not a missing-helper/type test.

## Coverage

- Both cards, normal and upgraded, run real OnPlay/native Apply at default and boosted stats. Original raw3/4/4/6 storage, target/applier/card source, first mutable power clone, existing-instance stacking, and targeted display agreement are checked.
- Independent INT/LUCK vectors verify their 2:1 weights. LUCK-only growth is checked through both targeted preview and actual OnPlay/storage. Current target E/floor changes affect new grants and previews. Prior stored layers never recalculate when current attributes change.
- INT675/LUCK100/E150/raw3 resolves exactly400. A nearby fractional potency399.5 stores399; two applications store798 instead of flooring aggregate799. The test preserves this exact integer boundary after the late-division precision fix.
- Native Given additive0.75 and multiplier2 follow resolved465, producing931 stored layers. Actual Artifact and native Received suppression remain effective. Deterministic custom Power listeners exercise the real hooks and notifications. Preview includes Given modifiers; Received modifiers and Artifact are outside its native preview boundary.
- Legitimate high-level/native Dexterity values test oversized first application and existing stacks. Given downscaling and Given amplification followed by Received downscaling prove final capping happens after all application modifiers. Actual IntegerStatAmountPatch installs on both native async bodies; passive native AfterPowerAmountChanged observation verifies final integer offsets.
- Four direct EvaluateBasePotency cases cover maximal basis/stats, saturated fallback, equal enormous E/stat cancellation, and zero basis without decimal exceptions.
- Canonical, untargeted and explicitly unpowered descriptions show the raw×100 reference. Targeted owned text uses the correct owner/target. Legacy owned descriptions retain the original raw PowerVar and no HBR display parameter. Native LocManager/LocTable/SmartFormat renders the actual templates. Native block Hook count stays zero and the caster gains no block.
- Actual Falling AfterDamageReceived runs with a constructed native DamageResult and player-owned phantom carrying PhantomPower. A valid callback applies exactly stored300 Doom once through native PowerCmd; a second stacks another300. Changing current INT/LUCK does not recalculate the stored effect. Existing damage/props/target/phantom-owner guards remain effective.

## Host boundary

The existing Fragile fixture supplies native combat/player/power state. Its hook enumeration returns actual fixture actors' Powers. StringName native construction, engine logging and waits are controlled leaves. Death to You's preceding AttackCommand.Execute is a controlled no-damage leaf; its builder call and subsequent target-alive guard remain exercised. The Doom test supplies a DamageResult directly, without simulating physical HP loss or the complete damage-delivery scheduler.

No card OnPlay, PowerCmd application, native clone/stack mutation, hook implementation, description calculation, or Falling trigger is replaced. Native storage runs with the actual production integer-normalization patch. These are managed checks, not rendered UI or in-game tests; no full save/load, multiplayer session, packaging, or installed-file changes are involved.

Earlier INT/LUCK-only small-basis and INT-only block rules were superseded. Their source/DLL/reports are archived under `outputs/falling-continually/superseded-int-luck/` and `superseded-block-only/`; neither is final acceptance evidence.

Reports relative to the repository root:

- `outputs/falling-continually/baseline-stable.json` — expected real application failure.
- `outputs/falling-continually/candidate-stable.json` — 131 passing checks.
- `outputs/falling-continually/candidate-beta.json` — 131 passing checks.

## Run

From the repository root in PowerShell:

```powershell
$hash = 'D6990D1CFD5D1B904653CD3164DD1FA981B33C6BE98F9D145F42D9627B312E6E'
$mod = 'outputs/falling-continually/candidate/HBRGOSakiMod.dll'
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/hbr-falling-continually/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hbr-falling-continually/bin/Release/net9.0/Checks.dll $mod $hash libs $godot outputs/falling-continually/candidate-stable.json

$profile = Join-Path (Get-Location) 'outputs/hotaru-overcharge/beta-profile'
dotnet build validation/hbr-falling-continually/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-falling-continually/bin/Beta/Checks.dll $mod $hash $profile $godot outputs/falling-continually/candidate-beta.json --isolate-telemetry
```

Both runtime runs load the same frozen production DLL. The shared bootstrap verifies its hash and the beta profile manifest/DLL hashes. Beta uses existing strict telemetry-only static-constructor isolation; original and isolated native hashes are recorded in its report. The installed game directory supplies read-only managed formatting dependencies, and `D:/stsmod/sts/localization/zhs` supplies native localization tables. To reproduce the expected baseline failure, use the baseline snapshot/hash and `baseline-stable.json` output path.