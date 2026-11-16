using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The gameplay hook only enqueues a receipt. No animation/audio task is awaited
// by a combat command, and neither the overlay nor its children capture input.
public partial class NThirtyOneACutinOverlay : CanvasLayer
{
    private sealed record Receipt(Player Player, object Combat, string Character, string Role, int Roll, bool Live);
    private sealed class Conversation
    {
        internal Receipt Receipt;
        internal ThirtyOneACutinLine Line;
        internal readonly HashSet<string> Used = new(StringComparer.Ordinal);
        internal int Count;
        internal double NextAt;
    }
    private sealed class Visual
    {
        internal Control Root;
        internal TextureRect Portrait;
        internal Polygon2D Backdrop;
        internal Polygon2D Bubble;
        internal Label Text;
        internal Label Name;
        internal ColorRect Nameplate;
        internal Label Roll;
        internal AudioStreamPlayer Audio;
        internal double Start;
        internal double Length;
        internal double VoiceLength;
        internal int Row;
    }
    private static readonly Queue<Receipt> Pending = new();
    private static ThirtyOneACutinLibrary _library;
    private readonly List<Visual> _visuals = [];
    private readonly Random _random = new();
    private Conversation _conversation;
    private object _combat;
    private double _clock;
    private const string DataPath = "res://data/thirty_one_a_cutin.json";
    private const string ArtRoot = "res://images/ui/thirty_one_a_cutin/";
    private const string VoiceRoot = "res://sounds/thirty_one_a_cutin/";
    private const string PortraitShader = """
        shader_type canvas_item;
        float cross2(vec2 a, vec2 b) { return a.x*b.y-a.y*b.x; }
        void fragment() {
            vec2 a=vec2(90.0/1024.0,164.0/1368.0);
            vec2 b=vec2(618.0/1024.0,23.0/1368.0);
            vec2 c=vec2(934.0/1024.0,1204.0/1368.0);
            vec2 d=vec2(406.0/1024.0,1345.0/1368.0);
            if(cross2(b-a,UV-a)<0.0 || cross2(c-b,UV-b)<0.0 ||
               cross2(d-c,UV-c)<0.0 || cross2(a-d,UV-d)<0.0) { discard; }
            COLOR=texture(TEXTURE,UV)*COLOR;
        }
        """;
    private static ShaderMaterial _portraitMaterial;

    internal static void Enqueue(Player player, string character, string role, int roll, bool live)
    {
        if (!HBRGOSakiConfig.ThirtyOneACutInPresentation || player?.Creature?.CombatState is not { } combat ||
            !ReferenceEquals(LocalContext.GetMe(combat), player)) return;
        Pending.Enqueue(new(player, combat, character, role, roll, live));
    }

    public override void _Ready()
    {
        Layer = 90;
        _combat = CombatManager.Instance.DebugOnlyGetState();
        GetViewport().SizeChanged += LayoutAll;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= LayoutAll;
        ClearVisuals();
        // Preserve receipts for a different, newly activating combat UI.
        Receipt[] keep = Pending.Where(item => !ReferenceEquals(item.Combat, _combat)).ToArray();
        Pending.Clear(); foreach (Receipt item in keep) Pending.Enqueue(item);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        object combat = CombatManager.Instance.DebugOnlyGetState();
        if (_combat == null) _combat = combat;
        if (!ReferenceEquals(_combat, combat) || !HBRGOSakiConfig.ThirtyOneACutInPresentation || CombatManager.Instance.IsOverOrEnding)
        { ClearVisuals(); return; }
        foreach (Visual visual in _visuals.ToArray())
        {
            double age = _clock - visual.Start;
            if (age >= visual.Length) { visual.Root.QueueFree(); _visuals.Remove(visual); continue; }
            LayoutVisual(visual);
        }
        if (_conversation != null && _clock >= _conversation.NextAt)
        {
            Conversation conversation = _conversation;
            ThirtyOneACutinLine next = conversation.Count < 4 ? Library?.PickReply(conversation.Line, conversation.Used,
                key => ThirtyOneACutinConditionHelper.Matches(conversation.Receipt.Player.Creature, key), _random, conversation.Receipt.Live) : null;
            if (next != null) StartLine(conversation, next, 0);
            else _conversation = null;
        }
        // The six primary speakers reuse the same safe start position instead
        // of pushing rows down into the hand. Responses can briefly overlap
        // the previous line's fade tail, then the next speaker starts afresh.
        if (_conversation == null && _visuals.Count == 0 && Pending.Count > 0)
        {
            Receipt receipt = Pending.Dequeue();
            if (!ReferenceEquals(receipt.Combat, _combat)) return;
            ThirtyOneACutinLine line = Library?.Pick(receipt.Character, receipt.Role, receipt.Live,
                key => ThirtyOneACutinConditionHelper.Matches(receipt.Player.Creature, key), _random);
            if (line == null) return;
            _conversation = new Conversation { Receipt = receipt };
            StartLine(_conversation, line, receipt.Roll);
        }
    }

