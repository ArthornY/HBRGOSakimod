using System.Globalization;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Nodes.Combat;

internal partial class NHbrEnemyStatusValueCaption : Control
{
    private Creature _creature;
    private MegaLabel _label;
    private decimal? _shownValue;
    private double _elapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        _label = new MegaLabel
        {
            Name = "EnemyStatusValueText",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 8,
            MaxFontSize = 16,
            ClipText = true
        };
        _label.AddThemeFontOverride(ThemeConstants.Label.Font,
            ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
        _label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 16);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color("FFE9D9"));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color("19151C"));
        _label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 2);
        AddChild(_label);
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Refresh();
    }

    internal void SetCreature(Creature creature)
    {
        if (!ReferenceEquals(_creature, creature))
        {
            _creature = creature;
            _shownValue = null;
        }
        Refresh();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.1) return;
        _elapsed = 0;
        Refresh();
    }

    internal static bool ShouldDisplay(Creature creature) =>
        EnemyStatScalingHelper.IsEnemy(creature) && creature.IsAlive &&
        HbrBattleStatsHelper.IsEnabled(creature);

    internal static float GetTopOffset(Creature creature) =>
        HbrEnemyStatusValueLayout.GetRect(0f,
            HbrBattleGaugeSizing.BaseHeight * HbrBattleGaugeSizing.GetThickness(creature),
            0f, NDeflectorProtectionDpBar.GetGaugeOffset(creature, Vector2.One).Y,
            HbrMultiHealthBar.GetExtraHeight(creature)).Top;

    private void Refresh()
    {
        Visible = ShouldDisplay(_creature);
        if (!Visible || _label == null || GetParent() is not Control hpBar) return;
        var layout = HbrEnemyStatusValueLayout.GetRect(hpBar.Size.X, hpBar.Size.Y,
            hpBar.Size.X * NDeflectorProtectionDpBar.GetWidthRatio(_creature),
            NDeflectorProtectionDpBar.GetGaugeOffset(_creature, Vector2.One).Y,
            HbrMultiHealthBar.GetExtraHeight(_creature));
        Vector2 position = new(layout.Left, layout.Top);
        Vector2 size = new(layout.Width, layout.Height);
        if (!Position.IsEqualApprox(position)) Position = position;
        if (!Size.IsEqualApprox(size)) Size = size;
        // Calamity and map/phase changes flow through the same live getter used
        // by damage. Never cache a base E or infer it from displayed power text.
        decimal current = HbrEnemyProfileHelper.GetEnemyParameter(_creature);
        if (_shownValue == current) return;
        _shownValue = current;
        _label.SetTextAutoSize("状态值 " + current.ToString("0.##", CultureInfo.InvariantCulture));
    }
}
