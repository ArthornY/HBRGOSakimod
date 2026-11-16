using System;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Public, read-only surface for the separate card-face UI. Read operations do
// not allocate a combat state, consume RNG, mutate previews, or emit events.
public static class IrohaDyeState
{
    public static int GetLayers(CardModel card) => IrohaDyeCombatHelper.Read(card)?.Layers ?? 0;
    public static decimal GetDamageBonusPercent(CardModel card) => IrohaDyeCombatHelper.Read(card)?.Percent ?? 0m;
    public static event Action<CardModel> Changed;
    internal static void Notify(CardModel card)
    {
        if (Changed == null) return;
        foreach (Action<CardModel> listener in Changed.GetInvocationList())
            try { listener(card); }
            catch (Exception) { Changed -= listener; }
    }
    internal static IHoverTip HoverTip() => HoverTipCompatibility.CreateKeyword(
        HbrCompactText.Literal("title", "染色"), HbrCompactText.Literal("text",
        "每次染色记录一层，本次加成50%（奇观艺术为80%），同牌各层相加。仅本场战斗有效；与攻击力上升相加，不占同种2份的名额。"), "HBR_IROHA_DYE");
}
