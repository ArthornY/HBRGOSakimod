# Native death API compatibility

The fixture loads one candidate mod DLL against an explicitly supplied, hashed
game/dependency profile. It installs the real production
`HbrDeathHookCompatibility` Harmony class and invokes the **real native
`AbstractModel.AfterDeath` entry** on all nine production listeners.

Only each model's final four-argument effect body is temporarily replaced with
a Harmony spy. The spy observes dispatch count, choice context, target,
removal-prevented flag, duration and returned Task. The bridge and native method
binding are not substituted. Checks cover pending completion, synchronous and
asynchronous failure, cancellation and passthrough for a native non-mod model.
The extra native `CardModel` argument in v0.113 is supplied as null. Each model
is allocated without a constructor to avoid Godot initialization.

```powershell
dotnet build validation/native-death-api-compat/Checks.csproj -c Release
dotnet validation/native-death-api-compat/bin/Release/net9.0/Checks.dll `
  <mod.dll> <expected-mod-sha256> <profile-directory> <GodotSharp.dll> <report.json>
```

Run in a fresh process for stable and beta. Use the prepared telemetry-isolated
beta profile; this fixture does not rewrite the game DLL. Bootstrap candidate
and profile hash guards are shared with `native-damage-api-compat`.

This verifies real native entry-to-model dispatch, not the nine effects'
animations or complete death/revival combat lifecycle. Their existing gameplay
suites remain necessary; game execution is not part of this source-only check.

## 2026-11-13 verification

One candidate DLL, SHA-256
`23968A4CF6350BAD2AAD5070B0F8908520E3AD918A3BF02A89D9FCC6EBFE083C`,
passed **278 checks on stable v0.107.1 and 278 checks on beta v0.113.0**. The
fixture built with `-warnaserror`, zero warnings and errors. Both runs reached
all nine real production models, with 27 dispatch assertions per model plus
registration and native passthrough checks. The actual native death entry had
four parameters on stable and five on beta.

Logs and JSON reports:
`outputs/game-update-compat-20261113/death-native/` in the validation checkout.

The final constructor-default-corrected candidate
`EA5DF3BF5C583A607F60D8AE1751F482346595E0B2C81F11C1C68781F225A5B9`
was subsequently rerun against both profiles with **278/278 checks passed**.
These final reports are separate in
`outputs/game-update-compat-20261113/death-native-release-check/`.
