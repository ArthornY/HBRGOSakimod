using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

static class CapAndDoomChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static readonly Type Fixture=typeof(Checks);
    static readonly Assembly Mod=typeof(FragilePower).Assembly;
    static AbstractModel? ExtraListener;
    static object? Invoke(string name,params object?[] args)=>Fixture.GetMethod(name,F)!.Invoke(null,args);
    static T State<T>(string name)=>(T)Fixture.GetField(name,F)!.GetValue(null)!;
    static void Reset()=>Invoke("Reset");
    static void Set(object owner,string name,object? value)=>Invoke("Set",owner,name,value);
    static T Blank<T>()=>(T)Fixture.GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)).Invoke(null,null)!;
    static T Power<T>(Creature owner,int amount=1)where T:PowerModel=>(T)Fixture.GetMethod("Power",F)!.MakeGenericMethod(typeof(T)).Invoke(null,[owner,amount])!;
    static Creature Actor(MonsterModel model,Player? owner=null)=>(Creature)Invoke("Actor",model,null,owner)!;
    static void Check(bool result,string name)=>Invoke("Check",result,"beta cap/Doom contract: "+name);
    static void Equal(decimal actual,decimal expected,string name)=>Invoke("Equal",actual,expected,"beta cap/Doom contract: "+name);
    static object? Helper(string name,string method,params object?[] args)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!.GetMethod(method,F)!.Invoke(null,args);
    static decimal Cap(AbstractModel model,Creature target,Creature dealer,CardModel? card=null)=>
        (decimal)Helper("DamageModifierCompatibility","ModifyDamageCap",model,target,ValueProp.Move,dealer,card,null)!;
    static decimal Damage(Creature target,Creature dealer,CardModel? card=null)=>(decimal)Invoke("Preview",target,dealer,6m,ValueProp.Move,card)!;
    static void AddExtraListener(ref IEnumerable<AbstractModel> __result)
    {if(ExtraListener!=null)__result=__result.Append(ExtraListener);}
    internal static void Run()
    {
        var harmony=new Harmony("hbr.beta.cap-and-doom.contracts");
        try
        {
            Reset();Creature hero=State<Creature>("Hero"),enemy=State<Creature>("Enemy");
            Player player=State<Player>("Player");CombatState combat=State<CombatState>("Combat");
            var model=Blank<HotaruCoffinCannon>();Creature cannon=Actor(model,player);
            Equal(Cap(model,cannon,enemy),0,"cannon compatibility bridge retains self-immunity cap");
            Equal(Cap(model,enemy,hero),decimal.MaxValue,"cannon compatibility bridge never caps another enemy");
            Equal(Cap(model,hero,enemy),decimal.MaxValue,"cannon compatibility bridge never caps its owner");
            decimal unrelatedDamage=Damage(enemy,hero);
            ExtraListener=model;
            harmony.Patch(typeof(RunState).GetMethod(nameof(RunState.IterateHookListeners))!,postfix:new HarmonyMethod(typeof(CapAndDoomChecks).GetMethod(nameof(AddExtraListener),F)!));
            Equal(Damage(cannon,enemy),0,"full native Hook damage route applies cannon zero cap in this runtime");
            Equal(Damage(enemy,hero),unrelatedDamage,"full native Hook leaves unrelated target damage unchanged");
            ExtraListener=null;

            var boss=Actor(Blank<NararaShibaBossMonster>());
            var enemyField=(FieldInfo)Fixture.GetMethod("Field",F)!.Invoke(null,[typeof(CombatState),"_enemies"])!;
            object? previousEnemies=enemyField.GetValue(combat);Set(combat,"_enemies",new List<Creature>{boss});
            try
            {
                // Nullification is switched off (2026-11-09): the compatibility bridge still
                // resolves, and caps nothing even inside a card's own play.
                var nullification=Power<NararaNullificationPower>(boss);
                var resistance=Power<SixSenseResistancePower>(boss);
                typeof(SixSenseResistancePower).GetProperty("Resistances",F)!.SetValue(resistance,new[]{10,10,10,10,10,10});
                var card=(FragileHbrgoSaki)Invoke("TestCard")!;
                var scope=Helper("NararaNullificationHelper","Begin",card)!;
                try
                {
                    Check(!(bool)Helper("NararaNullificationHelper","IsCardNullified",boss,card)!,"a scoped card is not captured by the switched-off nullification");
                    Equal(Cap(nullification,boss,hero,card),decimal.MaxValue,"Narara compatibility bridge applies no cap to the card's own play");
                    Check(Damage(boss,hero,card)>0,"full native Hook damages Narara from the card's own play");
                }
                finally{Helper("NararaNullificationHelper","Close",scope);}
                Equal(Cap(nullification,boss,hero,card),decimal.MaxValue,"closed scope keeps the ordinary damage cap");
            }
            finally{Set(combat,"_enemies",previousEnemies);Reset();}

            Type doomPatch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.LargeCreatureDoomThresholdPatch",true)!;
            Check(harmony.CreateClassProcessor(doomPatch).Patch().Count>0,"production Doom threshold patch resolves actual public/private runtime predicate");
            MethodInfo predicate=AccessTools.Method(typeof(DoomPower),"IsOwnerDoomed");
            Check(predicate!=null&&predicate.ReturnType==typeof(bool)&&predicate.GetParameters().Length==0,"runtime Doom predicate has expected native boolean shape");
#if HBR_BETA_NATIVE
            Check(predicate!.IsPrivate,"cached beta Doom predicate is private and production patch still installs");
#else
            Check(predicate!.IsPublic,"stable Doom predicate remains public");
#endif
            var health=Power<LargeHealthPower>(enemy);var doom=Power<DoomPower>(enemy,10000);
            Set(enemy,"_currentHp",1);
            typeof(LargeHealthPower).GetMethod("Store",F)!.Invoke(health,[5000000000m,6000000000m]);
            Check(enemy.CurrentHp==1&&health.CurrentHpData=="5000000000","native visible HP proxy is one while saved exact health is five billion");
            Check(!(bool)predicate!.Invoke(doom,null)!,"actual Doom predicate compares saved huge HP rather than the one-HP native proxy");
            Check(!DoomPower.GetDoomedCreatures(new[]{enemy}).Contains(enemy),"public native Doom creature filter invokes the corrected predicate");
            typeof(LargeHealthPower).GetMethod("Store",F)!.Invoke(health,[10001m,6000000000m]);
            Check(!(bool)predicate.Invoke(doom,null)!,"exact HP one above Doom threshold survives");
            typeof(LargeHealthPower).GetMethod("Store",F)!.Invoke(health,[10000m,6000000000m]);
            Check((bool)predicate.Invoke(doom,null)!&&DoomPower.GetDoomedCreatures(new[]{enemy}).Contains(enemy),"exact HP equal to Doom amount is correctly doomed");
            typeof(LargeHealthPower).GetMethod("Store",F)!.Invoke(health,[9999m,6000000000m]);
            Check((bool)predicate.Invoke(doom,null)!,"exact HP below Doom amount is correctly doomed");
            var powers=(List<PowerModel>)Invoke("Powers",enemy)!;powers.Remove(health);Set(enemy,"_currentHp",10001);
            Check(!(bool)predicate.Invoke(doom,null)!,"ordinary creature still runs native above-threshold Doom logic");
            Set(enemy,"_currentHp",10000);Check((bool)predicate.Invoke(doom,null)!,"ordinary creature still runs native equality Doom logic");
            Set(enemy,"_currentHp",5000);
        }
        finally{ExtraListener=null;harmony.UnpatchAll(harmony.Id);Reset();}
    }
}
