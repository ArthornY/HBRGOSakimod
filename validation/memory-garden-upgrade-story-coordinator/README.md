# Upgrade-memory coordinator native checks

The standalone host loads an explicitly hashed stable production DLL in each original native runtime profile, without Godot. The target is the local coordinator, not a reimplementation of the story rules.

It executes real `HbrMemoryGarden.ChooseUpgrade`, upgrade-offer RNG, native `CardCmd.Upgrade`, shared-copy progression, the actual local JSON receipt store/run-key facade and the registered native `CombatEnded` callback. Four real asynchronous caller bodies are checked for their common `ChooseUpgrade` call; this does not simulate all four complete source UI flows.

The host controls choice synchronization terminals, the catalog's resource-text input, local UI readiness and the reader's completion result. Temporary test dialogue is supplied only through that controlled resource input; it is not shipping story content. The real parser still validates it. A pending reader Task proves the synchronized upgrade returns before local reading finishes. The host checks actual levels 1–7, initial acquisition, shared copies, reloaded local receipts, All/Unseen/Off, missing text/viewpoint, another owner's upgrade, failed upgrades, battle deferral, safe-UI wakeup and stale run/room cleanup.

Run/load cleanup invokes the production cleanup method; this host does not load a full game save. UI readiness and result controls do not establish visual rendering or live multiplayer behavior. Existing linked fixture code may produce nullable warnings; production strict SDK build results are separate.

Use `outputs/memory-garden-upgrade-stories-100-20261116/coordinator/run-native.ps1 -Candidate <absolute DLL> -ExpectedSha256 <SHA> -ReportPrefix <unique name> -BuildHost`. The stable/beta projects run sequentially and preserve previous reports. Runtime assemblies are checked against their original profile manifests, without telemetry rewriting.

Validated candidate `32A04B821745FB8ACC6369743B206459C734E5D289B79993629844A8EC7F2A87`: 121 checks per native profile, 242 total, both process exits 0. The final index is `coordinator/final-index.json` under the output stage above. Host builds have 23 inherited nullable warnings and 0 errors; production SDK strict build logs are separate. Earlier fixture-development reports are retained.
