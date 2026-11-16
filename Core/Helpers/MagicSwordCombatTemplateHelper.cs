using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Permanent combat-excluded cards are never ordinary battle draws or random
// generated cards. Explicit finisher generation deliberately bypasses this
// candidate filter, while the permanent shop memory has no battle form.
internal static class MagicSwordCombatTemplateHelper
{
    internal static bool IsTemplate(CardModel card) =>
        card is MagicSwordLuckySevenHbrgoSaki or MagicSwordLuckySevenOpeningHbrgoSaki
            or AllOrdersConfirmedHbrgoSaki;

    internal static bool CanGenerateFromDeck(CardModel card) => card != null &&
        !IsTemplate(card) && !HBRGOSakiContentPack.RetiredCardTypes.Contains(card.GetType());
}
