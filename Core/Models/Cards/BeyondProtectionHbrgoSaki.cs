using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class BeyondProtectionHbrgoSaki : CardModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasAutoPlayedThisCombat { get; private set; }

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");
    public override string BetaPortraitPath => PortraitPath;
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new CardsVar(0) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<BeyondProtectionPower>() };

    public BeyondProtectionHbrgoSaki() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override Task BeforeCombatStart()
    {
        HasAutoPlayedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || HasAutoPlayedThisCombat || player.PlayerCombatState?.TurnNumber != 1 ||
            Pile == null || Pile.Type is not (PileType.Draw or PileType.Hand or PileType.Discard)) return;
        // Mark before awaiting: card moves and copy effects can re-enter hooks.
        HasAutoPlayedThisCombat = true;
        await CardCmd.AutoPlay(choiceContext, this, null);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.GetRelic<DeflectorProtection>() is { } protection)
        {
            int removedDp = await protection.ConvertDpToTemporaryMaxHp();
            if (removedDp > 0)
            {
                BeyondProtectionPower power = Owner.Creature.GetPower<BeyondProtectionPower>() ??
                    await PowerCmd.Apply<BeyondProtectionPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
                power?.AddConvertedDp(removedDp);
                await protection.ResolveMentalStrengthDpLossRewards(choiceContext, this);
            }
        }

        if (DynamicVars.Cards.BaseValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(2m);
}
