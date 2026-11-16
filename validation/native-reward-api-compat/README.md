# Native reward API compatibility

Calls the production reward factory against actual native reward constructors,
without replacing the constructors with spies. It checks predetermined relic
identity, null RNG override, no player RNG initialization/consumption, native
immutability errors, card choice count and optional synchronizer/selector-stack
defaults from the real RunManager. No game UI, reward selection, saves or random
reward population are executed.

```powershell
dotnet build validation/native-reward-api-compat/Checks.csproj -c Release -warnaserror
dotnet validation/native-reward-api-compat/bin/Release/net9.0/Checks.dll `
  <mod.dll> <expected-mod-sha256> <profile-directory> <GodotSharp.dll> <report.json>
```

Run stable and beta in separate processes with a pinned candidate and prepared
profile. The shared bootstrap validates candidate and profile hashes. Reports
record actual candidate/native hashes and distinguish native constructor checks
from rendering or gameplay execution.

## 2026-11-13 verification

Candidate SHA-256
`EA5DF3BF5C583A607F60D8AE1751F482346595E0B2C81F11C1C68781F225A5B9`:
stable v0.107.1 **15 checks passed**, beta v0.113.0 **16 checks passed**. Beta has
one additional assertion because its constructor includes a selector-stack
default; stable verifies that no such field is invented. Fixture compilation
used `-warnaserror` and produced zero warnings and errors.

Reports and logs: `outputs/game-update-compat-20261113/reward-native-release-check/`.
