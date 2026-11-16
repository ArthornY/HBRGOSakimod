using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

// A permanent shop memory. Its effects are read from the owner's permanent
// deck; no combat copy, draw or card play is needed to activate them.
public sealed class AllOrdersConfirmedHbrgoSaki : ThirtyOneBGardenCard, IThirtyOneCMemberCard
{
    internal const int Flash = 0, Saturation = 1, DestructionIllusion = 2,
        DiamondDust = 3, AmplifierCannon = 4, HappyDay = 5, Shopkeeper = 6;
    protected override int FlashEffectId => -1;
    public ThirtyOneCMember ThirtyOneCMember => ThirtyOneCMember.SatsukiMari;
    protected override bool IsPlayable => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Unplayable };
    public AllOrdersConfirmedHbrgoSaki() : base(0, CardType.Skill, TargetType.None) { }
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) => !ReferenceEquals(card, this);

    protected override void AddExtraArgsToDescription(LocString text)
    {
        base.AddExtraArgsToDescription(text);
        text.Add("MariMembers", IsMutable && Owner != null
            ? ThirtyOneCKeywordRules.GetPermanentDistinctCount(Owner) : 0);
        text.Add("MariMembership", HasUpgradeEffect(DiamondDust)
            ? $"\n当前31C：{(IsMutable && Owner != null ? ThirtyOneCKeywordRules.GetPermanentDistinctCount(Owner) : 0)}/6种。" : string.Empty);
    }

    public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        if (Owner != player) return;
        foreach (CardModel card in cards.OfType<AllOrdersConfirmedHbrgoSaki>().ToArray())
            if (card.Pile?.IsCombatPile == true) card.RemoveFromState();
        cards.RemoveAll(card => card is AllOrdersConfirmedHbrgoSaki);
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy) =>
        ReferenceEquals(card, this) && card.Pile?.IsCombatPile == true
            ? CardPileCmd.RemoveFromCombat(card) : Task.CompletedTask;

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
