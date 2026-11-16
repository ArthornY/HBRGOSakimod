using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The 30G proposal (requirement 72, picture side), ported frame for frame from the approved sample
// (ui-concepts/proposal-30g-20261115/proposal.py, frame_full): dim, the card flies to the middle,
// 白河's framed cut-in and her line, the target's framed surprise cut-in with 「——」, the story explosion
// (Adventure/Explosion, baked to frames) with 「轰隆———！！！」, 「叮」, the 「卒」 seal and the SAN count.
// Layout is in the sample's 1280 x 720 space, scaled by the screen height. The gameplay state is already
// saved before this runs; a click (or any key) jumps to the card flying back.
public partial class NThirtyGProposalOverlay : CanvasLayer
{
    private const string ArtRoot = NThirtyGCardDecor.ArtRoot;
    private const int FrameCount = 41;
    private const float DesignW = 1280f, DesignH = 720f;
    private const float WindowScale = 300f / 546f;          // the story window (546 px wide) shown 300 px wide
    private static readonly Vector2 DesignCentre = new(640, 322), DesignPile = new(54, 668);
    private static readonly Color Ink = new(27 / 255f, 21 / 255f, 48 / 255f);
    private static readonly Color Pink = new(1f, 92 / 255f, 168 / 255f);
    private static readonly Color PinkLight = new(1f, 190 / 255f, 222 / 255f);

