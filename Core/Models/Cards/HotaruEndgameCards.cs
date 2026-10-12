using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public abstract class HotaruEndgameCard : HotaruCannonCard
{
    protected HotaruEndgameCard(int energyCost)
        : base(energyCost, CardType.Skill, CardRarity.Token, HotaruTargetTypes.MaxLevelOwnedCannon)
    {
    }

    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    internal bool IsPermanentOriginRemoved => GetPermanentOrigin(this).HasBeenRemovedFromState;

    protected override bool IsPlayable => Owner?.GetRelic<Millsage>() is { } relic &&
        !HasBeenRemovedFromState &&
        !IsPermanentOriginRemoved &&
        Owner.Creature?.CombatState?.Allies.Any(relic.CanRetireCannon) == true;

    protected async Task<bool> RetireTarget(PlayerChoiceContext context, CardPlay play, bool sacrifice)
    {
        // A replayed effect cannot retire a second cannon or pay another reward
        // after this exact card has already removed its permanent origin.
        if (HasBeenRemovedFromState || IsPermanentOriginRemoved || play.Target == null ||
            Owner?.GetRelic<Millsage>() is not { } relic ||
            !relic.CanRetireCannon(play.Target)) return false;
        var kind = ((HotaruCannonModel)play.Target.Monster).Kind;
        bool previouslyRetired = relic.WasCannonRetired(kind);
        try
        {
            if (!await relic.RetireCannon(play.Target, sacrifice, context, this)) return false;
        }
        catch
        {
            // Retirement commits before UI/power callbacks. If one of those
            // fails, keep the committed payment from being used a second time.
            if (!previouslyRetired && relic.WasCannonRetired(kind))
                await RemovePermanentOrigin();
            throw;
        }
        await RemovePermanentOrigin();
        return true;
    }

    private async Task RemovePermanentOrigin()
    {
        CardModel origin = GetPermanentOrigin(this);
        CardModel[] copies = Owner.PlayerCombatState?.AllCards.Where(card =>
            !card.HasBeenRemovedFromState && ReferenceEquals(card.Owner, Owner) &&
            (ReferenceEquals(card, this) || ReferenceEquals(GetPermanentOrigin(card), origin)))
            .ToArray() ?? Array.Empty<CardModel>();
        if (ReferenceEquals(origin.Owner, Owner) && origin.Pile?.Type == PileType.Deck &&
            !origin.HasBeenRemovedFromState)
            await CardPileCmd.RemoveFromDeck(origin, showPreview: false);
        // Native deck removal also prevents detached ordinary copies from being
        // restored by OD. Match the permanent object, never another card's ID.
        foreach (CardModel copy in copies)
            if (!copy.HasBeenRemovedFromState && copy.Pile?.IsCombatPile == true)
                await CardPileCmd.RemoveFromCombat(copy, skipVisuals: true);
        if (!HasBeenRemovedFromState && Pile == null) RemoveFromState();
    }

    private static CardModel GetPermanentOrigin(CardModel card)
    {
        // Native clones clear DeckVersion, but keep their exact CloneOf chain.
        // Following that chain also covers dupes without deleting equal IDs.
        var visited = new HashSet<CardModel>(ReferenceEqualityComparer.Instance);
        CardModel current = card;
        while (visited.Add(current))
        {
            if (current.DeckVersion is { } origin) return origin;
            if (current.CloneOf == null) return current;
            current = current.CloneOf;
        }
        return card;
    }

    protected override void OnUpgrade() { }
}

[CustomID("HOTARU_SACRIFICE")]
public sealed class HotaruSacrifice : HotaruEndgameCard
{
    // Native negative costs represent X/unplayable cards. A zero cost plus an
    // explicit energy gain implements the agreed playable-at-zero behavior.
    public HotaruSacrifice() : base(0) { }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<HotaruDamageAmplificationPower>() };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (await RetireTarget(context, play, sacrifice: true))
            await PlayerCmd.GainEnergy(3m, Owner);
    }
}

[CustomID("HOTARU_REDEMPTION")]
public sealed class HotaruRedemption : HotaruEndgameCard
{
    public HotaruRedemption() : base(3) { }

    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) =>
        RetireTarget(context, play, sacrifice: false);
}
