using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class NararaContestBadgePatch
{
    private const string BadgeName = "HbrNararaContestBadge";
    private static Texture2D _texture;
    private static void Postfix(NCard __instance, PileType pileType)
    {
        TextureRect badge = __instance.GetNodeOrNull<TextureRect>(BadgeName);
        bool visible = pileType == PileType.Hand && __instance.Model?.Pile?.Type == PileType.Hand &&
            __instance.Visibility == MegaCrit.Sts2.Core.Entities.UI.ModelVisibility.Visible &&
            LocalContext.IsMe(__instance.Model.Owner) && NararaNullificationHelper.HasPendingContest(__instance.Model);
        if (badge == null && visible)
        {
            badge = new TextureRect
            {
                Name = BadgeName, Position = new Vector2(75, -155), Size = new Vector2(44, 44),
                MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 10,
                Texture = _texture ??= ResourceLoader.Load<Texture2D>("res://images/ui/hbr_fate_dice/fate_dice_glyph.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            __instance.AddChild(badge);
        }
        if (badge != null) badge.Visible = visible;
    }
}
