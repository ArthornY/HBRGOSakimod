# HBR Fragile instance checks

This fixture loads the exact candidate DLL by SHA256. It executes the real
native generic and non-generic `PowerCmd.Apply`, independent duration ticks and
expiry, Artifact received hooks, the shared native damage hook, and the actual
Strongest Shield damage wrapper and raw-damage ledger.

An application with authored strength `n` has base effect
`30 * 1.25^(n - 1)` percent. A positive attribute difference adds its 0.001
coefficient after that conversion. Each application lasts two owner turns,
independent of its strength, and at most two instances coexist. A new effect
that is at least as strong as the weakest replaces that weakest instance;
equal effect ties choose the shortest remaining duration. Weaker applications
are discarded. Separate applications keep their boundaries: two strength-one
applications total 60%, while one strength-two application has a 37.5% base.

Coverage includes source ownership and snapshots for the hero, phantom and
cannon; enemy, missing-source and card-owner fallbacks; independent effect and
duration; native first-tick protection; existing Unfading Scars; per-instance
saved-property roundtrip and real native clone isolation; positive native amount
changes copying the exact effect; compatibility strength counts; old-save
lifecycle migration; additive target debuffs; unchanged repeated previews;
shared actual/preview damage; final flooring and caps; and native localization
formatting with one decimal place for display. Disabled HBR mode retains its
original stacking, duration and fixed 50% effect.

Narara regression cases register and attach a real Ritsu SixSense capability,
create an active boss with real resistance, and use the actual `Begin` /
`CaptureTargets` / `Close` scope. They exercise the native apply entry for the
captured card, source-less effects, unrelated cards and cleanup. No scope target
is inserted by the fixture. Autoplay cases use the real `Suppress` scope and
observe native `PowerIncreased` silence, removal events and `AfterRemoved`.
A counted `Cmd.CustomScaledWait` leaf confirms that scoped new applications and
replacements make zero wait calls, while unrelated and later sources keep both
normal remove/apply waits.

```powershell
dotnet build validation/hbr-fragile/Checks.csproj -c Release `
  -p:ModAssemblyPath='<absolute candidate DLL>'

dotnet validation/hbr-fragile/bin/Release/net9.0/Checks.dll `
  '<absolute candidate DLL>' '<candidate SHA256>' libs `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

`bin/Release/net9.0/report.json` records every assertion and the candidate,
native assembly and localization hashes. StringName construction, logging and
hook-listener enumeration are isolated leaves; TestMode skips animation waits.
`Cmd.CustomScaledWait` is a controlled counted leaf only during the autoplay
cases; it is not replaced for the rest of the fixture.
Property roundtrip tests saved values rather than a full native save-file load.
This does not start the game, render UI, package or upload.
