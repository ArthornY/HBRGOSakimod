using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IsuzuOpenTheLockCombatHelper
{
    internal static bool IsEligible(OpenTheLockHbrgoSaki source,CardModel card)
    {
        if(card==null||ReferenceEquals(card,source)||card.Owner!=source.Owner||card.HasBeenRemovedFromState||
            card.Pile?.Type!=PileType.Hand||card.EnergyCost.CostsX)return false;
        // Cost is the reason to unlock a card; resource affordability alone
        // must not hide the expensive candidate. Native play restrictions stay.
        card.CanPlay(out UnplayableReason reason,out _);
        return (reason&~(UnplayableReason.EnergyCostTooHigh|UnplayableReason.StarCostTooHigh))==UnplayableReason.None;
    }
    internal static CardModel[] Highest(OpenTheLockHbrgoSaki card)
    {
        var hand=card.Owner?.PlayerCombatState?.Hand;
        CardModel[] eligible=hand?.Cards.Where(candidate=>IsEligible(card,candidate)).ToArray()??Array.Empty<CardModel>();
        if(eligible.Length==0)return eligible;
        int maximum=eligible.Max(candidate=>candidate.EnergyCost.GetWithModifiers(CostModifiers.All));
        return eligible.Where(candidate=>candidate.EnergyCost.GetWithModifiers(CostModifiers.All)==maximum).ToArray();
    }
    internal static async Task Unlock(PlayerChoiceContext context,OpenTheLockHbrgoSaki card)
    {
        if(card.Owner?.Creature is not {IsAlive:true,CombatState:not null} owner)return;
        var combat=owner.CombatState;
        CardModel[] candidates=Highest(card);if(candidates.Length==0)return;
        CardModel selected=candidates.Length==1?candidates[0]:(await CardSelectCmd.FromHand(context,card.Owner,
            new CardSelectorPrefs(new LocString("cards","OPEN_THE_LOCK_HBRGO_SAKI.selectionPrompt"),1),
            candidate=>candidates.Contains(candidate),card)).FirstOrDefault();
        if(selected==null||!ReferenceEquals(owner.CombatState,combat)||!owner.IsAlive||!IsEligible(card,selected))return;
        // Selection can await other effects. A stale choice cannot unlock a
        // different or newly more expensive physical hand card.
        if(!Highest(card).Contains(selected))return;
        selected.EnergyCost.SetThisTurn(0);selected.InvokeEnergyCostChanged();
    }
    internal static async Task ApplyAll<T>(PlayerChoiceContext context,OpenTheLockHbrgoSaki source,decimal basis) where T:PowerModel
    {
        using var batch=HbrDebuffApplicationScope.BeginBatch(source.Owner.Creature);
        foreach(Creature target in source.CombatState.GetOpponentsOf(source.Owner.Creature).Where(target=>target.IsAlive).ToArray())
            await HbrPercentDebuffApplication.ApplyBasis<T>(context,target,basis,source.Owner.Creature,source,
                new(HbrEffectLimit.Default,HbrEffectExit.EnemyTurnEnd,1));
    }
    internal static async Task Mischief(PlayerChoiceContext context,OpenTheLockHbrgoSaki source)
    {
        Creature[] enemies=source.CombatState.GetOpponentsOf(source.Owner.Creature).Where(enemy=>enemy.IsAlive).ToArray();
        if(enemies.Length==0)return;
        Creature target=enemies[source.Owner.RunState.Rng.CombatTargets.NextInt(enemies.Length)];
        HbrEffectProfile profile=new(HbrEffectLimit.Default,HbrEffectExit.EnemyTurnEnd,2);
        int effect=source.Owner.RunState.Rng.Niche.NextInt(3);
        if(effect==0)await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context,target,20m,source.Owner.Creature,source,profile);
        else if(effect==1)await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context,target,20m,source.Owner.Creature,source,profile);
        else await HbrPercentDebuffApplication.ApplyBasis<FragilePower>(context,target,42m,source.Owner.Creature,source,profile);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source,Creature target,Creature dealer,ValueProp props)
    {
        if(source is not OpenTheLockHbrgoSaki card||!source.IsMutable||source.Owner?.Creature!=dealer||
            !props.IsPoweredAttack()||target?.Side==dealer.Side||HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source))return 1m;
        return (card.HasUpgradeEffect(OpenTheLockHbrgoSaki.AngryRift)?1.6m:1.2m)/HbrDestructionGainHelper.DefaultSkillCoefficient;
    }
}
