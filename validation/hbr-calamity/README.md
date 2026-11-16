# HBR Calamity validation

The focused fixture verifies a hash-checked candidate against the selected native stable/beta profile. It reuses the managed setup from `hbr-fragile` without running that suite.

Current original-game policy is **None / EnemyTurnEnd 2**. Every application creates its own native instanced power. All active fixed reductions add, there is no two-instance cap, and weaker/equal applications never refresh or replace older clocks. Player and enemy holders both count enemy-side ends; turn starts and player-side ends do not count. Native amount mutations change only the chosen strength, while the actual Double card doubles that strength and its own remaining duration.

Coverage includes real generic/non-generic `PowerCmd.Apply`, normalized `ModifyAmount`, Artifact receiving/consumption, switched-off Narara scope, independent clocks and expiration, native clone plus saved-property value roundtrip, the actual Double card, all six allied stats and enemy E, owner-plus-local cannon reduction, live cannon block preview/Gain writer and Happiness's extra Strength ordering. Native Doom and the original native Calamity buff remain distinct.

Build `Checks.csproj` with `ModAssemblyPath`, `NativeAssemblyPath`, `NativeRitsuAssemblyPath` and `BetaNativeChecks` for the selected profile. Run the built executable with candidate DLL, SHA256, dependency profile and GodotSharp path. It writes `report.json` beside the executable. Current per-profile evidence is under `outputs/hbr-effect-framework-20261114/{stable,beta}/calamity-checks/report.json`; each report records the actual candidate/native hashes.

Only managed fixture-world construction, listener enumeration, logging/StringName and sound/VFX leaves are isolated. Saved-property checks do not simulate a complete native save-file load. No Godot scene, rendered UI, multiplayer session, packaging or publishing is performed.

Final verification on 2026-11-14: both native profiles passed **119 checks** after loading the final candidate.

| Profile | Candidate SHA256 |
| --- | --- |
| stable | `495E550A68EE2000E5D5F4C56ED8FFC1FD22FF338FB553D014349D541C53903B` |
| beta | `597318EB2C34FD2E566F3331B84EF160DF5336F059BB6811E2180A98B68CCB0D` |
