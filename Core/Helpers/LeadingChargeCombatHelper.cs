using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HBRGOSakiMod.Core.Helpers;
internal static class LeadingChargeCombatHelper
{
    internal readonly record struct TurnKey(int Number,object Extra,CombatSide Side);
    private sealed class Turn { internal int Attacks; internal decimal Percent; internal object ExcludedPlay; }
    private sealed class OwnerState { internal readonly Dictionary<TurnKey,Turn> Turns = new(); }
    private sealed class Battle { internal readonly ConditionalWeakTable<Player,OwnerState> Owners = new(); }
    internal sealed class AttackScope
    { internal AttackCommand Command; internal AttackScope Previous; internal bool Active=true; }
    private static readonly ConditionalWeakTable<ICombatState,Battle> Battles = new();
    private static readonly AsyncLocal<AttackScope> Current = new();
    internal static TurnKey Key(Player owner) => new(owner.PlayerCombatState?.TurnNumber ?? 0,
        OverclockImmediateTurnCoordinator.GetTurnToken(owner),owner.Creature.CombatState.CurrentSide);
    private static bool Active(Player owner) => owner?.Creature is {IsAlive:true,CombatState:not null} && HbrBattleStatsHelper.IsEnabled(owner);
    private static Turn State(Player owner,bool create)
    {
        if (!Active(owner)) return null;
        if (!Battles.TryGetValue(owner.Creature.CombatState,out Battle battle))
        { if (!create) return null; battle=Battles.GetOrCreateValue(owner.Creature.CombatState); }
        if (!battle.Owners.TryGetValue(owner,out OwnerState player))
        { if (!create) return null; player=battle.Owners.GetOrCreateValue(owner); }
        TurnKey key=Key(owner);
        if (!player.Turns.TryGetValue(key,out Turn value) && create) player.Turns.Add(key,value=new());
        return value;
    }
    internal static void BeforeCardPlayed(CardPlay play)
    {
        if (play?.Card is not {Type:CardType.Attack} card || play.PlayIndex!=0 || State(card.Owner,true) is not {} turn) return;
        bool first=turn.Attacks==0;if(turn.Attacks<int.MaxValue)turn.Attacks++;
        if (!first || card is not ILeadingChargeCard charge) return;
        turn.Percent=Math.Max(0m,charge.LeadingChargePercent);
        // One wrapper identity covers every replay, while an actual nested
        // autoplay, including the same physical card, owns its separate identity.
        turn.ExcludedPlay=CardPlayContextHelper.GetCurrentPlayToken(card) ?? play;
    }
    internal static void EndTurn(Player owner)
    { if(State(owner,false)is{} turn){turn.Percent=0m;turn.ExcludedPlay=null;} }
    internal static AttackScope BeginAttack(AttackCommand command)
    {
        if (command?.ModelSource is not CardModel {Type:CardType.Attack} card || State(card.Owner,false)?.Percent is not >0m) return null;
        var scope=new AttackScope {Command=command,Previous=Current.Value};Current.Value=scope;return scope;
    }
    internal static void RestoreCaller(AttackScope scope)
    {if(scope!=null&&ReferenceEquals(Current.Value,scope))Current.Value=scope.Previous;}
    internal static void Close(AttackScope scope){if(scope!=null)scope.Active=false;RestoreCaller(scope);}
    internal static async Task<AttackCommand> Finish(Task<AttackCommand> original,AttackScope scope)
    {try{return await original;}finally{Close(scope);}}
    internal static decimal GetDamageAmplificationPercent(CardModel source,Creature dealer,Creature target,ValueProp props)
    {
        if (source?.Type!=CardType.Attack || dealer?.Player is not {} owner || !ReferenceEquals(source.Owner,owner) ||
            target==null || target.Side==dealer.Side || !props.IsPoweredAttack() || State(owner,false) is not {Percent:>0m} turn ||
            HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source)) return 0m;
        object token=CardPlayContextHelper.GetCurrentPlayToken(source);
        if(token!=null&&ReferenceEquals(token,turn.ExcludedPlay))return 0m;
        if(HotaruCannonStatsHelper.IsDamagePreview)return turn.Percent;
        if(StrongestShieldDamagePatch.IsResolvingActualDamage)
        {
            for(AttackScope scope=Current.Value;scope!=null;scope=scope.Previous)
                if(scope.Active&&ReferenceEquals(scope.Command.ModelSource,source))
                    return HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command)?turn.Percent:0m;
            // Authored raw damage from a currently played attack also belongs
            // to that card; unrelated effects with a retained source do not.
            return token!=null?turn.Percent:0m;
        }
        return turn.Percent;
    }
}
