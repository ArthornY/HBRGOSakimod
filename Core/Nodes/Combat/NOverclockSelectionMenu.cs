using System;
using System.Globalization;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// A native overlay owns input/focus while the original HBR selection artwork
// is open. Details are a page of this overlay, so closing them returns here.
public partial class NOverclockSelectionMenu : Control, IOverlayScreen
{
    private const string Art = "res://images/ui/overclock/menu/";
    private const string FontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private static readonly Vector2 ReferenceSize = new(1820f, 1024f);
    private static readonly Color[] LevelColors =
        { new("F49DE8"), new("9AF4F6"), new("FFF3A1") };
    private readonly Button[] _levelButtons = new Button[Overclock.MaxLevel];
    private readonly TextureRect[] _levelImages = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _numbers = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[,] _rings = new TextureRect[Overclock.MaxLevel, 2];
    private readonly bool?[] _available = new bool?[Overclock.MaxLevel];
    private Overclock _relic;
    private object _combat;
    private Action<int> _activate;
    private Action _closed;
    private Control _canvas;
    private Control _selection;
    private Control _details;
    private Button _cancel;
    private Button _info;
    private Button _closeDetails;
    private Label _hint;
    private Label _charge;
    private NHbrOverdriveFloorBudget _floorBudget;
    private ShaderMaterial _disabledMaterial;
    private Font _font;
    private Font _grayFont;
    private readonly Font[] _levelFonts = new Font[Overclock.MaxLevel];
    private bool _showDetails;
    private bool _closing;
    private string _lastCharge;

    public NetScreenType ScreenType => NetScreenType.Room;
    public bool UseSharedBackstop => false;
    public Control DefaultFocusedControl => _showDetails ? _closeDetails : _cancel;

    internal static NOverclockSelectionMenu Show(Overclock relic, Action<int> activate, Action closed)
    {
        if (relic?.Owner?.Creature?.CombatState == null || NOverlayStack.Instance == null ||
            NOverlayStack.Instance.ScreenCount != 0 || NModalContainer.Instance?.OpenModal != null)
            return null;
        var menu = new NOverclockSelectionMenu
        {
            Name = "OverclockSelectionMenu", _relic = relic,
            _combat = relic.Owner.Creature.CombatState, _activate = activate, _closed = closed
        };
        NOverlayStack.Instance.Push(menu);
        return menu;
    }

