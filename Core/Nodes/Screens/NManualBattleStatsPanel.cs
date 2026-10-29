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
public partial class NManualBattleStatsPanel : Control
{
    private static readonly Color Accent = new("#ed72aa");
    private static readonly Color Muted = new("#a7afb7");
    private ElectronicSoldierManual _manual;
    private ScrollContainer _scroll;
    private GridContainer _statsGrid;
    private readonly Label[] _values = new Label[6];
    private readonly Label[] _senseGrowth = new Label[6];
    private Label _status;
    private Label _enemy;
    private Label _growth;
    private string _lastState;

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

    internal void Refresh(ElectronicSoldierManual manual)
    {
        _manual = manual;
        if (_status == null) return;
        bool enabled = manual?.IsHbrNumericsEnabled == true;
        Creature enemy = enabled ? manual.Owner?.Creature?.CombatState?.Enemies
            .FirstOrDefault(EnemyStatScalingHelper.IsEnemy) : null;
        decimal? enemyParameter = enemy == null ? null : HbrEnemyProfileHelper.GetEnemyParameter(enemy);
        string state = $"{manual?.HbrBattleProgressData}|{enemyParameter?.ToString(CultureInfo.InvariantCulture)}";
        if (enabled)
            state += "|" + string.Join("|", ElectronicSoldierManual.AllSixSenseAttributes.Select(
                attribute => $"{manual.GetSenseAmount(attribute)}:{manual.GetEffectiveSenseAmount(attribute)}"));
        if (_lastState == state) return;
        _lastState = state;

        _statsGrid.Visible = enabled;
        _status.Text = enabled
            ? $"HBR 战斗等级  Lv.{manual.HbrBattleLevel}"
            : "本局未启用 HBR 数值测试";
        _growth.Text = enabled
            ? $"初始六维基础各 {HbrBattleStatsHelper.InitialStat}；普通战、精英战、Boss 战胜利分别提升 1、2、3 级，每级六维各 +4。\n已确认的六感每级使对应战斗属性 +2，按有效等级结算；未确认的分配不改变此处数值。等级本身不再直接增加掷骰出目，原有六感出牌效果仍保留。\n浮游炮继承本体六维；幻影沿用本体六维，并保留自身等级加成。"
            : "旧存档继续使用原有数值，不会在读档或打开手册时自动转换。\n使用此测试版本新开一局，才会启用 HBR 数值系统。";
        _enemy.Visible = enabled;
        if (!enabled) return;

        HbrBattleStats stats = manual.GetHbrBattleStats();
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
            ? $"当前战斗敌能力参数：{enemyParameter.Value.ToString("0.##", CultureInfo.InvariantCulture)}。技能参照属性与此参数的差值决定基础威力。"
            : "进入战斗后，此处会显示用于技能威力计算的敌能力参数。";
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

        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 14);
        _scroll.AddChild(content);
        content.AddChild(MakeLabel("战斗属性", 26, Colors.White));
        content.AddChild(MakeLabel("力、器用、知性、体力、精神、运用于战斗计算。启用测试数值后，由战斗等级与已确认六感共同成长。", 18, Muted));
        _status = MakeLabel("", 21, Accent);
        content.AddChild(_status);
        _statsGrid = new GridContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statsGrid.AddThemeConstantOverride("h_separation", 10);
        _statsGrid.AddThemeConstantOverride("v_separation", 10);
        content.AddChild(_statsGrid);
        string[] names = ["力", "器用", "知性", "体力", "精神", "运"];
        string[] uses = ["HP 攻击的主要参照", "DP 攻击的主要参照", "格挡倍率的参照", "防御参照属性", "防御参照属性", "自然暴击概率的参照"];
        for (int i = 0; i < names.Length; i++)
        {
            PanelContainer card = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("#25282e"),
                ContentMarginLeft = 14, ContentMarginRight = 14,
                ContentMarginTop = 10, ContentMarginBottom = 10,
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
            });
            _statsGrid.AddChild(card);
            VBoxContainer cell = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 4);
            card.AddChild(cell);
            cell.AddChild(MakeLabel(names[i], 19, Colors.White));
            _values[i] = MakeLabel("—", 25, Accent);
            cell.AddChild(_values[i]);
            cell.AddChild(MakeLabel(uses[i], 15, Muted));
            _senseGrowth[i] = MakeLabel("", 15, Muted);
            cell.AddChild(_senseGrowth[i]);
        }
        _growth = MakeLabel("", 18, Muted);
        content.AddChild(_growth);
        _enemy = MakeLabel("", 18, Muted);
        content.AddChild(_enemy);
        AddSection(content, "技能威力",
            "对 DP：参照属性 A =（2 × 器用 + 力）÷ 3。\n对 HP：参照属性 A =（2 × 力 + 器用）÷ 3。\n力与器用参与攻击；知性参与格挡的倍率换算。\n" +
            "以 A − 敌能力参数计算基础威力：低于敌能力时递减，相等时达到威力下端，差值达到技能阈值时达到上端。\n超过阈值仍会继续成长：每超出 1 点，增加威力上端的 0.25%。");
        AddSection(content, "增减益与暴击",
            "基础威力 × 攻击增益区 × 防御减益区 × DP／HP 特攻 × 物理与元素相性 × 破坏率 × 其他倍率。\n" +
            "原作参考：同类普通增益取最高两层相加；攻击与元素攻击、心眼、场地等合入攻击区，防御下降与脆弱合入减益区。现有卡牌效果仍保留各自的持续和消耗规则。\n" +
            "自然暴击率 = 1.5% + max（0，运 − 敌能力）× 0.04%，自然部分最高 15%；暴击率增益另加。\n" +
            "暴击先按敌能力 −50 重新计算威力，敌能力最低为 0；随后乘（1.5 + 暴伤增益）。暴击判定独立于现有掷骰大成功。");
        AddSection(content, "破坏率与格挡",
            "破坏率以 100% 为起点。DP 击破后，按敌方与技能各自的破坏系数增长；增量与本次实际伤害量分别计算。\n" +
            "主动格挡先将旧基础值 ×100，再按知性计算增益曲线：下端 80%、上端 120%、阈值 300；高于阈值后仍继续成长。按实际伤害比例派生的格挡不重复换算。");
        AddSection(content, "敌方能力与受击",
            "敌人使用独立能力值 E、HP 和 DP；不会由玩家的体力、精神推导生命上限，也没有复制出来的六维。\n" +
            "敌方技能按自身配置参照（2 × 体力 + 精神）÷3，或（体力 + 2 × 精神）÷3。参照属性不随实际命中人数或当前 DP 改变。\n" +
            "受击基础威力按敌能力与防御参照的差值插值，最低为 1，达到该技能威力上端后封顶。后续再结算原有增减伤、格挡与 DP。\n" +
            "尖塔敌人沿用当前适配的资源与技能威力参数；尚未逐个替换成 HBR 原作敌技能数据。");
        AddSection(content, "测试量级与来源",
            "六维、成长速度及与尖塔数值的换算为本模组测试设定。玩家最大 HP ×50；敌人默认 HP ×1000，已有独立配置的敌人按自身基准换算。普通技能威力下端为旧基础伤害 ×1000，上端为下端的 2 倍，阈值为 140。\n" +
            "小型 Hopper 的能力值为 150；原血量 14 对应 HP 10000、DP 3000，保留原有随机血量、难度与多人倍率。\n" +
            "出伤公式参照 hbr-tool/damage-calculator，受击基础曲线参照カンタータ仮面的公式整理，均为社区研究。尖塔敌人的参数及技能防御权重为本模组适配配置。\n" +
            "卡牌、OD、掷骰与六感玩法继续保留。完整参数与来源见测试分支文档 docs/hbr-numerics-test.md。");
    }

    private static void AddSection(VBoxContainer content, string title, string description)
    {
        content.AddChild(new HSeparator());
        content.AddChild(MakeLabel(title, 21, Accent));
        content.AddChild(MakeLabel(description, 18, new Color("#d7dce1")));
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new()
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
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
