# Hanamura requirement 82 checks

Run this host through `outputs/31f-cards-80-82-20261115/run-suite.ps1`, using `-Suite 31f-hanamura`, the required `-Profile`, and an absolute immutable `-Candidate` path. Beta accepts `-CheckArgs @('--isolate-telemetry')`.

The 90 checks exercise the actual card body, native permanent deck and character identities, all seven independent garden upgrades, native positive-effect applications, and the real `OnPlayWrapper` attack/card/damage hooks. They cover:

- Orchestra character deduplication, owner isolation, self exclusion, both caps, and immediate cache invalidation after removal or same-sized replacement.
- Single-target and team support using the real Risa target range, including phantoms and summoned spirits while excluding ordinary pets; medium combo goes only to the selected unit.
- Aria's 77% three-hit request, destruction coefficient 1.6, real skill and connected-segment OD gain, one native resource payment, and ordinary discard. Existing attack buffs are consumed before this card grants its fresh buffs.
- Native relative hand-card discounts, the zero floor, expiry, no-choice handling, source exclusion, and selection returning after the owner's turn changed.
- HBR-mode isolation and captured execution contexts that resume after the Aria scope was disposed.

Attack execution and canonical damage terminals record requests and feed the real shared arithmetic. Native HP mutation, hand input, waits, visual effects, and scene refresh are controlled leaves. The host does not start Godot, render the UI, or run a live multiplayer session.

The final frozen pair passed 90/90 on each profile: stable `E1B27F0AF8F8FAE3D72131BE97604B37A8D7898E6D8BD6B00C46A878962594A3` and beta `8F47FE175BC12867ED5AE863A714C8813A3B788CC472BBF743AF58F4184E2162`. The associated full card-face suite also passed 8522/8522 per profile. Exact report paths and production-source hashes are recorded in the stage's `owned-integration-summary.json`. The production freeze verified 1471 files with no drift after these runs.
