# Compact hover tips

Hover tips show a short text (current values and effects) and holding Shift,
or pressing the right stick, shows the native full text. This suite checks the
short texts, their formatting, a layout estimate, and the real Harmony patches
on the stable and beta game assemblies. It does not start Godot or the game.

## What it checks

- **Localization**: every entry listed in `Core/Helpers/HbrCompactTextKeys.cs`
  has its full text and a short text next to it, under the key `CompactKey`
  returns. The short text uses only variables its full text already receives,
  plus the relic variables the hover adds (`ExtraVariables`). English has no
  short texts, so English keeps the full text. The interface entries use only
  the variables their code passes. Six-sense card attributes were removed on
  2026-11-09: no six-sense short line is listed or left in `card_keywords`, the
  removed resistance power has no short entry, and the four manual relics' short
  texts no longer tie levels to card rolls.
- **Formatting**: each short text is run through SmartFormat 3.0 from the game
  profile, with the game's extensions (the native energy icon formatter is
  stubbed). Tier variables are tried at every value: collections 0–6 kinds,
  iridescence 1–6 charges. No placeholder may remain, and the longest shown
  text must be shorter than the full text. Exact outputs are asserted for the
  31A collection tiers and iridescence charges.
- **Layout estimate**: the KillKiss case (roll, two debuffs, its power, phantom
  and the hint; its six-sense box left with the attributes) must fit one 1080p
  column (1030px), using 360px boxes,
  22px text in 293px and 30px lines. This is an estimate; the browser preview in
  `ui-concepts/hover-tips-20261106` measures the same case with the game fonts.
- **Patches**: installs the production patch classes with Harmony's class
  processor on the loaded game assembly. The nine hover call sites build their
  tips through `HbrCompactHoverTipHelper`, and no longer read `HoverTips`
  directly. A real `LocTable` reads short text only inside a compact scope,
  for prefixed copies too, and not for unlisted entries or a language without
  short texts. The keyword cache guard runs before RitsuLib's priority-800
  prefix and restores in a finalizer. The native members the full-text key
  reflects on exist, and the native tooltip `Remove` stays unpatched.

`LocManager.SmartFormat` is not patched: it has an exception filter that
Harmony cannot rebuild ("Incorrect code generation for exception block"). The
swap reads raw text in `LocTable.GetRawText` instead.

## Running

Build the candidate first, then run against the stable profile (it has
`SmartFormat.dll`) and the beta profile:

```powershell
$candidate = (Resolve-Path .godot/mono/temp/bin/Release/HBRGOSakiMod.dll).Path
$digest = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$profiles = 'D:/stsmod/worktrees/HBRGOSakiMod-beta-compat/outputs/beta-compat/profiles'
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'

dotnet build validation/hbr-compact-hover-tips/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/hbr-compact-hover-tips/bin/Release/net9.0/Checks.dll $candidate $digest "$profiles/stable-latest" $godot outputs/compact-hover-tips/stable.json

dotnet build validation/hbr-compact-hover-tips/Checks.csproj -c Release -p:SkipModOutputCopy=true "-p:NativeAssemblyPath=$profiles/beta-latest/sts2.dll" "-p:NativeRitsuAssemblyPath=$profiles/beta-latest/STS2-RitsuLib.dll" "-p:SmartFormatAssemblyPath=$profiles/beta-latest/SmartFormat.dll" -p:OutputPath=bin/Beta/ -p:IntermediateOutputPath=obj/Beta/
dotnet validation/hbr-compact-hover-tips/bin/Beta/Checks.dll $candidate $digest "$profiles/beta-latest" $godot outputs/compact-hover-tips/beta.json
```

The beta profile's `sts2.dll` is already the telemetry-isolated copy recorded
in its `profile-manifest.json`, so no `--isolate-telemetry` is needed.

Not covered: how the tooltips look in the game, holding the key in a real
session, controller input, and multiplayer. Those need the game.

## Immutable tooltip API compatibility (2026-11-13)

`HoverTipCompatibilityChecks.cs` adds 16 native checks: writable legacy keyword
IDs versus immutable native IDs, module keyword recognition, Narara card-penalty
debuff presentation without changing the real Narara Buff, unaffected ordinary
tooltips, and copied text retaining every flag, icon and canonical model.

Older runtimes expose identity/flag setters but readonly text. Newer runtimes
reverse that contract. The production helper uses cached public setters or
value copies as appropriate; it never writes readonly backing fields. Only the
new API installs the exact card-penalty `IsDebuff` getter compatibility patch.
Native constructors, structs and Harmony patches execute in these checks;
localized formatting and texture loading remain isolated host leaves.

The new 16-check group passed on stable v0.107.1 and beta v0.113.0 against the
same intermediate candidate `4FDF786B947613146C0C39BCDCE31AD7CB1C3DEBC7C62E1E28980732DCBD129B`.
That does not replace a final candidate/full-suite run or in-game rendering.
