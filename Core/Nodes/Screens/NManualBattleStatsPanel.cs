using System;
using System.Globalization;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// One set of controls is built per open manual; saved progression changes only
// update their text. Resizing changes the grid columns, not the control tree.
// Drawn in the phone manual's page style: stat cards in a 3x2 grid, the rules as
// collapsible sections (ui-concepts/manual-phone-20261104, 战斗属性).
public partial class NManualBattleStatsPanel : Control
{
    private ElectronicSoldierManual _manual;
    private ScrollContainer _scroll;
    private GridContainer _statsGrid;
    private readonly PanelContainer[] _cards = new PanelContainer[6];
    private readonly Label[] _values = new Label[6];
    private readonly Label[] _senseGrowth = new Label[6];
    private Label _status;
    private Label _enemy;
    private Label _growth;
    private string _lastState;
    private HbrBattleStats _lastStats;
    private int _highlight = -1;

    internal Control DefaultFocusedControl => _scroll ?? (Control)this;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutContent();
        Refresh(_manual);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutContent();
    }

    // The stat dock on the manual's home opens this page with its stat lit; -1 clears it.
    internal void Highlight(int index)
    {
        _highlight = index;
        if (_scroll != null) _scroll.ScrollVertical = 0;
        StyleCards();
    }

    internal void Refresh(ElectronicSoldierManual manual)
    {
        _manual = manual;
        if (_status == null) return;
        bool enabled = manual?.IsHbrNumericsEnabled == true;
        Creature enemy = enabled ? manual.Owner?.Creature?.CombatState?.Enemies
            .FirstOrDefault(EnemyStatScalingHelper.IsEnemy) : null;
        decimal? enemyParameter = enemy == null ? null : HbrEnemyProfileHelper.GetEnemyParameter(enemy);
        HbrBattleStats stats = default;
        if (enabled && !HbrBattleStatsHelper.TryGetStats(manual.Owner?.Creature, out stats))
            stats = manual.GetHbrBattleStats();
        string state = $"{manual?.HbrBattleProgressData}|{enemyParameter?.ToString(CultureInfo.InvariantCulture)}";
        if (enabled)
            state += "|" + string.Join("|", ElectronicSoldierManual.AllSixSenseAttributes.Select(
                attribute => $"{manual.GetSenseAmount(attribute)}:{manual.GetEffectiveSenseAmount(attribute)}"));
        if (_lastState == state && _lastStats == stats) return;
        _lastState = state;
        _lastStats = stats;

        _statsGrid.Visible = enabled;
        _status.Text = enabled
            ? $"HBR 战斗等级  Lv.{manual.HbrBattleLevel}"
            : "本局未启用 HBR 数值测试";
        _growth.Text = enabled
            ? $"初始六维基础各 {HbrBattleStatsHelper.InitialStat}；普通战、精英战、Boss 战胜利分别提升 1、2、3 级，每级六维各 +4。\n已确认的六感每级使对应战斗属性 +2，按有效等级结算；未确认的分配不改变此处数值。\n此处显示包含当前力量、敏捷效果的实时属性。浮游炮沿用本体属性及本体增益；幻影继承基础六维，并保留自身等级加成，只计算自身的力量、敏捷。"
            : "旧存档继续使用原有数值，不会在读档或打开手册时自动转换。\n使用此测试版本新开一局，才会启用 HBR 数值系统。";
        _enemy.Visible = enabled;
        if (!enabled) return;

        HbrBattleStats senseBonuses = HbrBattleStatsHelper.GetSixSenseBonuses(manual);
        decimal[] values = [stats.Strength, stats.Dexterity, stats.Intelligence,
            stats.Vitality, stats.Spirit, stats.Luck];
        decimal[] bonuses = [senseBonuses.Strength, senseBonuses.Dexterity, senseBonuses.Intelligence,
            senseBonuses.Vitality, senseBonuses.Spirit, senseBonuses.Luck];
        SixSenseAttribute[] senses = [SixSenseAttribute.Madness, SixSenseAttribute.LeadershipCharm,
            SixSenseAttribute.Talent, SixSenseAttribute.Gentleness, SixSenseAttribute.MentalStrength,
            SixSenseAttribute.Simplicity];
        for (int i = 0; i < _values.Length; i++)
        {
            _values[i].Text = values[i].ToString("0.##", CultureInfo.InvariantCulture);
            _senseGrowth[i].Text = $"{ElectronicSoldierManual.GetSenseTitle(senses[i])} Lv.{manual.GetSenseAmount(senses[i])}" +
                $" → +{bonuses[i].ToString("0.##", CultureInfo.InvariantCulture)}\n" +
                $"有效等级 {manual.GetEffectiveSenseAmount(senses[i]).ToString("0.##", CultureInfo.InvariantCulture)} × {HbrBattleStatsHelper.SenseStatGainPerLevel}";
        }
        _enemy.Text = enemyParameter.HasValue
            ? $"当前敌方状态值：{enemyParameter.Value.ToString("0.##", CultureInfo.InvariantCulture)}。技能参照属性与状态值的差值决定基础威力。"
            : "进入战斗后，此处会显示用于技能威力计算的敌方状态值。";
    }

    private void BuildUi()
    {
        _scroll = new ScrollContainer
        {
            Name = "BattleStatsScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(_scroll);
        _scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _scroll.GuiInput += ScrollInput;
        _scroll.GetVScrollBar().FocusMode = FocusModeEnum.All;
        HbrManualPhoneStyle.Scroll(_scroll);

        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 14);
        _scroll.AddChild(content);
        // #stats-head: 32px with the cyan glow.
        _status = HbrManualPhoneStyle.Heading("", 32, Colors.White, glow: true);
        _status.CustomMinimumSize = new Vector2(0f, 44f);
        content.AddChild(_status);
        _statsGrid = new GridContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statsGrid.AddThemeConstantOverride("h_separation", 14);
        _statsGrid.AddThemeConstantOverride("v_separation", 14);
        content.AddChild(_statsGrid);
        string[] names = ["力量", "灵巧", "智慧", "体力", "精神", "运气"];
        string[] uses = ["HP 攻击的主要参照", "DP 攻击的主要参照", "格挡倍率的参照", "防御参照属性", "防御参照属性", "自然暴击概率的参照"];
        for (int i = 0; i < names.Length; i++)
        {
            PanelContainer card = new()
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 168f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _cards[i] = card;
            _statsGrid.AddChild(card);
            VBoxContainer cell = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            cell.AddThemeConstantOverride("separation", 2);
            card.AddChild(cell);
            HBoxContainer top = new() { MouseFilter = MouseFilterEnum.Ignore };
            cell.AddChild(top);
            Label name = HbrManualPhoneStyle.Heading(names[i], 24, HbrManualPhoneArt.Cyan);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(name);
            _values[i] = HbrManualPhoneStyle.Heading("—", 48, Colors.White);
            _values[i].HorizontalAlignment = HorizontalAlignment.Right;
            top.AddChild(_values[i]);
            cell.AddChild(HbrManualPhoneStyle.Label(uses[i], 19, HbrManualPhoneArt.Muted));
            cell.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            _senseGrowth[i] = HbrManualPhoneStyle.Label("", 18, HbrManualPhoneStyle.Text);
            cell.AddChild(_senseGrowth[i]);
        }
        StyleCards();
        content.AddChild(HbrManualPhoneStyle.Label(
            "力量、灵巧、智慧、体力、精神、运气用于战斗计算。启用测试数值后，由战斗等级与已确认六感共同成长。",
            19, HbrManualPhoneArt.Muted, wrap: true));
        _growth = HbrManualPhoneStyle.Label("", 20, HbrManualPhoneStyle.Paragraph, wrap: true);
        _growth.AddThemeConstantOverride("line_spacing", 6);
        content.AddChild(_growth);
        _enemy = HbrManualPhoneStyle.Label("", 20, HbrManualPhoneArt.Muted, wrap: true);
        content.AddChild(_enemy);
        AddSection(content, "技能威力",
            "对 DP：参照属性 A =（2 × 灵巧 + 力量）÷ 3。\n对 HP：参照属性 A =（2 × 力量 + 灵巧）÷ 3。\n力量与灵巧参与攻击；智慧参与格挡的倍率换算。\n" +
            $"伤害 100% 对应基础威力 6000，其他技能倍率同比换算。属性系数 = max（0，1 +（A − 状态值）÷{HbrSkillPower.AttackAttributeScale}）。\n" +
            "基础威力乘属性系数，正倍率最低 1，截断到小数点后 2 位；属性优势持续线性增长，不再配置技能威力上下端。");
        AddSection(content, "增减益与暴击",
            "基础威力 × 攻击增益区 × 防御减益区 × DP／HP 特攻 × 物理与元素相性 × 破坏率 × 其他倍率。\n" +
            "原作参考：同类普通增益取最高两层相加；攻击与元素攻击、心眼、场地等合入攻击区，防御下降与脆弱合入减益区。现有卡牌效果仍保留各自的持续和消耗规则。\n" +
            "自然暴击率 = 1.5% + max（0，运气 − 状态值）× 0.04%，自然部分最高 15%；暴击率增益另加。\n" +
            "暴击先按状态值 −50 重新计算威力，状态值最低为 0；随后乘（1.5 + 暴伤增益）。暴击判定独立于现有掷骰大成功。");
        AddSection(content, "破坏率与格挡",
            "破坏率以 100% 为起点。DP 击破后，按敌方与技能各自的破坏系数增长；增量与本次实际伤害量分别计算。\n" +
            $"我方格挡 100% 对应基础 500，再乘智慧÷{HbrSkillPower.BlockReferenceIntelligence}，向下取整。卡牌、幻影、浮游炮及明确的固定技能格挡共用此规则；按实际伤害比例派生的格挡不重复换算。");
        AddSection(content, "力量与敏捷",
            "我方每层力量使力量属性与灵巧属性各提高 2%；每层敏捷使智慧属性提高 2%，同类层数相加。负层数按相同比例降低对应属性，属性最低为 0。灾厄在百分比加成后按点数扣除。绯刀流最后使力量、灵巧翻倍，体力、精神归零。力量、敏捷不再额外提供固定伤害或格挡。\n" +
            "敌方每层力量在攻击时无视目标一定数值的体力、精神属性；每层敏捷在受击时无视攻击者一定数值的力量、灵巧属性。第一至第五幕每层依次为 1、2、3、4、8 点，第六幕起保持 8 点。负层数反向修正，仅对本次攻击有效；参照属性最低为 0。");
        AddSection(content, "敌方状态值与受击",
            "敌人使用独立状态值 E、HP 和 DP；不会由玩家的体力、精神推导生命上限，也没有复制出来的六维。\n" +
            $"第一幕第 1 层普通房间 E 为 {HbrEnemyProfileHelper.InitialEnemyParameter}，每深入 1 层 +{HbrEnemyProfileHelper.GainPerFloor}；换幕保留此前各幕的楼层成长，再额外 +{HbrEnemyProfileHelper.GainPerAct}。精英当前房间再 +{HbrEnemyProfileHelper.EliteBonus}，Boss 当前房间再 +{HbrEnemyProfileHelper.BossBonus}；房间修正不跨幕累计。Hopper 同样成长。\n" +
            "敌方技能按自身配置参照（2 × 体力 + 精神）÷3，或（体力 + 2 × 精神）÷3。参照属性不随实际命中人数或当前 DP 改变。\n" +
            "受击基础威力按状态值与防御参照的差值插值，最低为 1，达到该技能威力上端后封顶。后续再结算原有增减伤、格挡与 DP。\n" +
            "尖塔敌人沿用当前适配的资源与技能威力参数；尚未逐个替换成 HBR 原作敌技能数据。");
        AddSection(content, "测试量级与来源",
            $"六维、成长速度及与尖塔数值的换算为本模组测试设定。角色初始 HP ×{HbrBattleStatsHelper.PlayerHealthScale}，DP 保留与 HP 的现有比例；固定数值的 HP、DP 增减 ×{HbrResourceValueHelper.FixedResourceScale}。百分比、转阶段回满和按实际伤害派生的资源不重复换算。\n" +
            "敌人默认 HP ×700，柴犬单独 ×1000，DP 使用独立参考值。我方伤害技能使用单一倍率，100% 基础威力为 6000；敌方攻击仍使用旧基础伤害 ×100／×200 的独立受击曲线，阈值为 140。\n" +
            "小型 Hopper 的原血量 14 对应 HP 9800、DP 3000，保留原有随机血量、难度与多人倍率；第一幕第 1 层普通战 E 为 150，此后按地图位置成长。\n" +
            "增减益分区及原作研究参照 hbr-tool/damage-calculator，受击基础曲线参照カンタータ仮面的公式整理，均为社区研究。我方单倍率线性公式、尖塔敌人参数及技能防御权重为本模组适配配置。\n" +
            "卡牌、OD、掷骰与六感玩法继续保留。完整参数与来源见测试分支文档 docs/hbr-numerics-test.md。");
    }

    private void StyleCards()
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i] == null) continue;
            _cards[i].AddThemeStyleboxOverride("panel", i == _highlight ? HbrManualPhoneStyle.Picked() : HbrManualPhoneStyle.Panel());
        }
    }

    // details/summary: a "＋ title" row that opens its text; "－" while open.
    private static void AddSection(VBoxContainer content, string title, string description)
    {
        PanelContainer section = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        section.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(16, 16f, 2f));
        content.AddChild(section);
        VBoxContainer layout = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 0);
        section.AddChild(layout);
        Button summary = new()
        {
            Text = "＋  " + title, Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0f, 52f), FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        HbrManualPhoneStyle.ListEntry(summary, false, 24, 12);
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            summary.AddThemeColorOverride(color, HbrManualPhoneArt.Cyan);
        layout.AddChild(summary);
        Label text = HbrManualPhoneStyle.Label(description, 20, HbrManualPhoneStyle.Paragraph, wrap: true);
        text.AddThemeConstantOverride("line_spacing", 6);
        MarginContainer body = new() { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("margin_bottom", 14);
        body.AddChild(text);
        layout.AddChild(body);
        summary.Pressed += () =>
        {
            body.Visible = !body.Visible;
            summary.Text = (body.Visible ? "－  " : "＋  ") + title;
        };
    }

    private void LayoutContent()
    {
        if (_statsGrid == null) return;
        _statsGrid.Columns = Size.X >= 650 ? 3 : Size.X >= 360 ? 2 : 1;
    }

    private void ScrollInput(InputEvent input)
    {
        if (!_scroll.HasFocus()) return;
        double amount;
        if (input.IsActionPressed("ui_down", true)) amount = 48;
        else if (input.IsActionPressed("ui_up", true)) amount = -48;
        else if (input is InputEventKey { Pressed: true, Keycode: Key.Pagedown }) amount = Math.Max(48, _scroll.Size.Y * 0.8);
        else if (input is InputEventKey { Pressed: true, Keycode: Key.Pageup }) amount = -Math.Max(48, _scroll.Size.Y * 0.8);
        else if (input is InputEventKey { Pressed: true, Keycode: Key.Home }) amount = -double.MaxValue;
        else if (input is InputEventKey { Pressed: true, Keycode: Key.End }) amount = double.MaxValue;
        else return;
        VScrollBar bar = _scroll.GetVScrollBar();
        bar.Value = Math.Clamp(bar.Value + amount, bar.MinValue, Math.Max(bar.MinValue, bar.MaxValue - bar.Page));
        _scroll.AcceptEvent();
    }
}
