# Independent multiplayer story regression

This managed host uses actual native event messages and production story choices with two independent player-owned event instances. It does not start Godot or a multiplayer session.

## Final scene/reward regression — 2026-11-15

**1299 checks pass on stable 0.107.1 and 1299 on unmodified beta 0.113.1.** Both reports use the same stable-compiled main DLL:

`A0AF2A844224D7252162191A90895859EBD9D7FEBE1E400F583E492F90CD937C`

Final reports are `outputs/garden-scene-distribution-20261115/validation/{stable,beta}/report.json`. The earlier append-only **157-check draft is not final evidence**; its reports remain as `report-append-only.json`.

The data checks read the actual compiled private garden roster, rather than a second handwritten roster:

- All 56 character IDs occur exactly once across six venues, with counts 9/9/9/9/10/10.
- Original 16 characters retain their original page. Original pages, choices, rewards and choice indexes remain exact after projecting away only newly added navigation/pages.
- The original one-time random venue assignment of the new 40 IDs stays fixed. Each new character has one sole-met page and one confirmation: one declared Sense +8 and a three-option card reward, then an original terminal page.
- Incoming menu choices give no rewards. Menus stay within the existing six-choice limit. All pages are reachable and can finish; branch previews return the exact production catalog names.
- All 242 new native page/option localization keys are read through real `LocString.GetFormattedText` from the merged production `events` table. The host does not inject JSON-derived fake localization.

The fixed baselines are `fixtures/garden-scenes-before.json` (original six definitions and three unrelated-story hashes) and `fixtures/garden-scenes-allocation.json` (the accepted static venue assignment). They travel with the validation project and do not depend on a Codex output directory. `HBR_GARDEN_SCENE_BASELINE_FILE` optionally selects a different before-definition fixture.

## Native cases

Every new character is exercised with both players through actual received-choice messages, including pagination. Each confirmation applies exactly +8 to its declared Sense, leaves the other five Senses and the teammate unchanged, and constructs an owner-bound `CardReward` with three options. Repeated native clicks and the stored stale production callback cannot grant another reward. B can independently claim the same character once; an additional six-venue case sends A and B down different new-character branches and checks exact separate encounter/reward sets.

GYM compares each owner's complete encounter CSV after Ordinal deduplication/sorting, rather than merely checking that `taki` or `karen` is present. INITIAL remains empty. Original stale-option, state-version, reward-UI-fault and personal repeat-claim checks remain. The repeat fixture preclaims every character in the selected branch.

Cinema preserves its original public `tomori` encounter on INITIAL. A new route may therefore record `tomori` plus its chosen newcomer, and no other newcomer. The original cinema route and reward remain unchanged.

Joining-ceremony cases also remain: nine conjecture outcomes, Noodles granting all six Senses +2 and a three-card offer, personal reward ownership, and HBR/native display of Ehe Chumi's maximum-DP reward. THEBEGINNING and INSIDETHEDOOR retain the shared-event route and reject personal choice messages.

## Test boundary

Real native `ToMutable`, `BeginEvent`, event synchronizer sender lookup/shared gate, `EventOption.Chosen`, pending-task drain, production page transitions, reward construction, Sense increases, encounter records, repeat claims and history writes run. Actual repository JSON is parsed and placed in the definition cache to replace Godot resource I/O. `RewardsCmd.OfferCustom` is the controlled terminal: it captures genuine rewards but does not select/add a card. StringName native construction, logging and relic Flash are isolated.

This is not a rendering, real-network, full save/load, card-materialization, map-room-transition or packaging test. The separate allocation-point/deck-limit rules have another validation suite; this host checks story rewards and ownership.

## Run

The phase convenience wrapper builds only this validation host and reads the phase's stable candidate manifest:

```powershell
& outputs/garden-scene-distribution-20261115/run-story-checks.ps1 -Profile stable
& outputs/garden-scene-distribution-20261115/run-story-checks.ps1 -Profile beta
```

For another checkout, build `validation/story-independent-choice/Checks.csproj` with `ModAssemblyPath`, `NativeAssemblyPath`, `NativeRitsuAssemblyPath` and `GodotSharpAssemblyPath` pointing at the selected DLL/profile. Invoke `Checks.dll` with:

```text
<mod DLL> <mod SHA256> <native profile directory> <GodotSharp DLL> <report path>
```

The bootstrap verifies the candidate and profile hashes before loading. Run profiles sequentially because they share project intermediate output. Current beta 0.113.1 uses the original native assembly, without telemetry rewriting. Historical 49-/68-check reports and the initial append-only draft are retained only as earlier evidence, not substituted for the final 1299-check reports.
