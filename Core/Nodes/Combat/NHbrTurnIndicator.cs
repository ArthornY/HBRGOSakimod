using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The standing turn counter (UIBattleTurn / UIBattleTurn2): "TURN n" over MordaTitleLineEdge with a
// dot10 end, tinted 0.83 grey. During extra turns an "EXTRA n" row appears above it.
public partial class NHbrTurnIndicator : Control
{
    private static readonly Color ExtraColor = new(HbrBattleHudLayout.ExtraTextColor);
    private static readonly Color TextOutline = new(0.07f, 0.03f, 0.09f, 0.9f);
    private static readonly Color LineShadow = new(0.04f, 0.024f, 0.063f, 0.6f);

    private Control _extraRow;
    private Label _turnNumber;
    private Label _extraNumber;
    private Label _turnWord;
    private Label _extraWord;
    private Tween _bumpTween;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(HbrBattleHudLayout.TurnWidth, HbrBattleHudLayout.TurnHeight);
        Control turnRow = Row("TurnRow", HbrBattleHudLayout.TurnRowTop, HbrBattleHudLayout.TurnWord, Colors.White,
            out _turnWord, out _turnNumber);
        AddChild(turnRow);
        _extraRow = Row("ExtraRow", 0f, HbrBattleHudLayout.ExtraWord, ExtraColor, out _extraWord, out _extraNumber);
        _extraRow.Visible = false;
        AddChild(_extraRow);
    }

    internal void Show(int turn, int extra, bool bump)
    {
        if (_turnNumber == null) return;
        _turnNumber.Text = turn.ToString();
        _extraNumber.Text = extra.ToString();
        _extraRow.Visible = extra > 0;
        PlaceNumber(_turnWord, _turnNumber);
        PlaceNumber(_extraWord, _extraNumber);
        if (!bump) return;
        Label number = extra > 0 ? _extraNumber : _turnNumber;
        number.Scale = Vector2.One * HbrBattleHudLayout.CountBumpScale;
        _bumpTween?.Kill();
        _bumpTween = CreateTween();
        _bumpTween.TweenProperty(number, "scale", Vector2.One, 0.5)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
    }

    private static Control Row(string name, float top, string word, Color color, out Label wordLabel, out Label numberLabel)
    {
        Control row = new()
        {
            Name = name,
            Position = new Vector2(0f, top),
            Size = new Vector2(HbrBattleHudLayout.TurnWidth, HbrBattleHudLayout.TurnRowHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };

        Control line = new()
        {
            Name = "Line",
            Position = new Vector2(0f, HbrBattleHudLayout.TurnLineTop),
            Size = new Vector2(HbrBattleHudLayout.TurnLineWidth, HbrBattleHudLayout.TurnLineHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(line);
        // HBR draws this line over a dark field; a 1px shadow keeps it visible on bright stages.
        line.AddChild(Edge("Shadow", new Vector2(0f, 1f), LineShadow));
        line.AddChild(Edge("Edge", Vector2.Zero, new Color(HbrBattleHudLayout.TurnLineBrightness,
            HbrBattleHudLayout.TurnLineBrightness, HbrBattleHudLayout.TurnLineBrightness)));
        line.AddChild(HbrBattleHudArt.Sprite("Dot", "dot10", new HbrHudRect(-10f, 0f, 10f, 10f),
            new Color(HbrBattleHudLayout.TurnDotColor)));

        wordLabel = HbrBattleHudArt.Label("Word", word, HbrBattleHudLayout.TurnWordSize, color, 3);
        wordLabel.HorizontalAlignment = HorizontalAlignment.Left;
        HbrBattleHudArt.Outline(wordLabel, TextOutline, 6);
        row.AddChild(wordLabel);
        numberLabel = HbrBattleHudArt.Label("Number", "1", HbrBattleHudLayout.TurnNumberSize, color, 4);
        numberLabel.HorizontalAlignment = HorizontalAlignment.Left;
        HbrBattleHudArt.Outline(numberLabel, TextOutline, 7);
        row.AddChild(numberLabel);
        return row;
    }

    private static NinePatchRect Edge(string name, Vector2 position, Color tint) => new()
    {
        Name = name,
        Position = position,
        Size = new Vector2(HbrBattleHudLayout.TurnLineWidth, HbrBattleHudLayout.TurnLineHeight),
        Texture = HbrBattleHudArt.Texture("MordaTitleLineEdge"),
        PatchMarginLeft = HbrBattleHudLayout.TurnLineLeftCap,
        PatchMarginRight = HbrBattleHudLayout.TurnLineRightCap,
        Modulate = tint,
        TextureFilter = HbrBattleHudArt.MipmapFilter,
        MouseFilter = MouseFilterEnum.Ignore
    };

    // "TURN" (25px) and the number (40px) share a baseline inside the 40px line box at (25, 2).
    private static void PlaceNumber(Label word, Label number)
    {
        Font font = HbrBattleHudArt.Font;
        float baseline = HbrBattleHudLayout.TurnTextTop + 32f;
        PlaceOnBaseline(word, font, HbrBattleHudLayout.TurnWordSize, HbrBattleHudLayout.TurnTextLeft, baseline);
        float wordWidth = word.GetThemeFont("font").GetStringSize(word.Text, HorizontalAlignment.Left, -1,
            HbrBattleHudLayout.TurnWordSize).X;
        PlaceOnBaseline(number, font, HbrBattleHudLayout.TurnNumberSize,
            HbrBattleHudLayout.TurnTextLeft + wordWidth + HbrBattleHudLayout.TurnNumberGap, baseline);
    }

    internal static void PlaceOnBaseline(Label label, Font font, int size, float left, float baseline)
    {
        float ascent = font.GetAscent(size);
        float height = ascent + font.GetDescent(size);
        float width = label.GetThemeFont("font").GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X + 4f;
        label.VerticalAlignment = VerticalAlignment.Top;
        label.Position = new Vector2(left, baseline - ascent);
        label.Size = new Vector2(width, height);
        label.PivotOffset = new Vector2(width / 2f, ascent * 0.6f);
    }
}
