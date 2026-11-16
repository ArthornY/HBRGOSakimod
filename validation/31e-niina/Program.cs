using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

static class NiinaStandalone { public static void Run(string path)=>EffectChecks.RunNiina(path); }
static partial class EffectChecks
{
    sealed record NiinaHit(decimal Raw,int Hits,decimal Makeup,decimal Amplification,decimal Destruction,decimal Next);
    static readonly List<NiinaHit> NiinaHits=[];
    static bool NiinaFault,NiinaChangeDp,NiinaNested;
    static bool NiinaProbeCannon;
    static CardModel? NiinaNestedCard;
    static AttackCommand NiinaCommand(decimal amount,CardModel card)=>(AttackCommand)Native(new AttackCommand(amount),"FromCard",card)!;
    static void NiinaReset()
    {
        NewReset();NiinaHits.Clear();NiinaFault=NiinaChangeDp=NiinaNested=false;NiinaNestedCard=null;
        var table=Helper("NiinaMakeupCombatHelper").GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);
    }
    static bool NiinaAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=NiinaResolve(__instance);return false;}
    static async Task<AttackCommand> NiinaResolve(AttackCommand command)
    {
        CardModel card=(CardModel)command.ModelSource!;await (Task)MisatoHook("BeforeAttack",command)!;
        decimal units=(decimal)Call("NiinaMakeupCombatHelper","GetBonusUnits",Hero,card,ValueProp.Move)!;
        int hits=(int)decimal.Floor((decimal)MisatoHook("ModifyAttackHitCount",command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!)!);
        NiinaHits.Add(new((decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!,hits,units,
            (decimal)Call("NiinaNarcissismCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,
            (decimal)Call("NiinaNarcissismCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,
            (decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,card,ValueProp.Move)!));
        if(NiinaFault)throw new InvalidOperationException("controlled Niina attack leaf fault");
        if(NiinaChangeDp)
        {
            NiinaChangeDp=false;Player.GetRelic<DeflectorProtection>()!.LoseDp(Player.GetRelic<DeflectorProtection>()!.Dp);
            Equal((decimal)Call("NiinaMakeupCombatHelper","GetBonusUnits",Hero,card,ValueProp.Move)!,units,"one actual attack keeps its initial DP snapshot after mid-attack DP loss");
        }
        if(NiinaNested&&NiinaNestedCard!=null)
        {
            NiinaNested=false;object play=Call("CardPlayContextHelper","Begin",NiinaNestedCard,false)!;
            try{await NiinaCommand(1m,NiinaNestedCard).Targeting(Enemy).Execute(new ThrowingPlayerChoiceContext());}
            finally{Call("CardPlayContextHelper","Close",play);}
        }
        var rows=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        foreach(var enemy in Combat.HittableEnemies)
        {
            List<DamageResult> results=[];
            for(int i=0;i<hits;i++)
            {
                var result=new DamageResult(enemy,ValueProp.Move);Set(result,"<UnblockedDamage>k__BackingField",2);results.Add(result);
                Call("CombatAttackHitHelper","Bind",new[]{result},Hero,card,null,0m,null);await NewGiven(card,result);
            }
            rows.Add(results);
        }
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static decimal Color(CardModel card)=>(decimal)Call("NiinaMakeupCombatHelper","GetBonusUnits",Hero,card,ValueProp.Move)!;
    static void NiinaCannonProbe(object[] __args)
    {
        if(!NiinaProbeCannon||__args[5] is not CardModel card)return;
        Equal(Color(card),0m,"real cannon retaining attack-card source receives no makeup coefficient");
        Equal((decimal)Call("NiinaNarcissismCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,0m,"Niina source amplification does not leak into real cannon");
        Equal((decimal)Call("NiinaNarcissismCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,1m,"Niina authored destruction does not leak into real cannon");
        Equal((decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,card,ValueProp.Move)!,1m,"next-attack gain never borrows onto real cannon");
    }
    static async Task NiinaCoreCases()
    {
        NiinaReset();var card=NewGarden<NarcissismIsFineHbrgoSaki>();await NewPlay(card);var hit=NiinaHits.Single();Equal(hit.Raw,1.62m,"Niina base per-hit27percent is raw1point62");Equal(hit.Hits,4,"Niina base uses4hits");Equal(hit.Destruction,4m/3m,"Niina authored destruction4normalizes against3");
        NiinaReset();var gauge=NewGauge();var dp=NewDp(0);card=NewGarden<NarcissismIsFineHbrgoSaki>(1,2,3);await NewPlay(card);hit=NiinaHits.Single();Equal(hit.Hits,8,"Innovation doubles base4through native combo hooks");Equal(hit.Destruction,7m,"PureLove destruction21normalizes to7");Equal(dp.Dp,4,"Sakura aggregates eight2HP hits before floor30percent recovery");Check(Hero.HasPower<NiinaNextDestructionPower>(),"PureLove grants next-attack destruction only after this attack");Equal(hit.Next,1m,"newly granted next-attack buff never affects the same card's already-finished attack");
        var follow=FormCard<StrikeIronclad>();await NiinaCommand(1m,follow).Targeting(Enemy).Execute(new ThrowingPlayerChoiceContext());Equal(NiinaHits.Last().Next,1.5m,"next actual attack gets fiftypercent gain increase");Check(!Hero.HasPower<NiinaNextDestructionPower>(),"next gain increase consumes once after the complete next attack");
        NiinaReset();card=NewGarden<NarcissismIsFineHbrgoSaki>(4);Power<DestructionRateCapIncreasePower>(Enemy,200);var rate=Power<DestructionRatePower>(Enemy,100);Equal((decimal)Call("NiinaNarcissismCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,0m,"199percent destruction does not satisfy200threshold");rate.SetAmount(101);Equal((decimal)Call("NiinaNarcissismCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,25m,"exact200percent destruction grants25normal amplification");
        NiinaReset();card=NewGarden<NarcissismIsFineHbrgoSaki>(6);Power<HbrDefenseUpPower>(Hero,1);Power<HbrAttackUpPower>(Hero,1);Power<StrengthPower>(Hero,4);Power<StrengthPower>(Hero,2);Equal((decimal)Call("NiinaNarcissismCombatHelper","GetDamageAmplificationPercent",card,Hero,Enemy,ValueProp.Move)!,30m,"three distinct native/mod buff types grant30points, duplicate type is counted once");
    }
    static async Task NiinaMakeupCases()
    {
        NiinaReset();var dp=NewDp();Set(dp,"_dp",dp.MaxDp);var source=NewGarden<NarcissismIsFineHbrgoSaki>(5);MisatoPermanent(source);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(Hero.HasPower<NiinaMakeupPower>(),"permanent Niina starts makeup even when not drawn");var attack=FormCard<StrikeIronclad>();Equal(Color(attack),1.8m,"fullDP grants30base percentage points to any attack card");var fubuki=NewGarden<MakeAChangeKillYourselfHbrgoSaki>();Equal(Color(fubuki),1.8m,"Makeup also boosts a different garden attack card");var skill=NewGarden<PaintTheWorldMyColorHbrgoSaki>();Equal(Color(skill),0m,"skill card never receives makeup raw coefficient");
        decimal power=(decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",7.8m,100m,150m,false)!;Equal(Actual(Enemy,Hero,6m,ValueProp.Move,attack),decimal.Floor(power),"real central skill pipeline adds30points before attribute multiplication");
        dp.LoseDp(dp.Dp/2);Equal(Color(attack),.9m,"halfDP grants15base percentage points");NiinaChangeDp=true;await NiinaCommand(6m,attack).Targeting(Enemy).Execute(new ThrowingPlayerChoiceContext());Equal(NiinaHits.Last().Makeup,.9m,"actual command snapshots current halfDP before its damage loop");Check(!Hero.HasPower<NiinaMakeupPower>(),"direct ordinary DP depletion immediately removes makeup marker");Equal(Color(attack),0m,"hand preview has zero bonus after DP depletion");dp.RestoreDp(dp.MaxDp);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(!Hero.HasPower<NiinaMakeupPower>(),"later recovery and repeated startup cannot restore makeup in the same combat");
        NiinaReset();dp=NewDp();Set(dp,"_dp",dp.MaxDp);source=NewGarden<NarcissismIsFineHbrgoSaki>(5);MisatoPermanent(source);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;fubuki=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(4);Set(dp,"_dp",1);await NewPlay(fubuki);Check(!Hero.HasPower<NiinaMakeupPower>(),"Fubuki direct resource cost to zero also removes makeup");Equal(NiinaHits.Last().Makeup,0m,"Fubuki cost settles before makeup attack snapshot");
        NiinaReset();dp=NewDp(0);source=NewGarden<NarcissismIsFineHbrgoSaki>(5);MisatoPermanent(source);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(!Hero.HasPower<NiinaMakeupPower>(),"zero initialDP does not enter makeup");
    }
    static async Task NiinaTokenCases()
    {
        NiinaReset();var first=FormCard<StrikeIronclad>();var other=FormCard<StrikeIronclad>();var buff=Power<NiinaNextDestructionPower>(Hero,1);Native(buff,"ConfigureEffect",50m,null);
        object outerPlay=Call("CardPlayContextHelper","Begin",first,false)!;
        try
        {
            var outer=NiinaCommand(1m,first);object outerScope=Call("HighHitDamageVisualHelper","BeginAttack",outer)!;await (Task)MisatoHook("BeforeAttack",outer)!;
            try
            {
                Equal((decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,first,ValueProp.Move)!,1.5m,"outer reserved native attack owns next buff");
                foreach(CardModel nested in new[]{other,first})
                {
                    object nestedPlay=Call("CardPlayContextHelper","Begin",nested,false)!;
                    var inner=NiinaCommand(1m,nested);object innerScope=Call("HighHitDamageVisualHelper","BeginAttack",inner)!;
                    try{await (Task)MisatoHook("BeforeAttack",inner)!;Equal((decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,nested,ValueProp.Move)!,1m,"nested new play cannot borrow outer reservation even when physical card is the same");}
                    finally{Call("HighHitDamageVisualHelper","CloseAttack",innerScope);Call("CardPlayContextHelper","Close",nestedPlay);}
                }
            }
            finally{Call("HbrComboHelper","CancelAttack",outer);Call("HighHitDamageVisualHelper","CloseAttack",outerScope);}
            using(var loop=(IDisposable)Call("HbrComboHelper","BeginCardAttack",first)!)
            {
                var child=NiinaCommand(1m,first);object childScope=Call("HighHitDamageVisualHelper","BeginAttack",child)!;
                try
                {
                    Equal((decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,first,ValueProp.Move)!,1.5m,"direct-loop native child maps to the logical dummy reservation");
                    object nestedPlay=Call("CardPlayContextHelper","Begin",first,false)!;var inner=NiinaCommand(1m,first);object innerScope=Call("HighHitDamageVisualHelper","BeginAttack",inner)!;
                    try{Equal((decimal)Call("NiinaNarcissismCombatHelper","GetNextDestructionMultiplier",Hero,first,ValueProp.Move)!,1m,"direct-loop outer cannot lend reservation to same instance new wrapper");}
                    finally{Call("HighHitDamageVisualHelper","CloseAttack",innerScope);Call("CardPlayContextHelper","Close",nestedPlay);}
                }
                finally{Call("HighHitDamageVisualHelper","CloseAttack",childScope);}
            }
            Check(Native(buff,"get_ReservedEffectAttackToken")==null,"direct loop dispose releases its actual dummy token");
            NiinaFault=true;bool failed=false;try{await NiinaCommand(1m,first).Targeting(Enemy).Execute(new ThrowingPlayerChoiceContext());}catch(InvalidOperationException){failed=true;}finally{NiinaFault=false;}
            Check(failed&&Native(buff,"get_ReservedEffectAttackToken")==null,"faulted native attack releases reservation and active token through real production finally path");
        }
        finally{Call("CardPlayContextHelper","Close",outerPlay);}
    }
    static async Task NiinaPhantomAndCannonCases(Harmony h)
    {
        NiinaReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());NewDp(0);
        var source=NewGarden<NarcissismIsFineHbrgoSaki>(5,6);MisatoPermanent(source);
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,phantom});Power<PhantomPower>(phantom,1);
        var shell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();Set(shell,"_owner",phantom);Set(shell,"_amount",1);Powers(phantom).Add(shell);decimal max=(decimal)Native(shell,"get_MaxAmount")!;Native(shell,"SetDpAmount",max,false);
        await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;Check(Hero.HasPower<NiinaMakeupPower>(),"30A startup recognizes positive main-phantomDP despite hero0DP");
        var attack=FormCard<StrikeIronclad>();Equal(Color(attack),1.8m,"30A source cards read main phantom fullDP");Equal((decimal)Call("NiinaMakeupCombatHelper","GetBonusUnits",phantom,attack,ValueProp.Move)!,0m,"phantom's own attack never gets makeup even while retaining player card source");
        Native(shell,"SetDpAmount",0m,false);Check(!Hero.HasPower<NiinaMakeupPower>(),"actual shell native write0immediately clears owner makeup");Native(shell,"SetDpAmount",max,false);Check(!Hero.HasPower<NiinaMakeupPower>(),"main phantom recovery does not restore the lost marker");
        NiinaReset();var dp=NewDp();Set(dp,"_dp",dp.MaxDp);source=NewGarden<NarcissismIsFineHbrgoSaki>(5,3,6);MisatoPermanent(source);await (Task)Call("NiinaMakeupCombatHelper","OnCombatStart",Combat)!;
        var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruCannonDamageContextPatch",true)!).Patch();var probe=typeof(EffectChecks).GetMethod(nameof(NiinaCannonProbe),F)!;h.Patch(Canonical,prefix:new HarmonyMethod(probe){priority=0});
        try{NiinaProbeCannon=true;await (Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,Enemy,6m,source,null,false,false)!;}
        finally{NiinaProbeCannon=false;h.Unpatch(Canonical,probe);}
    }
    public static void RunNiina(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".niina66");
            foreach(Type type in new[]{typeof(NarcissismIsFineHbrgoSaki),typeof(MakeAChangeKillYourselfHbrgoSaki),typeof(PaintTheWorldMyColorHbrgoSaki),typeof(StrikeIronclad),typeof(NiinaMakeupPower),typeof(NiinaNextDestructionPower),typeof(HbrComboLargePower),typeof(HbrAttackUpPower),typeof(HbrDefenseUpPower),typeof(HbrDefenseDownPower),typeof(DestructionRatePower),typeof(DestructionRateCapIncreasePower),typeof(DeflectorProtection),typeof(Overclock),typeof(StrengthPower),typeof(PhantomPower),typeof(PhantomHighDimensionalShellPower),typeof(SmallHopper)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"NiinaMakeupAttackPatch","NiinaMakeupDeflectorPatch","NiinaMakeupShellPatch","NiinaMakeupShellRemovedPatch","NiinaSakuraRecoveryPatch","HighHitDamageAttackScopePatch","HighDimensionalShellInitialAmountPatch","HighDimensionalShellNativeAmountPatch","HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch","HbrComboVirtualHitPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs "+name);
            foreach(Type listeners in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(listeners.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NiinaAttackLeaf),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            NiinaCoreCases().GetAwaiter().GetResult();NiinaMakeupCases().GetAwaiter().GetResult();NiinaTokenCases().GetAwaiter().GetResult();NiinaPhantomAndCannonCases(h).GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Native card upgrade, original framework attack/combo reserve/consume, shared raw coefficient pipeline, real native/current direct-loop attack identity, directDP/resource zero hooks and mutable power lifetime. Attack animation/terminal HP inputs are controlled, real AfterDamageGiven dispatch is used. No Godot/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" Niina checks");
    }
}


