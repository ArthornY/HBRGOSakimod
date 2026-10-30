# Automatic cannon hits and Personal Hive

This fixture checks the real native `PersonalHivePower.AfterDamageReceived`
callback with the production `PhantomCardRecipientPatch`, actual Dazed model
creation, and the automatic turn-origin scope. It uses production cannon damage
`Begin`/`Close` context matching with seeded issuing-intent metadata.

Manual cannon hits, including a null card source, still request the native two
Dazed cards for this two-layer hive. Automatic start/end cannon hits, nested
extra/reaction hits and continuations across `await` suppress those requests.
Ordinary player damage, other owners, prior phantom handling, nested scopes,
exceptions and a concurrent manual async branch are checked separately.

The fixture controls only card-pile insertion, preview/wait and Godot/logging
leaves. The native callback and its Dazed creation are real; a complete native
damage command and full Millsage turn are outside this fixture's boundary.

```powershell
dotnet build validation/hbr-auto-cannon-hive/Checks.csproj -c Release `
  -p:ModAssemblyPath='<candidate DLL>'
dotnet '<fixture Checks.dll>' '<candidate DLL>' '<SHA256>' `
  '<runtime profile directory>' '<GodotSharp.dll>' '<report.json>'
```

The candidate
`2D674B89E936C859F7185ED8E8B6FD5BAFB8A988B680CE702D7C6697F5F1652C`
passes **70 checks on stable and 70 on beta**, using the same stable-built mod
DLL. Reports are at
`outputs/od-auto-cannon/hive-validation/{stable,beta}/report.json`.

No game process, rendered UI, multiplayer session, package or upload is used.
