# Multiplayer healthbar width regression

Candidate `18CBFD50AA21476E1557A9D3EFD3985E3796AEF0894EF223C9C4582CDC69D836` passes 59 checks on stable and 59 on beta v0.111.0. Reports are `outputs/multiplayer-healthbar-width/candidate-stable.json` and `candidate-beta.json`.

The pre-change DLL is frozen at `outputs/multiplayer-healthbar-width/baseline/HBRGOSakiMod.dll`, SHA256 `B436B5839ED67193FD2B9B4E78FB9B75855BA28F16383A928B608C90C98E2433`. `baseline-stable.json` records actual execution of the native relative-width method with an HBR player's MaxHp8000, reference80 and reference width175: the old result is17500 instead of175.

This small managed fixture executes the original native `NHealthBar.UpdateWidthRelativeToReferenceValue` arithmetic body with the real production `HbrPlayerHealthBarWidthPatch` prefix. It replaces exactly two calls within that body: `Control.get_Size` returns an existing managed size, and private `NHealthBar.SetHpBarContainerSizeWithOffsetsImmediately(Vector2)` captures the result. Native `HpBarContainer`/Creature.MaxHp access and the complete width division/multiplication remain intact. The healthbar object is only an uninitialized managed carrier with its finalizer suppressed; no Godot node constructor, `_Ready`, native Godot binding, scene tree or renderer runs. This is not a screenshot or in-game UI test.

Real mutable Player/Creature models and saved manual progress verify HBR8000→175, HBR16000→350 and HBR3000→65.625 (matching old unscaled maximum30). Maximum HP doubling doubles width at the original slope. Repeated calls do not compound the reference normalization; current HP changes do not change a maximum-based width. Native legacy mode, vanilla without a manual, monsters and HBR-owned pets without an actual Player remain unnormalized. Alternate reference1000/width300 gives300 for HBR100000 and600 for HBR200000. Every call checks unchanged actual HP/max HP and height, and exactly one execution of each isolated size leaf.

Patch metadata requires one production prefix on the relative-width method and no production postfix/transpiler or patch on its private size writer. Native multiplayer caller IL retains reference80/width175. Top-bar HUD metadata retains its fixed250 width and has no call to this relative-width method. These caller/top-bar checks are metadata only. Other monster fixed-width patches and top-bar UI execution are not installed or rendered here; the monster test checks this relative-width method's unchanged native formula. HP text, fill animation, screen fit and event layout require game/Godot validation.

The project links only the existing overcharge bootstrap and strict telemetry isolator, leaving them unchanged. It adds no full combat host, save/clone suite, game process, package or installed-file changes.

Run from this checkout:

```powershell
$candidate = (Resolve-Path outputs/multiplayer-healthbar-width/candidate/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/multiplayer-healthbar-width/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/multiplayer-healthbar-width/bin/Release/net9.0/Checks.dll $candidate $digest libs $godot outputs/multiplayer-healthbar-width/candidate-stable.json
```

Beta uses the existing verified runtime profile and separate fixture build output:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/multiplayer-healthbar-width/Checks.csproj -c Release -p:SkipModOutputCopy=true -p:BetaNativeChecks=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/multiplayer-healthbar-width/bin/Beta/Checks.dll $candidate $digest $profile $godot outputs/multiplayer-healthbar-width/candidate-beta.json --isolate-telemetry
```

The bootstrap verifies candidate/profile hashes. Stable native SHA256 is `A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52`. Beta source native SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; output-local telemetry-only copy is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`. The shared isolator requires the exact Sentry initialization call+return, zeros only the five-byte call, verifies all other bytes remain identical, and records proof in the report. Original runtime/profile assemblies remain unchanged.
