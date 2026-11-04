# Independent multiplayer map-story regression

Managed fixture for the six noncombat map memories. It runs the real native received-message handler and production story choices with two player-owned GYM event clones. It does not start Godot or a multiplayer session.

Candidate `4F03A8992BF32B4C443E675C2DF5305CA4BC83391525B354D2E0B572D66D8FDD` passes **49 checks on stable and 49 on beta**. Frozen baseline `2CF1FE4A167353373B7975C908035684958419C37C5DA6FCA08B1F030733C588` fails after its one initialization check: the actual `EventSynchronizer.HandleEventOptionChosenMessage` rejects the first GYM personal-choice message with `Received OptionIndexChosenMessage during a shared event!`.

Reports, relative to the repository root:

- `outputs/story-independent-choice/baseline-stable.json` — expected routing failure.
- `outputs/story-independent-choice/candidate-stable.json` — 49 passing checks.
- `outputs/story-independent-choice/candidate-beta.json` — 49 passing checks.

## Exercised behavior

- Actual native `ToMutable` and `BeginEvent` create two distinct owner states. The harness invokes `HandleEventOptionChosenMessage` with each sender's real NetId; native `IsShared`, sender lookup, slot lookup, `EventOption.Chosen`, pending-task collection, and `AwaitPendingOptionTasks` remain intact. It never forces the private personal-choice branch.
- A selects TAKI, B selects KAREN. Real production `Choose` and `ApplyRewards` grant A eight LeadershipCharm increases and B eight Madness increases. Real encounter recording stores only `taki` for A and `karen` for B. A can finish while B stays on KAREN, then both finish after their own choices.
- The reward UI leaf captures actual owner-bound `CardReward` objects, each with three options. No card choice or inventory addition is simulated.
- Stale native options cannot grant another reward. A separate direct call to the old option's stored production callback bypasses only native WasChosen and proves the production state-version guard preserves both the page version and the current option instance. A controlled reward UI fault propagates, production `finally` clears its in-progress flag, and the native already-chosen option stays inert. This does not claim retry completion or reward rollback.
- Actual native history objects and per-player history writes run. An existing personal repeat claim lets A finish while B receives its own pending repeat-reward page. Opening that page changes neither claim string. The current history node alone does not count as a prior visit. Repeat-card selection/upgrades are outside this fixture.
- All six real map definitions are personal and contain no combat choices. THEBEGINNING and INSIDETHEDOOR remain shared; the native personal-message receiver rejects both.

## Host boundary

The harness supplies the registered character/event IDs, alive player creatures, actual manuals, RunState/RNG, and canonical/mutable story instances. Actual repository JSON is parsed by `HbrStoryDefinition.Parse` and preloaded into the real definition cache to replace Godot packaged-file reads. Actual localization tables initialize native LocManager for native option construction/history. Relic Flash, logging, StringName native construction, and the `RewardsCmd.OfferCustom` selection UI terminal are isolated. Story routing, reward construction, Sense changes, encounter/claim state, native history writes, and page/finished state are not replaced.

The same stable-compiled harness runs against both actual runtime assemblies. Reflection supplies beta's additional `EventCombatSynchronizer` argument to native `BeginEvent`; beta uses the existing verified runtime profile and strict telemetry-only static-constructor isolation. Original beta sts2 SHA256 is `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`; isolated copy SHA256 is `0C0C1CC3DD3FFF8E2D10BDE4FDA76A5E7743402047B518B4B188A7ED8B65BFAA`.

No network transport/message-buffer location filtering, real peers, game-room completion/transition, UI rendering, full save/load, card materialization, packaging, or installed-file changes are tested.

## Run

From the repository root in PowerShell (requires the existing stable libraries, beta profile, extracted native localization tables, and installed managed SmartFormat dependencies):

```powershell
$hash = '4F03A8992BF32B4C443E675C2DF5305CA4BC83391525B354D2E0B572D66D8FDD'
$mod = 'outputs/story-independent-choice/candidate/HBRGOSakiMod.dll'
$godot = 'C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll'
dotnet build validation/story-independent-choice/Checks.csproj -c Release -p:SkipModOutputCopy=true
dotnet validation/story-independent-choice/bin/Release/net9.0/Checks.dll $mod $hash libs $godot outputs/story-independent-choice/candidate-stable.json
dotnet validation/story-independent-choice/bin/Release/net9.0/Checks.dll $mod $hash outputs/hotaru-overcharge/beta-profile $godot outputs/story-independent-choice/candidate-beta.json --isolate-telemetry
```

The bootstrap validates the candidate hash, profile manifest, and all profile DLL hashes before loading. Both runtimes use the same production DLL. To reproduce the old routing failure, use the baseline snapshot/hash and `baseline-stable.json` as the output path; a nonzero exit is expected.