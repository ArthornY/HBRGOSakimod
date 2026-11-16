using System.Reflection;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Nodes.Screens;

internal sealed partial class NHbrgoSakiGameOverObserver : Node
{
    private const double PortraitWaitSeconds = 10.0;
    // NGameOverScreen.OpenSummaryScreen fades the summary backstop in over half a second.
    private const double SummaryFadeSeconds = 0.5;

    private static readonly FieldInfo HistoryField =
        PrivateAccess.Field<NGameOverScreen>("_history");
    private static readonly FieldInfo LocalPlayerField =
        PrivateAccess.Field<NGameOverScreen>("_localPlayer");
    private static readonly FieldInfo DeathQuoteField =
        PrivateAccess.Field<NGameOverScreen>("_deathQuote");
    private static readonly FieldInfo EncounterQuoteField =
        PrivateAccess.Field<NGameOverScreen>("_encounterQuote");
    private static readonly FieldInfo CreatureContainerField =
        PrivateAccess.Field<NGameOverScreen>("_creatureContainer");
    private static readonly FieldInfo SummaryContainerField =
        PrivateAccess.Field<NGameOverScreen>("_summaryContainer");

    private double _portraitWaitElapsed;
    private bool _waitingForPortrait;
    private Label _runPeak;
    private ColorRect _summaryBackstop;

    internal NGameOverScreen Screen { get; set; }

    public override void _Ready()
    {
        SetProcess(false);
        if (Screen == null || LocalPlayerField.GetValue(Screen) is not Player player ||
            !RollKeywordRules.UsesCommonRules(player)) return;
        ShowRunPeak(player);
        if (IsSakiLoss())
        {
            ApplyText();
            _waitingForPortrait = !TryShowNarbyPortrait();
        }

        SetProcess(_waitingForPortrait || _runPeak != null);
    }

    // The run summary container is visible from the start; vanilla keeps each of its rows transparent
    // until the player continues to the summary. The peak line waits for that too: shown at once, it
    // sat on the character standing in the middle of the defeat screen.
    private void ShowRunPeak(Player player)
    {
        if (SummaryContainerField.GetValue(Screen) is not NRunSummary summary ||
            summary.GetNodeOrNull<VBoxContainer>("Container") is not { } container ||
            container.GetNodeOrNull<Label>("HbrgoRunPeakTotalDamage") != null) return;

        string text = "最高总伤：" + RunPeakCardDamageHelper.Format(
            RunPeakCardDamageHelper.Get(player));
        Label label = new()
        {
            Name = "HbrgoRunPeakTotalDamage",
            Text = text,
            TooltipText = text,
            CustomMinimumSize = new Vector2(0f, 48f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming,
            // Hidden and out of the mouse's way until the summary opens; it keeps its row meanwhile.
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        container.AddChild(label);
        container.MoveChild(label, 0);
        _runPeak = label;
        _summaryBackstop = Screen.GetNodeOrNull<ColorRect>("%SummaryBackstop");
    }

    public override void _Process(double delta)
    {
        if (_waitingForPortrait)
        {
            _portraitWaitElapsed += delta;
            if (TryShowNarbyPortrait() || _portraitWaitElapsed >= PortraitWaitSeconds)
            {
                _waitingForPortrait = false;
            }
        }

        if (_runPeak != null && SummaryOpened())
        {
            RevealRunPeak();
        }

        if (!_waitingForPortrait && _runPeak == null)
        {
            SetProcess(false);
        }
    }

    // OpenSummaryScreen starts fading the summary backstop in; without that node the line shows at once.
    private bool SummaryOpened() =>
        !GodotObject.IsInstanceValid(_summaryBackstop) || _summaryBackstop.Modulate.A > 0.01f;

    private void RevealRunPeak()
    {
        Label label = _runPeak;
        _runPeak = null;
        if (!GodotObject.IsInstanceValid(label)) return;
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        CreateTween().TweenProperty(label, "modulate:a", 1f, SummaryFadeSeconds);
    }

    private bool IsSakiLoss()
    {
        return Screen != null &&
               HistoryField.GetValue(Screen) is RunHistory { Win: false } &&
               LocalPlayerField.GetValue(Screen) is Player
               {
                   Character: HbrgoSaki
               };
    }

    private void ApplyText()
    {
        if (DeathQuoteField.GetValue(Screen) is MegaRichTextLabel deathQuote)
        {
            deathQuote.Text = "hidesuwa";
        }

        EncounterQuoteField.SetValue(Screen, "兵败如山倒啊desuwa");
    }

    private bool TryShowNarbyPortrait()
    {
        if (CreatureContainerField.GetValue(Screen) is not Control creatureContainer)
        {
            return false;
        }

        NHBRGOSakiCreatureVisuals visuals =
            FindDescendant<NHBRGOSakiCreatureVisuals>(creatureContainer);
        if (visuals == null)
        {
            return false;
        }

        visuals.ShowNarbyPortrait();
        return true;
    }

    private static T FindDescendant<T>(Node parent)
        where T : Node
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is T match)
            {
                return match;
            }

            T descendant = FindDescendant<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }
}