    private sealed record Cast(string Zh, string En, string Cutin);
    // How 白河 addresses each memory-garden character (names.json of the sample: surnames; sisters by given
    // name; single-character surnames in full; 手冢咲 is 「司令官」) and which framed surprise cut-in shows.
    private static readonly Dictionary<string, Cast> Casts = new(StringComparer.Ordinal)
    {
        ["ruka"] = new("茅森", "Kayamori", "RKayamoriDefaultAdvCutInSurprise.png"),
        ["yuki"] = new("和泉", "Izumi", "YIzumiDefaultAdvCutInSurprise.png"),
        ["megumi"] = new("逢川", "Aikawa", "MAikawaDefaultAdvCutInSurprise.png"),
        ["tsukasa"] = new("东城", "Tojo", "TTojoDefaultAdvCutInSurprise.png"),
        ["karen"] = new("朝仓", "Asakura", "KAsakuraDefaultAdvCutInSurprise.png"),
        ["tama"] = new("国见", "Kunimi", "TKunimiDefaultAdvCutInSurprise.png"),
        ["erika"] = new("苍井", "Aoi", "EAoiDefaultAdvCutInSurprise.png"),
        ["ichigo"] = new("莓", "Ichigo", "IMinaseDefaultAdvCutInSurprise.png"),
        ["sumomo"] = new("李", "Sumomo", "SMinaseDefaultAdvCutInSurprise.png"),
        ["seika"] = new("樋口", "Higuchi", "SHiguchiDefaultAdvCutInSurprise.png"),
        ["kozue"] = new("柊木", "Hiiragi", "KHiiragiDefaultAdvCutInSurprise.png"),
        ["byakko"] = new("白虎", "Byakko", "ByakkoDefaultAdvCutInSurprise.png"),
        ["wakki"] = new("山胁", "Yamawaki", "BIYamawakiDefaultAdvCutInSurprise.png"),
        ["seira"] = new("樱庭", "Sakuraba", "SSakurabaDefaultAdvCutInSurprise.png"),
        ["miko"] = new("天音", "Tenne", "MTenneDefaultAdvCutInSurprise.png"),
        ["yayoi"] = new("丰后", "Bungo", "YBungoDefaultAdvCutInSurprise.png"),
        ["adelheid"] = new("神崎", "Kanzaki", "AKanzakiDefaultAdvCutInSurprise.png"),
        ["mari"] = new("佐月", "Satsuki", "MSatsukiDefaultAdvCutInSurprise.png"),
        ["misato"] = new("二阶堂", "Nikaido", "MNikaidoDefaultAdvCutInSurprise.png"),
        ["iroha"] = new("石井", "Ishii", "IIshiiDefaultAdvCutInSurprise.png"),
        ["fubuki"] = new("命吹雪", "Mikoto", "FMikotoDefaultAdvCutInSurprise.png"),
        ["risa"] = new("室伏", "Murofushi", "RMurohushiDefaultAdvCutInSurprise.png"),
        ["akari"] = new("伊达", "Date", "ADateDefaultAdvCutInSurprise.png"),
        ["aina"] = new("瑞原", "Mizuhara", "AMizuharaDefaultAdvCutInSurprise.png"),
        ["ichiko"] = new("一千子", "Ichiko", "IcOhshimaDefaultAdvCutInSurprise.png"),
        ["nina"] = new("二以奈", "Niina", "NiOhshimaDefaultAdvCutInSurprise.png"),
        ["minori"] = new("三野里", "Minori", "MiOhshimaDefaultAdvCutInSurprise.png"),
        ["yotsuha"] = new("四叶草", "Yotsuha", "YoOhshimaDefaultAdvCutInSurprise.png"),
        ["isuzu"] = new("五十铃", "Isuzu", "IrOhshimaDefaultAdvCutInSurprise.png"),
        ["mua"] = new("六宇亚", "Muua", "MuOhshimaDefaultAdvCutInSurprise.png"),
        ["monaka"] = new("月城", "Tsukishiro", "MTsukishiroDefaultAdvCutInSurprise.png"),
        ["miya"] = new("桐生", "Kiryu", "MKiryuDefaultAdvCutInSurprise.png"),
        ["chie"] = new("菅原", "Sugahara", "CSugaharaDefaultAdvCutInSurprise.png"),
        ["hisame"] = new("小笠原", "Ogasawara", "HOgasawaraDefaultAdvCutInSurprise.png"),
        ["satomi"] = new("藏里见", "Kura", "SKuraDefaultAdvCutInSurprise.png"),
        ["tezuka_saki"] = new("司令官", "Commander", "STezukaDefaultAdvCutInSurprise.png"),
        ["nanami"] = new("七濑", "Nanase", "NNanaseDefaultAdvCutInSurprise.png"),
        ["mutsumi"] = new("若叶", "Wakaba", "MWakabaDefaultAdvCutInSurprise.png"),
        ["taki"] = new("椎名", "Shiina", "TShiinaDefaultAdvCutInSurprise.png"),
        ["rana"] = new("要乐奈", "Kaname", "RKanameDefaultAdvCutInSurprise.png"),
        ["tomori"] = new("高松", "Takamatsu", "TTakamatsuDefaultAdvCutInSurprise.png"),
        ["anon"] = new("千早", "Chihaya", "AChihayaDefaultAdvCutInSurprise.png"),
        ["soyo"] = new("长崎", "Nagasaki", "SNagasakiDefaultAdvCutInSurprise.png"),
        ["mion"] = new("柳", "Yanagi", "MYanagiDefaultAdvCutInSurprise.png"),
        ["kanata"] = new("丸山", "Maruyama", "KMaruyamaDefaultAdvCutInSurprise.png"),
        ["shiki"] = new("华村", "Hanamura", "SHanamuraDefaultAdvCutInSurprise.png"),
        ["chiroru"] = new("松冈", "Matsuoka", "CMatsuokaDefaultAdvCutInSurprise.png"),
        ["inori"] = new("夏目", "Natsume", "INatsumeDefaultAdvCutInSurprise.png"),
        ["maki"] = new("黑泽", "Kurosawa", "MKurosawaDefaultAdvCutInSurprise.png"),
        ["carol"] = new("卡罗尔", "Carol", "CReaperDefaultAdvCutInSurprise.png"),   // the original masked surprise
        ["yingxia"] = new("李映夏", "Li Yingxia", "LShanhuaDefaultAdvCutInSurprise.png"),   // stand-in, redrawn later under the same name
        ["irene"] = new("艾琳", "Irene", "IRedmayneDefaultAdvCutInSurprise.png"),
        ["vritika"] = new("芙莉缇卡", "Vritika", "VBalakrishnanDefaultAdvCutInSurprise.png"),
        ["maria"] = new("玛利亚", "Maria", "MdAngelisDefaultAdvCutInSurprise.png"),
        ["charlotta"] = new("夏洛", "Charlotta", "CSkopovskayaDefaultAdvCutInSurprise.png"),
        // A character missing here (or a missing file) still gets the line and 「——」; the right window stays empty.
    };

    private sealed class Timeline
    {
        internal float Fly1, AIn0, AIn1, Line0, BIn0, BIn1, Out0, Out1, Boom, Zu, XSpeed, XFade0, XFade1,
            Soul, Ding, Stamp, San, Back0, Back1, End, DimOut0, DimOut1, ShakeAmp;
        internal bool Quick;
    }
    private static readonly Timeline Full = new()
    {
        Fly1 = .45f, AIn0 = .45f, AIn1 = .78f, Line0 = .8f, BIn0 = 1.7f, BIn1 = 1.86f, Out0 = 2.42f, Out1 = 2.56f,
        Boom = 2.5f, Zu = 2.56f, XSpeed = 1.2f, XFade0 = 58f, XFade1 = 80f, Soul = 3.38f, Ding = 3.5f, Stamp = 3.62f,
        San = 3.82f, Back0 = 4.5f, Back1 = 4.96f, End = 5f, DimOut0 = 4.58f, DimOut1 = 4.98f, ShakeAmp = 12f
    };
    private static readonly Timeline Quick = new()
    {
        Fly1 = .22f, AIn0 = .14f, AIn1 = .3f, Line0 = .2f, BIn0 = .3f, BIn1 = .42f, Out0 = .66f, Out1 = .76f,
        Boom = .72f, Zu = .76f, XSpeed = 1.6f, XFade0 = 40f, XFade1 = 62f, Soul = 1.22f, Ding = 1.26f, Stamp = 1.36f,
        San = -1f, Back0 = 1.6f, Back1 = 1.88f, End = 1.92f, DimOut0 = 1.66f, DimOut1 = 1.9f, ShakeAmp = 9f, Quick = true
    };

