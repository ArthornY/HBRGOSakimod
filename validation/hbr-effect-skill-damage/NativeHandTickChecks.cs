using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;

// A narrow entry into the existing actual native-effect fixture. It invokes
// native OnTurnEndInHand; it does not manufacture an environmental Begin scope.
static partial class EffectChecks
{
    static readonly (Type Type,decimal Raw)[] NativeHandTicks=[(typeof(Burn),2m),(typeof(Decay),2m),(typeof(Infection),3m),(typeof(Toxic),5m),(typeof(Wither),3m)];
    static CardModel HandTickCard(Type type)
    {
        var card=(CardModel)Activator.CreateInstance(type)!;Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);Set(card,"<Id>k__BackingField",ModelDb.GetId(type));
        Player.PlayerCombatState!.Hand.AddInternal(card);return card;
    }
    static async Task<Hit> ActualNativeHandTick(CardModel card)
    {
        Hits.Clear();await (Task)Native(card,"OnTurnEndInHand",new ThrowingPlayerChoiceContext())!;
        Check(Hits.Count==1,card.GetType().Name+" actual native hand-end emits exactly one canonical damage command");
        return Hits.Single();
    }
    static void InstallHandTickVisualLeaves()
    {
        var harmony=new Harmony(ProductionId+".hand-tick-visuals");
        foreach(MethodInfo method in typeof(VfxCmd).GetMethods(F).Where(m=>m.Name=="PlayOnCreatureCenter"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        foreach(MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
        Type? worm=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Vfx.NWormyImpactVfx");
        if(worm!=null)foreach(MethodInfo method in worm.GetMethods(F).Where(m=>m.Name=="Create"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip),F)!));
    }
    static async Task NativeHandTickBugNegative()
    {
        Reset();var card=HandTickCard(typeof(Infection));Hit hit=await ActualNativeHandTick(card);
        Cases.Add(new{source="old.actual.Infection.OnTurnEndInHand",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample,dealerIsHolder=ReferenceEquals(hit.Dealer,Hero),cardIsInfection=ReferenceEquals(hit.Card,card)});
        Check(hit.Raw==3m&&ReferenceEquals(hit.Target,Hero)&&ReferenceEquals(hit.Dealer,Hero)&&ReferenceEquals(hit.Card,card),"old infection preserves native self-dealer/card/raw3 attribution");
        Check(hit.Final>300m,"old real infection handler exceeds environmental300 because native card overload supplies the holder as dealer");
        Clean("old actual infection tick");
    }
    static async Task NativeHandTickEnvironmentalCases()
    {
        foreach(var row in NativeHandTicks)
        {
            Reset();var card=HandTickCard(row.Type);int before=RngCount(RunState.Rng.Niche);Hit hit=await ActualNativeHandTick(card);
            Cases.Add(new{source=row.Type.Name+".OnTurnEndInHand",hit.Raw,hit.Final,props=hit.Props.ToString(),hit.Enabled,hit.Sample,dealerIsHolder=ReferenceEquals(hit.Dealer,Hero),cardIsOriginal=ReferenceEquals(hit.Card,card)});
            Equal(hit.Final,row.Raw*100m,row.Type.Name+" actual hand-end uses raw times player-health100");
            Check(ReferenceEquals(hit.Target,Hero)&&ReferenceEquals(hit.Dealer,Hero)&&ReferenceEquals(hit.Card,card)&&hit.Raw==row.Raw&&hit.Props==card.DynamicVars.Damage.Props,row.Type.Name+" retains actual native target/dealer/card/raw/flags");
            Check(hit.Sample==null&&RngCount(RunState.Rng.Niche)==before,row.Type.Name+" environmental tick draws no critical RNG");
            Equal(card.DynamicVars.Damage.BaseValue,row.Raw,row.Type.Name+" damage storage remains raw native units");
            string text=card.Description.GetFormattedText();string expected=(row.Raw*100m).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Check(text.Contains(expected)&&!text.Contains((row.Raw*1000m).ToString(System.Globalization.CultureInfo.InvariantCulture))&&!text.Contains('{'),row.Type.Name+" actual native description shows the environmental value without unresolved arguments");
            Clean(row.Type.Name+" environmental tick");

            Reset(seed:FindSeed(true));H("Sense","Madness",99);var strength=Power<StrengthPower>(Hero,100);var amplifier=Power<HotaruDamageAmplificationPower>(Hero,200);var combo=Power<HbrComboLargePower>(Hero,2);Power<ReptureDualSwordsPower>(Hero,1);Power<MagicSwordModePower>(Hero,1);
            var attackUp=Power<HbrAttackUpPower>(Hero,1);Invoke(typeof(HbrAttackUpPower).GetMethod("ConfigureEffect",F)!,attackUp,65m,null);
            card=HandTickCard(row.Type);before=RngCount(RunState.Rng.Niche);hit=await ActualNativeHandTick(card);
            Equal(hit.Final,row.Raw*100m,row.Type.Name+" ignores holder stats, attack amplification, attack-up, dual swords and Magic Sword");
            Check(combo.Amount==2&&attackUp.Amount==1&&strength.Amount==100&&amplifier.Amount==200,row.Type.Name+" does not spend attack/combo consumables");
            Check(hit.Sample==null&&RngCount(RunState.Rng.Niche)==before,row.Type.Name+" even critical-favoring seed has no self-tick critical roll");
            Clean(row.Type.Name+" amplified holder tick");
        }
        Reset();var wither=(Wither)HandTickCard(typeof(Wither));wither.FakeUpgrade();Hit upgraded=await ActualNativeHandTick(wither);Equal(wither.DynamicVars.Damage.BaseValue,6m,"native Wither fake-upgrade retains raw6");Equal(upgraded.Final,600m,"native Wither changed raw6 scales once to environmental600");
        Reset(hbr:false);var legacy=HandTickCard(typeof(Infection));Hit old=await ActualNativeHandTick(legacy);Equal(old.Final,3m,"legacy Infection keeps native raw3 self-damage");
        await NativeKusarigamaCase();
        Reset();var fire=Owned<FirePotion>();await (Task)Native(fire,"OnUse",new ThrowingPlayerChoiceContext(),Enemy)!;Hit potion=Hits.Single();Equal(potion.Final,decimal.Floor(Base(20m)),"ordinary sourced FirePotion still uses holder attributes rather than hand-tick environmental basis");
    }
    static void RunNativeHandTick(string reportPath)
    {
        bool ok=false;string?error=null;bool negative=System.Environment.GetEnvironmentVariable("HBR_EXPECT_HAND_TICK_BUG")=="1";
        try
        {
            string hashing=Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),".nuget/packages/system.io.hashing/9.0.0/lib/net9.0/System.IO.Hashing.dll");AssemblyLoadContext.Default.LoadFromAssemblyPath(hashing);
            Install();Localization();InstallHandTickVisualLeaves();
            if(negative)NativeHandTickBugNegative().GetAwaiter().GetResult();else NativeHandTickEnvironmentalCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,expectedNegativeControl=negative,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CardModel).Assembly.Location))),sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual native five OnTurnEndInHand bodies, original DamageVar/cardSource overload chain, production skill adapters and real shared numeric pipeline. Scene/SFX/Wormy VFX leaves are suppressed; canonical terminal captures requests and evaluates the actual shared damage hook, without changing real HP/DP or invoking native after-damage events. Kusarigama/FirePotion positive controls use their real native handlers. No manufactured environmental declaration for hand ticks; no Godot/package/install."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native hand-tick checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
