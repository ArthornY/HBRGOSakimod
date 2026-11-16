# Player intent refresh guard

Remote `.113.1` logs show `Only valid on monsters.` from native `NCreature.UpdateIntent` through `CombatPreviewRefreshHelper.RefreshCreatureIntent`. Combo application/reservation changes and Noblesse's self AttackUp all reach this shared helper with a player entity.

The fix is confined to that helper: a combat player uses its existing `RefreshCombatState` coalesced notification before any scene lookup; monsters retain the original direct intent update, including allied summoned monsters. Detached/null entities and invalid/off-tree/missing nodes retain harmless returns.

This small host executes the actual production `RefreshCreatureIntent` and `RefreshCombatState`. It controls only scene/node lookup, Godot validity/tree terminals and the native state-tracker notification. Actual native `NCreature.UpdateIntent` keeps its original `Entity.Monster` check, original exception string, constructor and throw IL. Its rendering tail after that guard is replaced by a recording Task terminal, so the native player guard genuinely still throws. `CombatState.GetOpponentsOf` remains real.

Positive cases check player completion and notification without node access, enemy opponent targets, allied summoned-monster refresh, and all existing null/detached node boundaries. The released DCEF negative runs the actual old helper through an indexed player node into the actual retained native guard and reproduces the reported exception. No whole refresh helper is skipped. No full card OnPlay/Apply simulation, game engine, UI rendering, card drawing, game-action queue or multiplayer transport is claimed.

The same explicit stable candidate is dynamically loaded in original stable 0.107.1 and raw beta 0.113.1 profiles. Bootstrap verifies candidate and every runtime-manifest file hash. Reports and reusable compiled hosts are under `outputs/card-turn-stall-1131-20261115/preview-intent/{stable,beta}/`.
