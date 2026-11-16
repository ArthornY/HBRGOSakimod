# Personal immediate-turn native checks

This standalone executable links the existing managed effect host and loads an explicitly selected mod DLL and native game profile. It does not build the mod or start Godot.

Run through `outputs/ability-merge-20261114/run-personal-checks.ps1` after the stable/beta production candidates have been built. The script compiles this fixture against each matching candidate and native profile, then writes `personal-report.json` and build/run logs alongside that candidate. The bootstrap verifies the candidate SHA256 and profile manifest before loading assemblies.

The real production paths under test are source-power request/removal ordering, serialized managed action descriptors, personal `Begin`/`Finish`/`Restore`, owner-only hand snapshots, native permanent-deck cloning and shuffling, native `PlayerCmd.EndTurn`/`SetReadyToEndTurn`/undo voting, beta's one-shot end-turn signal, stable's deferred all-ready callback, owner/pet negative turn boundaries, duration protection, OD passive charge, and the OD-to-personal queue handoff. Two independently initialized native states replay the same serialized command sequence and compare cards, energy and ready votes after every boundary.

The host explicitly isolates the unavailable engine/transport lifecycle gates (`IsOverOrEnding`, multiplayer-mode and local-player detection), visual waits, the draw animation/completion leaf (cards still move through real pile APIs), the ally-entry action leaf, and native cancel completion. Stable's full enemy transition stays deferred using its native player-setup flag. The actual Ready/UndoReady/EndTurn methods, serializer/deserializer, turn coordinator and native card clones are not replaced.

These are managed integration checks, not an in-game rendering or real multiplayer transport test. Ally/cannon entry actions use a leaf isolation here; their actual execution is outside this fixture's coverage. No PCK, distributable or Steam upload is produced.


## HBR inserted-turn start dispatch

`InsertedStartCases.cs` exercises the real serialized personal Begin/Finish and the real OD DealOverclockHand, retaining its CanContinue guards. A Draw observer confirms native SetEnergy completes before the31D Intel and Muua SummerGrace callbacks, which both finish before the first inserted Draw. Repeated callbacks/Begin/hand setup share one receipt; returning to the suspended ordinary turn does not repeat it; a later ordinary own-turn number still works. Nonparticipating teammates retain their counters, energy and hand. The original283 stable/284 beta integration run was bound to859D2B6A/785182FA; its reports are retained as personal-inserted-intel-faith-report.json in the current phase.

The74extension adds one real ExorcisingStringPower per owner and uses its actual debuff application and independent turn ledger through those same coordinator entries. This extended case must run against a candidate containing74; the previous Intel/Faith-only report is not evidence for74.
