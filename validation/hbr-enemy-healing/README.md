# Native enemy healing regression

This fixture links the existing minimal stat host and runs the actual native
`RegenPower` healing hook and `WaterfallGiant.SiphonMove`. It installs the
production `HbrNativeFixedResourceCommandPatch` and checks that its targets
include the real declared regeneration state machine and Siphon state machine.
Stable v0.107.1 declares `AfterSideTurnEnd`; beta v0.111.0 declares
`BeforeSideTurnEndEarly`. The inherited empty stable hook on beta is not used.

Both runtime profiles pass 63 checks against candidate DLL SHA-256
`2CA73801C76A4E78C621A0F0858AFF70132EE724AA16A2F5ECE5B35ECB9FDFB9`:

- Regeneration 10 requests 1000 in HBR mode and 10 in legacy mode. Two owners
  each receive one conversion, the native participants/dead-owner gates remain
  intact, and raw stack storage is unchanged before the isolated decrement leaf.
- Real native `Creature.HealInternal` executes for every captured Heal request,
  including overheal and already-full HP. The Heal command's animation and hook
  orchestration are not executed.
- Siphon uses native fixed healing of 10 or 15, multiplied by the native combat
  player count (one or two). HBR requests are 1000/1500/2000/3000; legacy requests
  remain 10/15/20/30. Ascension input is a controlled branch-selection leaf.
- A direct unmapped Heal(10) stays 10, the LizardTail percentage-variable reader
  stays 50, and the production Unpowered damage wrapper keeps 6 as 6.

The frozen pre-fix DLL SHA-256
`E7D8443019B9030620D68B82D8C5ED69364334BEE0CB33D7176008BB6E790F76`
fails the same fixture on stable at Siphon (10 instead of 1000) and on beta at
regeneration (10 instead of 1000). Reports are under `outputs/enemy-healing/`:
`baseline-stable.json`, `baseline-beta/report.json`, `candidate-stable.json`,
and `candidate-beta/report.json`.

Logging/StringName, Flash/animation, native Regen decrement and Siphon's unrelated
SteamEruption application are isolated leaves. The native healing method bodies,
production amount conversion, native enemy HBR-mode lookup, native player count
and actual HP/clamping helper execute. This is not an in-game combat test and
does not execute complete Heal command hooks, saves, rendering or multiplayer
transport. Custom monster DP restoration is covered separately by
`../hbr-enemy-dp-recovery/README.md`.

Build and run stable from the checkout root:

```powershell
dotnet build validation/hbr-enemy-healing/Checks.csproj -c Release -p:SkipModOutputCopy=true
$mod = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$hash = (Get-FileHash -LiteralPath $mod -Algorithm SHA256).Hash
dotnet validation/hbr-enemy-healing/bin/Release/net9.0/Checks.dll $mod $hash libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/enemy-healing/candidate-stable.json
```

For beta, supply `BetaNativeChecks=true`, `NativeAssemblyPath=<profile>/sts2.dll`
and `NativeRitsuAssemblyPath=<profile>/STS2-RitsuLib.dll` to the build, with
separate `IntermediateOutputPath` and `OutputPath` outside the fixture directory.
Pass the checked profile directory instead of `libs` and append
`--isolate-telemetry` when running. The bootstrap verifies the candidate hash and
each profile-manifest entry. It links the existing checked telemetry isolator:
only the exact five-byte `SentryAutoInit.Init` module-initializer call is replaced
in a separate output copy, with all other bytes verified identical and a
`telemetry-isolated/proof.json` record. Installed and profile DLLs are untouched.
