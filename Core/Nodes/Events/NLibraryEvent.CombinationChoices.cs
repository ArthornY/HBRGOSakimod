using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Events;

public partial class NLibraryEvent
{
    public const int CombinedChoicesRuntimeVersion = 1;

    private readonly List<(Button Button, Label Label, ColorRect Pill)> _combinationLeftControls = new();
    private readonly List<(Button Button, Label Label, ColorRect Pill)> _combinationRightControls = new();
    private CombinationConnections _combinationConnections;
    private Button _combinationReset;
    private Button _combinationConfirm;
    private int _combinationLeft = -1;
    private int _combinationRight = -1;

    private static Rect2 CombinationBounds(bool right, int index) => new(
        right ? (index == 1 ? 1064 : 978) : (index == 1 ? 10 : 96),
        index switch { 0 => 166, 1 => 392, _ => 620 }, 530, 122);

    private static Vector2 CombinationPoint(bool right, int index)
    {
        Rect2 rect = CombinationBounds(right, index);
        return new Vector2(right ? rect.Position.X - 30 : rect.End.X + 30, rect.GetCenter().Y);
    }

    private bool TryShowCombinationChoices()
    {
        var page = StoryEvent?.CurrentStoryPage;
        if (_event.IsFinished || page?.ChoiceMode != "combine" || page.ChoiceLeft.Count != 3
            || page.ChoiceRight.Count != 3 || page.Choices.Count != 9 || _event.CurrentOptions.Count != 9)
            return false;
        _combinationLeft = -1;
        _combinationRight = -1;
        _combinationLeftControls.Clear();
        _combinationRightControls.Clear();
        Put(new ColorRect { Color = new Color(0.067f, 0.063f, 0.122f, 0.60f), MouseFilter = MouseFilterEnum.Ignore },
            _choiceCanvas, new Rect2(0, 0, DesignWidth, DesignHeight));
        _combinationConnections = new CombinationConnections { MouseFilter = MouseFilterEnum.Ignore };
        Put(_combinationConnections, _choiceCanvas, new Rect2(0, 0, DesignWidth, DesignHeight));
        AddCombinationHeading("问题", new Rect2(500, 12, 600, 38), 26);
        AddCombinationHeading(string.IsNullOrWhiteSpace(page.ChoicePrompt) ? "连接左右两段思绪" : page.ChoicePrompt,
            new Rect2(260, 56, 1080, 65), 38);
        for (int index = 0; index < 3; index++) AddCombinationSide(false, index, page.ChoiceLeft[index]);
        for (int index = 0; index < 3; index++) AddCombinationSide(true, index, page.ChoiceRight[index]);
        _combinationReset = AddCombinationAction("重来", new Rect2(406, 774, 354, 98), false, () =>
        {
            if (_dispatchPending || !_showingChoices || !IsReaderActive()) return;
            _combinationLeft = -1;
            _combinationRight = -1;
            UpdateCombinationSelection();
            FocusDefault();
        });
        _combinationConfirm = AddCombinationAction("就是它了！！", new Rect2(846, 774, 354, 98), true, () =>
        {
            if (_combinationLeft >= 0 && _combinationRight >= 0)
                SelectOption(_combinationLeft * 3 + _combinationRight);
        });
        UpdateCombinationSelection();
        return true;
    }

