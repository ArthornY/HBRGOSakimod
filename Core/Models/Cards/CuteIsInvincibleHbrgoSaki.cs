using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class CuteIsInvincibleHbrgoSaki : ThirtyOneBGardenCard, IThirtyGMemberCard
{
    internal const int Flash = 0, LolitaFullBurst = 1, WhimsicalWeariness = 2, LolitaAlliance = 3,
        StillRising = 4, LolitaGod = 5, LolitaPride = 6;
    protected override int FlashEffectId => Flash;
    public ThirtyGMember ThirtyGMember => ThirtyGMember.SugawaraChie;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    { HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalDamageUpPower>(),
      HoverTipFactory.FromPower<HbrAttackDownPower>(), HoverTipFactory.FromPower<HbrComboMediumPower>(), HoverTipFactory.FromPower<HbrFaithPower>() };
    public CuteIsInvincibleHbrgoSaki() : base(1, CardType.Skill, TargetType.Self) { }
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy)
    {
        if (!ReferenceEquals(card, this) || Pile?.Type != PileType.Deck || Owner == null) return;
        await TeamCollectionRelics.EnsureObtained(Owner); ChieLitaState.OnPermanentDeckChanged(Owner);
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        ChieLitaState.OnPermanentDeckChanged(Owner);
        bool Eligible(CardModel card) => card != this && card.Owner == Owner && HbrMemoryGardenCardCatalog.IsGardenCard(card) && !ThirtyGProposalState.IsDisabled(card);
        var hand = Owner.PlayerCombatState.Hand.Cards.Where(Eligible).ToArray();
        CardModel[] targets = HasUpgradeEffect(LolitaAlliance) ? hand.Where(card => ChieLitaState.Get(card).Lita >= 50).ToArray() : System.Array.Empty<CardModel>();
        if (targets.Length == 0)
        {
            CardModel[] choices = hand.Length > 0 ? hand : Owner.PlayerCombatState.DrawPile.Cards.Where(Eligible).ToArray();
            if (choices.Length == 0) return;
            targets = (await CardSelectCmd.FromSimpleGrid(context, choices, Owner,
                new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionPrompt"), 1))).Where(card => choices.Contains(card) && Eligible(card) &&
                    card.Pile?.Type is PileType.Hand or PileType.Draw).Take(1).ToArray();
        }
        if (targets.Length == 0) return;
        foreach (CardModel target in targets) ChieLitaState.Dress(target, this);
        if (HasUpgradeEffect(LolitaFullBurst))
            foreach (Creature ally in HbrPlayerTeamTargets.Get(Owner.Creature))
            {
                var once = new HbrEffectProfile(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
                await HbrEffectApplication.Apply<HbrAttackUpPower>(context, ally, 65m, Owner.Creature, this, profile: once);
                await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, ally, 45m, Owner.Creature, this, profile: once);
            }
        if (HasUpgradeEffect(LolitaGod) && targets.Any(card => ChieLitaState.Get(card).Lita >= 80))
        {
            using var batch = HbrDebuffApplicationScope.BeginBatch(Owner.Creature);
            foreach (Creature enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, enemy, 20m, Owner.Creature, this,
                    new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 1));
        }
    }
}
