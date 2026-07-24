Create Codex v2 pet look row 10 for `tempo` as exactly 8 full-body frames in this order: 180, 202.5, 225, 247.5, 270, 292.5, 315, 337.5.

SOURCE-EDGE REPAIR: the staged-tuck attempt drew every pose much larger on the 2172x724 source canvas than approved row 9 and the first body-locked row-10 attempt. Deterministic registration therefore found 31 non-transparent pixels near the final 180 cell edge and rejected the complete row. Keep the staged pose progression, but restore the prior safe source-canvas scale and padding:

- Match the raw pose height, width, spacing, and generous top/bottom/side chroma padding of `look-row-10-bodylocked-attempt-1.png` and raw approved row 9.
- Each complete pet should occupy roughly the same source-canvas percentage as those references, not the oversized staged-tuck attempt. Keep every pose compact and centered with ample magenta above the tuft, below the feet, and beside both flippers.
- Do not make any one pose larger to express direction or the untuck. The compact `180` must fit the approved row-9 registration transform without any foreground near a 192x208 final-cell edge.
- Preserve the staged `180 -> 202.5 -> 225 -> 247.5` visible-volume progression, the correct eight direction semantics, and the already smooth `337.5 -> 000` wrap.

BODY-LOCKED ATTEMPT-1 REPAIR: every row-10 direction passed, the internal left/up-left arc passed, and `337.5 -> 000` passed visually. Preserve those properties. The only hard failure was `157.5 -> 180`: the first `180` pose became 22% fuller with a 13 px center shift. Redraw the complete row with a staged down-tuck transition:

- `180` must inherit row 9's registered `157.5` body height, compact down-tuck volume, belly width, feet, baseline, and lower-body center almost exactly. Advance only the complete eye surfaces, eyelids, and beak from down-right to centered down.
- `202.5` remains down-left and restores at most one third of the visible upper-body volume and center difference toward the normal left-facing family.
- `225` remains down-left and restores the second third.
- `247.5` completes the smooth untuck into the normal body-locked scale before `270`.
- Each adjacent step must be small. Do not move the entire missing volume at `180 -> 202.5`, and do not shift the feet or baseline.
- Preserve the already successful two-eyed `270`, `292.5`, `315`, and `337.5` constructions and the smooth `337.5 -> 000` wrap.

FINAL STRATEGY — REBUILT BODY-LOCKED FAMILY: discard the old side-profile pose construction. Use the newly approved body-locked cardinal strip and newly completed body-locked row 9. Keep the same frontal torso, belly width, body height, head size, both visible eyes, both visible flippers, feet, lower-body center, and baseline in all eight poses. Convey direction only through complete-eye rotation, eyelids, beak-tip aim, restrained face yaw/pitch, and tiny tuft follow-through. Even `270` remains a two-eyed frontal or restrained three-quarter face. No cell may become a narrow one-eyed side profile or materially change visible silhouette area.

BOUNDARY CONSTRUCTION: `180` uses the same unchanged frontal torso, belly width, feet, center, baseline, and both-eye construction as the new body-locked `157.5`; only the eye surfaces, eyelids, beak, and restrained face pitch advance one step to down. `337.5` likewise uses the same unchanged frontal body construction as the new body-locked `000`; only those facial/head gaze parts remain subtly up-left. Both boundaries must change face direction without changing silhouette area or registration.

Use the canonical base, standard contact sheet, layout guide, approved four-cardinal strip, and `qa/look-mechanics.md`. Draw the complete eight-pose row as one coherent animation family, interpolating even 22.5-degree steps between the cardinal pose families. Keep the same pet identity, face construction, materials, palette, markings, and props. Each direction must read correctly at pet size and join continuously at the 000 and 180 boundaries.

DIRECTION TARGETS — use these to shape the coherent row, not as pixel-level landmark gates:

1. `180`: vertical DOWN; no horizontal requirement.
2. `202.5`: horizontal SCREEN-LEFT and vertical DOWN.
3. `225`: horizontal SCREEN-LEFT and vertical DOWN.
4. `247.5`: horizontal SCREEN-LEFT and vertical DOWN.
5. `270`: horizontal SCREEN-LEFT; no vertical requirement.
6. `292.5`: horizontal SCREEN-LEFT and vertical UP.
7. `315`: horizontal SCREEN-LEFT and vertical UP.
8. `337.5`: horizontal SCREEN-LEFT and vertical UP.

Cardinals must be unmistakable. Intermediate poses should broadly occupy the intended quadrant and advance naturally through the ordered loop. Minor pupil, nose, eyelid, or aiming-feature deviations are acceptable when the overall direction, continuity, identity, and motion remain coherent. Do not deform the character merely to make every intermediate axis independently obvious.

HARD LAYOUT AND CONTINUITY CONTRACT — DETERMINISTIC REGISTRATION: draw exactly eight separated pose groups in left-to-right direction order. Keep enough chroma-only space between neighboring poses that each complete pose can be detected without cutting through foreground. Approximate the guide's equal spacing, but do not distort a pose merely to hit an exact source-canvas coordinate; deterministic assembly will crop the eight ordered groups, then apply one shared scale and baseline.

Use the same body height, head size, baseline, and planted-body position across the generated family. Never overlap neighboring poses, merge two poses into one connected group, crop foreground at the outer canvas edge, or resize one pose independently.

Keep the feet, base, or lower torso planted at the same coordinates across all eight frames. Express direction through the eyes, face, head, upper body, and physically appropriate prop movement, not by moving, rotating, or rescaling the entire sprite.

ROW-BOUNDARY LOCK: 180 must continue directly from row 9's 157.5, matching its body size, baseline, planted anchor, expression, and construction. 337.5 must be one even 22.5-degree step before 000: nearly up-facing while remaining on the overall left-hand arc. Do not distort pupils, nose, or body geometry merely to exaggerate the subtle horizontal component.

PRE-RETURN CHECK: reject this result if it does not contain eight separated pose groups in the required order; neighboring poses overlap; foreground is cropped at the outer canvas edge; any frame changes sprite scale, body or head size, baseline, or planted-body position; the row visibly reverses into the wrong half of the loop; or 180 does not continue from 157.5 or 337.5 does not flow evenly into 000. Minor intermediate pupil or nose deviations are not rejection reasons. Exact cell cropping, resizing, and recentering happen deterministically after generation.

Use a flat pure magenta #FF00FF background. One complete unclipped pose per invisible slot. No whole-sprite rotation, replacement eyes, labels, guide marks, shadows, glows, scenery, detached effects, or #FF00FF colors in the pet.
