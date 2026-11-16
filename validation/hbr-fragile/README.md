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
duration; expiry at the second actual owner turn end; existing Unfading Scars; per-instance
saved-property roundtrip and real native clone isolation; positive native amount
changes copying the exact effect; real DoublePowers effect/duration doubling
and Weakness Strike effect-only doubling in the existing slots, including
Artifact/Narara rejection and unchanged provenance; compatibility strength counts; old-save
lifecycle migration; additive target debuffs; unchanged repeated previews;
shared actual/preview damage; final flooring and caps; and native localization
formatting with one decimal place for display. Disabled HBR mode retains its
original stacking, duration and fixed 50% effect.

Narara regression cases: nullification is switched off with the six-sense card
attributes (2026-11-09). An older nullification beside full old resistances is
inactive, the actual `Begin` / `CaptureTargets` / `Close` scope captures no
target, no card is nullified, and inside the card's play a stronger application
replaces the weakest instance as usual. No scope target
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

Scars cases install the actual native Weak/Vulnerable postfixes. HBR mode keeps
native Weak/Vulnerable effects and natural duration decay unchanged, while a 25%
enhancement changes 30% Fragile to 37.5% and protects Fragile duration. Native
Vulnerable multiplies independently with the additive Fragile bucket. Legacy
mode retains final effect multiplication after PaperKrane/PaperPhrog, Debilitate
and Cruelty, pooled upgraded copies and natural-decay protection: 50% Vulnerable
becomes 62.5%, and 25% Weak reduction becomes 31.25%.

## Cached beta runtime regression

The PowerCmd application and modification signatures, all direct Fragile
lifecycle callbacks, giver/receiver hooks, and `CombatHistory.PowerReceived`
are asserted explicitly. Their signatures match stable 0.107.1 and the cached
public-beta 0.111.0 snapshot, so Fragile needs no additional production adapter.
The same stable-compiled mod DLL must be used for both runtime runs.

The fixture itself can be built against either native DLL. Beta adds a required
`CardPlay.Player`, a separate `CombatTurnState` / `CombatStateTracker`, and a
lock around the extra-turn participant snapshot. The beta setup initializes
those actual structures; it does not replace combat-ending or duration logic.
The fixture's three custom native damage hooks use the appropriate five/six
parameter override for the selected runtime. Existing production
`HookCompatibility` and `DamageModifierCompatibility` dispatch real native
hooks, including the real Scars Harmony postfixes.

```powershell
dotnet build validation/hbr-fragile/Checks.csproj -c Release `
  -p:ModAssemblyPath='<same stable-compiled mod DLL>' `
  -p:NativeAssemblyPath='<isolated beta sts2.dll>' `
  -p:NativeRitsuAssemblyPath='<cached beta STS2-RitsuLib.dll>' `
  -p:BetaNativeChecks=true

$env:HBR_NATIVE_PROFILE='cached-beta-0.111.0'
dotnet validation/hbr-fragile/bin/Release/net9.0/Checks.dll `
  '<same stable-compiled mod DLL>' '<candidate SHA256>' '<beta dependency directory>' `
  'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
```

For stable, omit `BetaNativeChecks` and the two native assembly overrides. Save
`report.json` separately after each run; the next run replaces that file.

The frozen beta 0.111.0 source SHA256 is
`0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`.
Managed execution uses the known telemetry-isolated test copy SHA256
`0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`.
Its module initializer's five-byte call to `SentryAutoInit.Init` at file offset
585 is replaced by five nops; the following return and every other PE byte are
identical. This excludes telemetry initialization only and does not alter any
PowerCmd, Hook, damage, Artifact, save, duration, or history method. Source and
copy hashes, exact changed offsets, and original/replacement IL are verified in
`outputs/fragile-beta-runtime/boundary.json`; original dependencies stay intact.
The parent runtime audit separately confirms this frozen 0.111.0 is the current
public-beta target. The same candidate also runs with the prepared
`stable-latest` and `beta-latest` dependency profiles: BaseLib 3.4.7 and Ritsu
0.6.10's facade plus Runtime / Shared / Ui / Settings assemblies. Keep compile
references at the matching original monolithic Ritsu API; runtime resolution
uses the prepared profile directory and follows the actual type forwarders.
These results do not establish compatibility with future beta updates or a
complete live multiplayer session.

The same fixture also covers the changed damage-cap bridge and Doom predicate:
actual cannon self-immunity enters the complete native damage hook; a real
switched-off Narara nullification caps nothing, even inside a card's own play,
and the full native hook damages the boss. The actual production Doom Harmony
patch resolves the stable public and beta private predicate. Both direct native
predicate calls and `DoomPower.GetDoomedCreatures` read a saved five-billion HP
ledger while the visible native HP proxy is one, then verify exact threshold
edges and ordinary-creature fallback. These tests evaluate the real predicate
and public filter; they do not execute Doom's kill animation or death command.

The percentage Strength/Dexterity maintenance candidate
`724860094E42C5D9F719203E5C72C6D925ED6776CBA781E79ED3337A9D5F9B91`
passes 651 checks on stable 0.107.1 and 651 on beta 0.111.0 with the prepared
current dependency profiles. Both fixture builds have zero warnings and errors.
This changes one expected value: a level-seven phantom with base INT/LUCK 262
and three Dexterity layers now has INT `262 × 1.03 = 269.86`, so its one-strength
Fragile effect is exactly `33.4386%`, replacing the old flat-stat `33.39%`.
The original independent duration, save, copy, damage, and native-status checks
remain in place. Reports for this candidate are preserved separately at
`outputs/percent-stats/fragile-validation/stable/report.json` and
`outputs/percent-stats/fragile-validation/beta/report.json`; the earlier results
above remain historical records. This run retains the same managed-fixture
boundaries and does not start the game, package, or upload.
