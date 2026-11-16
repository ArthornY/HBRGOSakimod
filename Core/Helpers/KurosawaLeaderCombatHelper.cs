using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HBRGOSakiMod.Core.Helpers;
internal static class KurosawaLeaderCombatHelper
{
    private sealed class CardState{internal bool DogUsed,HasDiscard;internal LeadingChargeCombatHelper.TurnKey DiscardTurn;}
    private sealed class OwnerState
    {
        internal readonly HashSet<LeadingChargeCombatHelper.TurnKey> RetainTurns=new();
        internal readonly HashSet<LeadingChargeCombatHelper.TurnKey> Flushed=new();
        internal readonly Dictionary<object,HashSet<CardModel>> Carry=new(ReferenceEqualityComparer.Instance);
    }
    private sealed class Battle
    {internal readonly ConditionalWeakTable<CardModel,CardState> Cards=new();internal readonly ConditionalWeakTable<Player,OwnerState> Owners=new();}
    private sealed class Receipt:IDisposable
    {internal FollowBehindMeHbrgoSaki Card;internal AttackCommand Command;internal Receipt Previous;internal bool Active=true;
     public void Dispose(){Active=false;if(ReferenceEquals(Current.Value,this))Current.Value=Previous;}}
    private static readonly ConditionalWeakTable<ICombatState,Battle> Battles=new();
    private static readonly AsyncLocal<Receipt> Current=new();
    private static bool Active(Player player)=>player?.Creature is{IsAlive:true,CombatState:not null}&&HbrBattleStatsHelper.IsEnabled(player);
    private static CardModel Physical(CardModel card)=>!card.IsDupe&&!CardCopyContextHelper.IsMarkedCopy(card)&&card.DeckVersion!=null?card.DeckVersion:card;
    private static Battle State(Player owner)=>Battles.GetOrCreateValue(owner.Creature.CombatState);
    private static OwnerState OwnerStateOf(Player owner)=>State(owner).Owners.GetOrCreateValue(owner);
    internal static void RecordPile(FollowBehindMeHbrgoSaki card)
    {
        if(!Active(card?.Owner)||card.HasBeenRemovedFromState||card.Pile?.Type!=PileType.Discard)return;
        CardState state=State(card.Owner).Cards.GetOrCreateValue(Physical(card));state.HasDiscard=true;state.DiscardTurn=LeadingChargeCombatHelper.Key(card.Owner);
    }
    private static void MarkCarry(Player owner,CardModel card)
    {
        object token=OverclockImmediateTurnCoordinator.GetTurnToken(owner);if(token==null)return;
        var carry=OwnerStateOf(owner).Carry;if(!carry.TryGetValue(token,out var cards))carry.Add(token,cards=new(ReferenceEqualityComparer.Instance));cards.Add(card);
    }
    internal static CardModel[] ConsumeCarry(Player owner)
    {
        if(!Active(owner)||!Battles.TryGetValue(owner.Creature.CombatState,out var battle)||!battle.Owners.TryGetValue(owner,out var state))return Array.Empty<CardModel>();
        object token=OverclockImmediateTurnCoordinator.GetTurnToken(owner);
        if(token==null||!state.Carry.Remove(token,out var cards))return Array.Empty<CardModel>();
        return cards.Where(c=>ReferenceEquals(c.Owner,owner)&&!c.HasBeenRemovedFromState&&c.Pile==owner.PlayerCombatState.Hand).ToArray();
    }
    internal static bool KeepsForExtraFlush(Player owner,CardModel card)
    {
        if(!Active(owner)||!Battles.TryGetValue(owner.Creature.CombatState,out var battle)||!battle.Owners.TryGetValue(owner,out var state))return false;
        object token=OverclockImmediateTurnCoordinator.GetTurnToken(owner);
        return token!=null&&state.Carry.TryGetValue(token,out var cards)&&cards.Contains(card)&&card.ShouldRetainThisTurn;
    }
    internal static void DiscardCarry(Player owner)
    {
        if(owner?.Creature.CombatState is{} combat&&Battles.TryGetValue(combat,out var battle)&&battle.Owners.TryGetValue(owner,out var state)&&
            OverclockImmediateTurnCoordinator.GetTurnToken(owner)is{} token)state.Carry.Remove(token);
    }
    internal static void BeforeFlush(Player owner)
    {
        if(!Active(owner)||!Battles.TryGetValue(owner.Creature.CombatState,out var battle)||!battle.Owners.TryGetValue(owner,out var state)||
            !state.RetainTurns.Contains(LeadingChargeCombatHelper.Key(owner)))return;
        foreach(CardModel card in owner.PlayerCombatState.Hand.Cards)
            if(!card.HasBeenRemovedFromState&&ReferenceEquals(card.Owner,owner)&&HbrMemoryGardenCardCatalog.IsGardenCard(card))
            {card.GiveSingleTurnRetain();MarkCarry(owner,card);}
    }
    internal static async Task AfterFlush(PlayerChoiceContext context,Player owner)
    {
        if(!Active(owner))return;LeadingChargeCombatHelper.EndTurn(owner);
        OwnerState state=OwnerStateOf(owner);var key=LeadingChargeCombatHelper.Key(owner);state.RetainTurns.Remove(key);
        if(!state.Flushed.Add(key))return;
        foreach(FollowBehindMeHbrgoSaki card in owner.PlayerCombatState.DiscardPile.Cards.OfType<FollowBehindMeHbrgoSaki>().ToArray())
        {
            if(card.HasBeenRemovedFromState||!ReferenceEquals(card.Owner,owner)||!card.HasUpgradeEffect(FollowBehindMeHbrgoSaki.RescuedDog)||
                !State(owner).Cards.TryGetValue(Physical(card),out var record)||record.DogUsed||!record.HasDiscard||record.DiscardTurn!=key)continue;
            record.DogUsed=true;
            await CardPileCmd.Add(card,PileType.Hand);
            if(card.Pile==owner.PlayerCombatState.Hand)MarkCarry(owner,card);
        }
    }
    private static bool Applies(CardModel source,Creature target,Creature dealer,ValueProp props)
    {
        if(source is not FollowBehindMeHbrgoSaki||source.Owner?.Creature!=dealer||!Active(source.Owner)||target==null||target.Side==dealer.Side||
            !props.IsPoweredAttack()||HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source))return false;
        if(HotaruCannonStatsHelper.IsDamagePreview)return true;
        if(Current.Value is{Active:true} receipt&&ReferenceEquals(receipt.Card,source))return HighHitDamageVisualHelper.IsDirectAttackDamage(receipt.Command);
        return !StrongestShieldDamagePatch.IsResolvingActualDamage;
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source,Creature target,Creature dealer,ValueProp props)=>
        Applies(source,target,dealer,props)?((FollowBehindMeHbrgoSaki)source).DestructionCoefficient/HbrDestructionGainHelper.DefaultSkillCoefficient:1m;
    internal static async Task Play(PlayerChoiceContext context,FollowBehindMeHbrgoSaki card,CardPlay play)
    {
        if(!Active(card.Owner))return;Creature owner=card.Owner.Creature;
        if(card.HasUpgradeEffect(FollowBehindMeHbrgoSaki.BikersNiceToMeetYou))
        {
            var profile=new HbrEffectProfile(HbrEffectLimit.Default,HbrEffectExit.EnemyTurnEnd,2);
            using var batch=HbrDebuffApplicationScope.BeginBatch(owner);
            foreach(Creature enemy in owner.CombatState.GetOpponentsOf(owner).Where(e=>e.IsHittable).ToArray())
            {await HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context,enemy,30m,owner,card,profile);await HbrPercentDebuffApplication.ApplyBasis<HbrDpDefenseDownPower>(context,enemy,60m,owner,card,profile);}
        }
        using(var receipt=new Receipt{Card=card,Previous=Current.Value})
        {
            Current.Value=receipt;receipt.Command=DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCardCompat(card,play).TargetingAllOpponents(owner.CombatState).WithHitCount(1).WithHitFx("vfx/vfx_attack_slash");
            await receipt.Command.Execute(context);
        }
        if(card.HasUpgradeEffect(FollowBehindMeHbrgoSaki.NeverAbandonCompanions))OwnerStateOf(card.Owner).RetainTurns.Add(LeadingChargeCombatHelper.Key(card.Owner));
        if(card.HasUpgradeEffect(FollowBehindMeHbrgoSaki.LibraryCommittee))await CardPileCmd.Draw(context,1m,card.Owner);
    }
}
