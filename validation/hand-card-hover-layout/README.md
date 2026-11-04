# Hand-card hover-tip layout regression

This small managed fixture runs production `HandCardHoverTipLayoutHelper.ComputePlacement` and installs the real hand-card alignment Harmony patch against stable and beta v0.111.0 native metadata. Both profiles pass93 checks against candidate `89AE465F1CCFAB252D5C9327F58C2F953D1687BB2164417B0338F88331DF04B7`.

The bug-report geometry is modeled as viewport1920x1080, cardAABB(985,630,340,500), native tooltip width380, gap12 and margin16. Production geometry returns Bounds(593,16,380,1048), Scale1: a full-height left sidebar inside the safe viewport, separated from the card. These are model inputs/results, not a rendered screenshot or live UI measurement.

Geometry cases cover left/right edge switching, above/below fallback at700-pixel viewport width without shrinking fonts, extreme300-pixel width with one uniform width scale, translated viewport origins, uniform input-coordinate scaling, already-computed rotated-card AABBs, offscreen cards, negative gap/margin clamping, invalid/unreadable inputs and repeatable calls. Every valid result is checked against the viewport's safe margins and reserved card gap.

Metadata checks verify the native instance-field shapes `_followOwner: bool`, `_isHovered: bool` and `_draggedHolderIndex: int`, then install exactly one production postfix on native `NHoverTipSet.SetAlignmentForCardHolder`, verify the hand-holder subtype gate and Godot child-node inheritance, and confirm this feature does not patch native `Remove`. StringName construction and editor detection are isolated leaves. No Godot node is instantiated and no native alignment body or input event is dispatched. The fixture does not establish rendered entry count, scrollbar/wheel behavior, click/drag routing, cleanup, live transforms or gameplay; those require a game/Godot session. It creates no screenshots, package or installed-file changes.

Stable invocation from this checkout:

```powershell
$candidate = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
dotnet build validation/hand-card-hover-layout/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hand-card-hover-layout/bin/Release/net9.0/Checks.dll $candidate $digest libs C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hand-card-hover-layout/candidate-stable.json
```

Beta uses the verified runtime profile and checked telemetry-only output copy:

```powershell
$profile = (Resolve-Path outputs/hotaru-overcharge/beta-profile).Path
dotnet build validation/hand-card-hover-layout/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:NativeAssemblyPath=$profile/sts2.dll" "-p:NativeRitsuAssemblyPath=$profile/STS2-RitsuLib.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hand-card-hover-layout/bin/Beta/Checks.dll $candidate $digest $profile C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll outputs/hand-card-hover-layout/candidate-beta.json --isolate-telemetry
```

The project links only the existing overcharge bootstrap and telemetry isolator; it leaves those files unchanged. Candidate and runtime-manifest hashes are verified before loading. Reports preserve geometry coordinates, successful assertions, any error, candidate/native hashes and beta source/copy isolation proof. Generated reports and runtime copies stay under `outputs/hand-card-hover-layout/`.