    internal static bool IsLevelAvailable(Overclock relic, int level) =>
        relic?.CanActivateLevel(level) == true;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Font fallback = FontManager.GetSubstituteFont(LocManager.Instance.Language, FontType.Regular)
            ?? ResourceLoader.Load<Font>(FontPath);
        // The extracted bitmap subset lacks most characters used by these
        // descriptions. Use one complete locale font for uniform glyphs and
        // spacing instead of mixing authored glyphs with a fallback per letter.
        _font = fallback;
        _grayFont = fallback;
        for (int i = 0; i < Overclock.MaxLevel; i++)
            _levelFonts[i] = fallback;
        _disabledMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = "shader_type canvas_item; varying vec4 vertex_tint; void vertex(){ vertex_tint = COLOR; } " +
                    "void fragment(){ vec4 c = texture(TEXTURE, UV); " +
                    "float grey = dot(c.rgb, vec3(0.299, 0.587, 0.114)); COLOR = vec4(vec3(grey), c.a) * vertex_tint; }"
            }
        };
        BuildUi();
        LayoutCanvas();
        _relic.DisplayAmountChanged += RefreshAvailability;
        RefreshAvailability();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutCanvas();
    }

    public override void _Process(double delta)
    {
        if (_closing) return;
        if (!CombatManager.Instance.IsInProgress || _relic?.Owner?.Creature?.IsAlive != true ||
            !ReferenceEquals(_combat, _relic.Owner.Creature.CombatState) || _relic.ActiveLevel > 0)
        {
            Close();
            return;
        }
        // Action locks and multiplayer turn changes do not necessarily change
        // charge. Poll availability as well as listening to the relic event.
        RefreshAvailability();
    }

    public override void _Input(InputEvent input)
    {
        if (_closing || !IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed("ui_cancel") ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape or Key.Backspace } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            GetViewport().SetInputAsHandled();
            if (_showDetails) SetDetails(false); else Close();
            return;
        }
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.I)
        {
            GetViewport().SetInputAsHandled();
            SetDetails(!_showDetails);
        }
        else if (!_showDetails && key.Keycode is Key.Key1 or Key.Key2 or Key.Key3)
        {
            GetViewport().SetInputAsHandled();
            SelectLevel((int)(key.Keycode - Key.Key1) + 1);
        }
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (!_closing && IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(this) && input is InputEventKey)
            GetViewport().SetInputAsHandled();
    }

    public void AfterOverlayOpened() => NHoverTipSet.Clear();
    public void AfterOverlayShown() { Visible = true; RefreshAvailability(); }
    public void AfterOverlayHidden() => Visible = false;
    public void AfterOverlayClosed()
    {
        _closing = true;
        NotifyClosed();
        this.QueueFreeSafely();
    }

    public override void _ExitTree() => NotifyClosed();

    private void NotifyClosed()
    {
        if (_relic != null) _relic.DisplayAmountChanged -= RefreshAvailability;
        Action closed = _closed;
        _closed = null;
        closed?.Invoke();
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        NOverlayStack.Instance?.Remove(this);
    }

    private void SelectLevel(int level)
    {
        if (_closing || _showDetails || !IsLevelAvailable(_relic, level))
        {
            RefreshAvailability();
            return;
        }
        Action<int> activate = _activate;
        Close();
        activate?.Invoke(level);
    }

    private void SetDetails(bool show)
    {
        _showDetails = show;
        _details.Visible = show;
        _selection.Modulate = show ? new Color(0.5f, 0.5f, 0.5f, 1f) : Colors.White;
        _cancel.Disabled = show;
        _info.Disabled = show;
        _hint.Text = show ? "Esc / 右键  关闭详情" : "1 / 2 / 3  选择等级     I  超频详情     Esc / 右键  取消";
        RefreshAvailability();
        if (ActiveScreenContext.Instance.IsCurrent(this))
            (show ? _closeDetails : _cancel).GrabFocus();
    }

    private void RefreshAvailability()
    {
        if (_levelButtons[0] == null || _relic == null || _closing) return;
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            bool available = IsLevelAvailable(_relic, i + 1);
            _levelButtons[i].Disabled = _showDetails || !available;
            if (_available[i] == available) continue;
            _available[i] = available;
            _levelImages[i].Material = available ? null : _disabledMaterial;
            _levelImages[i].Texture = Texture($"ButtonOverdrive{i + 1}Default");
            _numbers[i].Material = available ? null : _disabledMaterial;
            Texture2D ring = Texture(available ? $"PartsOverdriveCircleLv{i + 1}" : "PartsOverdriveCircleDisable");
            _rings[i, 0].Texture = ring;
            _rings[i, 1].Texture = ring;
            _rings[i, 0].Modulate = _rings[i, 1].Modulate = available
                ? Colors.White : new Color(0.8313726f, 0.8313726f, 0.8313726f);
        }
        int remaining = _relic.RemainingFloorLevels;
        _floorBudget.SetRemaining(remaining);
        string charge = $"OD  {_relic.PreciseCharge.ToString("0.##", CultureInfo.InvariantCulture)} / {_relic.MaxCharge}    全队本幕剩余 {remaining} / {Overclock.FloorLevelLimit} 级";
        if (_lastCharge == charge) return;
        _lastCharge = charge;
        _charge.Text = charge;
    }

    private void LayoutCanvas()
    {
        if (_canvas == null) return;
        Vector2 available = Size;
        float scale = Math.Min(available.X / ReferenceSize.X, available.Y / ReferenceSize.Y);
        _canvas.Scale = Vector2.One * Math.Max(0.1f, scale);
        _canvas.Position = (available - ReferenceSize * scale) / 2f;
    }

    private void BuildUi()
    {
        var shade = new ColorRect { Color = new Color("28212E8B"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "SourceCanvas", Size = ReferenceSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        Sprite(_canvas, "Beam", "GradationLine", new(0, 486), new(1820, 52));
        Sprite(_canvas, "EnergySequence", "EnergySequenceColor_0", new(0, 492), new(1820, 40));
        Sprite(_canvas, "LeftCircuitTop", "PartsoverdriveBgLeft", new(38.63f, 409.91f), new(676, 92)).FlipV = true;
        Sprite(_canvas, "RightCircuitTop", "PartsoverdriveBgRight", new(1084.5f, 409.91f), new(676, 92)).FlipV = true;
        Sprite(_canvas, "LeftCircuitBottom", "PartsoverdriveBgLeft", new(38.63f, 520.68f), new(676, 92));
        Sprite(_canvas, "RightCircuitBottom", "PartsoverdriveBgRight", new(1084.5f, 520.68f), new(676, 92));
        _selection = new Control { Name = "LevelSelection", Size = ReferenceSize, MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_selection);
        for (int i = 0; i < Overclock.MaxLevel; i++) BuildLevel(i);
        _cancel = ArtButton(_selection, "Cancel", "取消", new(708, 844), new(404, 114), Close);
        Slice(_selection, "CancelLineLeft", "cancelParts", new(250.1f, 877), new(412.8f, 48), 199, 60);
        NinePatchRect right = Slice(_selection, "CancelLineRight", "cancelParts", new(1157.1f, 877), new(412.8f, 48), 199, 60);
        right.PivotOffset = right.Size / 2f;
        right.Scale = new(-1, 1);
        _info = IconButton(_selection, "Information", "BtnInformationSmallDefault", new(1706, 250.5f), new(80, 80), () => SetDetails(true));
        _floorBudget = new NHbrOverdriveFloorBudget
        {
            Name = "OriginalFloorBudgetTokens",
            Position = new Vector2((ReferenceSize.X - NHbrOverdriveFloorBudget.RowWidth) / 2f, 790f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _selection.AddChild(_floorBudget);
        _charge = Text(_selection, "Charge", "", new(460, 750), new(900, 36), 26, new("E4C8ED"));
        _hint = Text(_canvas, "Controls", "1 / 2 / 3  选择等级     I  超频详情     Esc / 右键  取消",
            new(890, 976), new(900, 36), 21, new("DDAAE1"), HorizontalAlignment.Right);
        _hint.AddThemeFontOverride("font", _grayFont);
        BuildDetails();
    }

    private void BuildLevel(int index)
    {
        int level = index + 1;
        Vector2 center = new(430 + 480 * index, 512);
        var button = EmptyButton($"Level{level}", center - new Vector2(166, 166), new(332, 332));
        _selection.AddChild(button);
        _levelButtons[index] = button;
        _levelImages[index] = Sprite(button, "Button", $"ButtonOverdrive{level}Default", new(-12, -12), new(356, 356));
        _numbers[index] = Sprite(button, "Number", $"NumberOverdrive{level}", new(124, 121), new(84, 90));
        _rings[index, 0] = Sprite(button, "LowerRing", $"PartsOverdriveCircleLv{level}", new(23.35f, 181.5f), new(280, 128));
        _rings[index, 1] = Sprite(button, "UpperRing", $"PartsOverdriveCircleLv{level}", new(28.65f, 22.5f), new(280, 128));
        _rings[index, 1].PivotOffset = new(140, 64);
        _rings[index, 1].Rotation = Mathf.Pi;
        for (int n = 0; n < level; n++)
            Sprite(button, $"Token{n + 1}", "IconOverDriveToken", new(148 + (n - index / 2f) * 37, -14.48f), new(36, 36));
        Text(button, "Shortcut", level.ToString(), new(136, 336), new(60, 48), 27, LevelColors[index]);
        button.Pressed += () => SelectLevel(level);
        button.MouseEntered += () => SetLevelHover(index, true);
        button.MouseExited += () => SetLevelHover(index, false);
        button.FocusEntered += () => SetLevelHover(index, true);
        button.FocusExited += () => SetLevelHover(index, false);
    }

    private void SetLevelHover(int index, bool hovered)
    {
        _levelImages[index].Texture = Texture($"ButtonOverdrive{index + 1}" +
            (hovered && !_levelButtons[index].Disabled ? "Active" : "Default"));
    }

    private void BuildDetails()
    {
        _details = new Control { Name = "Details", Size = ReferenceSize, Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _canvas.AddChild(_details);
        _details.AddChild(new ColorRect { Name = "DetailShade", Size = ReferenceSize, Color = new("3E32484D"), MouseFilter = MouseFilterEnum.Ignore });
        var panel = new Control { Name = "InformationWindow", Position = new(153, 190), Size = new(1514, 643.4f), MouseFilter = MouseFilterEnum.Stop };
        _details.AddChild(panel);
        var artwork = new Control { Name = "WindowArtwork", Size = panel.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(artwork);
        TextureRect background = Sprite(artwork, "Background", "WindowType2", Vector2.Zero, panel.Size);
        background.Modulate = new Color("1D1626F2");
        Decoration(artwork, "Emblem1", "WindowEmblem", new(123.8f, 287.5f), new(336, 420), 0);
        Decoration(artwork, "Emblem2", "WindowEmblem", new(432, 167.7f), new(336, 420), -120);
        Decoration(artwork, "Emblem3", "WindowEmblem", new(178, -35.3f), new(336, 420), -240);
        Decoration(artwork, "Circuit1", "WindowLine", new(457.7f, 370.4f), new(1042.364f, 56), -30);
        Decoration(artwork, "Circuit2", "WindowLine", new(1057, 272.9f), new(1042.364f, 56), -210);
        Decoration(artwork, "Circuits", "WindowLines", new(1474.9f, 517.1f), new(512, 616), -30);
        var frame = new NinePatchRect { Name = "Frame", Texture = Texture("WindowLineType2"),
            PatchMarginLeft = 53, PatchMarginTop = 53, PatchMarginRight = 53, PatchMarginBottom = 53,
            Position = new(-21, -21), Size = new(1556, 680.4f), MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(frame);
        Text(panel, "Title", "INFORMATION", new(300, 40), new(914, 54), 32, new("BE9898"));
        _closeDetails = IconButton(panel, "Close", "btnCloseNormal", new(1400, 0), new(112, 112), () => SetDetails(false));
        var table = new NinePatchRect { Name = "EffectsTable", Position = new(48, 141), Size = new(1418, 425),
            Texture = Texture("WindowBaseType1"), PatchMarginLeft = 17, PatchMarginRight = 17,
            PatchMarginTop = 17, PatchMarginBottom = 17, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(table);
        var header = new NinePatchRect { Position = new(26, 28), Size = new(1366, 44), Texture = Texture("box2"),
            PatchMarginLeft = 17, PatchMarginRight = 17, PatchMarginTop = 17, PatchMarginBottom = 17,
            SelfModulate = new("64546F"), MouseFilter = MouseFilterEnum.Ignore };
        table.AddChild(header);
        float[] widths = { 380, 322, 322, 342 };
        string[] rows = { "效果", "即时额外回合", "伤害加成", "OD 回合能量", "OD 回合抽牌", "OD 消耗" };
        for (int row = 0; row < rows.Length; row++)
        {
            float y = 28 + row * 64;
            Text(table, $"Row{row}", rows[row], new(26, y), new(widths[0], 44), 27, Colors.White);
            if (row > 1)
                table.AddChild(new ColorRect { Position = new(26, y - 7), Size = new(1366, 2), Color = new("776F7977"), MouseFilter = MouseFilterEnum.Ignore });
            float x = 26 + widths[0];
            for (int level = 1; level <= Overclock.MaxLevel; level++)
            {
                table.AddChild(new ColorRect { Position = new(x, y + 3), Size = new(3, 38), Color = new("BDB5BF77"), MouseFilter = MouseFilterEnum.Ignore });
                float width = widths[level];
                if (row == 3)
                {
                    int energy = (int)Overclock.GetExtraTurnEnergy(level);
                    Texture2D energyIcon = ResourceLoader.Load<Texture2D>(EnergyIconHelper.GetPath(_relic));
                    for (int n = 0; n < energy; n++)
                    {
                        // Ignore the source texture's minimum size before setting
                        // Size; otherwise Godot clamps this icon to the full image.
                        var icon = new TextureRect { Name = $"Level{level}Energy{n}",
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Texture = energyIcon,
                            Position = new(x + (width - energy * 35) / 2f + n * 35, y + 4), Size = new(35, 35),
                            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                            MouseFilter = MouseFilterEnum.Ignore };
                        table.AddChild(icon);
                    }
                }
                else
                {
                    string value = row switch
                    {
                        0 => $"LEVEL{level}",
                        1 => "1 回合",
                        2 => "+" + ((Overclock.GetDamageMultiplier(level) - 1m) * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%",
                        4 => $"{Overclock.GetExtraTurnDraw(level)} 张",
                        _ => $"-{level} 级"
                    };
                    Label valueLabel = Text(table, $"Level{level}Row{row}", value, new(x, y), new(width, 44), row == 0 ? 30 : 28,
                        row == 0 ? LevelColors[level - 1] : row == 5 ? new Color("F79FA6") : new Color("92E3EC"));
                    if (row == 0) valueLabel.AddThemeFontOverride("font", _levelFonts[level - 1]);
                }
                x += width;
            }
        }
        Text(panel, "Mechanics", "全队每幕累计最多使用12级OD（1级12次、2级6次或3级4次，可混合）；进入下一幕恢复额度，本幕跨战斗共享；免费触发不扣储量和额度。\n全队伤害50%转为OD，攻击/技能/能力牌每次获得2/5/10点，受获取倍率增幅；立即暂存手牌/能量，按上表重新抽牌/设置；已打出的能力牌和消耗牌不加入OD牌组。\n结束后恢复普通回合，战斗结束储量归零。进入时幻影与爪牙各行动一次；OD期间正面能力的持续时间不递减，触发消耗照常；伤害加成与同类效果相加。",
            new(60, 568), new(1394, 72), 17, new("D9CADC"));
    }

    private Button ArtButton(Control parent, string name, string caption, Vector2 position, Vector2 size, Action action)
    {
        Button button = EmptyButton(name, position, size);
        parent.AddChild(button);
        var art = new NinePatchRect { Texture = Texture("BtnDefL2Normal"), Size = size,
            PatchMarginLeft = 60, PatchMarginRight = 60, MouseFilter = MouseFilterEnum.Ignore };
        button.AddChild(art);
        Sprite(button, "CenterDecoration", "BtnDefDecoCenter", new(size.X / 2f - 59.5f, 9), new(119, 96)).Modulate = new("817773");
        Sprite(button, "LeftDecoration", "BtnDefDecoSide", new(29, 24.5f), new(130, 65)).Modulate = new("817773");
        TextureRect rightDecoration = Sprite(button, "RightDecoration", "BtnDefDecoSide", new(size.X - 159, 24.5f), new(130, 65));
        rightDecoration.Modulate = new("817773");
        rightDecoration.FlipH = true;
        Text(button, "Caption", caption, Vector2.Zero, size, 50, Colors.White)
            .AddThemeFontOverride("font", _grayFont);
        button.Pressed += action;
        bool hovered = false, focused = false, pressed = false;
        void UpdateArt()
        {
            bool highlighted = hovered || focused || pressed;
            art.Texture = Texture(pressed ? "BtnDefL2Active" : highlighted ? "BtnDefL2Focused" : "BtnDefL2Normal");
            art.Position = highlighted ? new Vector2(-16, -16) : Vector2.Zero;
            art.Size = highlighted ? size + new Vector2(32, 32) : size;
            art.PatchMarginLeft = art.PatchMarginRight = highlighted ? 72 : 60;
            art.PatchMarginTop = art.PatchMarginBottom = highlighted ? 66 : 0;
        }
        button.MouseEntered += () => { hovered = true; UpdateArt(); };
        button.MouseExited += () => { hovered = false; UpdateArt(); };
        button.FocusEntered += () => { focused = true; UpdateArt(); };
        button.FocusExited += () => { focused = false; UpdateArt(); };
        button.ButtonDown += () => { pressed = true; UpdateArt(); };
        button.ButtonUp += () => { pressed = false; UpdateArt(); };
        return button;
    }

    private Button IconButton(Control parent, string name, string sprite, Vector2 position, Vector2 size, Action action)
    {
        Button button = EmptyButton(name, position, size);
        parent.AddChild(button);
        TextureRect image = Sprite(button, "Image", sprite, Vector2.Zero, size);
        button.Pressed += action;
        if (sprite == "btnCloseNormal")
        {
            bool hovered = false, focused = false, pressed = false;
            void UpdateArt() => image.Texture = Texture(hovered || focused || pressed ? "btnCloseActive" : "btnCloseNormal");
            button.MouseEntered += () => { hovered = true; UpdateArt(); };
            button.MouseExited += () => { hovered = false; UpdateArt(); };
            button.FocusEntered += () => { focused = true; UpdateArt(); };
            button.FocusExited += () => { focused = false; UpdateArt(); };
            button.ButtonDown += () => { pressed = true; UpdateArt(); };
            button.ButtonUp += () => { pressed = false; UpdateArt(); };
        }
        return button;
    }

    private static Button EmptyButton(string name, Vector2 position, Vector2 size)
    {
        var button = new Button { Name = name, Position = position, Size = size, FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand, MouseFilter = MouseFilterEnum.Stop };
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        return button;
    }

    private Label Text(Control parent, string name, string text, Vector2 position, Vector2 size, int fontSize,
        Color color, HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var label = new Label { Name = name, Text = text, Position = position, Size = size,
            HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        // Source TMP material variants already carry their authored underlay.
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        label.AddThemeConstantOverride("outline_size", 0);
        parent.AddChild(label);
        return label;
    }

    private static TextureRect Sprite(Control parent, string name, string sprite, Vector2 position, Vector2 size)
    {
        var image = new TextureRect { Name = name, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = Texture(sprite), Position = position, Size = size, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(image);
        return image;
    }

    private static void Decoration(Control parent, string name, string sprite, Vector2 center, Vector2 size, float angle)
    {
        Control image = sprite == "WindowLine"
            ? Slice(parent, name, sprite, center - size / 2f, size, 30, 30)
            : Sprite(parent, name, sprite, center - size / 2f, size);
        image.PivotOffset = size / 2f;
        image.RotationDegrees = angle;
    }

    private static NinePatchRect Slice(Control parent, string name, string sprite, Vector2 position, Vector2 size, int left, int right)
    {
        var image = new NinePatchRect { Name = name, Texture = Texture(sprite), Position = position, Size = size,
            PatchMarginLeft = left, PatchMarginRight = right, MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(image);
        return image;
    }

    private static Texture2D Texture(string name) => ResourceLoader.Load<Texture2D>(Art + name + ".png");

}
