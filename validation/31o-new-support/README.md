# 31O support-card validation

This focused fixture links the existing managed effect/stat host and runs `NewSupportCardCases`. The bootstrap verifies the candidate DLL and native profile hashes before loading them.

It executes the actual five new card bodies and upgrades, Live's native Charge/gold/buff application, the common effect-instance application and side-end lifecycle, native Morale amount hooks, live six-dimensional stat reads and the shared original-HBR critical damage path. Attack animation/execution and draw terminals, power texture loading, repaint notifications and waits are explicit managed leaves; this is not a rendered game or multiplayer test.

The new damage coefficients deliberately use the same authoring units as Strike and the named comparison cards: Arpeggio is five/seven native units (base 5000/7000), Divine Craft eight/eleven (base 8000/11000), before target E, attributes, criticals and later modifiers. Rate-up and Live snapshot Wisdom using the shared reference-100 linear buff formula; critical-damage up remains fixed and enters the original critical factor `1.5 + sum / 100`, rather than multiplying a separate `1 + sum / 100` factor. Exact numerator/divisor application protects the final floor at tiny critical boundaries. Morale adds five raw points per level before percentage attribute buffs, Calamity and final Crimson zero/doubling.

The suite covers independent critical instances, replacement/rejection, duration/INT snapshots, critical-rate probability, final critical-damage multiplication, noncritical/preview/effect isolation, Morale level cap/permanence/stat order, default portraits and the actual 31O epoch-filter pool counts. No Godot, packaging, commit or publishing occurs.

The cannon regression separately verifies additive owner/local Wisdom Down and Spirit Down inheritance, sibling and phantom isolation, and the real cannon block preview source scope.

Final verification on 2026-11-14: both native profiles passed **157 checks** after loading the final candidate; reports are `outputs/hbr-effect-framework-20261114/{stable,beta}/support-report.json`.

| Profile | Candidate SHA256 |
| --- | --- |
| stable | `495E550A68EE2000E5D5F4C56ED8FFC1FD22FF338FB553D014349D541C53903B` |
| beta | `597318EB2C34FD2E566F3331B84EF160DF5336F059BB6811E2180A98B68CCB0D` |
