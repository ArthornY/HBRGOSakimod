using System;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The six senses page inside the phone: a large radar whose six vertices carry the name, level and
// ± buttons; a 六感 / 战斗属性 toggle that puts each sense's battle stat on the same axis; the selected
// sense's description and the confirm button. Body coordinates (836 x 912).
public partial class NManualSensesPage : Control
{
    private static readonly string PinkHex = "#" + HbrManualPhoneLayout.Pink;
    private static readonly string MutedHex = "#" + HbrManualPhoneLayout.Muted;

    internal Func<SixSenseAttribute, int, bool> Adjust { get; set; }
    internal Action<SixSenseAttribute> Select { get; set; }
    internal Action Confirm { get; set; }
    internal Action<bool> SetStatsView { get; set; }
    internal Action OpenStats { get; set; }

    private NManualSenseRadar _radar;
    private readonly (Button Root, NManualHighlight Highlight, RichTextLabel Text, NManualPointButton Minus, NManualPointButton Plus)[]
        _vertices = new (Button, NManualHighlight, RichTextLabel, NManualPointButton, NManualPointButton)[6];
    private Label _legend;
    private RichTextLabel _title;
    private RichTextLabel _detail;
    private Button _detailLink;
    private Button _more;
    private Button _confirm;
    private NManualPill _confirmPill;
    private Label _confirmText;
    private (Button Button, NManualPill Pill, Label Text) _senseTab;
    private (Button Button, NManualPill Pill, Label Text) _statTab;

    internal Control DefaultFocusedControl => _vertices[0].Plus;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        HbrPhoneRect body = HbrManualPhoneLayout.Body;
        Size = new Vector2(body.Width, body.Height);
        BuildToggle();

        _radar = new NManualSenseRadar
        {
            Name = "Radar", Size = new Vector2(body.Width, 680f),
            Centre = new Vector2(HbrManualPhoneLayout.RadarCentreX, HbrManualPhoneLayout.RadarCentreY),
            Radius = HbrManualPhoneLayout.RadarRadius, HalfRadius = HbrManualPhoneLayout.RadarRadius + 14f
        };
        AddChild(_radar);
        for (int i = 0; i < 6; i++) BuildVertex(i);

        _legend = HbrManualPhoneArt.Label("Legend", HbrManualPhoneLayout.SenseLegend, 20, HbrManualPhoneArt.Muted,
            align: HorizontalAlignment.Center);
        HbrManualPhoneArt.Place(_legend, new HbrPhoneRect(0f, HbrManualPhoneLayout.LegendTop - 4f, body.Width, 30f));
        AddChild(_legend);

        _title = Rich("Title", 32);
        HbrManualPhoneArt.Place(_title, HbrManualPhoneLayout.SixTitle);
        _title.AddThemeFontOverride("normal_font", HbrManualPhoneArt.Font);
        AddChild(_title);