    private const string DimShader = """
        shader_type canvas_item;
        // The sample's dim: the screen blurred a little, darkened with (8, 6, 14) at 165/255, blended in.
        uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
        uniform float amount = 0.0;
        uniform float lod = 2.0;
        void fragment() {
            vec3 base = textureLod(screen_tex, SCREEN_UV, 0.0).rgb;
            vec3 blur = textureLod(screen_tex, SCREEN_UV, lod).rgb;
            vec3 dark = mix(blur, vec3(8.0, 6.0, 14.0) / 255.0, 165.0 / 255.0);
            COLOR = vec4(mix(base, dark, amount), 1.0);
        }
        """;
    private const string HazeShader = """
        shader_type canvas_item;
        // The explosion's heat haze: the screen zoomed out round the blast six times and averaged.
        uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
        uniform vec2 centre = vec2(0.5);
        uniform float strength = 0.0;
        void fragment() {
            vec3 acc = textureLod(screen_tex, SCREEN_UV, 0.0).rgb;
            float total = 1.0;
            for (int i = 1; i <= 6; i++) {
                float z = 1.0 + 0.012 * float(i) * strength;
                float w = 1.0 / (1.0 + float(i) * 0.6);
                acc += textureLod(screen_tex, centre + (SCREEN_UV - centre) / z, 0.0).rgb * w;
                total += w;
            }
            COLOR = vec4(acc / total, 1.0);
        }
        """;
    private static Shader _dimShader, _hazeShader;

    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Texture2D[] _frames = new Texture2D[FrameCount];
    private readonly bool[] _requested = new bool[FrameCount];
    private ThirtyGProposalPresentation _proposal;
    private Timeline _tl;
    private bool _english, _finished, _kiryu;
    private float _t, _u, _ox;
    private Vector2 _screen, _pile, _centre;
    private int _sanBefore, _sanAfter;
    private ColorRect _dim, _haze, _flashB, _flash;
    private BackBufferCopy _dimCopy, _hazeCopy, _zuCopy;
    private Control _stage, _pivot, _san;
    private NCard _card;
    private TextureRect _halo, _zuGrey, _zuScorch, _stamp, _winA, _winB, _dash, _explosion, _rumble, _ding;
    private readonly TextureRect[] _puffs = new TextureRect[3];
    private readonly TextureRect[] _souls = new TextureRect[2];
    private readonly Label[] _lineGlow = new Label[3];
    private Label _line, _sanMain, _sanRoll, _broken;
    private ColorRect _sanDivider;
    private PanelContainer _cardPill;
    private int _lineA, _lineB;

    internal Task Play(ThirtyGProposalPresentation proposal, bool full)
    {
        _proposal = proposal;
        _tl = full ? Full : Quick;
        _english = NThirtyGCardDecor.English;
        _kiryu = proposal.CharacterId == "miya";
        _sanBefore = Math.Clamp(ThirtyGBondHelper.GetRunState(proposal.Player).San, 0, 100);
        _sanAfter = Math.Clamp(proposal.SanAfter, 0, _sanBefore);
        Layer = 95;
        ProcessMode = ProcessModeEnum.Always;
        RequestFrames();
        Build();
        Advance(0f);
        SetProcess(true);
        return _done.Task;
    }

    internal void Finish()
    {
        if (_finished) return;
        _finished = true;
        SetProcess(false);
        ReleaseCard();
        DrainFrames();
        if (IsInsideTree()) QueueFree();
        _done.TrySetResult();
    }

    public override void _ExitTree()
    {
        if (_finished) return;
        _finished = true;
        ReleaseCard();
        DrainFrames();
        _done.TrySetResult();
    }

    public override void _Process(double delta)
    {
        if (_finished) return;
        try { Advance(Mathf.Min((float)delta, 0.1f)); }
        catch (Exception error)
        {
            // Never leave a dimmed screen that eats clicks: stop and let the combat start go on.
            GD.PushWarning("[HBRGOSakiMod][30G] Proposal performance stopped: " + error.Message);
            Finish();
            return;
        }
        if (_t >= _tl.End) Finish();
    }

    public override void _Input(InputEvent input)
    {
        if (_finished) return;
        bool press = input is InputEventKey { Pressed: true, Echo: false } || input is InputEventJoypadButton { Pressed: true };
        if (!press) return;
        Skip();
        GetViewport().SetInputAsHandled();
    }

