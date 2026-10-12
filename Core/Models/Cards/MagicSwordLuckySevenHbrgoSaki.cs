using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MagicSwordLuckySevenHbrgoSaki : CardModel
{
    private int _magicSwordModeHitCount = -1;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/magic_sword_lucky_seven_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move)
    };

    public MagicSwordLuckySevenHbrgoSaki()
        : base(0, CardType.Attack, CardRarity.Basic, TargetType.RandomEnemy)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "UpgradeEffect",
            IsUpgradedVersion
                ? new LocString(
                        "cards",
                        "MAGIC_SWORD_LUCKY_SEVEN_HBRGO_SAKI.upgradeEffect")
                    .GetFormattedText()
                : string.Empty);
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
        CardPlayCompatibility.SetResultPile(cardPlay, PileType.None);
        int hitCount = GetHitCount();
        for (int i = 0; i < hitCount && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCardCompat(this, cardPlay)
                .TargetingRandomOpponents(base.CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (IsUpgradedVersion && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    private bool IsUpgradedVersion => IsUpgraded || DeckVersion?.IsUpgraded == true;

    private int GetHitCount()
    {
        return Math.Max(0, _magicSwordModeHitCount);
    }

    protected override void OnUpgrade()
    {
    }
}
