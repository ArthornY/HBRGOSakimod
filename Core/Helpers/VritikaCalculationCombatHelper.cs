using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class VritikaCalculationCombatHelper
{
    internal readonly record struct DisplayState(string Sequence,int Progress,string Status);
    private readonly record struct Turn(int Number,object Token);
    private sealed class Sequence
    {
        internal int Bits,Progress,Flags;internal bool Listening=true,Kabaddi;
        internal NumbersAreTheSourceHbrgoSaki Source;
        internal bool Has(int flag)=>(Flags&(1<<flag))!=0;
        internal CardType At(int step)=>(Bits&(1<<step))==0?CardType.Attack:CardType.Skill;
    }
    private sealed record NormalSnapshot(int Number,int Bits,int Progress,int Flags,bool Listening,bool Kabaddi);
    private sealed class OwnerState
    {internal bool Loaded;internal BigInteger Serial;internal readonly Dictionary<Turn,Sequence> Turns=new();}
    private sealed class Battle{internal readonly Dictionary<Player,OwnerState> Owners=new();}
    private sealed class Mark
    {
        internal Player Owner;internal CardModel Card;internal object Token;internal Turn Turn;
        internal Sequence Sequence;internal decimal Percent;internal bool Active=true,Success,Finished;
    }
    private sealed class AttackScope:IDisposable
    {
        internal CardModel Source;internal AttackCommand Command;internal decimal Destruction;
        internal AttackScope Previous;internal bool Active=true;
        public void Dispose(){Active=false;if(ReferenceEquals(Attacks.Value,this))Attacks.Value=Previous;}
    }
    private static readonly ConditionalWeakTable<ICombatState,Battle> Battles=new();
    private static readonly ConditionalWeakTable<object,Mark> Marks=new();
    private static readonly ConditionalWeakTable<CardPlay,Mark> Plays=new();
    private static readonly ConditionalWeakTable<object,object> Started=new();
    private static readonly AsyncLocal<AttackScope> Attacks=new();
    internal static event Action<Player,int,bool> Notice;
    private static Turn CurrentTurn(Player owner)=>new(owner.PlayerCombatState?.TurnNumber??owner.Creature.CombatState.RoundNumber,OverclockImmediateTurnCoordinator.GetTurnToken(owner));
    private static bool Active(VritikaCalculationPower power)=>power?.IsMutable==true&&power.Amount>0&&power.Owner is{IsAlive:true,Player:not null,CombatState:not null}&&
        power.Owner.Powers.Contains(power)&&HbrBattleStatsHelper.IsEnabled(power.Owner);
    private static OwnerState State(Player owner,VritikaCalculationPower power)
    {
        Battle battle=Battles.GetOrCreateValue(owner.Creature.CombatState);
        if(!battle.Owners.TryGetValue(owner,out OwnerState state))battle.Owners[owner]=state=new();
        state.Serial=BigInteger.Max(state.Serial,power.Serial);
        if(!state.Loaded)
        {
            state.Loaded=true;
            if(!string.IsNullOrEmpty(power.NormalSequences))
                foreach(NormalSnapshot row in JsonSerializer.Deserialize<NormalSnapshot[]>(power.NormalSequences)??Array.Empty<NormalSnapshot>())
                    state.Turns[new(row.Number,null)]=new(){Bits=row.Bits,Progress=row.Progress,Flags=row.Flags,Listening=row.Listening,Kabaddi=row.Kabaddi,Source=power.Source()};
        }
        return state;
    }
    private static void Save(OwnerState state,VritikaCalculationPower power)=>power.Save(JsonSerializer.Serialize(state.Turns.Where(pair=>pair.Key.Token==null)
        .Select(pair=>new NormalSnapshot(pair.Key.Number,pair.Value.Bits,pair.Value.Progress,pair.Value.Flags,pair.Value.Listening,pair.Value.Kabaddi)).ToArray()),state.Serial);
    private static void Notify(Player owner,int step,bool success)
    {
        Notice?.Invoke(owner,step,success);
        if(TestMode.IsOn)return;
        var text=new LocString("powers","VRITIKA_CALCULATION_POWER."+(success?"successNotice":"failureNotice"));text.Add("Step",step);
        ThinkCmd.Play(text,owner.Creature,2.0);
    }
    internal static async Task Play(PlayerChoiceContext context,NumbersAreTheSourceHbrgoSaki card)
    {
        Player owner=card.Owner;if(!HbrBattleStatsHelper.IsEnabled(owner)||owner.Creature?.IsAlive!=true)return;
        VritikaCalculationPower power=owner.Creature.GetPower<VritikaCalculationPower>();
        if(power==null){power=(VritikaCalculationPower)ModelDb.Power<VritikaCalculationPower>().ToMutable();power.Source(card);await PowerCmd.Apply(context,power,owner.Creature,1m,owner.Creature,null);}
        else power.Source(card);
        OwnerState state=State(owner,power);object playToken=CardPlayContextHelper.GetCurrentPlayToken(card);
        if(playToken==null||!Started.TryGetValue(playToken,out _))
        {
            if(playToken!=null)Started.Add(playToken,new object());
            state.Serial++;
            var rng=RngCompatibility.CreateNamed(owner.RunState.Rng,"HBR31X.Calculation/"+owner.NetId+"/"+owner.RunState.TotalFloor+"/"+
                owner.Creature.CombatState.Encounter?.Id.Entry+"/"+CurrentTurn(owner).Number+"/"+state.Serial);
            int bits=0;for(int step=0;step<3;step++)if(rng.NextInt(2)!=0)bits|=1<<step;
            int flags=card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel).Aggregate(0,(mask,effect)=>mask|1<<effect);
            state.Turns[CurrentTurn(owner)]=new(){Bits=bits,Flags=flags,Kabaddi=card.HasUpgradeEffect(NumbersAreTheSourceHbrgoSaki.Kabaddi),Source=card};Save(state,power);
        }
        var duration=new HbrEffectProfile(HbrEffectLimit.Default,HbrEffectExit.EnemyTurnEnd,card.HasUpgradeEffect(NumbersAreTheSourceHbrgoSaki.DancingKali)?3:1);
        using(HbrDebuffApplicationScope.BeginBatch(owner.Creature))
            foreach(Creature enemy in owner.Creature.CombatState.GetOpponentsOf(owner.Creature).Where(enemy=>enemy.IsAlive).ToArray())
                await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context,enemy,20m,owner.Creature,card,duration);
        if(card.HasUpgradeEffect(NumbersAreTheSourceHbrgoSaki.DancingKali))
            using(HbrDebuffApplicationScope.BeginBatch(owner.Creature))
                foreach(Creature enemy in owner.Creature.CombatState.GetOpponentsOf(owner.Creature).Where(enemy=>enemy.IsAlive).ToArray())
                    await PowerCmd.Apply<HbrCalamityPower>(context,enemy,3m,owner.Creature,card);
        if(card.HasUpgradeEffect(NumbersAreTheSourceHbrgoSaki.RhythmOfPrinciples))
            await HbrEffectApplication.Apply<HbrDefenseUpPower>(context,owner.Creature,30m,owner.Creature,card,
                remaining:1,profile:new(HbrEffectLimit.Default,HbrEffectExit.EnemyTurnEnd,1));
        if(card.HasUpgradeEffect(NumbersAreTheSourceHbrgoSaki.CompleteSubtraction))await Attack(context,card,null,51m,3,1.2m);
    }
    internal static void BeforePlay(VritikaCalculationPower power,CardPlay play)
    {
        if(!Active(power)||play?.Card is not{Owner:not null} card||card.Owner!=power.Owner.Player||card is NumbersAreTheSourceHbrgoSaki)return;
        Player owner=card.Owner;object token=CardPlayContextHelper.GetCurrentPlayToken(card);if(token==null)return;
        if(Marks.TryGetValue(token,out Mark prior)&&prior.Active&&ReferenceEquals(prior.Card,card))
        {Plays.Remove(play);Plays.Add(play,prior);return;}
        OwnerState state=State(owner,power);Turn turn=CurrentTurn(owner);
        if(!state.Turns.TryGetValue(turn,out Sequence sequence))return;
        Marks.Remove(token);
        var mark=new Mark{Owner=owner,Card=card,Token=token,Turn=turn,Sequence=sequence};
        if(sequence.Listening)
        {
            if(sequence.At(sequence.Progress)==card.Type)
            {
                sequence.Progress++;mark.Percent=sequence.Has(NumbersAreTheSourceHbrgoSaki.Multiplication)?sequence.Progress*10m:10m;
                if(sequence.Progress==3){sequence.Listening=false;mark.Success=true;}
            }
            else{sequence.Listening=false;sequence.Kabaddi=false;Notify(owner,sequence.Progress+1,false);}
        }
        Marks.Add(token,mark);Plays.Remove(play);Plays.Add(play,mark);Save(state,power);
    }
    internal static async Task AfterPlay(PlayerChoiceContext context,VritikaCalculationPower power,CardPlay play)
    {
        if(!Active(power)||play==null||!Plays.TryGetValue(play,out Mark mark)||mark.Finished)return;
        mark.Finished=true;OwnerState state=State(mark.Owner,power);
        if(!state.Turns.TryGetValue(mark.Turn,out Sequence current)||!ReferenceEquals(current,mark.Sequence))return;
        if(mark.Success)
        {
            Notify(mark.Owner,3,true);await PlayerCmd.GainEnergy(1m,mark.Owner);await CardPileCmd.Draw(context,1m,mark.Owner);
            if(current.Has(NumbersAreTheSourceHbrgoSaki.UltimateCurry))
                foreach(Creature ally in HbrPlayerTeamTargets.Get(mark.Owner.Creature))await SeiraDpRecoveryHelper.Restore(context,ally,.05m,false,current.Source);
        }
        if(current.Kabaddi&&play.Card.Type==CardType.Attack&&mark.Owner.Creature.IsAlive&&!CombatManager.Instance.IsOverOrEnding)
        {
            Creature[] enemies=mark.Owner.Creature.CombatState.GetOpponentsOf(mark.Owner.Creature).Where(enemy=>enemy.IsHittable).ToArray();
            if(enemies.Length>0)await Attack(context,current.Source,enemies[mark.Owner.RunState.Rng.CombatTargets.NextInt(enemies.Length)],20m,1,HbrDestructionGainHelper.DefaultSkillCoefficient);
        }
    }
    private static async Task Attack(PlayerChoiceContext context,CardModel source,Creature target,decimal percent,int hits,decimal destruction)
    {
        AttackCommand command=DamageCmd.Attack(HbrSkillPower.DamageUnits(percent)).FromCardCompat(source).WithHitCount(hits).WithHitFx("vfx/vfx_attack_slash");
        if(target==null)command.TargetingAllOpponents(source.Owner.Creature.CombatState);else command.Targeting(target);
        using var scope=new AttackScope{Source=source,Command=command,Destruction=destruction,Previous=Attacks.Value};Attacks.Value=scope;
        await command.Execute(context);
    }
    internal static decimal GetCalculationBonusPercent(CardModel source,Creature dealer,ValueProp props)
    {
        if(source?.IsMutable!=true||source.Owner?.Creature!=dealer||!HbrBattleStatsHelper.IsEnabled(source.Owner)||HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source))return 0m;
        object token=CardPlayContextHelper.CurrentPlayToken;
        return token!=null&&Marks.TryGetValue(token,out Mark mark)&&mark.Active&&ReferenceEquals(mark.Card,source)&&mark.Turn==CurrentTurn(mark.Owner)?mark.Percent:0m;
    }
    internal static object CapturePlayToken(CardModel card)=>card is NumbersAreTheSourceHbrgoSaki||card?.Owner?.Creature?.CombatState is{} combat&&Battles.TryGetValue(combat,out Battle battle)&&
        battle.Owners.TryGetValue(card.Owner,out OwnerState state)&&state.Turns.ContainsKey(CurrentTurn(card.Owner))
        ?CardPlayContextHelper.GetCurrentPlayToken(card):null;
    internal static void ClosePlay(object token)
    {if(token==null)return;Started.Remove(token);if(Marks.TryGetValue(token,out Mark mark)){mark.Active=false;Marks.Remove(token);}}
    internal static void OnTurnEnd(Player owner,object token=null)
    {
        if(owner?.Creature?.CombatState is not{} combat||!Battles.TryGetValue(combat,out Battle battle)||!battle.Owners.TryGetValue(owner,out OwnerState state))return;
        foreach(Turn turn in state.Turns.Keys.Where(turn=>ReferenceEquals(turn.Token,token)&&(token!=null||turn.Number==CurrentTurn(owner).Number)).ToArray())state.Turns.Remove(turn);
        if(owner.Creature.GetPower<VritikaCalculationPower>() is not{} power)return;
        Save(state,power);if(state.Turns.Count==0)power.RemoveInternal();
    }
    internal static Task OnOwnTurnStart(PlayerChoiceContext context,Player owner)
    {if(owner?.Creature?.GetPower<VritikaCalculationPower>() is{} power&&Active(power))power.RefreshDisplay();return Task.CompletedTask;}
    internal static decimal GetPreviewCalculationBonusPercent(CardModel source)
    {
        if(source?.IsMutable!=true||source is NumbersAreTheSourceHbrgoSaki||source.Owner?.Creature?.GetPower<VritikaCalculationPower>() is not{} power||!Active(power))return 0m;
        if(!State(source.Owner,power).Turns.TryGetValue(CurrentTurn(source.Owner),out Sequence sequence)||!sequence.Listening||sequence.At(sequence.Progress)!=source.Type)return 0m;
        return sequence.Has(NumbersAreTheSourceHbrgoSaki.Multiplication)?(sequence.Progress+1)*10m:10m;
    }
    internal static DisplayState Display(Player owner)
    {
        if(owner?.Creature?.GetPower<VritikaCalculationPower>() is not{} power||!Active(power))return default;
        if(!State(owner,power).Turns.TryGetValue(CurrentTurn(owner),out Sequence state))return default;
        string TypeName(CardType type)=>new LocString("powers","VRITIKA_CALCULATION_POWER."+(type==CardType.Attack?"attack":"skill")).GetFormattedText();
        string status=new LocString("powers","VRITIKA_CALCULATION_POWER."+(state.Listening?"waiting":state.Progress==3?"complete":"interrupted")).GetFormattedText();
        return new(string.Join(" → ",Enumerable.Range(0,3).Select(step=>TypeName(state.At(step)))),state.Progress,status);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source,Creature target,Creature dealer,ValueProp props)
    {
        AttackScope scope=Attacks.Value;
        return scope?.Active==true&&ReferenceEquals(scope.Source,source)&&source.Owner?.Creature==dealer&&target?.Side!=dealer.Side&&props.IsPoweredAttack()&&
            !HotaruCannonStatsHelper.IsCannonAttackSource(dealer,source)&&ReferenceEquals(scope.Command,HighHitDamageVisualHelper.GetCurrentAttackIdentity().Command)&&HighHitDamageVisualHelper.IsDirectAttackDamage(scope.Command)
            ?scope.Destruction/HbrDestructionGainHelper.DefaultSkillCoefficient:1m;
    }
}
