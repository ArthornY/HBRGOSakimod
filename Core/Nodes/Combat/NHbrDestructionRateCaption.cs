using System.Globalization;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Read-only presentation of the current effect and its full potential cap.
// Power stacks are not percentages; DP does not reduce the stored cap.
internal partial class NHbrDestructionRateCaption : Control
{
    // Keep the current percentage prominent while rasterizing the scalable
    // font at the final pixel size, rather than stretching a small bitmap.
    internal const float Height = 60f;
    internal const float TopClearance = Height + 6f;
    private static readonly int[] SourceFontSizes = { 40, 30, 28, 30 };
    private Creature _creature;
    private Control _textGroup;
    private readonly MegaLabel[] _parts = new MegaLabel[4];
    private string[] _values;
    private Font _font;
    private float _contentWidth;
    private float _contentHeight;
    private float _lastWidth = -1f;
    private PowerModel _rate;
    private PowerModel _cap;
    private PowerModel _conversion;
    private bool _subscribed;
    private decimal _shownCurrent = -1m;
    private decimal _shownMaximum = -1m;
    private double _refreshElapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorRight = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        _font = HbrBattleGaugeStyle.DestructionNumberFont;
        _contentHeight = _font.GetHeight(SourceFontSizes[0]);
        _textGroup = new Control
        {
            Name = "SourceDestructionReadout",
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_textGroup);
        for (int i = 0; i < _parts.Length; i++)
        {
            _parts[i] = new MegaLabel
            {
                Name = i == 0 ? "CurrentPercent" : i == 1 ? "Divider" : i == 2 ? "MaximumPercent" : "PercentSign",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                MouseFilter = MouseFilterEnum.Ignore
            };
            StylePart(_parts[i], SourceFontSizes[i]);
            _textGroup.AddChild(_parts[i]);
            // Native MegaLabel._Ready installs a locale substitute.
            StylePart(_parts[i], SourceFontSizes[i]);
        }
        Refresh();
    }

    internal void SetCreature(Creature creature)
    {
        if (!ReferenceEquals(_creature, creature))
        {
            Unsubscribe();
            _creature = creature;
            _shownCurrent = _shownMaximum = -1m;
        }
        Refresh();
    }

    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < 0.1) return;
        _refreshElapsed = 0;
        Refresh();
    }

    public override void _ExitTree() => Unsubscribe();

    internal static bool ShouldDisplay(Creature creature) =>
        creature?.CombatState != null &&
        (creature.HasPower<DestructionRatePower>() ||
            (creature.IsEnemy && (creature.HasPower<BreakPower>() ||
                creature.HasPower<DestructionRateCapIncreasePower>())));

    internal static float GetTopOffset(Creature creature) =>
        NDeflectorProtectionDpBar.GetHudTop(creature) - TopClearance;

    internal bool TryGetContentTopOffset(out float top)
    {
        top = 0f;
        if (!Visible || _textGroup == null || _values == null) return false;
        // Long percentages fit to a smaller font at the bottom of the caption.
        // Other HUD rows only need to clear the text, not its empty top space.
        top = OffsetTop + _textGroup.Position.Y;
        return true;
    }

    internal static bool TryGetPercentages(Creature creature, out decimal current, out decimal maximum)
    {
        current = maximum = 0m;
        if (!ShouldDisplay(creature)) return false;
        DestructionRatePower rate = creature.GetPower<DestructionRatePower>();
        DestructionRatePower.GetDisplayMultipliers(creature, rate?.Amount ?? 0, out current, out maximum);
        current *= 100m;
        maximum *= 100m;
        return true;
    }

    internal static string FormatPercentages(decimal current, decimal maximum) =>
        current.ToString("0.0", CultureInfo.InvariantCulture) + "/" +
        maximum.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private void Refresh()
    {
        Bind();
        Visible = TryGetPercentages(_creature, out decimal current, out decimal maximum);
        if (!Visible || _textGroup == null) return;
        OffsetTop = GetTopOffset(_creature);
        OffsetBottom = OffsetTop + Height;
        if (_shownCurrent != current || _shownMaximum != maximum)
        {
            _shownCurrent = current;
            _shownMaximum = maximum;
            _values = new[] { current.ToString("0.0", CultureInfo.InvariantCulture), "/",
                maximum.ToString("0.0", CultureInfo.InvariantCulture), "%" };
            _lastWidth = -1f;
        }
        if (Mathf.IsEqualApprox(_lastWidth, Size.X)) return;
        _lastWidth = Size.X;
        LayoutText();
    }

    private void StylePart(MegaLabel label, int fontSize)
    {
        label.AutoSizeEnabled = false;
        label.MinFontSize = label.MaxFontSize = fontSize;
        label.AddThemeFontOverride(ThemeConstants.Label.Font, _font);
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color("F6E5E5"));
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color("512939"));
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 2);
        label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, Colors.Transparent);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
    }

    private void LayoutText()
    {
        float naturalWidth = 0f;
        for (int i = 0; i < _parts.Length; i++)
            naturalWidth += _font.GetStringSize(_values[i], fontSize: SourceFontSizes[i]).X;
        float sizeRatio = Mathf.Min(1f, Mathf.Max(1f, Size.X - 8f) / Mathf.Max(1f, naturalWidth));
        int currentSize = System.Math.Max(1, (int)(SourceFontSizes[0] * sizeRatio));
        float baseline = _font.GetAscent(currentSize);
        _contentHeight = _font.GetHeight(currentSize);
        _contentWidth = 0f;
        for (int i = 0; i < _parts.Length; i++)
        {
            int fontSize = System.Math.Max(1, (int)(SourceFontSizes[i] * sizeRatio));
            float width = _font.GetStringSize(_values[i], fontSize: fontSize).X;
            StylePart(_parts[i], fontSize);
            _parts[i].Position = new Vector2(Mathf.Round(_contentWidth), Mathf.Round(baseline - _font.GetAscent(fontSize)));
            _parts[i].Size = new Vector2(Mathf.Ceil(width) + 1f, _font.GetHeight(fontSize));
            _parts[i].SetTextAutoSize(_values[i]);
            _contentWidth += width;
        }
        _textGroup.Scale = Vector2.One;
        _textGroup.Position = new Vector2(Mathf.Round((Size.X - _contentWidth) / 2f),
            Mathf.Round(Height - (_contentHeight - _font.GetDescent(currentSize))));
    }

    private void Bind()
    {
        if (_creature == null) return;
        if (!_subscribed)
        {
            _creature.PowerApplied += OnPowerChanged;
            _creature.PowerRemoved += OnPowerChanged;
            _subscribed = true;
        }
        BindPower(ref _rate, _creature.GetPower<DestructionRatePower>());
        BindPower(ref _cap, _creature.GetPower<DestructionRateCapIncreasePower>());
        BindPower(ref _conversion, _creature.GetPower<SixSenseResistancePower>());
    }

    private void BindPower(ref PowerModel previous, PowerModel next)
    {
        if (ReferenceEquals(previous, next)) return;
        if (previous != null) previous.DisplayAmountChanged -= Refresh;
        previous = next;
        if (previous != null) previous.DisplayAmountChanged += Refresh;
    }

    private void OnPowerChanged(PowerModel power) => Refresh();

    private void Unsubscribe()
    {
        if (_subscribed && _creature != null)
        {
            _creature.PowerApplied -= OnPowerChanged;
            _creature.PowerRemoved -= OnPowerChanged;
        }
        _subscribed = false;
        BindPower(ref _rate, null);
        BindPower(ref _cap, null);
        BindPower(ref _conversion, null);
    }
}