        HbrPhoneRect detail = HbrManualPhoneLayout.SixDetail;
        Panel box = new() { Name = "DetailBox", MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0.04f, 0.12f, 0.18f, 0.55f), 18f,
            new Color(0.52f, 0.91f, 0.94f, 0.28f)));
        HbrManualPhoneArt.Place(box, detail);
        AddChild(box);
        _detail = new RichTextLabel
        {
            // The effect texts are formatted LocStrings with BBCode, as in the old manual.
            Name = "Detail", BbcodeEnabled = true, ScrollActive = true, FitContent = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Pass,
            FocusMode = FocusModeEnum.None
        };
        _detail.AddThemeFontSizeOverride("normal_font_size", 20);
        _detail.AddThemeColorOverride("default_color", HbrManualPhoneArt.Body);
        _detail.AddThemeConstantOverride("line_separation", 6);
        HbrManualPhoneArt.Place(_detail, new HbrPhoneRect(16f, 12f, detail.Width - 32f, detail.Height - 24f));
        box.AddChild(_detail);
        // In the battle stat view the box is a link to the stats page.
        _detailLink = new Button { Name = "DetailLink", TooltipText = "查看战斗属性详情" };
        HbrManualPhoneArt.Bare(_detailLink);
        _detailLink.AddThemeStyleboxOverride("hover", HbrManualPhoneArt.Rounded(new Color(0.08f, 0.2f, 0.27f, 0.25f), 18f,
            new Color(0.59f, 0.94f, 0.98f, 0.8f)));
        HbrManualPhoneArt.Place(_detailLink, detail);
        _detailLink.Pressed += () => OpenStats?.Invoke();
        AddChild(_detailLink);

        _more = PillButton("More", HbrManualPhoneLayout.SixMore, HbrManualPhoneLayout.MoreText, 20, NManualPill.Look.Secondary,
            Colors.White, () => OpenStats?.Invoke()).Button;

        HbrPhoneRect confirm = HbrManualPhoneLayout.Confirm;
        var built = PillButton("Confirm", confirm, HbrManualPhoneLayout.ConfirmText, 32, NManualPill.Look.Primary,
            new Color("06232b"), () => Confirm?.Invoke());
        (_confirm, _confirmPill, _confirmText) = built;
    }

    private void BuildToggle()
    {
        HbrPhoneRect rect = HbrManualPhoneLayout.Toggle;
        Panel track = new() { Name = "Toggle", MouseFilter = MouseFilterEnum.Ignore };
        track.AddThemeStyleboxOverride("panel", HbrManualPhoneArt.Rounded(new Color(0.024f, 0.078f, 0.125f, 0.6f), 30f,
            new Color(0.52f, 0.91f, 0.94f, 0.45f)));
        HbrManualPhoneArt.Place(track, rect);
        AddChild(track);
        _senseTab = PillButton("Senses", new HbrPhoneRect(rect.X + 4f, rect.Y + 4f, 116f, 52f), HbrManualPhoneLayout.SensesView, 28,
            NManualPill.Look.Clear, new Color("dff6f8"), () => SetStatsView?.Invoke(false));
        _senseTab.Button.TooltipText = "按六感等级显示";
        _statTab = PillButton("Stats", new HbrPhoneRect(rect.X + 120f, rect.Y + 4f, 156f, 52f), HbrManualPhoneLayout.StatsView, 28,
            NManualPill.Look.Clear, new Color("dff6f8"), () => SetStatsView?.Invoke(true));
        _statTab.Button.TooltipText = "按对应的战斗属性显示，± 仍调整六感";
    }

    private (Button Button, NManualPill Pill, Label Text) PillButton(string name, HbrPhoneRect rect, string text, int size,
        NManualPill.Look look, Color color, Action press)
    {
        Button button = new() { Name = name };
        HbrManualPhoneArt.Bare(button);
        HbrManualPhoneArt.Place(button, rect);
        NManualPill pill = new() { Name = "Pill", Size = new Vector2(rect.Width, rect.Height), Style = look };
        button.AddChild(pill);
        Label label = HbrManualPhoneArt.Label("Text", text, size, color, align: HorizontalAlignment.Center);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        HbrManualPhoneArt.Place(label, new HbrPhoneRect(0f, 0f, rect.Width, rect.Height));
        button.AddChild(label);
        button.MouseEntered += () => pill.Hovered = !button.Disabled;
        button.MouseExited += () => pill.Hovered = false;
        button.FocusEntered += () => pill.Hovered = !button.Disabled;
        button.FocusExited += () => pill.Hovered = false;
        button.Pressed += press;
        AddChild(button);
        return (button, pill, label);
    }

    private void BuildVertex(int index)
    {
        var (x, y) = HbrManualPhoneLayout.Point(HbrManualPhoneLayout.RadarCentreX, HbrManualPhoneLayout.RadarCentreY, index,
            HbrManualPhoneLayout.RadarLabelRadius);
        float width = HbrManualPhoneLayout.VertexWidth, height = HbrManualPhoneLayout.VertexHeight;
        SixSenseAttribute sense = ElectronicSoldierManual.AllSixSenseAttributes.ElementAt(index);
        Button root = new() { Name = $"Vertex{index}", TooltipText = ElectronicSoldierManual.GetSenseTitle(sense) };
        HbrManualPhoneArt.Bare(root);
        root.FocusMode = FocusModeEnum.None;
        HbrManualPhoneArt.Place(root, new HbrPhoneRect(x - width / 2f, y - height / 2f, width, height));
        AddChild(root);
        NManualHighlight highlight = new() { Name = "Highlight", Size = new Vector2(width, height), Radius = height / 2f };
        root.AddChild(highlight);
        root.MouseEntered += () => highlight.Hovered = true;
        root.MouseExited += () => highlight.Hovered = false;
        root.Pressed += () => Select?.Invoke(sense);

        float button = HbrManualPhoneLayout.PointButtonSize;
        NManualPointButton minus = PointButton("Minus", "minus", new Vector2(0f, (height - button) / 2f), sense, -1);
        minus.TooltipText = "撤回" + ElectronicSoldierManual.GetSenseTitle(sense);
        root.AddChild(minus);
        RichTextLabel text = Rich("Text", 22);
        text.AddThemeFontOverride("normal_font", HbrManualPhoneArt.Font);
        HbrManualPhoneArt.Place(text, new HbrPhoneRect(button, 4f, width - button * 2f, height - 4f));
        root.AddChild(text);
        NManualPointButton plus = PointButton("Plus", "btnPlus", new Vector2(width - button, (height - button) / 2f), sense, 1);
        plus.TooltipText = "增加" + ElectronicSoldierManual.GetSenseTitle(sense);
        root.AddChild(plus);
        _vertices[index] = (root, highlight, text, minus, plus);
    }

    private NManualPointButton PointButton(string name, string art, Vector2 position, SixSenseAttribute sense, int direction)
    {
        float size = HbrManualPhoneLayout.PointButtonSize;
        string prefix = art == "btnPlus" ? "btnPlus" : "minus";
        return new NManualPointButton
        {
            Name = name, Position = position, Size = new Vector2(size, size),
            TextureNormal = HbrManualPhoneArt.Texture(prefix + "Normal"),
            TextureHover = HbrManualPhoneArt.Texture(prefix + "Active"),
            TexturePressed = HbrManualPhoneArt.Texture(prefix + "Active"),
            TextureDisabled = HbrManualPhoneArt.Texture(prefix + "Disable"),
            Attempt = () => Adjust?.Invoke(sense, direction) == true
        };
    }

    private static RichTextLabel Rich(string name, int size)
    {
        RichTextLabel label = new()
        {
            Name = name, BbcodeEnabled = true, FitContent = false, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", Colors.White);
        return label;
    }

    internal void StopRepeating()
    {
        foreach (var vertex in _vertices)
        {
            vertex.Minus?.Stop();
            vertex.Plus?.Stop();
        }
    }

    internal void Refresh(ElectronicSoldierManual manual, SixSenseAllocationDraft draft, SixSenseAttribute selected,
        bool statsView, bool editable, string detail)
    {
        if (_radar == null || manual == null || draft == null) return;
        statsView &= manual.IsHbrNumericsEnabled;
        var senses = ElectronicSoldierManual.AllSixSenseAttributes.ToArray();
        int selectedIndex = Array.IndexOf(senses, selected);
        StyleTab(_senseTab, !statsView);
        StyleTab(_statTab, statsView);
        _statTab.Button.Visible = manual.IsHbrNumericsEnabled;

        HbrBattleStats stats = HbrManualPhoneData.Stats(manual);
        decimal baseStat = statsView ? HbrManualPhoneData.BaseStat(manual) : 0m;
        float[] values, pending;
        if (statsView)
        {
            values = senses.Select(sense => (float)HbrManualPhoneData.StatValue(stats, HbrManualPhoneData.StatIndex(sense))).ToArray();
            pending = senses.Select((sense, i) => values[i] + (float)HbrManualPhoneData.PendingGain(manual, draft, sense)).ToArray();
            float max = HbrManualPhoneLayout.StatRadarMax((float)baseStat,
                (float)HbrBattleStatsHelper.SenseStatGainPerLevel, pending.Max());
            _radar.Max = max;
            _radar.Rings = HbrManualPhoneLayout.StatRings(max);
            _radar.Dashed = (float)baseStat;
            _legend.Text = HbrManualPhoneLayout.StatLegend(manual.HbrBattleLevel, HbrManualPhoneData.Number(baseStat),
                HbrManualPhoneData.Number((decimal)max));
        }
        else
        {
            values = senses.Select(sense => (float)manual.GetSenseAmount(sense)).ToArray();
            pending = senses.Select(sense => (float)draft.Level(sense)).ToArray();
            _radar.Max = HbrManualPhoneLayout.MaxLevel;
            _radar.Rings = HbrManualPhoneLayout.SenseRings.Select(v => (float)v).ToArray();
            _radar.Dashed = HbrManualPhoneLayout.Threshold;
            _legend.Text = HbrManualPhoneLayout.SenseLegend;
        }
        _radar.Values = values;
        _radar.Pending = pending;
        _radar.Drafted = senses.Select(sense => draft.Added(sense) > 0).ToArray();
        _radar.Selected = selectedIndex;
        _radar.QueueRedraw();

        for (int i = 0; i < 6; i++)
        {
            SixSenseAttribute sense = senses[i];
            var vertex = _vertices[i];
            int added = draft.Added(sense);
            vertex.Highlight.Selected = i == selectedIndex;
            vertex.Root.TooltipText = $"{ElectronicSoldierManual.GetSenseTitle(sense)}：{HbrManualPhoneLayout.Leanings[i]}";
            string name = ElectronicSoldierManual.GetSenseTitle(sense);
            if (statsView)
            {
                decimal gain = HbrManualPhoneData.PendingGain(manual, draft, sense);
                vertex.Text.Text = $"[center][color=#{HbrManualPhoneLayout.Cyan}]{HbrBattleStatsHelper.GetBattleStatTitle(sense)}[/color]" +
                    $"[font_size=15][color={MutedHex}] {name}[/color][/font_size]\n[font_size=26]{HbrManualPhoneData.Number((decimal)values[i])}[/font_size]" +
                    (gain > 0 ? $"[font_size=20][color={PinkHex}] (+{HbrManualPhoneData.Number(gain)})[/color][/font_size]" : "") + "[/center]";
            }
            else
            {
                vertex.Text.Text = $"[center][color=#{HbrManualPhoneLayout.TierColours[i]}]{name}[/color]\n" +
                    $"[font_size=26]Lv.{draft.Level(sense)}[/font_size]" +
                    (added > 0 ? $"[font_size=20][color={PinkHex}] (+{added})[/color][/font_size]" : "") + "[/center]";
            }
            vertex.Plus.Disabled = !editable || draft.Remaining <= 0 || draft.Level(sense) >= ElectronicSoldierManual.MaxSixSense;
            vertex.Minus.Disabled = !editable || added <= 0;
            if (vertex.Plus.Disabled) vertex.Plus.Stop();
            if (vertex.Minus.Disabled) vertex.Minus.Stop();
        }

        string selectedName = ElectronicSoldierManual.GetSenseTitle(selected);
        int selectedAdded = draft.Added(selected);
        if (statsView)
        {
            int statIndex = HbrManualPhoneData.StatIndex(selected);
            decimal gain = HbrManualPhoneData.PendingGain(manual, draft, selected);
            _title.Text = $"{HbrBattleStatsHelper.GetBattleStatTitle(selected)}   {HbrManualPhoneData.Number(HbrManualPhoneData.StatValue(stats, statIndex))}" +
                (gain > 0 ? $"  [color={PinkHex}](+{HbrManualPhoneData.Number(gain)})[/color]" : "");
        }
        else
        {
            _title.Text = $"{selectedName}   Lv.{draft.Level(selected)}" +
                (selectedAdded > 0 ? $"  [color={PinkHex}](+{selectedAdded})[/color]" : "");
        }
        _detail.Text = detail;
        _detailLink.Visible = statsView;
        _more.Visible = statsView;

        bool canConfirm = editable && draft.Total > 0;
        _confirm.Disabled = !canConfirm;
        _confirmPill.Style = canConfirm ? NManualPill.Look.Primary : NManualPill.Look.PrimaryDisabled;
        if (!canConfirm) _confirmPill.Hovered = false;
        _confirmText.Text = manual.IsAllocationPending ? HbrManualPhoneLayout.ConfirmingText : HbrManualPhoneLayout.ConfirmText;
        _confirmText.AddThemeColorOverride("font_color", canConfirm ? new Color("06232b") : new Color(1f, 1f, 1f, 0.45f));
    }

    private static void StyleTab((Button Button, NManualPill Pill, Label Text) tab, bool on)
    {
        tab.Pill.Style = on ? NManualPill.Look.Primary : NManualPill.Look.Clear;
        tab.Text.AddThemeColorOverride("font_color", on ? new Color("06232b") : new Color("dff6f8"));
    }
}
