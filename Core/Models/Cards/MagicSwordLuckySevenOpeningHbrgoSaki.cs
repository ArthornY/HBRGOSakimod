using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MagicSwordLuckySevenOpeningHbrgoSaki : CardModel
{
    private const string OverclockVar = "Overclock";

    // A permanent/library template has no resolved expenditure yet. It shows
    // the per-heart basis; an explicitly configured zero remains a real zero.
    private int _consumedSwordheartAmount = -1;
    internal bool HasExecutedForFinisher { get; private set; }

    internal void PrepareForFinisherAttempt()
    {
        AssertMutable();
        HasExecutedForFinisher = false;
    }

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/magic_sword_lucky_seven_opening_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(OverclockVar, 50m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<FragilePower>(),
            HoverTipFactory.FromCard<MagicSwordLuckySevenHbrgoSaki>()
        };

    public MagicSwordLuckySevenOpeningHbrgoSaki()
        : base(
            0,
            CardType.Attack,
            CardRarity.Ancient,
            TargetType.AnyEnemy)
    {
    }

    public override void ModifyShuffleOrder(
        Player player,
        List<CardModel> cards,
        bool isInitialShuffle)
    {
        if (Owner == player)
        {
            foreach (CardModel card in cards.OfType<MagicSwordLuckySevenOpeningHbrgoSaki>().ToArray())
                if (card.Pile?.IsCombatPile == true) card.RemoveFromState();
            cards.RemoveAll(card =>
                card is MagicSwordLuckySevenOpeningHbrgoSaki);
        }
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel clonedBy)
    {
        if (ReferenceEquals(card, this) &&
            card.Pile?.Type == PileType.Hand)
        {
            // Native skipVisuals also skips removing the hand's card holder.
            // Use full removal so a rejected entry cannot leave a ghost card.
            await CardPileCmd.RemoveFromCombat(card);
        }
    }

    internal void SetConsumedSwordheartAmount(int amount)
    {
        AssertMutable();
        _consumedSwordheartAmount = Math.Max(0, amount);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if (HbrSkillCardDescription.IsEnabled(this))
        {
            int previewHearts = _consumedSwordheartAmount < 0 ? 1 : _consumedSwordheartAmount;
            description.Add("ConsumedSwordheartFragilePercent",
                HbrFragileCardDescription.Format(this, previewHearts));
            description.Add("ConsumedSwordheartVulnerablePercent",
                HbrPercentDebuffCardDescription.Format(this, ModelDb.Power<VulnerablePower>(), previewHearts));
            HbrSkillCardDescription.AddDamageParameters(this, description, "OpeningDamage",
                HbrSkillPower.DamageUnits(previewHearts * 20m));
            HbrSkillCardDescription.AddDamageParameters(this, description, "PerSwordheartDamage",
                HbrSkillPower.DamageUnits(20m));
        }
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        HasExecutedForFinisher = true;
        CardPlayCompatibility.SetResultPile(cardPlay, PileType.None);
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));

        Owner.GetRelic<Overclock>()?
            .GainLevelProgress(DynamicVars[OverclockVar].BaseValue);

        decimal amount = Math.Max(0, _consumedSwordheartAmount);
        if (amount > 0m)
        {
            decimal damage = HbrBattleStatsHelper.IsEnabled(Owner)
                ? HbrSkillPower.DamageUnits(amount * 20m) : amount;
            await DamageCmd.Attack(damage)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (cardPlay.Target.IsAlive &&
                !CombatManager.Instance.IsOverOrEnding)
            {
                await HbrPercentDebuffApplication.ApplyDefenseDown(
                    choiceContext,
                    cardPlay.Target,
                    amount,
                    Owner.Creature,
                    this);
                await PowerCmd.Apply<FragilePower>(
                    choiceContext,
                    cardPlay.Target,
                    amount,
                    Owner.Creature,
                    this);
            }
        }

        await PlayMagicSwordLuckySeven(choiceContext);
    }

    private async Task PlayMagicSwordLuckySeven(
        PlayerChoiceContext choiceContext)
    {
        if (CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        MagicSwordLuckySevenHbrgoSaki luckySeven =
            CombatState.CreateCard<MagicSwordLuckySevenHbrgoSaki>(
                Owner);
        if (IsUpgraded || DeckVersion?.IsUpgraded == true)
        {
            luckySeven.UpgradeInternal();
            luckySeven.FinalizeUpgradeInternal();
        }

        luckySeven.SetMagicSwordModeHitCount(
            _consumedSwordheartAmount);
        await CardCmd.AutoPlay(
            choiceContext,
            luckySeven,
            null);
        await CombatFlowHelper.FinishAutoPlayedCard(luckySeven);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[OverclockVar].UpgradeValueBy(25m);
    }
}
