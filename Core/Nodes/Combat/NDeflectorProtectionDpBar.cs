using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NDeflectorProtectionDpBar : Control
{
    private const string HealthBarBackgroundPath = "res://images/ui/combat/health_bar_bg.png";
    private const string HealthBarMaskPath = "res://images/ui/combat/health_bar.png";
    private const string HealthBarFillPath = "res://images/ui/combat/health_bar_fill.png";
    private const string HealthBarFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const float VerticalOffsetFromHpBar = -36f;
    private static readonly StringName ShadowOffsetX = "shadow_offset_x";
    private static readonly StringName ShadowOffsetY = "shadow_offset_y";
    private static readonly StringName ShadowOutlineSize = "shadow_outline_size";

    private readonly Color _outlineColor = new("10435B");
    private NinePatchRect _background;
    private Control _foregroundContainer;
    private NinePatchRect _mask;
    private NinePatchRect _foreground;
    private MegaLabel _label;
    private Control _hpBarContainer;
    private Creature _creature;
    private DeflectorProtection _relic;

    public void SetCreature(Creature creature, Control hpBarContainer)
    {
        _creature = creature;
        _hpBarContainer = hpBarContainer;
        _relic = creature.Player?.GetRelic<DeflectorProtection>();
        Subscribe();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Size = new Vector2(250f, 16f);

        BuildUi();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _ExitTree()
    {
        Unsubscribe();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_relic == null || _foreground == null || _label == null)
        {
            return;
        }

        SyncLayoutToHpBar();
        _relic.EnsureDpInitialized();
        int maxDp = _relic.MaxDp;
        int currentDp = maxDp > 0 ? Mathf.Clamp(_relic.Dp, 0, maxDp) : _relic.Dp;
        Visible = maxDp > 0;

        float ratio = maxDp > 0 ? (float)currentDp / maxDp : 0f;
        float maxWidth = _mask.Size.X > 0f ? _mask.Size.X : Size.X - 10f;
        _foreground.OffsetRight = maxWidth * ratio - maxWidth;
        _foreground.Visible = currentDp > 0;
        _label.SetTextAutoSize($"{currentDp}/{maxDp}");
    }

    private void BuildUi()
    {
        if (_background != null)
        {
            return;
        }

        _background = new NinePatchRect
        {
            Name = "DpBackground",
            Modulate = new Color(0.164706f, 0.376471f, 0.419608f, 0.752941f),
            Texture = LoadTexture(HealthBarBackgroundPath),
            PatchMarginLeft = 9,
            PatchMarginRight = 9,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_background, 1f, 0f, -1f, 0f);
        AddChild(_background);

        _foregroundContainer = new Control
        {
            Name = "DpForegroundContainer",
            ClipChildren = ClipChildrenMode.Only,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foregroundContainer, 5f, 3f, -5f, -3f);
        AddChild(_foregroundContainer);

        _mask = new NinePatchRect
        {
            Name = "DpMask",
            ClipChildren = ClipChildrenMode.Only,
            Texture = LoadTexture(HealthBarMaskPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_mask);
        _foregroundContainer.AddChild(_mask);

        _foreground = new NinePatchRect
        {
            Name = "DpForeground",
            SelfModulate = DeflectorProtection.DpColor,
            Texture = LoadTexture(HealthBarFillPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foreground, 0f, -4f, 0f, 4f);
        _mask.AddChild(_foreground);

        _label = new MegaLabel
        {
            Name = "DpLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 12,
            MaxFontSize = 24
        };
        SetFullRect(_label, 0f, -7f, 0f, 8f);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color(1f, 0.964706f, 0.886275f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0f, 0f, 0f, 0.25098f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, _outlineColor);
        _label.AddThemeConstantOverride(ShadowOffsetX, 6);
        _label.AddThemeConstantOverride(ShadowOffsetY, 4);
        _label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 16);
        _label.AddThemeConstantOverride(ShadowOutlineSize, 0);
        _label.AddThemeFontOverride(ThemeConstants.Label.Font, LoadFont(HealthBarFontPath));
        _label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 24);
        AddChild(_label);
    }

    private void SyncLayoutToHpBar()
    {
        if (_hpBarContainer == null)
        {
            return;
        }

        Position = _hpBarContainer.Position + new Vector2(0f, VerticalOffsetFromHpBar);
        Size = _hpBarContainer.Size;
    }

    private static void SetFullRect(Control control, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
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

    private static Font LoadFont(string path)
    {
        return ResourceLoader.Load<Font>(path);
    }

    private void Subscribe()
    {
        if (_creature != null)
        {
            _creature.MaxHpChanged += OnCreatureMaxHpChanged;
        }

        if (_relic != null)
        {
            _relic.DisplayAmountChanged += Refresh;
        }
    }

    private void Unsubscribe()
    {
        if (_creature != null)
        {
            _creature.MaxHpChanged -= OnCreatureMaxHpChanged;
        }

        if (_relic != null)
        {
            _relic.DisplayAmountChanged -= Refresh;
        }
    }

    private void OnCreatureMaxHpChanged(int oldMaxHp, int newMaxHp)
    {
        Refresh();
    }
}
