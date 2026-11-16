# Overclock compact-hover variables

The host installs the real Overclock description-variable patch alongside both existing compact patches. Actual native Relic.DynamicDescription, DynamicEventDescription and OverclockUpgrade.ExtraHoverTips run through native SmartFormat; canonical assertions stay active. The ownerless/canonical fallback uses the authored allowance limit, while owned cards display the real party-shared remaining allowance.

The fixed stable DLL `30FCC918549D3D65C8EBFAF98E4EE3CD591A70CABD28E1E255B37DE667723110` passes **37 checks against stable0.107.1 and original beta0.113.1**. A real old-DCEF negative control passes five checks by reproducing the missing `hbrRemaining` FormattingException from the actual card tooltip. Reports are indexed by `outputs/card-turn-stall-1131-20261115/overclock-hover/index.json`.

Coverage includes canonical and owned full/event/compact formatting, compatibility with duplicate native LocString.Add from the two last-priority postfixes, party spending3/8→remaining4 then3/10→2, the actual owned card tooltip showing2, generic canonical/ownerless12, next-act reset, and no OD variable added to an unrelated vanilla Anchor. Full OD wording is unchanged. The first fixture attempt called a model constructor after canonical registration and was rejected by the real DuplicateModelException; its report is retained, and the fixture now uses native MutableClone.

Run `outputs/card-turn-stall-1131-20261115/run-overclock-hover-checks.ps1 -Profile stable` followed by `-Profile beta`. An explicit old `-Candidate`, `-ExpectedSha256` and `-NegativeControl` reproduces the old tooltip failure.

Only StringName/logging/icon-resource and energy image-path/color leaves are controlled. Formatter, variable registration, native dictionary overwrite behavior, card/relic selection and OD spending policy run normally. No Godot engine, rendered tooltip, save, installation or package is executed.
