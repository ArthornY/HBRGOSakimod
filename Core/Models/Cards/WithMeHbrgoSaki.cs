using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class WithMeHbrgoSaki : CardModel
{
    public override int MaxUpgradeLevel => 7;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    public WithMeHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        string suffix = CurrentUpgradeLevel > 0 ? $"+{CurrentUpgradeLevel}" : string.Empty;
        description.Add("GardenUpgradeSuffix", suffix);
        description.Add("GardenUpgradeDescription", CurrentUpgradeLevel > 0
            ? new LocString("cards", Id.Entry + ".randomUpgradeDescription").GetFormattedText()
            : string.Empty);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var candidates = HbrMemoryGardenCardCatalog.CardTypes;
        var rng = Owner.RunState.Rng.CombatCardGeneration;
        int index = rng.NextInt(candidates.Count);
        CardModel generated = CombatState.CreateCard(
            ModelDb.GetById<CardModel>(ModelDb.GetId(candidates[index])),
            Owner);

        for (int upgrade = 0; upgrade < CurrentUpgradeLevel; upgrade++)
        {
            // Recompute after each choice so prerequisites and mutually exclusive
            // effects stay valid, even when the source can upgrade more often.
            IReadOnlyList<int> effects = HbrMemoryGardenUpgradeCatalog.GetAvailableEffectIds(generated);
            if (effects.Count == 0) break;
            int effect = effects[effects.Count == 1 ? 0 : rng.NextInt(effects.Count)];
            if (!HbrMemoryGardenUpgradeCatalog.TryApplyUpgrade(generated, effect)) break;
        }

        await CardPileCmd.AddGeneratedCardToCombat(
            generated,
            PileType.Hand,
            Owner);
    }

    protected override void OnUpgrade()
    {
        // Each level upgrades the generated card once; this card remains 1 energy.
    }
}
