# Additive skill descriptions and native hover references

This fixture links the established effect-skill/stat-power hosts and installs the final candidate's actual additive description, final-text and hover-scope Harmony patches. It uses the final staged Chinese and English locale files, preserving the accepted native and mod tables underneath, and records each staged table SHA. Real LocString variables, SmartFormat, native FromPower overrides, Tainted affliction nesting and the VitalSpark hover chain execute.

Coverage includes final Amount3/6 overrides, canonical per-layer rules, unchanged mutable/canonical counters, negative/zero fallback, real nested legacy power and attached-affliction hovers, restored parent scopes, native Accuracy/Calcify/PhantomBlades base/upgraded cards, FakeStrikeDummy/MysticLighter owned and merchant scopes, fallback-only locale rejection, and the separate PhantomPollution expiry/reference rule.

The immutable stable native profile gives PhantomBlades 9/12 units (150%/200%). Beta has its own native balance of 8/11 (133.33%/183.33%); its original OnUpgrade adds three. The fixture asserts those explicit profile values, logs original upgrade instructions and checks that display preserves the same raw values.

The accepted source has no English mod actor-title tables; English full smart hovers use native Ironclad/Osty actors with their real English labels and the same actual HBR manual state. It also lacks the old English PhantomPollution title/description: its positive HBR Description is tested directly, while the full title-bearing tooltip and legacy manual run only in Chinese. The English static HBR reference is checked directly. No missing old translation is synthesized.

Only shared-host terminal/logging/StringName leaves and Power.Icon resource loading are controlled. This is source/native localization verification, not a complete Godot visual or multiplayer/save acceptance test. The common host contributes its 23 existing nullable warnings; this fixture adds none.

```powershell
$candidate = 'D:/absolute/frozen/HBRGOSakiMod.dll'
$profile = 'D:/absolute/native-profile'
$locales = 'D:/absolute/staged-files/HBRGOSakiMod/localization'
$out = 'D:/absolute/display-output'
$sha = (Get-FileHash -LiteralPath $candidate).Hash
dotnet build validation/additive-skill-display/Checks.csproj --nologo `
  -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$profile/sts2.dll" `
  -p:NativeRitsuAssemblyPath="$profile/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$profile/BaseLib.dll" `
  -p:HarmonyAssemblyPath="$profile/0Harmony.dll" `
  -p:OutputPath="$out/bin/" -p:BaseIntermediateOutputPath=obj/
dotnet "$out/bin/Checks.dll" $candidate $sha $profile `
  C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll "$out/report.json" $locales
```

For stage-only compilation pass `-p:RepositoryRoot=D:/stsmod/HBRGOSakiMod`. For beta add `-p:BetaNativeChecks=true`, separate build outputs and runtime `--isolate-telemetry`. The bootstrap verifies the complete `profile-manifest.json`, exact candidate SHA and the established telemetry-isolation proof; it never changes profile source assemblies.