    private static ThirtyOneACutinLibrary Library
    {
        get
        {
            if (_library != null) return _library;
            if (!FileAccess.FileExists(DataPath)) return null;
            try { _library = ThirtyOneACutinLibrary.Parse(FileAccess.GetFileAsString(DataPath)); }
            catch (JsonException ex) { GD.PushWarning(ex.Message); }
            catch (InvalidOperationException ex) { GD.PushWarning(ex.Message); }
            return _library;
        }
    }

    private void StartLine(Conversation conversation, ThirtyOneACutinLine line, int roll)
    {
        conversation.Line = line; conversation.Count++; conversation.Used.Add(line.Key);
        string[] available = line.Expressions.Where(expression => ResourceLoader.Exists(ArtRoot + line.Character + "DefaultAdvCutIn" + expression + ".png")).ToArray();
        if (available.Length == 0) { conversation.NextAt = _clock; return; }
        string expression = available[_random.Next(available.Length)];
        string voicePath = VoiceRoot + line.Voice + ".ogg";
        AudioStream stream = ResourceLoader.Exists(voicePath) ? ResourceLoader.Load<AudioStream>(voicePath) : null;
        var visual = new Visual
        {
            Root = new Control { Name = "31ACutin", MouseFilter = Control.MouseFilterEnum.Ignore },
            Portrait = new TextureRect { Texture = ResourceLoader.Load<Texture2D>(ArtRoot + line.Character + "DefaultAdvCutIn" + expression + ".png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore, Material = PortraitMaterial },
            Backdrop = new Polygon2D { Color = new Color(.035f, .025f, .06f, .78f) },
            Bubble = new Polygon2D { Color = new Color("FFFDF8") },
            Text = MakeLabel(line.Text.Replace('\n', ' '), Colors.Black),
            Name = MakeLabel(line.Name, new Color("F8D36A")),
            Nameplate = new ColorRect { Color = new Color(.04f, .025f, .07f, .9f), MouseFilter = Control.MouseFilterEnum.Ignore },
            Roll = MakeLabel(roll > 0 ? $"原始出目 {roll}" : string.Empty, new Color("9BE9EE")),
            Audio = new AudioStreamPlayer { Stream = stream, Bus = AudioServer.GetBusIndex("Voice") >= 0 ? "Voice" : "Master" },
            Start = _clock, VoiceLength = stream?.GetLength() ?? .95, Row = conversation.Count - 1
        };
        visual.Length = Math.Max(1.15, visual.VoiceLength + .3);
        AddChild(visual.Root);
        visual.Root.AddChild(visual.Backdrop); visual.Root.AddChild(visual.Portrait);
        visual.Root.AddChild(visual.Bubble); visual.Root.AddChild(visual.Text);
        visual.Root.AddChild(visual.Nameplate); visual.Root.AddChild(visual.Name); visual.Root.AddChild(visual.Roll); visual.Root.AddChild(visual.Audio);
        _visuals.Add(visual);
        LayoutVisual(visual);
        if (stream != null) visual.Audio.Play();
        // Replies start at voice completion, not after the cosmetic tail.
        conversation.NextAt = _clock + Math.Max(.1, visual.VoiceLength);
    }

    private static ShaderMaterial PortraitMaterial => _portraitMaterial ??= new ShaderMaterial { Shader = new Shader { Code = PortraitShader } };

    private static Label MakeLabel(string text, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MaxLinesVisible = 2 };
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void LayoutAll() { foreach (Visual visual in _visuals) LayoutVisual(visual); }
    private void LayoutVisual(Visual visual)
    {
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        float height = screen.Y * .24f, width = height * 1024f / 1368f;
        double age = _clock - visual.Start;
        float enter = (float)Math.Clamp(age / .18, 0, 1); enter = 1 - (1 - enter) * (1 - enter) * (1 - enter);
        float exit = (float)Math.Clamp((age - (visual.Length - .2)) / .2, 0, 1);
        Vector2 origin = new(screen.X * .2f - width / 2, screen.Y * (.28f + visual.Row * .08f) - height / 2);
        origin.X += (1 - enter) * (-screen.X * .4f) + exit * screen.X * .05f;
        origin.Y += (1 - enter) * screen.Y * .012f;
        visual.Root.Position = origin; visual.Root.Modulate = new Color(1, 1, 1, enter * (1 - exit));
        visual.Portrait.Size = new(width, height);
        visual.Backdrop.Polygon = [new(width * .075f, height * .10f), new(width * .62f, 0), new(width * .93f, height * .90f), new(width * .39f, height)];
        float bubbleX = width * .85f, bubbleWidth = screen.X * .24f, bubbleHeight = height * .40f;
        visual.Bubble.Position = new(bubbleX, height * .26f);
        visual.Bubble.Polygon = [new(0, bubbleHeight * .5f), new(bubbleWidth * .07f, 0), new(bubbleWidth, 0),
            new(bubbleWidth * .94f, bubbleHeight), new(bubbleWidth * .06f, bubbleHeight), new(bubbleWidth * .08f, bubbleHeight * .67f)];
        visual.Text.Position = visual.Bubble.Position + new Vector2(bubbleWidth * .08f, bubbleHeight * .08f);
        visual.Text.Size = new(bubbleWidth * .82f, bubbleHeight * .84f);
        int fontSize = Math.Max(12, (int)(screen.Y * .026f));
        Font font = visual.Text.GetThemeDefaultFont();
        while (fontSize > 10 && font.GetStringSize(visual.Text.Text, fontSize: fontSize).X > visual.Text.Size.X * 1.88f) fontSize--;
        visual.Text.AddThemeFontSizeOverride("font_size", fontSize);
        visual.Name.Position = new(width * .12f, height * .85f); visual.Name.Size = new(width * .85f, height * .14f);
        visual.Nameplate.Position = visual.Name.Position; visual.Nameplate.Size = visual.Name.Size;
        visual.Name.AddThemeFontSizeOverride("font_size", Math.Max(12, (int)(screen.Y * .019f)));
        visual.Roll.Position = new(bubbleX, height * .68f); visual.Roll.Size = new(bubbleWidth, height * .12f);
        visual.Roll.AddThemeFontSizeOverride("font_size", Math.Max(11, (int)(screen.Y * .016f)));
    }

    private void ClearVisuals()
    {
        foreach (Visual visual in _visuals) { visual.Audio.Stop(); visual.Root.QueueFree(); }
        _visuals.Clear(); _conversation = null;
        while (Pending.Count > 0 && ReferenceEquals(Pending.Peek().Combat, _combat)) Pending.Dequeue();
    }
}
