# Phantom Familiarity regression

Standalone native/model regression for the30A「习以为常」change. It links the existing `hbr-resource-scale` Fixture/phantom-roster helpers; no production implementation is copied into the test.

Accepted stable-built candidate: `F3D18EBB160B05F25D34DD2FE98B88A6D200C1C229083C4544FD7A78E2645238`. The same DLL passes79 checks on stable0.107.1 and79 on beta0.113.1. Both independent host builds have0 warnings/0 errors. Reports and source-drift checks are in `outputs/phantom-familiarity-20261115/validation-summary.json`. Reproduce with that stage's `run-checks.ps1 -Candidate <absolute DLL> -Sha256 <SHA>`; it verifies native profile manifests and builds the profiles serially.

Coverage:

-36 direct combinations of real shared helper inputs: absent/1/2/negative/int.MaxValue stacks with negative, zero, ordinary and maximal awards. Zero/negative awards stay0; one layer doubles and two triple; results saturate at the existing1,000,000,000 experience cap.
-Real active-roster and all-roster award methods populate the actual pending receipts. Changing/removing the power before real `AfterCombatEnd(null)` does not multiply queued values again. A second battle-end call does not grant them twice.
-Real minion-sacrifice active/pending, inactive/direct and late-completion/direct branches; zero/negative minion count guards.
-Real combat consumption and repeated-fusion methods use the multiplier once. These run with no live phantom and one effect, so they need neither a rendered effect replacement choice nor a phantom stat/VFX synchronization leaf.
-Real out-of-combat manual `TryUpgradeRosterEntry` and `TryDevourRosterEntries` retain their original4 and8 experience even with20 Familiarity stacks.
-Real native card upgrade keeps Innate; two actual card `OnPlay` executions call native PowerCmd and leave Counter stacks1 then2. Real PowerCmd.Remove restores unbuffed shared-helper behavior.
-Actual `ModelDb.GetId` values are `FAMILIARITY_HBRGO_SAKI` and `PHANTOM_FAMILIARITY_POWER`; exact zhs title/description keys exist and state doubling /100percent per layer. No guessed namespace alias is substituted for the native IDs.

The combat lifecycle booleans are controlled to exercise in-combat versus out-of-combat branches without constructing a game scene. Native StringName/log/audio/visual/wait/history terminals are isolated. Roster serialization, award/commit/fusion arithmetic and card/power command bodies remain real. This is not a Godot rendering, complete combat, network or complete save-file test. Earlier fixture failures (missing PlayerCombatState, then missing native CombatManager state behind IsEnding) are archived under the stable report directory; both were fixed in the host without production edits.
