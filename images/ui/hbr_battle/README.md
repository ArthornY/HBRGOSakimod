# HBR combat gauge assets

These are original BattleAtlas sprites extracted from the local HBR 6.2.0.0
client; they have not been repainted. `source-manifest.json` records bundle,
Unity object, sprite border and PNG hashes. The original prefab is
`UIBattleHeadGauge`, with the matching member/status gauge assets.

- HP: `GaugeHPBattleLarge`, original pink gradient.
- DP: `GaugeDPBattleLarge`, original cyan gradient.
- Background: `GaugeHPDPBaseBattle`, 28 × 16, left/right slices 9/12.
- Frame: `GaugeHPDPFlameBattle`, 28 × 16, left/right slices 9/8.
- Damage tail: `GaugeDamageBattle`, original red damage indicator.
- Numbers: `font/hbr_ui_numbers.fnt`, glyphs from the original HYZhengYuan 75S
  TMP SDF atlas, converted to a small RGBA atlas using original metrics and
  the original gray underlay parameters. Fixed-size font scaling and color
  modulation are enabled, so small gauges and lethal warning colors work.
  The native STS font is a fallback for Chinese labels and death text.
- Destruction percentage: `font/hbr_ui_numbers_red.fnt` uses the same source
  glyph metrics with the original pale pink face and red underlay. Current,
  divider, maximum and percent use 40/30/28/30, aligned to a shared baseline.

## Reference layout and host integration

The original HBR member prefab has a 225 × 16 HP bar and a roughly 108 × 16
DP bar partly over its upper-left edge. These proportions are now retained:
player DP is 108.09894/225 of the native HP width at Y=-8.699; enemy DP is
115/230 of that width at Y=-6.4. Both bars are 16 pixels tall. Player
`GaugeBaseFront` and enemy `GaugeBaseEnemy` are the original extracted shaped
backplates. Native phantom/special-scene creature anchors remain unchanged.

The player's source upper-right readout shows DP and its current amount,
switching to HP after DP reaches zero. Enemies have no DP number overlay.
As explicitly requested, the native HP current/max label remains centered on
the HP bar, above the overlapping DP artwork. Native infinity, poison/doom
warnings and block animations remain in front. No native container is replaced.

`gauge_fill.gdshader` preserves Unity's fixed-width gradient when the native
STS fill node shrinks, instead of compressing the full gradient into the
remaining health. Ordinary DP changes use the source 0.15-second count-up
duration.

Multiple bars use `UIBattleEnemy2`'s complete overlapping slots, following the
SkullFeather HP / Amon Gate DP presentation requested for this mod. HP slots
have source offsets (0,0), (8,9.7), (15,19.8), (22,30.5); DP slots move
(-9,-10) per layer. Later siblings are drawn in front. The current HP slot
is normalized to the existing creature anchor, with full reserve slots
behind it. DP's lowest slot stays at the existing group anchor; additional
slots rise to the left. DP and resistance badges also move upward when HP
reserves need that space, accounting for the inherited canvas scale.

HP reserves read real phase receipts from SkullFeather, DeathSlug and FlatHand;
they do not combine future lives into a fabricated maximum. DP reserves use
the Shiba's actual remaining layers. Only the current slot shows the existing
HP numerical label. HBR's `textHp`/`textDpN` objects are inactive in the source
prefab, so they are not copied as extra per-layer labels. The original
`EnemyHPGuard` sprites belong to a separate EShield mechanic and are not used
as life counters.

Multi-HP enemies show the actual destruction damage multiplier and cap above
the highest gauge when Break/destruction rate applies. This reads the mod's
stack conversion and cap; the reference screenshot's 9999 cap is not imposed
on combat rules. Resistance badges sit above the complete header, leaving
room for the player roll readout and RedCrimson's existing action bar.

Poison and doom previews are STS mechanics: they retain their native nodes,
masking, warning colors and calculation. The neutral poison fill is retained
because multiplying HBR's red damage sprite by green would change the warning
color. Block keeps its native icon, break animation and blue frame indication.

Extraction scripts, prefab data and offline reference renders:
`E:/stsmod/output/hbr-battle-ui-20260930/`.
The separate original OD sprite/animation data is in
`../overclock/original/`.
