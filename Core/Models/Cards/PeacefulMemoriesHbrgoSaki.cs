using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Models.Cards;
public sealed class PeacefulMemoriesHbrgoSaki : ThirtyOneBGardenCard
{
    internal const int BattlefieldFlowers = 0, AngelsWings = 1, DanceInAzure = 2,
        EssenceOfProtection = 3, Flash = 4, Battlecry = 5, PeacefulMemory = 6;
    protected override int FlashEffectId => Flash;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    { HoverTipFactory.FromPower<AoiInvincibleShieldPower>(), HoverTipFactory.FromPower<RollBonusPower>(),
      HoverTipFactory.FromPower<AoiMiracleShieldPower>(), HoverTipFactory.FromPower<ProtectiveCharmPower>(),
      HoverTipFactory.FromPower<HbrAttackUpPower>(), HoverTipFactory.FromPower<HbrCriticalRateUpPower>() };
    public PeacefulMemoriesHbrgoSaki() : base(2, CardType.Power, TargetType.Self) { }
    protected override void AddExtraArgsToDescription(LocString text)
    { base.AddExtraArgsToDescription(text); text.Add("AoiChecks", HasUpgradeEffect(BattlefieldFlowers) ? 2 : 1); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await PowerCmd.Apply<AoiInvincibleShieldPower>(context, Owner.Creature, 1m, Owner.Creature, this);
        Owner.Creature.GetPower<AoiInvincibleShieldPower>()?.Configure(this);
        if (HasUpgradeEffect(AngelsWings))
            await PowerCmd.Apply<AoiMiracleShieldPower>(context, Owner.Creature, 2m, Owner.Creature, this);
        if (HasUpgradeEffect(DanceInAzure))
            await ThirtyOneBBondHelper.GrantCharm(context, Owner.Creature, 1, Owner.Creature, this);
        if (HasUpgradeEffect(Battlecry))
            foreach (var ally in CombatState.GetTeammatesOf(Owner.Creature).Where(ally => ally.IsAlive && ally.Player != null).ToArray())
                await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, ally, 100m, Owner.Creature, this,
                    profile: new HbrEffectProfile(HbrEffectLimit.None, HbrEffectExit.Eternal, 1), potencyOverride: 100m);
        if (HasUpgradeEffect(PeacefulMemory) && PileType.Discard.GetPile(Owner).Cards.Count > 0)
        {
            CardModel selected = (await CardSelectCmd.FromCombatPile(context, PileType.Discard.GetPile(Owner), Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
            if (selected != null) await CardPileCmd.Add(selected, PileType.Hand);
        }
    }
}
