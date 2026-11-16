namespace HBRGOSakiMod.Core.Nodes.Combat;

// Every image and sound of 31O's magic sword and fate dice interface, in one place (design v3, sections 5
// and 6). The images are placeholders until the final icons are chosen; a final image replaces the file at
// the same path, so no code changes. The fate dice and fragment placeholders are drawn by
// ui-concepts/magic-sword-finisher-20261109/make_placeholders.py, which also writes their .import files
// and images/ui/hbr_fate_dice/sources.json (a replaced file updates its line there).
internal static class HbrMagicSwordArt
{
    // The magic sword: the HUD switch, the magic sword mode power, and the sword of the finisher show. Until
    // the Lucky Seven icon is drawn it is the existing power icon, a painted square with flames around the sword;
    // the finisher keeps only a band along the blade (HbrLuckySevenTimeline.SwordMask, whose five numbers follow
    // the blade's position on this icon and change with a final icon of another pose).
    internal const string SwordIcon = "res://images/powers/magic_sword_mode_power.png";

    // 31O for the finisher, drawn as a silhouette with a violet rim (「祥子：用现有的 31O 立绘或剪影」).
    internal const string SakiPortrait = "res://images/characters/hbrgo_saki_normal.png";

    // Fate dice (two stacked ten-sided dice reading 70 and 7) and a fate fragment broken from it, on transparent
    // ground. The fragment's outer ring is tinted per category in code (HbrFateDiceCatalog.CategoryColor), and
    // cracks are drawn over the dice in code (「裂痕：在骰子图标上叠一道深红裂纹，用程序叠，不另画」).
    // The fragment also stands for the fate dice on the reward screen and the rest site's 修骰子.
    internal const string FateDiceIcon = "res://images/ui/hbr_fate_dice/fate_dice.png";
    internal const string FateFragmentIcon = "res://images/ui/hbr_fate_dice/fate_fragment.png";

    // The manual's home button: the die as a white glyph with a halo like the other home icons, tinted 31O blue.
    internal const string FateDiceGlyph = "res://images/ui/hbr_fate_dice/fate_dice_glyph.png";

    // The round mask the roster medallions use, reused to cut the square sword icon into a circle.
    internal const string CircleMask = "res://images/ui/hbr_phantom_roster/dimensionEnemyBg.png";

    // Built-in game sounds (「音效：用游戏自带的斩击、重击音效」).
    internal const string SlashSfx = "event:/sfx/enemy/enemy_attacks/the_kin_minion/the_kin_minion_quick_slash";
    internal const string BigSlashSfx = "event:/sfx/enemy/enemy_attacks/spectral_knight/spectral_knight_soul_slash";
    internal const string ImpactSfx = "event:/sfx/enemy/enemy_attacks/kaiser_crab/kaiser_crab_attack_slam";
}
