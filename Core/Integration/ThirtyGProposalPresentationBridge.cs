using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Config;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Integration;

public sealed record ThirtyGProposalPresentation(Player Player, CardModel TargetCard, string CharacterId,
    int Reaction, int SanRoll, int SanAfter, bool Broken, int Popularity, bool VoteTriggered);

public static class ThirtyGProposalPresentationBridge
{
    public static event Func<ThirtyGProposalPresentation, Task> Requested;
    internal static async Task Present(ThirtyGProposalPresentation proposal)
    {
        if (!LocalContext.IsMe(proposal.Player)) return;
        if (HBRGOSakiConfig.ThirtyGProposalPresentation && Requested is { } requested)
        {
            try
            {
                foreach (Func<ThirtyGProposalPresentation, Task> show in requested.GetInvocationList()) await show(proposal);
                return;
            }
            catch (Exception error) { Log.Warn("[HBRGOSakiMod][30G] Proposal presentation failed: " + error.Message); }
        }
        Fallback(proposal);
    }
    // A single non-modal line is the complete fallback, not the full cut-in UI.
    internal static void Fallback(ThirtyGProposalPresentation proposal)
    {
        if (TestMode.IsOn || NCombatRoom.Instance?.Ui is not { } ui) return;
        Label label = new()
        {
            Text = proposal.TargetCard.Title + "……和我、结婚吧。", ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore, Position = new(180, 180), Size = new(1100, 60),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 28);
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        ui.AddChild(label);
        Tween tween = label.CreateTween(); tween.TweenInterval(2);
        tween.TweenProperty(label, "modulate:a", 0f, .3); tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
