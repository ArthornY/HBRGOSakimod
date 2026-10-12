using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrMemoryGardenCompendiumHelper
{
    private static readonly Regex EnergyIcon = new(
        @"\[img\](res://images/packed/sprite_fonts/[^\[\]]*energy_icon\.png)\[/img\]",
        RegexOptions.Compiled);

    internal static bool CanBrowseSkills(CardModel card) =>
        HbrgoSakiCardPortraitContext.LibraryVariant != null &&
        HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
        !(card.IsMutable && card.Owner != null);

    // The source energy artwork is larger than the text. Native hover tips size
    // it themselves; this independent reference list needs an explicit size.
    internal static string FormatSkillDescription(string description) =>
        EnergyIcon.Replace(HbrManualGlossaryPresentation.FormatDescription(description, "cards"),
            "[img=28x28]$1[/img]");
}
