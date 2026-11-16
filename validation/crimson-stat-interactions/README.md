# Crimson and native stat interactions

This independent managed fixture supplements the broad stat suite and the direct Crimson/Focused card checks. It loads a selected mod DLL and matching native game profile; it does not build production code or launch Godot.

Run `outputs/crimson-stats-20261114/run-interaction-checks.ps1` after stable/beta production candidates exist. Reports and logs are written under each profile's output directory. The bootstrap verifies candidate and native-profile hashes.

The checks exercise these interactions:

- Virtual Crimson from the external dual-swords rune, plus simultaneous real Crimson, in HBR and legacy arithmetic.
- Owner and cannon-local Calamity before final Crimson doubling; separate HP/DP attack weights; Happiness Lv.3's extra positive Strength; signed penalties; enemy Dexterity's fixed ignore after the stance; zero-stat clamping.
- Independent phantom buffs and the phantom's own Crimson, rather than inheriting the summoner's native powers.
- Sakura's unchanged percentage defense, final Crimson zeroing, and unchanged stored HP/DP pools.
- Actual native temporary Strength/Dexterity application and expiry with negative base layers.
- Actual native `CreatureCmd.GainBlock`, native `AfterBlockGained` dispatch and native Strength application at HBR 48-point and legacy 12-point thresholds, including combined Wisdom/Focused/Crimson modifiers.

The explicit isolation leaves are external rune availability, the inactive headless combat-ending gate, sound/VFX and visual waits. Native block, FocusedShot, power application and temporary-power lifecycle are not replaced. Cannon scope checks use the production command-context adapter and real damage conversion; they do not render or fire an animated cannon action. Direct stat read/removal checks supplement the separate card/turn lifecycle suite.

The final reviewed candidates passed 84 assertions on each native profile. No production edits were needed from this independent review. These managed checks do not substitute for an in-game rendering or multiplayer transport test.
