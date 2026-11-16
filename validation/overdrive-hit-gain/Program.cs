using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class OdHitStandalone { public static void Run(string path) => EffectChecks.RunOdHits(path); }
static partial class EffectChecks
{
    static Overclock OdGauge()
    {
        MisatoReset(); var gauge=Owned<Overclock>();
        Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),gauge});
        typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData",F)!.SetValue(State<ElectronicSoldierManual>("Manual"),string.Empty);
        return gauge;
    }
    static DamageResult OdResult(Creature target,int hp=0,int block=0,int overkill=0) =>
        new(target,ValueProp.Move){UnblockedDamage=hp,BlockedDamage=block,OverkillDamage=overkill};
    static object OdBegin(Creature dealer,CardModel? card,ValueProp props)
    {
        object?[] args={new[]{Enemy},1m,props,dealer,card};
        return Invoke(Helper("HpOnlyDamageContext").GetMethod("Begin",F)!,null,args)!;
    }
    static void OdBind(DamageResult[] results)
    {
        var destination=new List<DamageResult>();
        Call("ItsMygoIncomingDamageCapture","AppendResults",destination,results);
        Check(destination.SequenceEqual(results),"canonical append retains every physical result");
    }
    static async Task OdDispatch(Overclock gauge,Creature dealer,CardModel? card,ValueProp props,params DamageResult[] results)
    { foreach(var result in results)await gauge.AfterDamageGiven(new ThrowingPlayerChoiceContext(),dealer,result,props,result.Receiver,card!); }
    static async Task OdHitCases()
    {
        var gauge=OdGauge(); var card=Owned<StrikeHbrgoSaki>();
        foreach(int magnitude in new[]{1,1000,1000000000})
        {
            var result=OdResult(Enemy,magnitude); await OdDispatch(gauge,Hero,card,ValueProp.Move,result);
        }
        Equal(gauge.PreciseCharge,7.5m,"one damage and one billion damage each earn exactly2.5");
        gauge=OdGauge(); card=Owned<StrikeHbrgoSaki>();
        for(int i=0;i<40;i++)await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,1));
        Equal(gauge.Level,1,"forty single-target segments reach exactly one OD level");
        Equal(gauge.PreciseCharge,100m,"forty hit receipts equal100 points without damage scaling");
        gauge=OdGauge(); card=Owned<StrikeHbrgoSaki>();
        Creature other=MisatoEnemy();
        for(int segment=0;segment<3;segment++)
            foreach(var target in new[]{Enemy,other})await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(target,1));
        Equal(gauge.PreciseCharge,15m,"three AoE segments against two targets earn six hits");
        var blocked=OdResult(Enemy,block:100);await OdDispatch(gauge,Hero,card,ValueProp.Move,blocked);
        Equal(gauge.PreciseCharge,17.5m,"ordinary block still counts as a connected attack");
        await OdDispatch(gauge,Hero,card,ValueProp.Move,blocked);
        Equal(gauge.PreciseCharge,17.5m,"duplicate dispatch of one result cannot award twice");
        await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy));
        await OdDispatch(gauge,Hero,card,ValueProp.Unpowered,OdResult(Enemy,500));
        await OdDispatch(gauge,Hero,card,ValueProp.Unpowered|ValueProp.Unblockable,OdResult(Enemy,500));
        await OdDispatch(gauge,Enemy,card,ValueProp.Move,OdResult(Hero,500));
        Equal(gauge.PreciseCharge,17.5m,"zero/nullified, continuous/thorns, HP supplements and enemy attacks earn nothing");

        gauge=OdGauge();card=Owned<StrikeHbrgoSaki>();object scope=OdBegin(Hero,card,ValueProp.Move);
        try
        {
            // Exercise the independent receipt consumers with both optional
            // protocols enabled; this does not re-enable obsolete effects.
            Set(scope,"CaptureMagic",true);Set(scope,"CaptureSavior",true);
            Call("HpOnlyDamageContext","RecordDpAbsorption",Enemy,70m);
            var result=OdResult(Enemy);OdBind([result]);
            await OdDispatch(gauge,Hero,card,ValueProp.Move,result);
            Equal(gauge.PreciseCharge,2.5m,"pure DP damage is a connected hit even when native HP loss is zero");
            Equal((decimal)Call("CombatAttackHitHelper","GetResourceDamage",result)!,70m,"actual DP loss remains readable for independent lifesteal");
            Equal((decimal)Call("CombatAttackHitHelper","GetResourceDamage",result)!,70m,"resource receipt reads never consume another effect's accounting");
            object?[] magic={result,0m},savior={result,0m};
            Check((bool)Call("HpOnlyDamageContext","TryTakeDpReceipt",magic)! && (decimal)magic[1]! == 70m,"Magic independently claims the exact DP receipt");
            Check((bool)Call("HpOnlyDamageContext","TryTakeSaviorDpReceipt",savior)! && (decimal)savior[1]! == 70m,"Savior independently claims its own exact DP receipt");
            Check(!(bool)Call("HpOnlyDamageContext","TryTakeDpReceipt",magic)!&&
                !(bool)Call("HpOnlyDamageContext","TryTakeSaviorDpReceipt",savior)!,"each optional consumer remains one-shot");
            Equal((decimal)Call("CombatAttackHitHelper","GetResourceDamage",result)!,70m,"both one-shot consumers leave lifesteal resource accounting unchanged");
            var first=OdResult(other,20);var redirected=OdResult(Enemy,10,5);
            Call("HpOnlyDamageContext","RecordDpAbsorption",Enemy,30m);OdBind([first,redirected]);
            await OdDispatch(gauge,Hero,card,ValueProp.Move,first,redirected);
            Equal(gauge.PreciseCharge,5m,"redirected pet and overflow results share one original target hit");
            Equal((decimal)Call("CombatAttackHitHelper","GetResourceDamage",first)!+
                (decimal)Call("CombatAttackHitHelper","GetResourceDamage",redirected)!,60m,"resource accounting adds actual HP and DP once and excludes block");
            var dodge=OdResult(Enemy,block:100);Call("HpOnlyDamageContext","RecordDodge",Enemy);OdBind([dodge]);
            await OdDispatch(gauge,Hero,card,ValueProp.Move,dodge);
            Equal(gauge.PreciseCharge,5m,"a native block-shaped dodge receipt earns no OD");
            var next=OdResult(Enemy,block:100);OdBind([next]);await OdDispatch(gauge,Hero,card,ValueProp.Move,next);
            Equal(gauge.PreciseCharge,7.5m,"dodge marker is cleared before the next target/hit");
            var capped=OdResult(Enemy,hp:3,overkill:999);OdBind([capped]);
            Equal((decimal)Call("CombatAttackHitHelper","GetResourceDamage",capped)!,3m,"overkill never inflates resource lifesteal");
        }
        finally{Call("HpOnlyDamageContext","Close",scope);}
        gauge=OdGauge();card=Owned<StrikeHbrgoSaki>();
        using(MisatoExtra(true))
        {
            await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,100));
            Equal(gauge.PreciseCharge,0m,"true OD suppresses passive hit charge");
            Native(gauge,"GainLevelProgress",30m);Equal(gauge.PreciseCharge,30m,"direct30percent skill award still works during OD");
        }
        using(MisatoExtra(false))await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,100));
        Equal(gauge.PreciseCharge,32.5m,"other personal extra turns keep passive hit charge");
        gauge=OdGauge();card=Owned<StrikeHbrgoSaki>();
        var maid=Power<MaidCultivationPower>(Hero,1);Set(maid,"_enhancedCards",new HashSet<CardModel>());
        await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,1));
        Equal(gauge.PreciseCharge,3.75m,"Maid's150percent OD gain scales a fixed hit to3.75");
        int[] equipped=new int[15];equipped[2]=1;
        string gear=JsonSerializer.Serialize(new{Schema=1,Revision=1,NextItemId=2,Seed=1UL,RandomCounter=0,
            Items=new[]{new{Id=1,Template="Acc.Earring006.F003",Rolls=new[]{2,0,0},Plus=Array.Empty<object>()}},
            Equipped=equipped,PlusRelease=0,PlusChange=0,Receipts=Array.Empty<string>()});
        var manual=State<ElectronicSoldierManual>("Manual");
        typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData",F)!.SetValue(manual,gear);
        await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,1));
        Equal(gauge.PreciseCharge,7.69m,"Maid and real Drive earring compose and round one hit to3.94");
        Set(maid,"_amount",0);Powers(Hero).Remove(maid);
        await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,1));
        Equal(gauge.PreciseCharge,10.32m,"Drive alone rounds2.625 to2.63 with hundredth precision");
        using((IDisposable)Call("HbrDestructionGainHelper","BeginSkill",Hero,card,10m,3m)!)
            await OdDispatch(gauge,Hero,card,ValueProp.Move,OdResult(Enemy,1));
        Equal(gauge.PreciseCharge,13.20m,"a real ten-hit skill uses the earring's15percent curve and rounds2.875 to2.88");
        gauge=OdGauge();card=Owned<StrikeHbrgoSaki>();
        var peer=SupportTeammate();var peerGauge=Owned<Overclock>();Set(peerGauge,"_owner",peer);
        Set(RunState,"_players",new List<MegaCrit.Sts2.Core.Entities.Players.Player>{Player,peer});
        Set(peer,"_relics",new List<RelicModel>{peerGauge});
        var shared=OdResult(Enemy,1);
        await OdDispatch(gauge,Hero,card,ValueProp.Move,shared);await OdDispatch(peerGauge,Hero,card,ValueProp.Move,shared);
        Equal(gauge.PreciseCharge,2.5m,"the source player's team gauge earns the hit once");
        Equal(peerGauge.PreciseCharge,2.5m,"another owner's team gauge independently earns the same hit once");
        Equal(peerGauge.ChargePerLevel,200m,"two-player gauge still requires100 times player count per level");
    }
    public static void RunOdHits(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();
            foreach(Type type in new[]{typeof(HBRGOSakiMod.Core.Models.Monsters.SmallHopper),typeof(MaidCultivationPower)})
            {
                if(!ModelDb.Contains(type))ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
            }
            OdHitCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Native Overclock damage listener and real canonical AppendResults/DP/dodge receipt binding, actual extra-turn membership and direct gauge API. Physical damage results and unavailable engine rendering/HP leaf are controlled; no Godot/game/network test or packaging."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" OD hit checks");
    }
}
