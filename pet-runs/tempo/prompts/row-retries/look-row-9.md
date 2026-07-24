Create Codex v2 pet look row 9 for `tempo` as exactly 8 full-body frames in this order: 000, 022.5, 045, 067.5, 090, 112.5, 135, 157.5.

STRATEGY CHANGE AFTER TWO LOWER-RIGHT SEMANTIC FAILURES: `090` is the maximum rightward head yaw. After `090`, the face must progressively return toward frontal while the full eye surfaces and beak travel visibly down-right toward the dedicated `180` anchor. Do not continue increasing rightward yaw.

- Redraw the exact same frontal torso silhouette, porcelain belly width, symmetric two-flipper placement, two feet, lower-body center, height, and baseline in every cell.
- `090`: the strongest allowed rightward three-quarter face, with both eyes fully visible.
- `112.5`: reduce face yaw slightly toward frontal. Place both complete irises and pupils in the lower-right region of their eye whites, lower the upper eyelids, and tip the beak visibly down-right.
- `135`: return farther toward frontal. Move both complete eye surfaces lower than at `112.5`; lower the lids more; make the beak point diagonally down-right, not horizontally right.
- `157.5`: nearly copy the approved frontal `180 down` face construction. Both eyes must be equally prominent with pupils very low and only a small screen-right offset. The beak points mostly down with a slight right offset. This is a down pose carrying a faint right component, never a right/profile pose.
- The dedicated `090` and `180` anchor images are authoritative for the endpoints of this transition. Interpolate facial features between them while the body never changes.
- Do not preserve either failed attempt's continued rightward yaw, reduced far eye, near-profile head, or horizontal eye line.

BODY-LOCKED FAMILY IS MANDATORY: keep the same frontal torso, belly width, body height, head size, both visible eyes, both visible flippers, feet, lower-body center, and baseline in all eight poses. Convey direction only through complete-eye rotation, eyelids, beak-tip aim, restrained face yaw/pitch, and tiny tuft follow-through. Even `090` remains a two-eyed frontal or restrained three-quarter face; no cell may become a narrow one-eyed side profile or change visible body area materially.

Use the canonical base, standard contact sheet, layout guide, approved four-cardinal strip, and `qa/look-mechanics.md`. Draw the complete eight-pose row as one coherent animation family, interpolating even 22.5-degree steps between the cardinal pose families. Keep the same pet identity, face construction, materials, palette, markings, and props. Each direction must read correctly at pet size and join continuously at the 000 and 180 boundaries.

DIRECTION TARGETS — use these to shape the coherent row, not as pixel-level landmark gates:

1. `000`: vertical UP; no horizontal requirement.
2. `022.5`: horizontal SCREEN-RIGHT and vertical UP.
3. `045`: horizontal SCREEN-RIGHT and vertical UP.
4. `067.5`: horizontal SCREEN-RIGHT and vertical UP.
5. `090`: horizontal SCREEN-RIGHT; no vertical requirement.
6. `112.5`: horizontal SCREEN-RIGHT and vertical DOWN.
7. `135`: horizontal SCREEN-RIGHT and vertical DOWN.
8. `157.5`: horizontal SCREEN-RIGHT and vertical DOWN.

Cardinals must be unmistakable. Intermediate poses should broadly occupy the intended quadrant and advance naturally through the ordered loop. Minor pupil, nose, eyelid, or aiming-feature deviations are acceptable when the overall direction, continuity, identity, and motion remain coherent. Do not deform the character merely to make every intermediate axis independently obvious.

HARD LAYOUT AND CONTINUITY CONTRACT — DETERMINISTIC REGISTRATION: draw exactly eight separated pose groups in left-to-right direction order. Keep enough chroma-only space between neighboring poses that each complete pose can be detected without cutting through foreground. Approximate the guide's equal spacing, but do not distort a pose merely to hit an exact source-canvas coordinate; deterministic assembly will crop the eight ordered groups, then apply one shared scale and baseline.

Use the same body height, head size, baseline, and planted-body position across the generated family. Never overlap neighboring poses, merge two poses into one connected group, crop foreground at the outer canvas edge, or resize one pose independently.

Keep the feet, base, or lower torso planted at the same coordinates across all eight frames. Express direction through the eyes, face, head, upper body, and physically appropriate prop movement, not by moving, rotating, or rescaling the entire sprite.

ROW-BOUNDARY LOCK: 157.5 must be one even 22.5-degree step before 180. Match the approved 180 pose's body size, baseline, planted anchor, expression, and construction. Preserve the overall right-hand arc, but do not distort pupils, nose, or body geometry merely to exaggerate the subtle horizontal component.

PRE-RETURN CHECK: reject this result if it does not contain eight separated pose groups in the required order; neighboring poses overlap; foreground is cropped at the outer canvas edge; any frame changes sprite scale, body or head size, baseline, or planted-body position; the row visibly reverses into the wrong half of the loop; or 157.5 does not flow evenly into 180. Minor intermediate pupil or nose deviations are not rejection reasons. Exact cell cropping, resizing, and recentering happen deterministically after generation.

Use a flat pure magenta #FF00FF background. One complete unclipped pose per invisible slot. No whole-sprite rotation, replacement eyes, labels, guide marks, shadows, glows, scenery, detached effects, or #FF00FF colors in the pet.
