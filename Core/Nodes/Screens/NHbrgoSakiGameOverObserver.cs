using System.Reflection;
using Godot;
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

    private double _portraitWaitElapsed;

    internal NGameOverScreen Screen { get; set; }

    public override void _Ready()
    {
        SetProcess(false);
        if (!IsSakiLoss())
        {
            return;
        }

        ApplyText();
        if (!TryShowNarbyPortrait())
        {
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        _portraitWaitElapsed += delta;
        if (TryShowNarbyPortrait() ||
            _portraitWaitElapsed >= PortraitWaitSeconds)
        {
            SetProcess(false);
        }
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
