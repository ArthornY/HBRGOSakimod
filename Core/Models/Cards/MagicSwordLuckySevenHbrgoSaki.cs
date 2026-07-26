using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MagicSwordLuckySevenHbrgoSaki : CardModel
{
    private const int _swordheartRollMin = 1;
    private const int _swordheartRollExclusiveMax = 4;
    private int _magicSwordModeHitCount = -1;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/magic_sword_lucky_seven_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move)
    };

    public MagicSwordLuckySevenHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Basic, TargetType.RandomEnemy)
    {
    }

    public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        if (isInitialShuffle || Owner != player)
        {
            return;
        }

        cards.RemoveAll(card => card is MagicSwordLuckySevenHbrgoSaki);
    }

    public void SetMagicSwordModeHitCount(int hitCount)
    {
        AssertMutable();
        _magicSwordModeHitCount = hitCount < 0 ? 0 : hitCount;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int hitCount = GetHitCount();
        for (int i = 0; i < hitCount && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this)
                .TargetingRandomOpponents(base.CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (IsUpgradedVersion && !CombatManager.Instance.IsOverOrEnding)
        {
            int swordheartAmount = Owner.RunState.Rng.Niche.NextInt(_swordheartRollMin, _swordheartRollExclusiveMax);
            await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner.Creature, swordheartAmount, Owner.Creature, this);
        }
    }

    private bool IsUpgradedVersion => IsUpgraded || DeckVersion?.IsUpgraded == true;

    protected override PileType GetResultPileTypeForCardPlay()
    {
        return PileType.None;
    }

    private int GetHitCount()
    {
        return Math.Max(0, _magicSwordModeHitCount);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
