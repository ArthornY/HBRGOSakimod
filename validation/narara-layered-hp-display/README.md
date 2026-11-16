# Narara four-HP display and protection

This fixture installs the final candidate's real HP fill prefix on native NHealthBar.GetFgWidth(int,float), calls its actual text postfix, and exercises the read-only exact layer helpers. It checks current-quarter HP/capacity and four-to-one reserve counts without changing the underlying total HP or native survival proxies.

Gauge cases cover normal and odd integer totals, every remaining-bar count, partial bars, absolute poison-preview deltas, large HP and 2^64 values. Legacy enemy Narara retains the intrinsic four-bar UI; allied phantoms and unrelated monsters retain their existing single-bar ratios. The existing third reserve offset is reused without new art or layout geometry.

The real native foreground-refresh body also runs with only its Godot measurement/visibility/offset leaves controlled. Doom's absolute total-HP threshold is projected separately from poison's current-HP-minus-damage preview, including a large-HP case where the native proxy would falsely appear doomed. Poison cannot mark total death while reserve HP bars remain. Doom and explicit native Kill retain their existing immediate-death semantics: Doom at least as large as the true total remaining HP can still be lethal. The poison contribution to that future Doom threshold is limited to the current HP bar.

Real power getters, native hover construction, player-dispel policy, undispellable footer/explanation, compact key selection and static manual references run against staged Chinese/English powers tables. Both the actual CustomID and compatibility aliases are checked. Old TourGuide HP thresholds and DP one-layer-per-turn claims are checked as removed.

The fixture seeds HP ledgers and proxies for display checks; it does not validate gameplay HP writes, damage scopes, healing or save transitions. Those are covered separately by the gameplay suite. MegaLabel.SetTextAutoSize captures the final text instead of invoking the renderer, Power.Icon loading is skipped, and the established shared host isolates its usual terminal leaves. NinePatchRect margins and native width measurements are fixed rendering inputs, while the candidate's actual arithmetic, Doom/poison predicates and original native refresh branches execute. No Godot UI, game, package, installation, network or save changes occur.

```powershell
$candidate = 'D:/absolute/frozen/HBRGOSakiMod.dll'
$profile = 'D:/absolute/native-profile'
$locales = 'D:/absolute/staged-files/HBRGOSakiMod/localization'
$out = 'D:/absolute/display-output'
$sha = (Get-FileHash -LiteralPath $candidate).Hash
dotnet build validation/narara-layered-hp-display/Checks.csproj --nologo `
  -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$profile/sts2.dll" `
  -p:NativeRitsuAssemblyPath="$profile/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$profile/BaseLib.dll" `
  -p:HarmonyAssemblyPath="$profile/0Harmony.dll" `
  -p:OutputPath="$out/bin/" -p:BaseIntermediateOutputPath=obj/
dotnet "$out/bin/Checks.dll" $candidate $sha $profile `
  C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll "$out/report.json" $locales
```

For stage-only compilation add `-p:RepositoryRoot=D:/stsmod/HBRGOSakiMod`. For beta add `-p:BetaNativeChecks=true`, separate outputs and runtime `--isolate-telemetry`. The bootstrap verifies every file in profile-manifest.json and the exact candidate SHA. Telemetry isolation changes only the established exact Sentry initializer call in a copied native DLL; the profile itself remains untouched.

The inherited effect/stat hosts currently have 23 nullable warnings. This fixture adds none; main-mod strict compilation is a separate check.
