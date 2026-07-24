using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicDreamBubbleHbrgoSaki : CardModel
{
    private const int _hitCount = 4;

    private int _lastPrimaryDamageTotal;
    private int _originalPrimaryDamageTotal;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_dream_bubble_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(2m, ValueProp.Move),
        new RepeatVar(_hitCount)
    };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public MimicDreamBubbleHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this || CanUseDualSwordsCard();
    }

    internal int TakeLastPrimaryDamageTotal()
    {
        AssertMutable();
        int total = _lastPrimaryDamageTotal;
        _lastPrimaryDamageTotal = 0;
        return total;
    }

    internal void SetOriginalPrimaryDamageTotal(int total)
    {
        AssertMutable();
        _originalPrimaryDamageTotal = Math.Max(0, total);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        int primaryDamageTotal = 0;
        for (int i = 0; i < _hitCount && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            var attack = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            primaryDamageTotal += attack.Results
                .SelectMany(results => results)
                .Where(result => result.Receiver == cardPlay.Target)
                .Sum(result => result.TotalDamage);
        }

        if (!IsDupe)
        {
            _lastPrimaryDamageTotal = primaryDamageTotal;
            return;
        }

        int combinedDamageTotal = _originalPrimaryDamageTotal + primaryDamageTotal;
        _originalPrimaryDamageTotal = 0;
        if (combinedDamageTotal <= 0 || !cardPlay.Target.IsAlive || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await DamageCmd.Attack(combinedDamageTotal)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Unpowered()
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastPrimaryDamageTotal = 0;
        _originalPrimaryDamageTotal = 0;
    }

    private bool CanUseDualSwordsCard()
    {
        return Owner?.Creature?.HasPower<ReptureDualSwordsPower>() == true ||
               Owner?.Creature?.HasPower<MagicSwordModePower>() == true ||
               Owner?.Creature?.HasPower<CrimsonBladeFlowPower>() == true;
    }
}
