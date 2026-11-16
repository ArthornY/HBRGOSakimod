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
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

static class DyeAttackStandalone { public static void Run(string path) => EffectChecks.RunDyeAttacks(path); }
static partial class EffectChecks
{
    sealed record NewAttack(decimal Raw,int Hits,decimal Conditional,decimal Destruction,bool AttackUp);
    static readonly List<NewAttack> NewAttacks=[];
    static CardModel? SelectedDye;
    static bool SimulateAkariBreak, ThrowDyeAttack;
    static bool ProbeNewCannon;
    static void NewCannonProbe(object[] __args)
    {
        if(!ProbeNewCannon||__args[5] is not CardModel source)return;
        Equal((decimal)Call("IrohaDyeCombatHelper","GetDamageAmplificationPercent",source,Hero,Enemy,ValueProp.Move)!,0m,"dyed source does not amplify triggered cannon damage");
        Equal((decimal)Call("IrohaDyeCombatHelper","GetCriticalRatePercent",source,Hero,Enemy,ValueProp.Move)!,0m,"dyed source critical grant does not leak into cannon");
        foreach(string helper in new[]{"FubukiChangeCombatHelper","AkariAceCombatHelper"})
        {
            Equal((decimal)Call(helper,"GetConditionalDamageMultiplier",source,Hero,Enemy,ValueProp.Move)!,1m,helper+" card-only damage excludes actual cannon context");
            Equal((decimal)Call(helper,"GetDestructionGainMultiplier",source,Enemy,Hero,ValueProp.Move)!,1m,helper+" card coefficient excludes actual cannon context");
        }
        Check(!(bool)Call("FubukiChangeCombatHelper","IsOwnAttack",source,Hero,Enemy,ValueProp.Move)!,"cannon cannot feed Fubuki lifesteal receipt even with retained source");
        Equal((decimal)Call("AinaDeepDiveCombatHelper","GetDestructionGainMultiplier",source,Enemy,Hero,ValueProp.Move)!,1m,"Aina card destruction coefficient also excludes real triggered cannon");
    }
    static bool DyeSelectLeaf(Func<CardModel,bool> filter,ref Task<IEnumerable<CardModel>> __result)
    {Check(SelectedDye!=null&&filter(SelectedDye),"native hand selector accepts only an attack");__result=Task.FromResult<IEnumerable<CardModel>>([SelectedDye!]);return false;}
    static T NewGarden<T>(params int[] effects) where T:ThirtyOneBGardenCard,new()
    {
        var card=FormCard<T>();
        foreach(int id in effects){Check((bool)Native(card,"PrepareUpgradeEffect",id)!,typeof(T).Name+" accepts upgrade "+id);card.UpgradeInternal();card.FinalizeUpgradeInternal();}
        return card;
    }
    static void NewReset()
    {
        Call("IrohaDyeCombatHelper","Clear",Combat);MisatoReset();NewAttacks.Clear();SelectedDye=null;SimulateAkariBreak=false;ThrowDyeAttack=false;
        foreach(string helper in new[]{"FubukiChangeCombatHelper","AkariAceCombatHelper"})
        {var table=Helper(helper).GetField("Battles",F)!.GetValue(null)!;table.GetType().GetMethod("Remove")!.Invoke(table,[Combat]);}
    }
    static Overclock NewGauge()
    {var relic=(Overclock)ModelDb.Relic<Overclock>().ToMutable();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),relic});return relic;}
    static DeflectorProtection NewDp(int current=2000)
    {var relic=Blank<DeflectorProtection>();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(relic,"_hasInitializedDp",true);Set(relic,"_dp",current);Set(Player,"_relics",Player.Relics.Concat(new[]{relic}).ToList());return relic;}
    static async Task NewPlay(CardModel card)
    {var play=Play(card);Set(play,"<Target>k__BackingField",Enemy);await (Task)Native(card,"OnPlay",new ThrowingPlayerChoiceContext(),play)!;}
    static async Task NewAfterPlay(CardModel card)
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterCardPlayed");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(Combat)?(object)Combat:
            typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():p.ParameterType==typeof(CardPlay)?Play(card):throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(method,null,args)!;
    }
    static async Task NewDrawn(CardModel card,bool turnDraw=false)
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterCardDrawn");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(Combat)?(object)Combat:
            typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():p.ParameterType==typeof(CardModel)?card:
            p.ParameterType==typeof(bool)?turnDraw:throw new Exception(p.ToString())).ToArray();await (Task)Invoke(method,null,args)!;
    }
    static async Task NewGiven(CardModel card,DamageResult result)
    {
        MethodInfo method=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterDamageGiven");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:
            p.ParameterType.IsInstanceOfType(Combat)?Combat:typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():
            p.ParameterType==typeof(DamageResult)?result:p.ParameterType==typeof(ValueProp)?ValueProp.Move:
            p.ParameterType==typeof(Creature)?p.Name=="dealer"?Hero:Enemy:p.ParameterType==typeof(CardModel)?card:throw new Exception(p.ToString())).ToArray();
        await (Task)Invoke(method,null,args)!;
    }
    static bool NewAttackLeaf(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=ResolveNewAttack(__instance);return false;}
    static async Task<AttackCommand> ResolveNewAttack(AttackCommand command)
    {
        CardModel card=(CardModel)command.ModelSource!;
        await (Task)MisatoHook("BeforeAttack",command)!;
        decimal raw=(decimal)typeof(AttackCommand).GetField("_damagePerHit",F)!.GetValue(command)!;
        int hits=(int)decimal.Floor((decimal)MisatoHook("ModifyAttackHitCount",command,(int)typeof(AttackCommand).GetField("_hitCount",F)!.GetValue(command)!)!);
        string helper=card is MakeAChangeKillYourselfHbrgoSaki?"FubukiChangeCombatHelper":card is DiveAsDeepAsIWantHbrgoSaki?"AinaDeepDiveCombatHelper":"AkariAceCombatHelper";
        NewAttacks.Add(new(raw,hits,card is DiveAsDeepAsIWantHbrgoSaki?1m:(decimal)Call(helper,"GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,
            (decimal)Call(helper,"GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,Hero.HasPower<HbrAttackUpPower>()));
        if(ThrowDyeAttack)throw new InvalidOperationException("controlled attack animation leaf");
        var results=(List<List<DamageResult>>)typeof(AttackCommand).GetField("_results",F)!.GetValue(command)!;
        foreach(var target in Combat.HittableEnemies)
        {
            results.Add(Enumerable.Range(0,hits).Select(_=>new DamageResult(target,ValueProp.Move)).ToList());
            if(SimulateAkariBreak)
                for(int i=0;i<2;i++)Call("AkariAceCombatHelper","OnEnemyBroken",Hero,target,card,ValueProp.Move,false);
        }
        await (Task)MisatoHook("AfterAttack",command)!;return command;
    }
    static async Task DyeCases()
    {
        NewReset();var gauge=NewGauge();var painter=NewGarden<PaintTheWorldMyColorHbrgoSaki>();var attack=FormCard<StrikeIronclad>();SelectedDye=attack;
        var events=new List<CardModel>();void Changed(CardModel c)=>events.Add(c);IrohaDyeState.Changed+=Changed;
        try
        {
            int rng=RngCount(RunState.Rng.Niche);Equal(IrohaDyeState.GetLayers(attack),0,"unpainted public read is zero");Equal(events.Count,0,"preview read emits no change event");
            await NewPlay(painter);Equal(IrohaDyeState.GetLayers(attack),1,"base card uses native selector and dyes one chosen hand attack");Equal(IrohaDyeState.GetDamageBonusPercent(attack),50m,"one dye layer records50percent");
            await NewPlay(painter);Equal(IrohaDyeState.GetDamageBonusPercent(attack),100m,"repeated base paint adds rather than multiplies");
            Check(events.Count==2&&events.All(c=>c==attack),"paint and stack each notify the affected card");Check(RngCount(RunState.Rng.Niche)==rng,"paint and public previews do not spend numerical RNG");
            var upgraded=NewGarden<PaintTheWorldMyColorHbrgoSaki>(1,2);var second=FormCard<StrikeIronclad>();await NewPlay(upgraded);
            Equal(IrohaDyeState.GetLayers(attack),3,"saturation dyes every existing hand attack once");Equal(IrohaDyeState.GetDamageBonusPercent(attack),180m,"mixed historical50+50+new80 snapshots remain additive");Equal(IrohaDyeState.GetDamageBonusPercent(second),80m,"spectacle records80on a fresh attack");
            var copy=(CardModel)attack.MutableClone();Call("CardCopyContextHelper","RecordCopy",copy);Set(copy,"_owner",Player);Player.PlayerCombatState!.Hand.AddInternal(copy);((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(copy);
            Equal(IrohaDyeState.GetDamageBonusPercent(copy),180m,"temporary native clone inherits current mixed layer snapshot");Call("IrohaDyeCombatHelper","Paint",copy,50m);Equal(IrohaDyeState.GetDamageBonusPercent(copy),230m,"copy accepts independent future layers");Equal(IrohaDyeState.GetDamageBonusPercent(attack),180m,"copy painting never rewrites source dye");
            var origin=FormCard<StrikeIronclad>();MisatoPermanent(origin);((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Remove(origin);var ordinary=(CardModel)origin.MutableClone();Set(ordinary,"_owner",Player);ordinary.DeckVersion=origin;Player.PlayerCombatState.Hand.AddInternal(ordinary);((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(ordinary);
            Call("IrohaDyeCombatHelper","Paint",ordinary,50m);var mirror=(CardModel)ordinary.MutableClone();Set(mirror,"_owner",Player);mirror.DeckVersion=origin;Player.PlayerCombatState.Hand.AddInternal(mirror);((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(mirror);
            Call("IrohaDyeCombatHelper","Paint",mirror,80m);Equal(IrohaDyeState.GetDamageBonusPercent(ordinary),130m,"OD mirror uses this combat's physical identity");Equal(IrohaDyeState.GetLayers(origin),0,"permanent deck never exposes a battle dye layer");
            Call("IrohaDyeCombatHelper","Clear",Combat);Equal(IrohaDyeState.GetLayers(attack),0,"combat clear erases original dye");Equal(IrohaDyeState.GetLayers(copy),0,"combat clear erases copied dye");Check(events.Count>7,"clear emits changes for known card instances");
        }
        finally{IrohaDyeState.Changed-=Changed;}
        NewReset();painter=NewGarden<PaintTheWorldMyColorHbrgoSaki>();await NewPlay(painter);await NewPlay(painter);
        attack=FormCard<StrikeIronclad>();Equal(IrohaDyeState.GetLayers(attack),0,"generating an attack directly into hand does not settle draw promises");
        await NewDrawn(attack,false);Equal(IrohaDyeState.GetLayers(attack),2,"one actual effect Draw fulfills every pending next-attack promise");await NewDrawn(attack,true);Equal(IrohaDyeState.GetLayers(attack),2,"settled promises cannot dye a later draw again");
        NewReset();gauge=NewGauge();painter=NewGarden<PaintTheWorldMyColorHbrgoSaki>(3,5);await NewPlay(painter);Equal(gauge.PreciseCharge,30m*gauge.ChargePerLevel/100m,"first colorless bullet grants30percent of one OD level");await NewPlay(painter);Equal(gauge.PreciseCharge,30m*gauge.ChargePerLevel/100m,"later plays of the same physical painter grant no extra30");
        attack=FormCard<StrikeIronclad>();await NewDrawn(attack);await NewAfterPlay(attack);Equal(gauge.PreciseCharge,35m*gauge.ChargePerLevel/100m,"impasto pays once per dyed card play even with two layers");
        await NewAfterPlay(attack);Equal(gauge.PreciseCharge,40m*gauge.ChargePerLevel/100m,"another real play receives one new5percent payment");
    }
    static async Task DyeDeathAndCriticalCases()
    {
        NewReset();var painter=NewGarden<PaintTheWorldMyColorHbrgoSaki>(2,4,6);var attack=FormCard<StrikeIronclad>();Call("IrohaDyeCombatHelper","Paint",attack,80m);
        var duplicate=NewGarden<PaintTheWorldMyColorHbrgoSaki>(6);var excluded=FormCard<StrikeIronclad>();Call("IrohaDyeCombatHelper","Paint",excluded,50m);excluded.Pile!.RemoveInternal(excluded,true);Player.PlayerCombatState!.ExhaustPile.AddInternal(excluded);
        var dead=MisatoEnemy();Set(dead,"_currentHp",0);int before=RngCount(RunState.Rng.CombatCardSelection);await MisatoDeath(dead);
        Equal(IrohaDyeState.GetLayers(attack),2,"one final death adds exactly one layer despite duplicate tombstone owners");Equal(IrohaDyeState.GetLayers(excluded),1,"exhausted dyed attack is not a tombstone candidate");Check(RngCount(RunState.Rng.CombatCardSelection)==before+1,"one owner draws one synchronized card-selection sample");
        await MisatoDeath(dead);Equal(IrohaDyeState.GetLayers(attack),2,"duplicate native death completion cannot paint twice");
        dead=MisatoEnemy();Power<MinionPower>(dead,1);Set(dead,"_currentHp",0);await MisatoDeath(dead);Equal(IrohaDyeState.GetLayers(attack),2,"minion death never paints");
        int count=RngCount(RunState.Rng.CombatCardSelection);attack.Pile!.RemoveInternal(attack,true);Player.PlayerCombatState.ExhaustPile.AddInternal(attack);dead=MisatoEnemy();Set(dead,"_currentHp",0);await MisatoDeath(dead);Check(RngCount(RunState.Rng.CombatCardSelection)==count,"no eligible tombstone candidate consumes no RNG");
        attack.Pile!.RemoveInternal(attack,true);Player.PlayerCombatState.Hand.AddInternal(attack);
        using(var play=(IDisposable)new MisatoExtraScope(()=>{}))
        {
            object scope=Call("CardPlayContextHelper","Begin",attack,false)!;
            try
            {
                Equal((decimal)Call("IrohaDyeCombatHelper","GetCriticalRatePercent",attack,Hero,Enemy,ValueProp.Move)!,50m,"one dyed play gets the standard INT50critical-rate basis once");
                Equal((decimal)Call("IrohaDyeCombatHelper","GetCriticalDamagePercent",attack,Hero,Enemy,ValueProp.Move)!,50m,"one dyed play gets standard50critical-damage once");
                var power=Power<HbrCriticalDamageUpPower>(Hero,1);Native(power,"ConfigureEffect",80m,null);Equal((decimal)Call("IrohaDyeCombatHelper","GetCriticalDamagePercent",attack,Hero,Enemy,ValueProp.Move)!,0m,"stronger existing critical buff is preserved under Only rule");
            }
            finally{Call("CardPlayContextHelper","Close",scope);}
        }
        Equal((decimal)Call("IrohaDyeCombatHelper","GetCriticalDamagePercent",attack,Hero,Enemy,ValueProp.Move)!,0m,"critical source grant disappears after this actual play scope closes");
    }
    static async Task NewPipelineAndCannonCases(Harmony h)
    {
        NewReset();var painter=NewGarden<PaintTheWorldMyColorHbrgoSaki>();var attack=FormCard<StrikeIronclad>();
        decimal basis=Actual(Enemy,Hero,6m,ValueProp.Move,attack);Call("IrohaDyeCombatHelper","Paint",attack,50m);
        decimal power=(decimal)Call("HbrSkillPower","EvaluateDamageFromUnits",6m,100m,150m,false)!;
        Equal(Actual(Enemy,Hero,6m,ValueProp.Move,attack),decimal.Floor(power*1.5m),"dye enters real shared damage pipeline");
        var up=Power<HbrAttackUpPower>(Hero,1);Native(up,"ConfigureEffect",50m,null);
        Equal(Actual(Enemy,Hero,6m,ValueProp.Move,attack),decimal.Floor(power*2m),"dye50and AttackUp50add in the same original attack bucket rather than multiply");
        var cannon=(Creature)H("Actor",Blank<HotaruContestCannon>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,cannon});
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruCannonDamageContextPatch",true)!).Patch();
        var method=typeof(EffectChecks).GetMethod(nameof(NewCannonProbe),F)!;h.Patch(Canonical,prefix:new HarmonyMethod(method){priority=0});
        try
        {
            ProbeNewCannon=true;
            foreach(CardModel source in new CardModel[]{attack,NewGarden<MakeAChangeKillYourselfHbrgoSaki>(1,5),NewGarden<AcePlayerIsNotMeHbrgoSaki>(4)})
            {
                object? own=source is MakeAChangeKillYourselfHbrgoSaki?Call("FubukiChangeCombatHelper","Begin",source):null;
                try{await (Task)Call("HotaruCannonStatsHelper","ReactionDamage",new ThrowingPlayerChoiceContext(),cannon,Enemy,6m,source,null,false,false)!;}
                finally{(own as IDisposable)?.Dispose();}
            }
        }
        finally{ProbeNewCannon=false;h.Unpatch(Canonical,method);}
    }
    static async Task FubukiCases()
    {
        NewReset();var card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>();await NewPlay(card);var hit=NewAttacks.Single();Equal(hit.Raw,2.4m,"Fubuki base raw coefficient is40percent per hit");Equal(hit.Hits,3,"Fubuki base attack has three hits");Equal(hit.Destruction,1m/3m,"Fubuki destruction1 uses shared coefficient normalization");Check(Enemy.HasPower<HbrDefenseDownPower>(),"native card grants defense-down only after attack");
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"amended Fubuki base energy is0");
        var down=Enemy.GetPower<HbrDefenseDownPower>()!;Equal((decimal)Native(down,"get_EffectPercent")!,19m,"Fubuki basis20uses real debuff attribute framework");Equal(down.Amount,1,"Fubuki defense down lasts through the next enemy end");
        NewReset();card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(1,2,3);await NewPlay(card);hit=NewAttacks.Single();Equal(hit.Raw,3.78m,"RIP upgrades raw per-hit coefficient to63percent");Equal(hit.Destruction,1.4m/3m,"RIP uses destruction1point4");Equal(Enemy.GetPower<HbrCalamityPower>()!.Amount,20,"GoodnightExtinction applies actual20calamity");Equal(Enemy.GetPower<HbrCalamityPower>()!.RemainingTurns,2,"calamity lasts two enemy ends");Check(!Enemy.HasPower<HbrStunPower>(),"RIP never adds the excluded stun");
        NewReset();var gauge=NewGauge();var dp=NewDp(2000);Check(dp.MaxDp>0,"payment fixture has an actual positive DP pool");Set(dp,"_dp",dp.MaxDp);card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(4,5);decimal max=dp.MaxDp;
        Call("FubukiChangeCombatHelper","PayCompensation",card);Equal(dp.Dp,decimal.Ceiling(max/2m),"compensation deducts floored half of currentDP");Equal(gauge.PreciseCharge,decimal.Floor(max/2m)/max*200m*gauge.ChargePerLevel/100m,"payment grants precisely lost max-DP share times2OD");
        Equal((decimal)Call("FubukiChangeCombatHelper","GiftMultiplier",card)!,1m+.75m*(1m-dp.Dp/max),"gift reads the already paid DP pool");Set(dp,"_dp",0);decimal charge=gauge.PreciseCharge;Call("FubukiChangeCombatHelper","PayCompensation",card);Equal(gauge.PreciseCharge,charge,"zeroDP grants no compensation OD");Equal((decimal)Call("FubukiChangeCombatHelper","GiftMultiplier",card)!,1.75m,"zeroDP grants75percent gift coefficient");
        await (Task)Call("FubukiChangeCombatHelper","RestoreDamage",new ThrowingPlayerChoiceContext(),card,1000m)!;Equal(dp.Dp,300,"gift restores30percent actual resource damage to real hero DP");
        Set(dp,"_dp",0);object receipt=Call("FubukiChangeCombatHelper","Begin",card)!;
        try
        {
            for(int i=0;i<3;i++)
            {
                var result=new DamageResult(Enemy,ValueProp.Move);Set(result,"<UnblockedDamage>k__BackingField",2);Set(result,"<BlockedDamage>k__BackingField",1000);Set(result,"<OverkillDamage>k__BackingField",9000);
                Call("CombatAttackHitHelper","Bind",new[]{result},Hero,card,null,0m,null);await NewGiven(card,result);await NewGiven(card,result);
            }
            Equal(dp.Dp,0,"gift does not heal or floor during individual hits");await (Task)Call("FubukiChangeCombatHelper","CompleteAttack",new ThrowingPlayerChoiceContext(),receipt)!;
            Equal(dp.Dp,1,"three actual2HP receipts aggregate to floor6times30percent=1, excluding ordinary block/overkill/duplicate dispatch");
        }
        finally{((IDisposable)receipt).Dispose();}
        NewReset();NewGauge();decimal regularMax=Hero.MaxHp,regularHp=Hero.CurrentHp;Hero.SetMaxHpInternal(7m);Hero.SetCurrentHpInternal(7m);dp=NewDp(0);card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(5);
        Equal(dp.MaxDp,14,"coprime restoration fixture uses an actual14maximum pool");await (Task)Call("FubukiChangeCombatHelper","RestoreDamage",new ThrowingPlayerChoiceContext(),card,6m)!;Equal(dp.Dp,1,"flat floor6times30percent restores1even when1over14roundtrip would floor0");Hero.SetMaxHpInternal(regularMax);Hero.SetCurrentHpInternal(regularHp);
        NewReset();gauge=NewGauge();dp=NewDp();Set(dp,"_dp",dp.MaxDp);card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(4,5);max=dp.MaxDp;await NewPlay(card);
        decimal baseCost=decimal.Floor(max*.1m),compensation=decimal.Floor((max-baseCost)/2m);Equal(dp.Dp,max-baseCost-compensation,"amended card pays10percent maximum first then half remainingDP");Equal(gauge.PreciseCharge,compensation/max*200m*gauge.ChargePerLevel/100m,"compensation OD excludes the mandatory base DP cost");Equal(NewAttacks.Single().Conditional,1m+.75m*(1m-dp.Dp/max),"actual attack sees both DP costs already paid");
        NewReset();gauge=NewGauge();dp=NewDp();Set(dp,"_dp",dp.MaxDp);card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(0);max=dp.MaxDp;await NewPlay(card);Equal(dp.Dp,max-decimal.Floor(max*.05m),"Flash changes DP cost to5percent rather than negative energy");Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"Flash leaves Fubuki at0energy");Set(dp,"_dp",1);await NewPlay(card);Equal(dp.Dp,0,"insufficient DP is clamped to zero without HP loss");
        NewReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());gauge=NewGauge();dp=NewDp(0);card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(4,5);
        var phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(Combat,"_allies",new List<Creature>{Hero,phantom});Power<PhantomPower>(phantom,1);
        var shell=Power<PhantomHighDimensionalShellPower>(phantom,1);decimal phantomMax=(decimal)Native(shell,"get_MaxAmount")!;Native(shell,"SetDpAmount",phantomMax,false);
        Call("FubukiChangeCombatHelper","PayCompensation",card);Equal(shell.CurrentDp,phantomMax-decimal.Floor(phantomMax/2m),"30A compensation spends the current owned main phantomDP instead of hero0DP");
        Check(gauge.PreciseCharge>0m,"30A payment from main phantom earns normal OD");decimal beforeRestore=shell.CurrentDp;await (Task)Call("FubukiChangeCombatHelper","RestoreDamage",new ThrowingPlayerChoiceContext(),card,100m)!;Equal(shell.CurrentDp,Math.Min(phantomMax,beforeRestore+30m),"30A gift restores the same main phantom resource pool with its real maximum cap");Equal(dp.Dp,0,"30A recovery never invents heroDP");
        NewReset();card=NewGarden<MakeAChangeKillYourselfHbrgoSaki>(6);var attack=FormCard<StrikeIronclad>();var skill=NewGarden<PaintTheWorldMyColorHbrgoSaki>();var power=FormCard<AllRounderHbrgoSaki>();
        await NewAfterPlay(attack);await NewAfterPlay(attack);await NewAfterPlay(skill);await NewAfterPlay(power);await NewPlay(card);Equal(NewAttacks.Single().Hits,6,"one-person band counts three distinct earlier card types once each");
        Set(Player.PlayerCombatState!,"<TurnNumber>k__BackingField",2);Equal((int)Call("FubukiChangeCombatHelper","GetPriorTypes",Player)!,0,"new native player turn resets prior-type ledger even within one combat round");
        await NewAfterPlay(attack);using(MisatoExtra(true)){Equal((int)Call("FubukiChangeCombatHelper","GetPriorTypes",Player)!,0,"OD has an isolated prior-type ledger");await NewAfterPlay(skill);}
        Equal((int)Call("FubukiChangeCombatHelper","GetPriorTypes",Player)!,1,"resuming the suspended normal turn restores its own prior-type ledger");
    }
    static async Task AkariCases()
    {
        NewReset();var gauge=NewGauge();var card=NewGarden<AcePlayerIsNotMeHbrgoSaki>();await NewPlay(card);var hit=NewAttacks.Single();Equal(hit.Raw,3.3m,"Akari base55percent per hit");Equal(hit.Hits,2,"Akari base two hits");Equal(hit.Destruction,1m/3m,"Akari destruction1 normalized once");Equal(gauge.PreciseCharge,10m*gauge.ChargePerLevel/100m,"Akari baseline grants one tenthOD level after play");
        NewReset();gauge=NewGauge();card=NewGarden<AcePlayerIsNotMeHbrgoSaki>(1,2,3,5,6);SimulateAkariBreak=true;MisatoEnemy();await NewPlay(card);hit=NewAttacks.Single();Equal(hit.Raw,1.5m,"five-ball changes each hit to25percent");Equal(hit.Hits,10,"pre-attack large combo doubles upgraded five hits to ten through native combo hook");Check(hit.AttackUp,"Paranoia attack-up is present before this attack");Check(Hero.HasPower<HbrDefenseDownPower>(),"PitchBlackServe applies self defense-down");var down=Hero.GetPower<HbrDefenseDownPower>()!;Check(Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("None"),"self100basis defense-down is outside two-slot cap");Equal(down.Amount,1,"self defense-down covers the next enemy turn");Equal(gauge.PreciseCharge,160m*gauge.ChargePerLevel/100m,"multiple new Breaks and duplicate notifications pay150only once plus baseline10");
        NewReset();card=NewGarden<AcePlayerIsNotMeHbrgoSaki>(4);await (Task)Call("AkariAceCombatHelper","OnCombatStart",Combat)!;Check(Hero.HasPower<HbrNegativityPower>(),"LateBloomer alone grants actual dispellable negativity at battle start");Equal(Hero.GetPower<HbrNegativityPower>()!.Amount,5,"negativity starts at five enemy turns");Check(!(bool)Call("AkariAceCombatHelper","IsLateBloomerActive",card)!,"negativity suppresses late-bloomer bonuses");
        await (Task)Call("AkariAceCombatHelper","OnCombatStart",Combat)!;Equal(Powers(Hero).OfType<HbrNegativityPower>().Count(),1,"OD mirrors and repeated start calls do not duplicate the marker");
        var marker=Hero.GetPower<HbrNegativityPower>()!;for(int i=0;i<4;i++)await marker.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[Enemy]);Equal(marker.Amount,1,"four real enemy-end ticks leave one turn");await marker.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,[Enemy]);Check(!Hero.HasPower<HbrNegativityPower>(),"fifth enemy-end removes the marker naturally");Equal((decimal)Call("AkariAceCombatHelper","GetConditionalDamageMultiplier",card,Hero,Enemy,ValueProp.Move)!,1.5m,"no negativity enables fiftypercent damage factor");Equal((decimal)Call("AkariAceCombatHelper","GetDestructionGainMultiplier",card,Enemy,Hero,ValueProp.Move)!,1m,"latebloomer triples authored destruction1 relative to default3");
        NewReset();card=NewGarden<AcePlayerIsNotMeHbrgoSaki>();await (Task)Call("AkariAceCombatHelper","OnCombatStart",Combat)!;Check(!Hero.HasPower<HbrNegativityPower>(),"unupgraded Akari never acquires negativity");
    }
    static async Task AinaCases()
    {
        NewReset();var card=NewGarden<DiveAsDeepAsIWantHbrgoSaki>();Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),10,"Aina starts with actual native10cost");var second=MisatoEnemy();Power<WeakPower>(Enemy,1);Power<WeakPower>(second,2);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),9,"same native debuff across two enemies reduces cost only once");Power<HbrCalamityPower>(Enemy,20);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),8,"another mod debuff type immediately reduces actual cost again");await PowerCmd.Remove(Enemy.GetPower<HbrCalamityPower>()!);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),9,"removing a debuff immediately restores native current cost");
        Power<StrengthPower>(Enemy,-1);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),8,"negative nativeStrength counts as current Debuff despite canonical Buff type");
        NewReset();card=NewGarden<DiveAsDeepAsIWantHbrgoSaki>(0,1);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),9,"Aina Flash changes printed10to9");await NewPlay(card);var hit=NewAttacks.Single();Equal(hit.Raw,13.92m,"WideOcean232percent per hit");Equal(hit.Hits,10,"deep dive issues ten native hits");Equal(hit.Destruction,1.6m/3m,"WideOcean destruction1point6 normalized once");
        NewReset();var gauge=NewGauge();card=NewGarden<DiveAsDeepAsIWantHbrgoSaki>(2,3,4,5,6);Power<BreakPower>(Enemy,1);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All),0,"rainbow miracle sets actual cost0with a broken living enemy");await NewPlay(card);hit=NewAttacks.Single();Equal(hit.Raw,9.12m,"base deep dive raw152percent");Equal(hit.Destruction,2m,"rainbow miracle multiplies coefficient1point2by5before normalization");Equal(gauge.PreciseCharge,50m*gauge.ChargePerLevel/100m,"SeaDuo gives50percentODbefore attack");Check(Enemy.HasPower<FragilePower>(),"AbyssSmash applies real fragile before attack");var down=Enemy.GetPower<HbrDefenseDownPower>()!;Equal((decimal)Native(down,"get_EffectPercent")!,71.25m,"broken enemy adds25basispoints to base50defense down through attributes");Check(Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("Eternal")&&Native(down,"get_CurrentEffectProfile")!.ToString()!.Contains("None"),"first Jellyfish grants eternal uncapped defense down");Check(Enemy.HasPower<HbrRecoveryDownPower>(),"first Jellyfish grants actual recovery-down ability");Equal((decimal)Native(Enemy.GetPower<HbrRecoveryDownPower>()!,"get_EffectPotency")!,100m,"recovery-down is fixed100percent ordinary recovery denial");
        Set(Enemy,"_currentHp",1);await CreatureCmd.Heal(Enemy,50m);Equal(Enemy.CurrentHp,1,"actual native Heal cannot bypass100percent recovery-down");Enemy.SetCurrentHpInternal(50m);Equal(Enemy.CurrentHp,50,"structural phase HP assignment remains available");
        var shell=Power<HighDimensionalShellPower>(Enemy,1);Native(shell,"SetDpAmount",1m,false);Equal(shell.Restore(10m),0m,"real shell Restore is blocked");Equal((decimal)Native(shell,"RestoreWithLimit",10m,true)!,0m,"real overcap RestoreWithLimit is also blocked");Native(shell,"SetDpAmount",2m,false);Equal(shell.CurrentDp,2m,"structural DP assignment remains available");
        await NewPlay(card);Check(Powers(Enemy).OfType<HbrRecoveryDownPower>().Count()==1,"Jellyfish once receipt does not apply another recovery-down on later plays");Check(Powers(Enemy).OfType<HbrDefenseDownPower>().Any(p=>Native(p,"get_CurrentEffectProfile")!.ToString()!.Contains("Default")),"subsequent defense down uses ordinary capped enemy-end instance alongside eternal one");
    }
    public static void RunDyeAttacks(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var h=new Harmony(ProductionId+".dye-60-62");
            foreach(Type type in new[]{typeof(PaintTheWorldMyColorHbrgoSaki),typeof(MakeAChangeKillYourselfHbrgoSaki),typeof(AcePlayerIsNotMeHbrgoSaki),typeof(DiveAsDeepAsIWantHbrgoSaki),typeof(TengenBrilliantMoveHbrgoSaki),typeof(StrikeIronclad),typeof(AllRounderHbrgoSaki),typeof(FragilePower),typeof(HbrNegativityPower),typeof(HbrRecoveryDownPower),typeof(HbrAttackUpPower),typeof(HbrDefenseDownPower),typeof(HbrCalamityPower),typeof(HbrCriticalRateUpPower),typeof(HbrCriticalDamageUpPower),typeof(HbrComboLargePower),typeof(MinionPower),typeof(SmallHopper),typeof(DeflectorProtection),typeof(Overclock),typeof(PhantomPower),typeof(PhantomHighDimensionalShellPower),typeof(HighDimensionalShellPower),typeof(StrengthPower)})
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(string name in new[]{"TengenBrilliantMoveDeathPatch","IrohaDyeClonePatch","IrohaDyeUpgradePatch","IrohaDyeDrawPatch","IrohaDyePlayedPatch","IrohaDyeEndPatch","IrohaDyeResetPatch","FubukiChangePlayedPatch","FubukiChangeDamagePatch","AinaRecoveryDownHealPatch","AinaRecoveryDownDpPatch","AinaRecoveryDownOvercapDpPatch","HbrEffectApplicationPatch","HbrFragileApplyPatch","HbrFragileStackingPatch","HbrComboVirtualHitPatch"})Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!).Patch().Count>0,"installs production "+name);
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch",true)!).Patch();
            foreach(Type listeners in new[]{RunState.GetType(),typeof(CombatState)})h.Patch(listeners.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            h.Patch(typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(NewAttackLeaf),F)!));
            h.Patch(typeof(CardSelectCmd).GetMethods(F).Single(m=>m.Name=="FromHand"),prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(DyeSelectLeaf),F)!));
            h.Patch(typeof(PowerModel).GetProperty("Icon",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf),F)!));
            foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>(m.Name is "Wait" or "CustomScaledWait")&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
            DyeCases().GetAwaiter().GetResult();DyeDeathAndCriticalCases().GetAwaiter().GetResult();NewPipelineAndCannonCases(h).GetAwaiter().GetResult();FubukiCases().GetAwaiter().GetResult();AkariCases().GetAwaiter().GetResult();AinaCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Native card upgrades, real hand-selector command boundary (controlled choice leaf), real Draw/AfterCardPlayed hook dispatch, public dye events/native MutableClone/death ledger, native combo/buff/debuff side-turn clocks, real DP resource pool and OD relic accounting. Attack animation/terminal damage results controlled; no Godot or package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" dye/attack checks");
    }
}









