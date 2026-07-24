# Tempo look mechanics

## Natural motion

Tempo looks around like a compact little penguin, not like a rigid turntable model. After full-profile synthesis produced unavoidable wraparound scale pops, the approved strategy is now a body-locked gaze family. The feet, lower belly, full frontal torso, baseline, lower-body center, belly width, and both flippers stay visually anchored in every direction. The amber eyes lead by rotating as complete physical eye surfaces inside their existing apertures; pupils, irises, highlights, eye whites, and eyelids move as one coherent construction. The beak and face then aim toward the target with restrained head yaw or pitch. Both original eyes remain visible in all 16 poses. The violet-tipped tuft follows with a very small, continuous lag. The rounded torso does not turn into a side profile, compress, grow, shrink, or translate.

Tempo has no prop. Do not add one. Do not rotate, skew, or tilt the whole sprite to fake gaze, and do not replace the original eyes with floating pupils or googly eyes.

## Stable anchor and motion budget

- Keep both plum feet and the lower-body center on one shared baseline.
- Keep the frontal body height, belly width, head size, both-eye visibility, and cell registration consistent with the approved idle frame.
- Each 22.5-degree step moves the eyes, beak, head, tuft, and flippers by roughly the same visual amount.
- Neighboring poses must not jump, flip sides, teleport, or change apparent scale.
- The beak tip and eye line carry horizontal direction; eye rotation, eyelid shape, beak pitch, head pitch, and crown/underside visibility carry vertical direction.
- Horizontal cardinals use strong eye rotation, eyelid asymmetry, and a clear beak-tip offset plus at most a restrained three-quarter face turn. The torso, belly, feet, and flippers stay frontal.
- No pose may become a one-eyed or narrow side-profile silhouette.
- Intermediate poses interpolate the adjacent cardinal pose families. No direction is an independently restyled pose.

## Cardinal pose families

### 000 — up

Both body sides and both eyes remain visible and balanced. The eyes rotate unmistakably upward, the lower eye surfaces become slightly more visible, the eyelids lift, and the beak tips up. The head pitches back just enough to show a little more beak underside while the frontal belly, flippers, and feet stay fixed. The tuft follows upward. This must read as up, never neutral/front.

### 090 — screen-right

Both eyes remain visible. The complete eye surfaces, eyelids, and beak tip shift unmistakably to the viewer's screen-right of the head center. The face may yaw only into a restrained three-quarter view; the full frontal torso, porcelain belly width, both flippers, and both feet remain visible and body-locked. This is viewer/image-right, not character-relative right.

### 180 — down

Both body sides and both eyes remain visible and balanced. The eyes rotate unmistakably downward, the upper eye surfaces and lids compress slightly, and the beak tips down toward the porcelain belly. The face tucks within the fixed frontal head-and-torso silhouette; do not crouch, shorten, widen, or enlarge the body. This must read as down, never sad-neutral.

### 270 — screen-left

Mirror the physical pose family of 090 without mirroring identity. Both eyes remain visible. The complete eye surfaces, eyelids, and beak tip shift unmistakably to the viewer's screen-left of head center. The face may yaw only into a restrained three-quarter view; the full frontal torso, porcelain belly width, both flippers, and both feet remain visible and body-locked.

## Clockwise continuity

Row 9 advances evenly from 000 through 090 to 157.5 by moving the complete eye surfaces, eyelids, beak aim, restrained face yaw/pitch, and tuft while the frontal body stays locked. Row 10 begins exactly one step after 157.5 at 180, advances through 270, and lands at 337.5 exactly one step before the approved 000 pose. Because the torso never leaves the frontal family, every cell should have nearly equal visible silhouette area and center. The ordered 16-pose loop must read as one calm clockwise scan with no reversal or boundary snap.

The deep down pose is the one intentional asymmetry: row 9's `157.5` uses a compact face-and-upper-body tuck. Row 10's `180` must inherit that exact compact registration and visible volume. The pet then untucks gradually across `202.5`, `225`, and `247.5`, never in one jump, while the feet, belly width, baseline, and lower-body center remain anchored. This staged untuck is part of the natural motion budget.
