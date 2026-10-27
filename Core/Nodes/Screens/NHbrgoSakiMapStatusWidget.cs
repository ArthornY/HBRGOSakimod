using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NHbrgoSakiMapStatusWidget : Control
{
    private const string IconPath =
        "res://images/ui/map_character_status.jpg";
    private const decimal ReferenceDamage = 100m;

    private Player _player;
    private NTopBarMapButton _mapButton;
    private NMapScreen _mapScreen;
    private Button _entryButton;
    private Control _popup;
    private Label _damageMultiplierLabel;
    private Label _damageTakenMultiplierLabel;
    private Label _blockMultiplierLabel;
    private Label _destructionRateMultiplierLabel;
    private Label _overdriveMultiplierLabel;
    private Label _rollBonusLabel;
    private Label _powerHeader;
    private VBoxContainer _powerList;
    private Button _closeButton;

    public override void _Ready()
    {
        Name = "HBRGOCharacterStatusWidget";
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;
        SetFullRect(this);
        BuildEntryButton();
        BuildPopup();
        if (_mapScreen != null)
        {
            _mapScreen.Connect(
                CanvasItem.SignalName.VisibilityChanged,
                Callable.From(OnMapVisibilityChanged));
        }

        RefreshBoundState();
    }

    public override void _ExitTree()
    {
        if (_entryButton != null &&
            GodotObject.IsInstanceValid(_entryButton))
        {
            _entryButton.QueueFree();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (_popup?.Visible == true &&
            @event.IsActionPressed("ui_cancel"))
        {
            ClosePopup();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void Bind(
        Player player,
        NTopBarMapButton mapButton,
        NMapScreen mapScreen)
    {
        _player = player;
        _mapButton = mapButton;
        _mapScreen = mapScreen;
        if (IsNodeReady())
        {
            BuildEntryButton();
            RefreshBoundState();
        }
    }

    private void BuildEntryButton()
    {
        if (_entryButton != null ||
            _mapButton?.GetParent() is not Container topBarContainer)
        {
            return;
        }

        _entryButton = new Button
        {
            Name = "CharacterStatusButton",
            CustomMinimumSize = new Vector2(80f, 80f),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "查看角色状态"
        };
        Color buttonColor = new(0.08f, 0.1f, 0.13f, 0.98f);
        Color borderColor = new(0.95f, 0.58f, 0.74f, 0.95f);
        _entryButton.AddThemeStyleboxOverride(
            "normal",
            CreatePanelStyle(buttonColor, borderColor, 3, 7));
        _entryButton.AddThemeStyleboxOverride(
            "hover",
            CreatePanelStyle(
                buttonColor.Lightened(0.12f),
                Colors.White,
                3,
                7));
        _entryButton.AddThemeStyleboxOverride(
            "pressed",
            CreatePanelStyle(
                buttonColor.Darkened(0.1f),
                Colors.White,
                3,
                7));
        _entryButton.AddThemeStyleboxOverride(
            "focus",
            CreatePanelStyle(buttonColor, Colors.White, 3, 7));
        _entryButton.Pressed += OpenPopup;
        topBarContainer.AddChild(_entryButton);
        topBarContainer.MoveChild(_entryButton, _mapButton.GetIndex());

        TextureRect icon = new()
        {
            Texture = ResourceLoader.Load<Texture2D>(IconPath),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 5f,
            OffsetTop = 5f,
            OffsetRight = -5f,
            OffsetBottom = -5f,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _entryButton.AddChild(icon);
    }

    private void BuildPopup()
    {
        _popup = new Control
        {
            Name = "CharacterStatusPopup",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 101
        };
        SetFullRect(_popup);
        AddChild(_popup);

        Button backdrop = new()
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.Arrow
        };
        StyleBoxFlat backdropStyle = new()
        {
            BgColor = new Color(0.01f, 0.012f, 0.016f, 0.88f)
        };
        backdrop.AddThemeStyleboxOverride("normal", backdropStyle);
        backdrop.AddThemeStyleboxOverride("hover", backdropStyle);
        backdrop.AddThemeStyleboxOverride("pressed", backdropStyle);
        backdrop.Pressed += ClosePopup;
        _popup.AddChild(backdrop);

        PanelContainer dialog = new()
        {
            Name = "CharacterStatusDialog",
            AnchorLeft = 0.19f,
            AnchorTop = 0.08f,
            AnchorRight = 0.81f,
            AnchorBottom = 0.92f,
            MouseFilter = MouseFilterEnum.Stop
        };
        dialog.AddThemeStyleboxOverride(
            "panel",
            CreatePanelStyle(
                new Color(0.055f, 0.065f, 0.078f, 0.99f),
                new Color(0.86f, 0.64f, 0.75f, 0.95f),
                2,
                8));
        _popup.AddChild(dialog);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        dialog.AddChild(margin);

        VBoxContainer content = new();
        content.AddThemeConstantOverride("separation", 14);
        margin.AddChild(content);

        HBoxContainer header = new();
        content.AddChild(header);

        Label title = CreateLabel("角色状态", 32, Colors.White);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        _closeButton = new Button
        {
            Text = "×",
            CustomMinimumSize = new Vector2(52f, 52f),
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "关闭"
        };
        _closeButton.AddThemeFontSizeOverride("font_size", 30);
        _closeButton.AddThemeColorOverride("font_color", Colors.White);
        _closeButton.AddThemeColorOverride(
            "font_hover_color",
            Colors.White);
        _closeButton.AddThemeStyleboxOverride(
            "normal",
            CreatePanelStyle(
                new Color(0.13f, 0.15f, 0.18f),
                new Color(0.4f, 0.43f, 0.47f),
                1,
                6));
        _closeButton.AddThemeStyleboxOverride(
            "hover",
            CreatePanelStyle(
                new Color(0.3f, 0.12f, 0.18f),
                new Color(1f, 0.62f, 0.72f),
                1,
                6));
        _closeButton.Pressed += ClosePopup;
        header.AddChild(_closeButton);

        GridContainer multiplierGrid = new()
        {
            Columns = 3
        };
        multiplierGrid.AddThemeConstantOverride("h_separation", 26);
        multiplierGrid.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(multiplierGrid);

        _damageMultiplierLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "总伤害倍率",
            new Color(1f, 0.66f, 0.5f));
        _damageTakenMultiplierLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "承伤倍率",
            new Color(0.48f, 0.82f, 1f));
        _blockMultiplierLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "格挡倍率",
            new Color(0.48f, 0.9f, 0.66f));
        _destructionRateMultiplierLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "破坏率",
            new Color(1f, 0.62f, 0.42f));
        _overdriveMultiplierLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "OD倍率",
            new Color(0.95f, 0.58f, 0.82f));
        _rollBonusLabel = CreateMultiplierDisplay(
            multiplierGrid,
            "出目修正",
            new Color(1f, 0.82f, 0.38f));

        HSeparator separator = new();
        separator.AddThemeConstantOverride("separation", 2);
        content.AddChild(separator);

        _powerHeader = CreateLabel(
            "可视能力",
            24,
            new Color(1f, 0.82f, 0.45f));
        content.AddChild(_powerHeader);

        ScrollContainer scroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        _powerList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _powerList.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(_powerList);
    }

    private void RefreshBoundState()
    {
        if (_entryButton == null)
        {
            return;
        }

        bool mapVisible = _mapScreen != null && _mapScreen.Visible;
        _entryButton.Visible = _player != null && !mapVisible;
        if (_player == null)
        {
            ClosePopup();
        }
    }

    private void OnMapVisibilityChanged()
    {
        RefreshBoundState();
        if (_mapScreen?.Visible == true)
        {
            ClosePopup();
        }
    }

    private void OpenPopup()
    {
        if (_player == null || _popup == null)
        {
            return;
        }

        RefreshStatus();
        _popup.Visible = true;
        _closeButton?.GrabFocus();
    }

    private void ClosePopup()
    {
        if (_popup != null)
        {
            _popup.Visible = false;
        }

        if (_entryButton?.Visible == true)
        {
            _entryButton.GrabFocus();
        }
    }

    private void RefreshStatus()
    {
        Creature creature = _player?.Creature;
        if (creature == null)
        {
            return;
        }

        decimal damageMultiplier = CalculateDamageMultiplier(
            _player,
            outgoing: true);
        decimal damageTakenMultiplier = CalculateDamageMultiplier(
            _player,
            outgoing: false);
        decimal blockMultiplier = CalculateBlockMultiplier(_player);
        decimal destructionRateMultiplier =
            CalculateDestructionRateMultiplier(_player);
        decimal overdriveMultiplier =
            CalculateOverdriveMultiplier(_player);
        var rollModifiers =
            RollGlossaryCapability.GetProjectedAttackRollModifiers(_player);
        _damageMultiplierLabel.Text = FormatMultiplier(damageMultiplier);
        _damageTakenMultiplierLabel.Text =
            FormatMultiplier(damageTakenMultiplier);
        _blockMultiplierLabel.Text = FormatMultiplier(blockMultiplier);
        _destructionRateMultiplierLabel.Text =
            FormatMultiplier(destructionRateMultiplier);
        _overdriveMultiplierLabel.Text =
            FormatMultiplier(overdriveMultiplier);
        _rollBonusLabel.Text = $"固定 {FormatSignedValue(rollModifiers.FlatBonus)}  /  {FormatSignedValue(rollModifiers.PercentBonus)}%";

        foreach (Node child in _powerList.GetChildren())
        {
            child.QueueFree();
        }

        List<PowerModel> visiblePowers = creature.Powers
            .Where(power => power.IsVisible)
            .ToList();
        _powerHeader.Text = $"可视能力（{visiblePowers.Count}）";
        if (visiblePowers.Count == 0)
        {
            Label empty = CreateLabel(
                "当前没有可视能力",
                20,
                new Color(0.66f, 0.69f, 0.73f));
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.CustomMinimumSize = new Vector2(0f, 90f);
            empty.VerticalAlignment = VerticalAlignment.Center;
            _powerList.AddChild(empty);
            return;
        }

        foreach (PowerModel power in visiblePowers)
        {
            _powerList.AddChild(CreatePowerRow(power));
        }
    }

    private Control CreatePowerRow(PowerModel power)
    {
        Color accent = power.Type switch
        {
            PowerType.Buff => new Color(0.36f, 0.82f, 0.72f),
            PowerType.Debuff => new Color(1f, 0.42f, 0.42f),
            _ => new Color(0.88f, 0.72f, 0.42f)
        };
        PanelContainer row = new()
        {
            CustomMinimumSize = new Vector2(0f, 92f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeStyleboxOverride(
            "panel",
            CreatePanelStyle(
                new Color(0.09f, 0.105f, 0.125f, 0.98f),
                accent.Darkened(0.25f),
                1,
                6));

        MarginContainer rowMargin = new();
        rowMargin.AddThemeConstantOverride("margin_left", 14);
        rowMargin.AddThemeConstantOverride("margin_top", 10);
        rowMargin.AddThemeConstantOverride("margin_right", 16);
        rowMargin.AddThemeConstantOverride("margin_bottom", 10);
        row.AddChild(rowMargin);

        HBoxContainer rowContent = new();
        rowContent.AddThemeConstantOverride("separation", 14);
        rowMargin.AddChild(rowContent);

        TextureRect icon = new()
        {
            Texture = TryGetPowerIcon(power),
            CustomMinimumSize = new Vector2(62f, 62f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        rowContent.AddChild(icon);

        VBoxContainer textColumn = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        textColumn.AddThemeConstantOverride("separation", 3);
        rowContent.AddChild(textColumn);

        Label title = CreateLabel(
            GetPowerTitle(power),
            22,
            accent);
        textColumn.AddChild(title);

        MegaRichTextLabel description = new()
        {
            BbcodeEnabled = true,
            AutoSizeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
        {
            description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
            description.AddThemeFontSizeOverride($"{font}_size", 17);
        }
        description.AddThemeColorOverride(
            ThemeConstants.RichTextLabel.DefaultColor,
            new Color(0.86f, 0.87f, 0.89f));
        description.Text = GetPowerDescription(power);
        textColumn.AddChild(description);
        return row;
    }

    private static string GetPowerTitle(PowerModel power)
    {
        string title = power.Title.GetFormattedText();
        return power.StackType == PowerStackType.Counter
            ? $"{title}  {power.DisplayAmount}"
            : title;
    }

    private static string GetPowerDescription(PowerModel power)
    {
        try
        {
            HoverTip tip = power.HoverTips
                .OfType<HoverTip>()
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tip.Description))
            {
                return tip.Description;
            }
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Unable to build status text for {power.Id}: " +
                exception.Message);
        }

        return power.Description.GetFormattedText();
    }

    private static Texture2D TryGetPowerIcon(PowerModel power)
    {
        try
        {
            return power.Icon;
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Unable to load status icon for {power.Id}: " +
                exception.Message);
            return null;
        }
    }

    private static decimal CalculateDamageMultiplier(
        Player player,
        bool outgoing)
    {
        Creature creature = player?.Creature;
        if (creature == null)
        {
            return 1m;
        }

        if (outgoing)
        {
            try { return TemporaryDamageBonusHelper.CalculateOutgoingMultiplier(creature); }
            catch (Exception exception)
            {
                Log.Warn("Unable to calculate character damage multiplier: " + exception.Message);
                return 1m;
            }
        }

        Creature target = outgoing ? null : creature;
        Creature dealer = outgoing ? creature : null;
        decimal modifiedDamage = ReferenceDamage;
        decimal modeMultiplier = 1m;
        decimal manualMultiplierOffset = 0m;
        decimal finalMultiplier = 1m;
        bool hbrNumerics = HbrBattleStatsHelper.IsEnabled(creature);
        HbrDamageMultiplierAccumulator hbrFactors = new(dealer, 1m,
            HbrDamageMultiplierPolicy.Combat, hbrNumerics, target);
        decimal unusedRawDamage = 0m;
        AdditiveDamageResult groupedResult = AdditiveDamageResult.Empty;
        bool completed = false;

        HbrgoSakiDamageMultiplierHelper.Begin(dealer, target, hbrNumerics);
        try
        {
            foreach (AbstractModel model in player.RunState
                         .IterateHookListeners(creature.CombatState))
            {
                if (PhantomHelper.IsSummonerOwnedDamageModifier(
                        model,
                        dealer))
                {
                    continue;
                }

                decimal multiplier;
                try
                {
                    decimal amountForHook =
                        model.GetType().Assembly ==
                            typeof(NHbrgoSakiMapStatusWidget).Assembly
                            ? ReferenceDamage
                            : modifiedDamage;
                    multiplier = DamageModifierCompatibility
                        .ModifyDamageMultiplicative(
                            model,
                            target,
                            amountForHook,
                            ValueProp.Move,
                            dealer,
                            null,
                            null);
                }
                catch (Exception exception)
                {
                    Log.Warn(
                        $"Unable to evaluate damage multiplier from " +
                        $"{model.Id}: {exception.Message}");
                    continue;
                }

                if (hbrNumerics)
                {
                    hbrFactors.Add(model, multiplier, ref modifiedDamage, ref unusedRawDamage);
                }
                else if (IsModeDamageModel(model))
                {
                    modeMultiplier *= multiplier;
                }
                else if (model is ElectronicSoldierManual)
                {
                    manualMultiplierOffset += multiplier - 1m;
                }
                else if (model is HighDimensionalShellPower)
                {
                    finalMultiplier *= multiplier;
                }
                else
                {
                    modifiedDamage *= multiplier;
                }
            }

            groupedResult = HbrgoSakiDamageMultiplierHelper.Complete();
            completed = true;
        }
        catch (Exception exception)
        {
            Log.Warn(
                "Unable to calculate character damage multiplier: " +
                exception.Message);
        }
        finally
        {
            if (!completed)
            {
                HbrgoSakiDamageMultiplierHelper.Abort();
            }
        }

        if (hbrNumerics)
            return DamageAmplificationLimits.MultiplySaturated(
                DamageAmplificationLimits.MultiplySaturated(
                    hbrFactors.Resolve(groupedResult, false), hbrFactors.OtherMultiplier),
                hbrFactors.FinalMultiplier);

        decimal manualMultiplier = Math.Max(
            0m,
            1m + manualMultiplierOffset);
        decimal hbrgoMultiplier = Math.Max(
            0m,
            modeMultiplier * manualMultiplier +
            groupedResult.MultiplierOffset);
        decimal result = modifiedDamage * hbrgoMultiplier * finalMultiplier;
        return Math.Max(0m, result / ReferenceDamage);
    }

    private static bool IsModeDamageModel(AbstractModel model)
    {
        return model is NormalStatePower or
            ReptureDualSwordsPower or
            ReptureBowPower or
            LoliFormPower;
    }

    private static decimal CalculateBlockMultiplier(Player player)
    {
        Creature creature = player?.Creature;
        if (creature == null)
        {
            return 1m;
        }

        decimal modifiedBlock = ReferenceDamage;
        foreach (AbstractModel model in player.RunState
                     .IterateHookListeners(creature.CombatState))
        {
            try
            {
                modifiedBlock *= model.ModifyBlockMultiplicative(
                    creature,
                    modifiedBlock,
                    ValueProp.Move,
                    null,
                    null);
            }
            catch (Exception exception)
            {
                Log.Warn(
                    $"Unable to evaluate block multiplier from " +
                    $"{model.Id}: {exception.Message}");
            }
        }

        return Math.Max(0m, modifiedBlock / ReferenceDamage);
    }

    private static decimal CalculateDestructionRateMultiplier(Player player)
    {
        Creature creature = player?.Creature;
        Creature target = creature?.CombatState?
            .GetOpponentsOf(creature)
            .FirstOrDefault(opponent => opponent.IsHittable);
        if (creature == null || target == null)
        {
            return 1m;
        }

        decimal modifiedAmount = ReferenceDamage;
        PowerModel destructionRate = ModelDb.Power<DestructionRatePower>();
        foreach (AbstractModel model in player.RunState
                     .IterateHookListeners(creature.CombatState))
        {
            try
            {
                modifiedAmount *=
                    model.ModifyPowerAmountGivenMultiplicative(
                        destructionRate,
                        creature,
                        modifiedAmount,
                        target,
                        null);
            }
            catch (Exception exception)
            {
                Log.Warn(
                    $"Unable to evaluate destruction-rate multiplier from " +
                    $"{model.Id}: {exception.Message}");
            }
        }

        return Math.Max(0m, modifiedAmount / ReferenceDamage);
    }

    private static decimal CalculateOverdriveMultiplier(Player player)
    {
        return player?.Creature?
            .GetPower<MaidCultivationPower>()?
            .GetAvailableOverclockMultiplier() ?? 1m;
    }

    private static string FormatMultiplier(decimal multiplier)
    {
        return $"×{multiplier:0.##}  ({multiplier * 100m:0.##}%)";
    }

    private static string FormatSignedValue(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static Label CreateMultiplierDisplay(
        Container parent,
        string title,
        Color color)
    {
        VBoxContainer column = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 3);
        parent.AddChild(column);

        Label titleLabel = CreateLabel(title, 19, new Color(0.72f, 0.75f, 0.79f));
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(titleLabel);

        Label valueLabel = CreateLabel("×1  (100%)", 28, color);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(valueLabel);
        return valueLabel;
    }

    private static Label CreateLabel(
        string text,
        int fontSize,
        Color color)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            fontSize);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            color);
        return label;
    }

    private static StyleBoxFlat CreatePanelStyle(
        Color background,
        Color border,
        int borderWidth,
        int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius
        };
    }

    private static void SetFullRect(Control control)
    {
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }
}
