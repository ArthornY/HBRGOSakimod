# Native maximum-HP percentage damage caps

Runs the production Harmony adapters against real native `HardToKillPower` and `HardenedShellPower` methods. Ordinary HBR stacks are percentages of maximum HP: raw9 means9%, raw20 means20%, and multiplayer-adjusted raw40 means40%. At maximum3000 those caps are270,600,1200. Raw counters remain unchanged. Legacy mode keeps native flat9/20 behavior, while health-relative main-phantom fusion caps keep their existing30%/60% origin and repeated-fusion ratios.

The stable and beta v0.111.0 profiles each pass127 checks. Coverage includes:

- Real cap/HP-loss methods, target identity, blocked hits, actual HP received this turn, immunity display, side-turn reset, native mutable cloning and raw-amount recreation.
- Current HP loss leaving caps unchanged; maximum HP changes updating the cap, remaining budget and display. Received100 followed by a cap drop to20 yields0 remaining rather than negative HP loss; a later cap300 yields200 remaining without rewriting the received ledger. Raising maximum HP after actual exhaustion restores a normal HP display when budget reopens.
- Real native `DisplayAmountChanged` events: maximum-HP setter callbacks read refreshed badges for both powers, while percentages and received HP stay unchanged. Special HP-display kinds remain unchanged but badges refresh. Disabled HBR and detached powers receive no extra notifications.
- Positive fractional caps flooring at the final result, a minimum1 for positive percentages at tiny HP, and zero/negative percentages remaining0. Exact monster maximum4e9 produces9%/20% caps360000000/800000000; larger caps retain decimal precision while badges stay bounded.
- Actual native `LocManager`, `LocTable` and SmartFormat descriptions, with `CapPercent` and resolved `Amount`. Override7 previews7% without mutating state; zero/negative previews, ownerless/canonical gates and large-fusion override behavior are covered.
- Small/large fusion source priority, saved fusion-count parsing, native Intangible's literal1 cap and duration counter, and unrelated powers.

Stable build and run from this checkout:

```powershell
$candidate = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash
dotnet build validation/hbr-damage-caps/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:ModAssemblyPath=$candidate"
dotnet validation/hbr-damage-caps/bin/Release/net9.0/Checks.dll $candidate $digest ./libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/percent-damage-caps/candidate-stable.json
```

Beta uses the verified runtime profile prepared for the overcharge checks, including the matching Ritsu runtime and System.IO.Hashing dependency:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hbr-damage-caps/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:ModAssemblyPath=$candidate" "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-damage-caps/bin/Beta/Checks.dll $candidate $digest $profile C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/percent-damage-caps/candidate-beta.json --isolate-telemetry
```

The local bootstrap checks the candidate SHA256 and every supplied profile-manifest entry before loading. The optional fifth argument selects a report path; otherwise `report.json` is written beside the executable. Beta links the verified telemetry-only output-copy isolator from `../hotaru-overcharge/TelemetryIsolation.cs`; original profile and installed DLLs are untouched. The report distinguishes source/native hashes and records the exact isolation proof. Native method invocation adapts the optional beta card-play parameter without replacing their bodies.

Formatting requires the native Chinese power table at `D:/stsmod/sts/localization/zhs/powers.json`, overlaid with this checkout's mod localization. Native formatter assemblies resolve from `HBR_NATIVE_RUNTIME_DIRECTORY`, defaulting to the installed game runtime. Godot `StringName`, logging, energy-icon leaves and read-only phantom roster lookup are isolated. Damage receipts are supplied as explicit actual-HP inputs to the real native ledger. Clone/raw-amount recreation and saved fusion-data parsing are covered; complete save loading, rendered UI, complete damage commands and gameplay are outside this managed fixture. No Godot process, packaging, network or upload runs.

The unchanged2CA73801C76A4E78C621A0F0858AFF70132EE724AA16A2F5ECE5B35ECB9FDFB9 baseline is intentionally red in `outputs/percent-damage-caps/baseline-stable.json`: maxHP3500/raw9 still returns the old900 rather than315. Both candidate reports pass against5FEA6EFA86681C4E90C98F2FC201C392A47E1712E2804EE259715692D1C3AB76. Reports and runtime copies are generated local evidence, not distributed binaries.
