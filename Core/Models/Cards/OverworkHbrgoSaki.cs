using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class OverworkHbrgoSaki : CardModel
{
    private Dictionary<CardPlay, ICombatState> _pendingPlays = new();

    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/overwork_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public OverworkHbrgoSaki() : base(3, CardType.Status, CardRarity.Status, TargetType.None) { }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        // Power previews show an unowned status card, so its energy icon must
        // not depend on the local player's character being available.
        HbrgoSakiEnergyIconHelper.Add31OPrefix(description);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (OverworkPlayScope.WasHeldForPlay(this, cardPlay))
            _pendingPlays[cardPlay] = Owner.Creature.CombatState;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_pendingPlays.Remove(cardPlay, out ICombatState combat) ||
            Owner?.Creature.IsAlive != true || Owner.PlayerCombatState == null ||
            combat == null || !ReferenceEquals(Owner.Creature.CombatState, combat))
            return;

        CardModel played = cardPlay.Card;
        if (HbrMemoryGardenCardCatalog.IsGardenCard(played))
            HbrMemoryGardenCombatDowngrade.TryDowngrade(played);
        else if (played.IsUpgraded && played.Pile?.Type != PileType.Deck)
            CardCmd.Downgrade(played);

        // These consequences apply even when the played card cannot lose an
        // upgrade or this player has already lost every point of maximum DP.
        DeflectorProtection protection = Owner.GetRelic<DeflectorProtection>();
        bool losesPlayerDp = Owner.Character is not HbrgoSaki30A &&
            protection?.MaxDp > 0;
        if (Owner.Character is HbrgoSaki30A)
        {
            if ((Owner.GetRelic<MomentOfReign>()?.LosePhantomMaxDp(HbrResourceValueHelper.Fixed(Owner.Creature, 3m)) ?? 0) == 0)
                await CreatureCmd.LoseMaxHp(choiceContext, Owner.Creature, HbrResourceValueHelper.Fixed(Owner.Creature, 3m), true);
        }
        else if (losesPlayerDp)
            protection.LoseMaxDp(HbrResourceValueHelper.FixedInt(Owner.Creature, 1m));
        else
            await CreatureCmd.LoseMaxHp(choiceContext, Owner.Creature, HbrResourceValueHelper.Fixed(Owner.Creature, 1m), true);
        // A currently free card stays free without storing negative debt for a
        // later turn. This also respects an external effect making it free.
        if (EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
        {
            EnergyCost.AddThisCombat(-1);
            InvokeEnergyCostChanged();
        }
        if (losesPlayerDp)
            await protection.ResolveMentalStrengthDpLossRewards(choiceContext, this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingPlays = new();
    }
}