    private void OnBackdropInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true }) Skip();
    }

    private void Skip()
    {
        if (_t < _tl.Back0) _t = _tl.Back0;
    }

    private void ReleaseCard()
    {
        if (!GodotObject.IsInstanceValid(_card)) return;
        if (_card.HasMeta(NThirtyGCardDecor.HoldMeta)) _card.RemoveMeta(NThirtyGCardDecor.HoldMeta);
        _card.QueueFreeSafely();
        _card = null;
    }

    // ---------------------------------------------------------------- building

    private Vector2 S(float x, float y) => new(_ox + x * _u, y * _u);

    private void Build()
    {
        _screen = GetViewport().GetVisibleRect().Size;
        _u = _screen.Y / DesignH;
        _ox = (_screen.X - DesignW * _u) * 0.5f;
        _centre = S(DesignCentre.X, DesignCentre.Y);
        _pile = S(DesignPile.X, DesignPile.Y);
        if (NCombatRoom.Instance?.Ui?.DrawPile is { } drawPile && GodotObject.IsInstanceValid(drawPile) && drawPile.IsVisibleInTree())
        {
            Vector2 p = drawPile.GlobalPosition + drawPile.Size * 0.5f;
            if (p.X > 0 && p.Y > 0 && p.X < _screen.X && p.Y < _screen.Y) _pile = p;
        }
        Vector2 margin = new(48 * _u, 48 * _u);

        _dimCopy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport };
        AddChild(_dimCopy);
        _dim = new ColorRect { Position = Vector2.Zero, Size = _screen, MouseFilter = Control.MouseFilterEnum.Stop };
        _dim.Material = new ShaderMaterial { Shader = _dimShader ??= new Shader { Code = DimShader } };
        ((ShaderMaterial)_dim.Material).SetShaderParameter("lod", Mathf.Max(0.5f, Mathf.Log(3f * _u) / Mathf.Log(2f)));
        _dim.GuiInput += OnBackdropInput;
        AddChild(_dim);
        _stage = new Control { Position = Vector2.Zero, Size = _screen, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_stage);

        // The card, with everything that sits on it in card units (300 x 422 round its centre).
        _pivot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _stage.AddChild(_pivot);
        _halo = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite("glow.png"), NThirtyGCardDecor.SheetPos * 1.04f, NThirtyGCardDecor.SheetSize * 1.04f);
        _pivot.AddChild(_halo);
        _card = NCard.Create(_proposal.TargetCard);
        if (_card != null)
        {
            _card.SetMeta(NThirtyGCardDecor.HoldMeta, true);
            _pivot.AddChild(_card);
            _card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        }
        _zuCopy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Rect, Rect = new Rect2(-200, -262, 400, 524), Visible = false };
        _pivot.AddChild(_zuCopy);
        _zuGrey = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite("mask.png"), NThirtyGCardDecor.SheetPos, NThirtyGCardDecor.SheetSize);
        _zuGrey.Material = new ShaderMaterial { Shader = NThirtyGCardDecor.GreyShader };
        _zuGrey.Visible = false;
        _pivot.AddChild(_zuGrey);
        _zuScorch = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite("scorch.png"), NThirtyGCardDecor.SheetPos, NThirtyGCardDecor.SheetSize);
        _zuScorch.Visible = false;
        _pivot.AddChild(_zuScorch);
        for (int i = 0; i < _puffs.Length; i++)
        {
            _puffs[i] = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite("puff_" + i + ".png"), Vector2.Zero, new Vector2(120, 120));
            _puffs[i].Visible = false;
            _pivot.AddChild(_puffs[i]);
        }
        string soul = _proposal.Reaction == 3 ? "soul_pink.png" : "soul.png";
        for (int i = 0; i < _souls.Length; i++)
        {
            _souls[i] = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite(soul), Vector2.Zero, NThirtyGCardDecor.SoulSize);
            _souls[i].Visible = false;
            _pivot.AddChild(_souls[i]);
        }
        _stamp = NThirtyGCardDecor.Picture(NThirtyGCardDecor.Sprite("stamp.png"), Vector2.Zero,
            new Vector2(NThirtyGCardDecor.StampSize, NThirtyGCardDecor.StampSize));
        _stamp.Visible = false;
        _pivot.AddChild(_stamp);
        (string tag, Color fill) = _proposal.Reaction == 2
            ? (_english ? "Discharge!" : "放电！", NThirtyGCardDecor.PillDischarge)
            : NThirtyGCardDecor.GraduatedTag(_kiryu);
        if (!string.IsNullOrEmpty(tag))
        {
            _cardPill = NThirtyGCardDecor.Pill(tag, fill, 17f, 2f);
            _cardPill.Visible = false;
            _pivot.AddChild(_cardPill);
            _cardPill.ResetSize();
            _cardPill.PivotOffset = _cardPill.Size * 0.5f;
            _cardPill.Position = new Vector2(0, 187) - _cardPill.Size * 0.5f;
        }

        // 白河's window, her line, the target's window and 「——」.
        Vector2 windowSize = new Vector2(1024, 1368) * WindowScale * _u;
        _winA = Window("YShirakawaProposal.png", windowSize);
        Casts.TryGetValue(_proposal.CharacterId ?? "", out Cast cast);
        string address = cast == null ? CatalogName() : _english ? cast.En : cast.Zh;
        string partA = _english ? address + "..." : address + "……";
        string partB = _english ? " Marry me." : "和我、结婚吧。";
        _lineA = partA.Length;
        _lineB = partB.Length;
        float fontSize = 46f * _u;
        float[] glowSizes = { 40f, 28f, 18f };
        float[] glowAlpha = { 0.16f, 0.28f, 0.5f };
        for (int i = 0; i < _lineGlow.Length; i++)
        {
            _lineGlow[i] = TextLabel(partA + partB, fontSize, new Color(1, 1, 1, 0), new Color(Pink, glowAlpha[i]), glowSizes[i] * _u);
            _stage.AddChild(_lineGlow[i]);
        }
        _line = TextLabel(partA + partB, fontSize, Colors.White, Ink, 12f * _u);
        _stage.AddChild(_line);
        _winB = cast?.Cutin == null ? null : Window(cast.Cutin, windowSize);
        _dash = Sprite("dash.png", 1.5f);
        _stage.AddChild(_dash);
        _flashB = Flash(margin);

        // The explosion over everything drawn so far: haze, frames, the red sound word, the white flash.
        _hazeCopy = new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Visible = false };
        _stage.AddChild(_hazeCopy);
        _haze = new ColorRect { Position = -margin, Size = _screen + margin * 2, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _haze.Material = new ShaderMaterial { Shader = _hazeShader ??= new Shader { Code = HazeShader } };
        ((ShaderMaterial)_haze.Material).SetShaderParameter("centre", new Vector2(_centre.X / _screen.X, _centre.Y / _screen.Y));
        _stage.AddChild(_haze);
        float cover = Mathf.Max(_screen.X / DesignW, _screen.Y / DesignH);
        _explosion = NThirtyGCardDecor.Picture(null, _centre - DesignCentre * cover, new Vector2(DesignW, DesignH) * cover);
        _explosion.Visible = false;
        _stage.AddChild(_explosion);
        _rumble = Sprite(_english ? "rumble_en.png" : "rumble.png", 1.5f);
        _stage.AddChild(_rumble);
        _flash = Flash(margin);
        _ding = Sprite(_english ? "ding_en.png" : "ding.png", 1.5f);
        _stage.AddChild(_ding);
        BuildSan();
    }

    private string CatalogName()
    {
        foreach (var entry in HBRGOSakiMod.Core.Story.HbrStoryCharacterCatalog.Entries)
            if (entry.Id == _proposal.CharacterId) return entry.Name;
        return _proposal.TargetCard?.Title ?? "";
    }

    private TextureRect Window(string file, Vector2 size)
    {
        string path = ArtRoot + "cutin/" + file;
        if (!ResourceLoader.Exists(path)) return null;
        TextureRect window = NThirtyGCardDecor.Picture(ResourceLoader.Load<Texture2D>(path), Vector2.Zero, size);
        window.Visible = false;
        _stage.AddChild(window);
        return window;
    }

    // A sprite exported at `exported` times the sample's 720p size, shown at the screen's scale.
    private TextureRect Sprite(string file, float exported)
    {
        Texture2D texture = NThirtyGCardDecor.Sprite(file);
        Vector2 size = texture == null ? Vector2.One : texture.GetSize() * (_u / exported);
        TextureRect sprite = NThirtyGCardDecor.Picture(texture, Vector2.Zero, size);
        sprite.Visible = false;
        return sprite;
    }

    private ColorRect Flash(Vector2 margin)
    {
        var flash = new ColorRect { Color = new Color(1, 1, 1, 0), Position = -margin, Size = _screen + margin * 2,
            MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _stage.AddChild(flash);
        return flash;
    }

    private static Label TextLabel(string text, float size, Color fill, Color outline, float outlineSize)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        label.AddThemeFontSizeOverride("font_size", Mathf.Max(8, Mathf.RoundToInt(size)));
        label.AddThemeColorOverride("font_color", fill);
        label.AddThemeColorOverride("font_outline_color", outline);
        label.AddThemeConstantOverride("outline_size", Mathf.Max(1, Mathf.RoundToInt(outlineSize)));
        return label;
    }

    // 「白河 SAN 100 → 65 | 1d100 = 35」: the number part has a fixed width, the roll sits behind a divider.
    private void BuildSan()
    {
        if (_tl.San < 0f) return;
        string who = _english ? "Yuina SAN" : "白河 SAN";
        _san = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _stage.AddChild(_san);
        var box = new StyleBoxFlat { BgColor = new Color(26 / 255f, 20 / 255f, 40 / 255f, 220 / 255f), BorderColor = PinkLight };
        box.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(2 * _u)));
        box.SetCornerRadiusAll(Mathf.RoundToInt(30 * _u));
        var panel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", box);
        _san.AddChild(panel);
        _sanMain = TextLabel(who + "  " + _sanBefore + " → " + _sanBefore, 22 * _u, Colors.White, new Color(0, 0, 0, 0), 1);
        _sanRoll = TextLabel("1d100 = " + _proposal.SanRoll, 16 * _u, PinkLight, new Color(0, 0, 0, 0), 1);
        _sanMain.Visible = _sanRoll.Visible = true;
        _san.AddChild(_sanMain);
        _san.AddChild(_sanRoll);
        Font font = _sanMain.GetThemeDefaultFont();
        float mainWidth = font.GetStringSize(who + "  100 → 100", HorizontalAlignment.Left, -1, Mathf.RoundToInt(22 * _u)).X;
        float rollWidth = font.GetStringSize(_sanRoll.Text, HorizontalAlignment.Left, -1, Mathf.RoundToInt(16 * _u)).X;
        float height = 62 * _u;
        float divider = 24 * _u + mainWidth + 22 * _u;
        float width = divider + 2 * _u + 18 * _u + rollWidth + 24 * _u;
        panel.Size = new Vector2(width, height);
        _sanMain.ResetSize();
        _sanMain.Position = new Vector2(24 * _u, (height - _sanMain.Size.Y) * 0.5f);
        _sanDivider = new ColorRect { Color = new Color(PinkLight, 170 / 255f), Position = new Vector2(divider, 16 * _u),
            Size = new Vector2(Mathf.Max(1, 2 * _u), 30 * _u), MouseFilter = Control.MouseFilterEnum.Ignore };
        _san.AddChild(_sanDivider);
        _sanRoll.ResetSize();
        _sanRoll.Position = new Vector2(divider + 20 * _u, (height - _sanRoll.Size.Y) * 0.5f);
        _san.Size = new Vector2(width, height);
        _san.PivotOffset = _san.Size * 0.5f;
        _san.Position = S(64, 640) - new Vector2(0, height * 0.5f);
        if (_proposal.Broken)
        {
            _broken = TextLabel(_english ? "Broken!" : "破防！", 26 * _u, new Color(1f, 0.32f, 0.42f), Ink, 8 * _u);
            _san.AddChild(_broken);
            _broken.ResetSize();
            _broken.PivotOffset = _broken.Size * 0.5f;
            _broken.Position = new Vector2(width + 14 * _u, (height - _broken.Size.Y) * 0.5f);
        }
    }

    // ---------------------------------------------------------------- explosion frames

    private static string FramePath(int i) => ArtRoot + "explosion/x" + i.ToString("00") + ".png";

    private void RequestFrames()
    {
        for (int i = 0; i < FrameCount; i++)
        {
            string path = FramePath(i);
            _requested[i] = ResourceLoader.Exists(path) && ResourceLoader.LoadThreadedRequest(path) == Error.Ok;
        }
    }

    private Texture2D Frame(int i)
    {
        if (_frames[i] == null && _requested[i])
        {
            _requested[i] = false;
            _frames[i] = ResourceLoader.LoadThreadedGet(FramePath(i)) as Texture2D;
        }
        return _frames[i];
    }

    private void DrainFrames()
    {
        for (int i = 0; i < FrameCount; i++)
        {
            if (!_requested[i]) continue;
            _requested[i] = false;
            if (ResourceLoader.LoadThreadedGetStatus(FramePath(i)) == ResourceLoader.ThreadLoadStatus.Loaded)
                ResourceLoader.LoadThreadedGet(FramePath(i));
        }
        Array.Clear(_frames);
    }

    // ---------------------------------------------------------------- timeline

    private static float Seg(float t, float a, float b) => NThirtyGCardDecor.Seg(t, a, b);
    private static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float InCubic(float x) => x * x * x;
    private static float OutBack(float x, float s = 1.7f) => NThirtyGCardDecor.OutBack(x, s);
    private static float OutExpo(float x) => x >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
    private static float HazeStrength(float f) => f <= 60f ? 1f : Mathf.Max(0f, 1f - (f - 60f) / 10f);

    private Vector2 Shake(float t, float t0, float amp, float duration)
    {
        float k = Seg(t, t0, t0 + duration);
        if (k <= 0f || k >= 1f) return Vector2.Zero;
        float a = amp * (1f - k);
        return new Vector2(Mathf.Round(Mathf.Sin(t * 90f) * a), Mathf.Round(Mathf.Cos(t * 77f) * a * 0.7f)) * _u;
    }

    private void Advance(float delta)
    {
        _t += delta;
        float t = _t;
        Timeline T = _tl;
        float dim = 0.85f * OutCubic(Seg(t, 0f, T.Fly1 * 0.8f)) * (1f - Seg(t, T.DimOut0, T.DimOut1));
        ((ShaderMaterial)_dim.Material).SetShaderParameter("amount", dim);

        // Card: from the draw pile to the middle, back into the pile at the end.
        float kFly = OutCubic(Seg(t, 0f, T.Fly1));
        float kBack = InCubic(Seg(t, T.Back0, T.Back1));
        Vector2 centre = _pile.Lerp(_centre, kFly).Lerp(_pile, kBack);
        float scale = Mathf.Lerp(Mathf.Lerp(0.28f, 0.82f, kFly), 0.22f, kBack);
        float rot = -Mathf.DegToRad(Mathf.Lerp(Mathf.Lerp(24f, 0f, kFly), -30f, kBack));
        _pivot.Position = centre;
        _pivot.Scale = new Vector2(scale * _u, scale * _u);
        _pivot.Rotation = rot;
        bool zu = t >= T.Zu;
        float halo = !zu && t < T.Boom ? Seg(t, T.Fly1 * 0.6f, T.Fly1 + 0.2f) : 0f;
        _halo.Visible = halo > 0f;
        _halo.Modulate = new Color(1, 1, 1, 0.75f * halo);
        _zuCopy.Visible = _zuGrey.Visible = _zuScorch.Visible = zu;
        float smoke = zu ? 1f - Seg(t, T.Zu + 0.3f, T.Zu + 1.4f) : 0f;
        for (int i = 0; i < _puffs.Length; i++)
        {
            if (smoke <= 0f) { _puffs[i].Visible = false; continue; }
            float ph = (t * 0.9f + i / 3f) % 1f;
            // The sample sizes the puffs on screen, not on the card.
            NThirtyGCardDecor.Place(_puffs[i], new Vector2(-80f + i * 80f, -191f - ph * 90f), (0.3f + ph * 0.5f) / scale, 0f,
                (1f - ph) * 0.55f * smoke);
        }
        int souls = zu ? 1 : 0;
        for (int i = 0; i < _souls.Length; i++)
        {
            if (i >= souls) { _souls[i].Visible = false; continue; }
            float start = T.Soul + i * 0.15f;
            float rise = (souls == 2 && i == 1 ? 75f : 95f) * OutCubic(Seg(t, start, start + 0.6f));
            float offset = souls == 2 ? (i == 0 ? -58f : 58f) : 0f;
            float phase = t + i * 0.7f;
            NThirtyGCardDecor.Place(_souls[i], new Vector2(offset, -217f - rise + Mathf.Sin(phase * 5f) * 3f), 1f,
                -Mathf.DegToRad(Mathf.Sin(phase * 3f) * 6f), Seg(t, start, start + 0.25f));
        }
        float sk = zu ? Seg(t, T.Stamp, T.Stamp + 0.16f) : 0f;
        NThirtyGCardDecor.Place(_stamp, new Vector2(0, -35), Mathf.Lerp(2.1f, 1f, OutBack(sk, 2.5f)), Mathf.DegToRad(14f),
            sk > 0f ? Mathf.Min(1f, sk * 3f) : 0f);
        if (_cardPill != null)
        {
            float pk = zu ? Seg(t, T.Stamp + 0.25f, T.Stamp + 0.45f) : 0f;
            _cardPill.Visible = pk > 0f;
            float ps = Mathf.Lerp(1.4f, 1f, OutBack(pk, 2f));
            _cardPill.Scale = new Vector2(ps, ps);
            _cardPill.Modulate = new Color(1, 1, 1, Mathf.Min(1f, pk * 2f));
        }

        // 白河 from the left, her line, the target from the right.
        float aIn = OutBack(Seg(t, T.AIn0, T.AIn1), 1.2f);
        float aOut = InCubic(Seg(t, T.Out0, T.Out1));
        if (_winA != null)
        {
            bool shown = aIn > 0f && aOut < 1f;
            NThirtyGCardDecor.Place(_winA, S(Mathf.Lerp(-420f, 250f, aIn) - 700f * aOut, 350f), 1f, 0f, shown ? 1f : 0f);
        }
        int visible = 0;
        if (aOut < 1f && t > T.Line0)
        {
            if (T.Quick) visible = _lineA + _lineB;
            else
            {
                visible = Mathf.RoundToInt(_lineA * Seg(t, 0.8f, 1.02f));
                if (t >= 1.1f) visible += Mathf.RoundToInt(_lineB * Seg(t, 1.1f, 1.4f));
            }
        }
        Vector2 linePos = S(106f - 700f * aOut, 586f);
        foreach (Label label in _lineGlow) PlaceLine(label, linePos, visible);
        PlaceLine(_line, linePos, visible);
        float bIn = OutExpo(Seg(t, T.BIn0, T.BIn1));
        bool targetShown = bIn > 0f && aOut < 1f;
        if (_winB != null)
            NThirtyGCardDecor.Place(_winB, S(Mathf.Lerp(DesignW + 420f, 1032f, bIn) + 700f * aOut, 350f), 1f, 0f, targetShown ? 1f : 0f);
        float dashK = Seg(t, T.BIn1 - 0.02f, T.BIn1 + 0.12f);
        NThirtyGCardDecor.Place(_dash, S(902f + 700f * aOut, 598f), Mathf.Lerp(1.5f, 1f, OutBack(dashK, 2f)), 0f,
            targetShown && dashK > 0f ? 1f : 0f);
        float flashB = 1f - Seg(t, T.BIn1 - 0.04f, T.BIn1 + 0.12f);
        _flashB.Visible = targetShown && t >= T.BIn0 && flashB < 1f && flashB > 0f;
        _flashB.Color = new Color(1, 1, 1, 110f / 255f * flashB);

        // The story explosion, its haze, 「轰隆———！！！」 and the flash.
        bool boom = t >= T.Boom;
        float te = t - T.Boom;
        float f = te * 60f * T.XSpeed;
        float fade = boom ? 1f - Seg(f, T.XFade0, T.XFade1) : 0f;
        bool blast = boom && f <= 100f && fade > 0f;
        float haze = blast ? HazeStrength(f) * fade : 0f;
        _hazeCopy.Visible = _haze.Visible = haze > 0f;
        if (haze > 0f) ((ShaderMaterial)_haze.Material).SetShaderParameter("strength", haze);
        _explosion.Visible = blast;
        if (blast)
        {
            _explosion.Texture = Frame(Mathf.Clamp(Mathf.RoundToInt(f / 2f), 0, FrameCount - 1));
            _explosion.Modulate = new Color(1, 1, 1, fade);
        }
        float rk = boom ? Seg(f, 4f, 12f) : 0f;
        float ra = fade * (1f - Seg(f, 50f, 66f));
        NThirtyGCardDecor.Place(_rumble, S(640f + Mathf.Sin(t * 61f) * 3f, 612f + Mathf.Cos(t * 53f) * 2f),
            Mathf.Lerp(1.45f, 1f, OutBack(rk, 2f)), 0f, rk > 0f ? ra : 0f);
        float flash = boom ? 1f - Seg(te, 0f, 0.12f) : 0f;
        _flash.Visible = flash > 0f;
        _flash.Color = new Color(1, 1, 1, 225f / 255f * flash);

        // 「叮」 just before the seal, at the card's top right corner.
        float dk = Seg(t, T.Ding, T.Ding + 0.12f);
        Vector2 dingAt = centre + (new Vector2(160f, -201f) * scale * _u).Rotated(rot);
        NThirtyGCardDecor.Place(_ding, dingAt, Mathf.Lerp(1.4f, 0.62f, OutBack(dk, 2.4f)), Mathf.DegToRad(12f),
            dk > 0f && t < T.Back0 ? 1f - Seg(t, T.Ding + 0.6f, T.Ding + 0.85f) : 0f);

        if (_san != null)
        {
            float k = Seg(t, T.San, T.San + 0.45f);
            _san.Visible = k > 0f && t < T.Back0 + 0.2f;
            if (_san.Visible)
            {
                float count = OutCubic(Seg(t, T.San + 0.12f, T.San + 0.45f));
                _sanMain.Text = (_english ? "Yuina SAN" : "白河 SAN") + "  " + _sanBefore + " → " + Mathf.RoundToInt(Mathf.Lerp(_sanBefore, _sanAfter, count));
                _san.Modulate = new Color(1, 1, 1, OutCubic(k));
                if (_broken != null)
                {
                    float bk = Seg(t, T.San + 0.45f, T.San + 0.6f);
                    _broken.Visible = bk > 0f;
                    float bs = Mathf.Lerp(1.6f, 1f, OutBack(bk, 2.4f));
                    _broken.Scale = new Vector2(bs, bs);
                }
            }
        }
        _stage.Position = Shake(t, T.Boom, T.ShakeAmp, 0.36f) + Shake(t, T.Stamp + 0.1f, 4f, 0.14f);
    }

    private static void PlaceLine(Label label, Vector2 position, int visible)
    {
        label.Visible = visible > 0;
        if (!label.Visible) return;
        label.Position = position;
        label.VisibleCharacters = visible;
    }
}