    private void AddCombinationHeading(string text, Rect2 bounds, int size)
    {
        Label label = MakeLabel(text, size);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeFontOverride("font", new FontVariation
        {
            BaseFont = label.GetThemeFont("font"),
            VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(-0.18f, 1), Vector2.Zero)
        });
        ScrollContainer scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = MouseFilterEnum.Pass };
        Put(scroll, _choiceCanvas, bounds);
        label.CustomMinimumSize = new Vector2(bounds.Size.X - 20, 0);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.AddChild(label);
    }

    private void AddCombinationSide(bool right, int index, string text)
    {
        Rect2 bounds = CombinationBounds(right, index);
        ColorRect pill = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = """
                shader_type canvas_item;
                uniform bool selected = false;
                uniform bool right_side = false;
                void fragment() {
                    vec2 p = UV * vec2(530.0,122.0);
                    float d = length(vec2(max(abs(p.x-265.0)-204.0,0.0),p.y-61.0))-61.0;
                    vec4 fill = right_side ? vec4(.455,.455,.47,.40) : vec4(.114,.098,.20,.60);
                    if (selected) fill = mix(vec4(.529,.302,.416,.62),vec4(.745,.098,.49,.88),right_side ? 1.0-UV.x : UV.x);
                    vec4 edge = vec4(.925,.90,.965,selected ? .94 : .58);
                    COLOR = mix(fill,edge,smoothstep(-3.0,-1.0,d));
                    COLOR.a *= 1.0-smoothstep(-1.0,1.0,d);
                }
                """ } }
        };
        ((ShaderMaterial)pill.Material).SetShaderParameter("right_side", right);
        Put(pill, _choiceCanvas, bounds);
        Button button = MakeButton(string.Empty, bounds, () =>
        {
            if (_dispatchPending || !_showingChoices || !IsReaderActive()
                || _combinationLeft >= 0 && _combinationRight >= 0 || right && _combinationLeft < 0) return;
            if (right) _combinationRight = index;
            else _combinationLeft = index;
            UpdateCombinationSelection();
            if (_combinationRight >= 0 && !_combinationConfirm.Disabled) _combinationConfirm.GrabFocus();
        });
        button.TooltipText = text;
        TransparentStyles(button);
        _choiceCanvas.AddChild(button);
        _choiceButtons.Add(button);
        Label label = MakeLabel(text, 32);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ScrollContainer scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = MouseFilterEnum.Pass };
        Put(scroll, button, new Rect2(54, 20, bounds.Size.X - 108, bounds.Size.Y - 40));
        label.CustomMinimumSize = new Vector2(bounds.Size.X - 128, 0);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.AddChild(label);
        (right ? _combinationRightControls : _combinationLeftControls).Add((button, label, pill));
    }

    private Button AddCombinationAction(string text, Rect2 bounds, bool confirm, Action action)
    {
        Button button = MakeButton(text, bounds, action);
        button.AddThemeFontSizeOverride("font_size", 38);
        button.AddThemeFontOverride("font", _dialogue.GetThemeFont("font"));
        ColorRect background = new() { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true,
            Material = new ShaderMaterial { Shader = new Shader { Code = """
                shader_type canvas_item;
                uniform bool confirm = false;
                void fragment() {
                    vec2 p=UV*vec2(354.0,98.0);
                    float d=length(vec2(max(abs(p.x-177.0)-128.0,0.0),p.y-49.0))-49.0;
                    float edge=pow(abs(UV.y-.5)*2.0,2.0);
                    COLOR=confirm ? mix(vec4(.72,.18,.51,.90),vec4(1.0,.59,.83,.90),edge)
                        : mix(vec4(.125,.086,.18,.90),vec4(.286,.196,.333,.90),edge);
                    COLOR.a*=1.0-smoothstep(-1.0,1.0,d);
                }
                """ } } };
        ((ShaderMaterial)background.Material).SetShaderParameter("confirm", confirm);
        Put(background, button, new Rect2(Vector2.Zero, bounds.Size));
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            StyleBoxFlat style = RoundStyle(Colors.Transparent, new Color(0.94f, 0.87f, 0.96f), 49);
            style.ShadowColor = confirm ? new Color(1, 0.66f, 0.88f, 0.75f) : new Color(0.82f, 0.70f, 1, 0.22f);
            style.ShadowSize = confirm ? 15 : 5;
            if (state is "hover" or "focus") style.BorderColor = Colors.White;
            button.AddThemeStyleboxOverride(state, style);
        }
        _choiceCanvas.AddChild(button);
        _choiceButtons.Add(button);
        return button;
    }

    private void UpdateCombinationSelection()
    {
        bool complete = _combinationLeft >= 0 && _combinationRight >= 0;
        for (int index = 0; index < 3; index++)
        {
            var left = _combinationLeftControls[index];
            var right = _combinationRightControls[index];
            left.Button.Disabled = _dispatchPending || complete
                || Enumerable.Range(index * 3, 3).All(option => _event.CurrentOptions[option].IsLocked);
            right.Button.Disabled = _dispatchPending || complete || _combinationLeft < 0
                || _event.CurrentOptions[Math.Max(0, _combinationLeft) * 3 + index].IsLocked;
            left.Label.Modulate = left.Button.Disabled && !complete ? new Color(1, 1, 1, 0.5f) : Colors.White;
            right.Label.Modulate = right.Button.Disabled && !complete ? new Color(1, 1, 1, 0.5f) : Colors.White;
            ((ShaderMaterial)left.Pill.Material).SetShaderParameter("selected", index == _combinationLeft);
            ((ShaderMaterial)right.Pill.Material).SetShaderParameter("selected", index == _combinationRight);
        }
        _combinationReset.Visible = complete;
        _combinationConfirm.Visible = complete;
        _combinationReset.Disabled = _dispatchPending || !complete;
        _combinationConfirm.Disabled = _dispatchPending || !complete
            || _event.CurrentOptions[_combinationLeft * 3 + _combinationRight].IsLocked;
        if (complete)
        {
            var page = StoryEvent.CurrentStoryPage;
            var choice = page.Choices[_combinationLeft * 3 + _combinationRight];
            string description = StoryChoiceDescription(choice);
            _combinationConfirm.TooltipText = page.ChoiceLeft[_combinationLeft] + page.ChoiceRight[_combinationRight]
                + (string.IsNullOrWhiteSpace(description) ? string.Empty : "\n" + description);
        }
        _combinationConnections.Left = _combinationLeft;
        _combinationConnections.Right = _combinationRight;
        _combinationConnections.QueueRedraw();
        ConfigureFocusNavigation();
        if (!complete && _combinationLeft >= 0)
            for (int index = 0; index < 3; index++)
            {
                var left = _combinationLeftControls[index].Button;
                var right = _combinationRightControls[index].Button;
                if (right.Disabled) continue;
                left.FocusNeighborRight = left.GetPathTo(right);
                right.FocusNeighborLeft = right.GetPathTo(left);
            }
    }

    private sealed partial class CombinationConnections : Control
    {
        public int Left = -1;
        public int Right = -1;

        public override void _Draw()
        {
            for (int left = 0; left < 3; left++)
                for (int right = 0; right < 3; right++)
                {
                    if (left == Left && right == Right) continue;
                    Vector2 from = CombinationPoint(false, left), to = CombinationPoint(true, right);
                    float length = from.DistanceTo(to);
                    for (float distance = 0; distance <= length; distance += 11)
                        DrawCircle(from.Lerp(to, distance / length), 3.7f, new Color(0.804f, 0.773f, 0.796f, 0.40f), true, -1, true);
                }
            if (Left >= 0 && Right >= 0)
            {
                Vector2 from = CombinationPoint(false, Left), to = CombinationPoint(true, Right);
                DrawLine(from, to, new Color(1, 0.16f, 0.58f, 0.18f), 15, true);
                DrawLine(from, to, new Color(1, 0.16f, 0.58f, 0.30f), 9, true);
                DrawLine(from, to, new Color(1, 0.514f, 0.737f), 5, true);
                DrawLine(new Vector2(0, 823), new Vector2(DesignWidth, 823), new Color(0.94f, 0.47f, 0.78f, 0.16f), 10, true);
                DrawLine(new Vector2(0, 823), new Vector2(DesignWidth, 823), new Color(0.93f, 0.82f, 0.91f, 0.63f), 2, true);
            }
            for (int index = 0; index < 3; index++)
            {
                DrawCircle(CombinationPoint(false, index), 8, new Color(0.91f, 0.90f, 0.94f), true, -1, true);
                DrawCircle(CombinationPoint(true, index), 8, new Color(0.91f, 0.90f, 0.94f), true, -1, true);
            }
        }
    }
}
