# Managed runtime compatibility audit

This source-only tool loads an existing mod DLL against explicitly supplied game
and dependency DLLs. It records hashes and identities, resolves compiled external
type/member references, evaluates every Harmony target factory, checks injected
parameters, and optionally installs the actual patch classes. It does not launch
Godot, export a PCK, modify the installed game, or access saves.

```powershell
dotnet run --project validation/beta-compat/Checks.csproj -- `
  --mod <candidate/HBRGOSakiMod.dll> `
  --game <reference/sts2.dll> `
  --dependencies <matching-dependencies-directory> `
  --runtime <game/data_sts2_windows_x86_64> `
  --godot <GodotSharp.dll> `
  --report <output/report.json> `
  --isolate-telemetry --install
```

`--isolate-telemetry` is needed for the beta's managed module initializer. The
tool accepts **only** a six-byte `<Module>..cctor` body consisting of a direct
call to `SentryAutoInit.Init` followed by `ret`. It creates an output-only copy
beside the report with that one call replaced by NOPs, validates that every byte
outside this body is unchanged, and records both hashes and the exact byte range.
The original reference DLL is never written. Omit this option for game versions
without that initializer.

`--install` runs Harmony's real class processor and verifies each class appears
in Harmony's registered patches. `--patch-filter <regular-expression>` limits
installation (target discovery still audits every class). Use a fresh process
for each game version. An incremental report is written before installation and
after every class, so a native-boundary crash has a last-known class.

`--expected-mod-sha256 <hash>` rejects a different candidate before loading it.
`--extra-probe <directory[;directory]>` adds optional assembly resolution roots.
`--embedded-resource HBRGOSakiMod.HextechCompat.dll` audits that exact embedded
assembly instead, recording the container and extracted hashes; extraction is
output-only. Optional plugin reference stubs can establish game API binding but
cannot establish compatibility with the actual optional plugin runtime.

Some game methods require native Godot static initialization even just to patch
them. Such installation failures are reported separately from target/signature
failures, and are not an in-game compatibility result. The tool intentionally
does not fabricate native Godot behavior. `Prepare=false` is respected by the
real Harmony processor and is reported as a skipped class. Metadata references
whose generic context cannot be instantiated are reported as unresolved context
reviews rather than silently counted as passing.

The report contains `ok`, issue lists, per-class targets/parameter checks,
installation results, external member references, dependency hashes, reflection
loader failures, and native hook signatures (including Doom and damage caps).
Exit code 1 means a confirmed issue; 2 means tool configuration or load failure.
Successful resolution and installation do not execute complete combat/card
flows, transpiler behavior, rendering, networking, or full save serialization.
