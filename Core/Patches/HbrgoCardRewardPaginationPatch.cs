using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
internal static class HbrgoCardRewardPaginationPatch
{
    private static void Postfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options)
    {
        Control ui = __instance.GetNode<Control>("UI");
        NHbrgoCardRewardPager pager = ui.GetNodeOrNull<NHbrgoCardRewardPager>(NHbrgoCardRewardPager.NodeName);
        if (pager == null)
        {
            if (!options.Any(option => option.Card.Owner?.Character is HbrgoSaki))
            {
                return;
            }

            pager = new NHbrgoCardRewardPager { Name = NHbrgoCardRewardPager.NodeName };
            ui.AddChild(pager);
        }

        pager.Configure(options);
    }
}
