using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MariaRepentanceCombatHelper
{
    private sealed class LifesaveBattle{internal bool Used;}
    private static readonly ConditionalWeakTable<ICombatState,LifesaveBattle> Lifesaves=new();
    private static readonly ConditionalWeakTable<ICombatState,Dictionary<Player,Dictionary<Creature,HbrAttackUpPower>>> Inspiration=new();
    private sealed class KnockScope:IDisposable
    {
        internal CardModel Source;internal AttackCommand Command;internal decimal Percent;internal KnockScope Previous;internal bool Active=true;
        public void Dispose(){Active=false;if(ReferenceEquals(Knock.Value,this))Knock.Value=Previous;}
    }
    private static readonly AsyncLocal<KnockScope> Knock=new();
    private static bool Active(MariaRepentancePower power)=>power?.IsMutable==true&&power.Amount>0&&
        power.Owner is{IsAlive:true,Player:not null,CombatState:not null}&&power.Owner.Powers.Contains(power)&&HbrBattleStatsHelper.IsEnabled(power.Owner);
    internal static void GainFaith(Creature ally,BigInteger amount)
    {
        if(ally?.IsAlive!=true||amount<=0)return;
        if(ally.Player!=null){HbrFaithResource.Gain(ally,amount);return;}
        if(!PhantomHelper.IsOwnedMainPhantom(ally,ally.PetOwner)||!HbrBattleStatsHelper.IsEnabled(ally.PetOwner))return;
        if(ally.GetPower<HbrFaithPower>() is{} existing)existing.SetExact(existing.ExactAmount+amount);
        else{var power=(HbrFaithPower)ModelDb.Power<HbrFaithPower>().ToMutable();power.ApplyExact(ally,amount);}
    }
    private static decimal CurrentDp(Creature target)=>target.Player is{} player
        ?player.GetRelic<DeflectorProtection>()?.Dp??0m:target.GetPower<HighDimensionalShellPower>()?.CurrentDp??0m;
    private static decimal MaximumDp(Creature target)=>target.Player is{} player
        ?player.GetRelic<DeflectorProtection>()?.MaxDp??0m:target.GetPower<HighDimensionalShellPower>()?.MaxAmount??0m;
    internal static async Task Play(PlayerChoiceContext context,RepentanceTimeHbrgoSaki card)
    {
        Player owner=card.Owner;if(!HbrBattleStatsHelper.IsEnabled(owner)||owner.Creature?.IsAlive!=true)return;
        var dp=FubukiChangeCombatHelper.Dp(owner);decimal cost=decimal.Floor(dp.Current*.3m);Creature pool=FubukiChangeCombatHelper.DpTarget(owner);
        if(cost>0m)
        {if(pool.Player!=null)owner.GetRelic<DeflectorProtection>()?.LoseDpDirect(cost);else if(pool.GetPower<HighDimensionalShellPower>() is{} shell)shell.SetDpAmount(shell.CurrentDp-cost);}
        GainFaith(owner.Creature,2);BigInteger knockFaith=HbrFaithResource.Get(owner.Creature);
        MariaRepentancePower power=owner.Creature.GetPower<MariaRepentancePower>();
        if(power==null){power=(MariaRepentancePower)ModelDb.Power<MariaRepentancePower>().ToMutable();power.Register(card);await PowerCmd.Apply(context,power,owner.Creature,1m,owner.Creature,null);}
        else power.Register(card);
        SynchronizeLifesaveHistory(owner.Creature.CombatState,power);
        if(card.HasUpgradeEffect(RepentanceTimeHbrgoSaki.Punishment))
        {
            Creature target=HbrPlayerTeamTargets.Get(owner.Creature).Select(ally=>ally.Player is{} player?FubukiChangeCombatHelper.DpTarget(player):ally)
                .Distinct().Where(ally=>ally?.IsAlive==true&&MaximumDp(ally)>0m).OrderBy(CurrentDp).FirstOrDefault();
            if(target!=null)await SeiraDpRecoveryHelper.Restore(context,target,.1m,false,card);
        }
        if(card.HasUpgradeEffect(RepentanceTimeHbrgoSaki.Knock))
        {
            Creature[] enemies=owner.Creature.CombatState.GetOpponentsOf(owner.Creature).Where(enemy=>enemy.IsHittable).ToArray();
            if(enemies.Length>0)
            {
                Creature target=enemies[owner.RunState.Rng.CombatTargets.NextInt(enemies.Length)];
                AttackCommand command=DamageCmd.Attack(HbrSkillPower.DamageUnits(105m)).FromCardCompat(card).Targeting(target).WithHitCount(2).WithHitFx("vfx/vfx_attack_slash");
                using var scope=new KnockScope{Source=card,Command=command,Percent=(decimal)BigInteger.Min((BigInteger)decimal.MaxValue/16,knockFaith)*16m,Previous=Knock.Value};Knock.Value=scope;
                await command.Execute(context);
            }
        }
        if(card.HasUpgradeEffect(RepentanceTimeHbrgoSaki.Requiem))
            foreach(Creature ally in HbrPlayerTeamTargets.Get(owner.Creature))
                foreach(var down in ally.Powers.OfType<HbrAttackDownPower>().ToArray())await PowerCmd.Remove(down);
        if(card.HasUpgradeEffect(RepentanceTimeHbrgoSaki.DinnerIntroduction))
            foreach(Creature ally in HbrPlayerTeamTargets.Get(owner.Creature))GainFaith(ally,1);
        if(Active(power)){power.ClaimTurn();await Inspire(context,power);}
    }
    private static async Task Inspire(PlayerChoiceContext context,MariaRepentancePower power)
    {
        int percent=(int)BigInteger.Min(10,HbrFaithResource.Get(power.Owner))*3;power.SetInspiration(percent);
        var owners=Inspiration.GetOrCreateValue(power.Owner.CombatState);
        if(!owners.TryGetValue(power.Owner.Player,out var applications))owners[power.Owner.Player]=applications=new();
        foreach(Creature ally in HbrPlayerTeamTargets.Get(power.Owner))
        {
            if(applications.Remove(ally,out var previous)&&ally.Powers.Contains(previous))await PowerCmd.Remove(previous);
            if(percent<=0)continue;
            var buff=(HbrAttackUpPower)ModelDb.Power<HbrAttackUpPower>().ToMutable();
            await HbrEffectApplication.Apply(context,buff,ally,percent,power.Owner,power.Source(),remaining:1,
                profile:new(HbrEffectLimit.Default,HbrEffectExit.PlayerTurnEnd,1),potencyOverride:percent);
            if(ally.Powers.Contains(buff))applications[ally]=buff;
        }
    }
    internal static async Task OnOwnTurnStart(PlayerChoiceContext context,Player owner)
    {if(owner?.Creature?.GetPower<MariaRepentancePower>() is{} power&&Active(power)&&power.ClaimTurn())await Inspire(context,power);}
    internal static void OnDpRestored(Creature receiver,decimal actualAmount,CardModel source,bool fromAbility=false)
    {
        Player owner=receiver?.Player??receiver?.PetOwner;
        if(actualAmount<=0m||source==null&&!fromAbility||owner?.Creature?.GetPower<MariaRepentancePower>() is not{} power||!Active(power)||
            !ReferenceEquals(receiver,FubukiChangeCombatHelper.DpTarget(owner))||!power.Has(RepentanceTimeHbrgoSaki.WarriorsBlessing)||!power.ClaimBlessing())return;
        GainFaith(owner.Creature,1);
    }
    internal static bool TryConsumeLifesave(Creature target)
    {
        if(target?.IsAlive!=true||target.CombatState is not{} combat)return false;
        LifesaveBattle battle=Lifesaves.GetOrCreateValue(combat);
        MariaRepentancePower[] all=combat.Players.SelectMany(player=>player.Creature.Powers.OfType<MariaRepentancePower>()).ToArray();
        if(battle.Used||all.Any(power=>power.LifesaveUsed)||HasSavedLifesave(combat)){battle.Used=true;return false;}
        foreach(Player owner in combat.Players.OrderBy(player=>player.NetId))
            if(owner.Creature.GetPower<MariaRepentancePower>() is{} power&&Active(power)&&power.Has(RepentanceTimeHbrgoSaki.DoNotDareDie)&&
                HbrPlayerTeamTargets.Get(owner.Creature).Contains(target))
            {
                battle.Used=true;foreach(var holder in all)holder.ClaimLifesave();
                foreach(Player player in combat.Players.Where(HbrBattleStatsHelper.IsEnabled))
                    player.GetRelic<ElectronicSoldierManual>().MarkMariaLifesaveUsed(combat);
                return true;
            }
        return false;
    }
    private static void SynchronizeLifesaveHistory(ICombatState combat,MariaRepentancePower power)
    {
        LifesaveBattle battle=Lifesaves.GetOrCreateValue(combat);
        if(!battle.Used&&(HasSavedLifesave(combat)||combat.Players.Any(player=>player.Creature.Powers.OfType<MariaRepentancePower>().Any(holder=>holder.LifesaveUsed))))battle.Used=true;
        // A dispelled/recreated ability must carry the already-spent party
        // receipt into the save, even if no lethal command occurs before saving.
        if(battle.Used)power.ClaimLifesave();
    }
    private static bool HasSavedLifesave(ICombatState combat)=>combat.Players.Any(player=>
        HbrBattleStatsHelper.IsEnabled(player)&&player.GetRelic<ElectronicSoldierManual>().HasUsedMariaLifesave(combat));
    internal static void EndBattle(ICombatState combat){if(combat!=null)Lifesaves.Remove(combat);}
    private static bool OwnKnock(CardModel source,Creature dealer,Creature target,ValueProp props)=>Knock.Value is{Active:true} scope&&
        ReferenceEquals(scope.Source,source)&&source.Owner?.Creature==dealer&&target?.Side!=dealer.Side&&props.IsPoweredAttack()&&
        !HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source)&&ReferenceEquals(scope.Command,HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command)&&HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command);
    internal static decimal GetDamageAmplificationPercent(CardModel source,Creature dealer,Creature target,ValueProp props)=>OwnKnock(source,dealer,target,props)?Knock.Value.Percent:0m;
    internal static decimal GetPreviewKnockBonusPercent(CardModel source)=>source is RepentanceTimeHbrgoSaki card&&card.HasUpgradeEffect(RepentanceTimeHbrgoSaki.Knock)
        ?(decimal)BigInteger.Min((BigInteger)decimal.MaxValue/16,HbrFaithResource.Get(card.Owner?.Creature)+2)*16m:0m;
    internal static decimal GetDestructionGainMultiplier(CardModel source,Creature target,Creature dealer,ValueProp props)=>OwnKnock(source,dealer,target,props)?1.625m/HbrDestructionGainHelper.DefaultSkillCoefficient:1m;
}
