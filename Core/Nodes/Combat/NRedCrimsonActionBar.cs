using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NRedCrimsonActionBar : Control
{
    private const string BarBackgroundPath =
        "res://images/ui/combat/health_bar_bg.png";
    private const string BarMaskPath =
        "res://images/ui/combat/health_bar.png";
    private const string BarFillPath =
        "res://images/ui/combat/health_bar_fill.png";
    private const string FontPath =
        "res://themes/kreon_bold_glyph_space_one.tres";
    private const float BarWidth = 104f;
    private const float BarHeight = 14f;
    private const float HorizontalInset = 4f;
    private const float VerticalOffsetFromHpBar = -58f;

    private NinePatchRect _background;
    private Control _foregroundContainer;
    private NinePatchRect _mask;
    private NinePatchRect _foreground;
    private MegaLabel _label;
    private Control _hpBarContainer;
    private Creature _creature;
    private RedCrimson _redCrimson;
    private double _refreshElapsed;

    public void SetCreature(
        Creature creature,
        Control hpBarContainer)
    {
        _creature = creature;
        _redCrimson = creature?.Monster as RedCrimson;
        _hpBarContainer = hpBarContainer;
        SyncLayoutToDpBar();
        Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Size = new Vector2(BarWidth, BarHeight);
        BuildUi();
        SyncLayoutToDpBar();
        Refresh();
    }

    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < 0.1)
        {
            return;
        }

        _refreshElapsed = 0;
        Refresh();
    }

    public void Refresh()
    {
        if (_foreground == null ||
            _label == null ||
            _redCrimson == null ||
            _creature?.IsAlive != true)
        {
            Visible = false;
            return;
        }

        SyncLayoutToDpBar();
        Visible = true;
        int amount = Mathf.Clamp(
            _redCrimson.ActionGauge,
            0,
            RedCrimson.MaxActionGauge);
        float ratio =
            (float)amount / RedCrimson.MaxActionGauge;
        float maxWidth =
            _mask.Size.X > 0f
                ? _mask.Size.X
                : Size.X - HorizontalInset * 2f;
        _foreground.OffsetRight =
            maxWidth * ratio - maxWidth;
        _foreground.Visible = amount > 0;
        _label.SetTextAutoSize(
            $"{amount}/{RedCrimson.MaxActionGauge}");
    }

    private void BuildUi()
    {
        if (_background != null)
        {
            return;
        }

        _background = new NinePatchRect
        {
            Name = "ActionBackground",
            Modulate = new Color("381012E8"),
            Texture = LoadTexture(BarBackgroundPath),
            PatchMarginLeft = 8,
            PatchMarginRight = 8,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_background);
        AddChild(_background);

        _foregroundContainer = new Control
        {
            Name = "ActionForegroundContainer",
            ClipChildren = ClipChildrenMode.Only,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(
            _foregroundContainer,
            HorizontalInset,
            3f,
            -HorizontalInset,
            -3f);
        AddChild(_foregroundContainer);

        _mask = new NinePatchRect
        {
            Name = "ActionMask",
            ClipChildren = ClipChildrenMode.Only,
            Texture = LoadTexture(BarMaskPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_mask);
        _foregroundContainer.AddChild(_mask);

        _foreground = new NinePatchRect
        {
            Name = "ActionForeground",
            SelfModulate = new Color("E53A3A"),
            Texture = LoadTexture(BarFillPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foreground, 0f, -4f, 0f, 4f);
        _mask.AddChild(_foreground);

        _label = new MegaLabel
        {
            Name = "ActionLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 10,
            MaxFontSize = 18
        };
        SetFullRect(_label, 0f, -5f, 0f, 6f);
        _label.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            new Color("FFE9D9"));
        _label.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            new Color("280407"));
        _label.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            8);
        _label.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            ResourceLoader.Load<Font>(FontPath));
        _label.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            18);
        AddChild(_label);
    }

    private void SyncLayoutToDpBar()
    {
        if (_hpBarContainer == null)
        {
            return;
        }

        Position = _hpBarContainer.Position +
            new Vector2(
                Mathf.Max(
                    0f,
                    _hpBarContainer.Size.X - BarWidth),
                VerticalOffsetFromHpBar);
        Size = new Vector2(BarWidth, BarHeight);
    }

    private static void SetFullRect(
        Control control,
        float left = 0f,
        float top = 0f,
        float right = 0f,
        float bottom = 0f)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }

    private static Texture2D LoadTexture(string path)
    {
        return ResourceLoader.Load<Texture2D>(path);
    }
}
