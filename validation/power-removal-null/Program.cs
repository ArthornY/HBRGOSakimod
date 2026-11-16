using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

static class RemovalNullStandalone { public static void Run(string path) => EffectChecks.RunRemovalNull(path).GetAwaiter().GetResult(); }
static partial class EffectChecks
{
    static readonly Harmony RemovalHarmony = new("hbr.null-power-removal.checks");
    static bool RemovalNegative => Environment.GetEnvironmentVariable("HBR_NULL_REMOVE_NEGATIVE") == "1";
    static void RemovalSetup()
    {
        H("Setup"); Localization(); NonCriticalSeed=FindSeed(false);
        foreach(Type type in Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(type=>!type.IsAbstract&&!type.ContainsGenericParameters&&
            (typeof(PowerModel).IsAssignableFrom(type)||typeof(RelicModel).IsAssignableFrom(type)||typeof(CardPoolModel).IsAssignableFrom(type)||type.Assembly==Mod&&typeof(CardModel).IsAssignableFrom(type))))
        {
            if(!ModelDb.Contains(type))ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));
        }
        var state=Blank<PlayerCombatState>();Set(state,"_player",Player);Set(state,"_pets",new List<Creature>());Set(state,"<TurnNumber>k__BackingField",1);
        Set(Player,"<PlayerCombatState>k__BackingField",state);
        Set(RunState,"_allCards",new List<CardModel>());Set(Combat,"_allCards",new List<CardModel>());
        FormReset();Set(Player,"<Character>k__BackingField",Blank<HbrgoHotaru>());
        foreach(MethodInfo method in typeof(Cmd).GetMethods(F).Where(method=>method.Name is "Wait" or "CustomScaledWait"&&method.ReturnType==typeof(Task)))
            RemovalHarmony.Patch(method,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(VisualTask),F)!));
    }
    static async Task ExpectRemoval(Func<Task> action,string name)
    {
        Exception? failure=null;try{await action();}catch(Exception exception){failure=exception;}
        if(RemovalNegative)Check(failure is NullReferenceException,name+" reproduces actual released Aoi prefix null dereference");
        else Check(failure==null,name+" completes through real patched native Remove");
    }
    static async Task RemovalCoreCases()
    {
        await PowerCmd.Remove((PowerModel?)null);
        Check(true,"original native Remove(null) is an explicitly supported no-op before mod removal patches");
        MethodInfo target=typeof(PowerCmd).GetMethod(nameof(PowerCmd.Remove),F,null,[typeof(PowerModel)],null)!;
        foreach(string name in new[]{"ThirtyOneBAoiAttachedBuffRemovalPatch","MuuaFaithShellRemovalPatch","NararaNullificationRemovePowerPatch"})
        {
            Type type=Mod.GetTypes().Single(type=>type.Name==name);
            Check(RemovalHarmony.CreateClassProcessor(type).Patch().Count==1,"installs actual production removal patch "+name);
            Check(Harmony.GetPatchInfo(target)!.Prefixes.Any(patch=>patch.owner==RemovalHarmony.Id&&patch.PatchMethod.DeclaringType==type),"real native Remove owns "+name+" prefix");
        }
        var before=Powers(Hero).ToArray();
        await ExpectRemoval(()=>PowerCmd.Remove((PowerModel?)null),"direct null remove");
        await ExpectRemoval(()=>PowerCmd.Remove<HotaruTurnDamageAmplificationPower>(Hero),"generic absent Hotaru turn damage remove");
        var millsage=ModelDb.Relic<Millsage>().ToMutable();Set(millsage,"_owner",Player);
        await ExpectRemoval(()=>millsage.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),Hero.Side,new[]{Hero},Combat),"actual Millsage first-turn BeforeSideTurnStart -> ClearTurnDamage");
        Check(Powers(Hero).SequenceEqual(before),"absent cleanup does not add/remove any real powers");
        Check(AsyncValue("MuuaFaithCombatHelper","Removing")==null,"Muua removal receipt is not left open after null cleanup/failure");
        if(RemovalNegative)return;
        for(int index=0;index<3;index++)await millsage.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),Hero.Side,new[]{Hero},Combat);
        Check(true,"repeated first-turn/extra-turn cleanup without expiring power remains safe");
        var ordinary=Power<StrengthPower>(Hero,1);int removed=0;ordinary.Removed+=()=>removed++;
        await PowerCmd.Remove(ordinary);
        Check(!Powers(Hero).Contains(ordinary)&&removed==1,"nonnull buff runs actual native RemoveInternal once and fires its original removal event");
        var temporary=Power<HotaruTurnDamageAmplificationPower>(Hero,1);
        await millsage.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),Hero.Side,new[]{Hero},Combat);
        Check(!Powers(Hero).Contains(temporary),"Millsage still clears an actually present expiring Hotaru damage buff");
        await millsage.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),CombatSide.Enemy,new[]{Hero},Combat);
        await millsage.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),Hero.Side,Array.Empty<Creature>(),Combat);
        Check(true,"wrong side/participant Millsage boundaries retain their original harmless behavior");
        await RemovalAoiScopeCases();
    }
    static async Task RemovalAoiScopeCases()
    {
        FormReset();Power<AoiInvincibleShieldPower>(Hero,1);
        var ordinary=Power<StrengthPower>(Hero,1);
        object? scope=Call("ThirtyOneBEnemyActionHelper","Begin",Enemy);
        Check(scope!=null,"real Aoi enemy-action Begin activates its existing scope");
        try
        {
            await PowerCmd.Remove(ordinary);
            Check(Powers(Hero).Contains(ordinary),"nonnull buff removal remains staged before the Aoi action's real hit decision");
            Check(((ICollection)scope!.GetType().GetField("Pending",F)!.GetValue(scope)!).Count==1,"actual removal prefix queued exactly one existing deferred command");
            await (Task)Call("ThirtyOneBEnemyActionHelper","Finish",Task.CompletedTask,scope)!;
            Check(!Powers(Hero).Contains(ordinary),"successful non-null deferred removal replays real native Remove and clears the buff");
        }
        finally{Call("ThirtyOneBEnemyActionHelper","Close",scope);}
        Check(AsyncValue("ThirtyOneBEnemyActionHelper","Current")==null,"deferred removal closes its existing action scope");
        FormReset();Power<AoiInvincibleShieldPower>(Hero,1);ordinary=Power<StrengthPower>(Hero,1);
        scope=Call("ThirtyOneBEnemyActionHelper","Begin",Enemy);
        try
        {
            Type receiptType=scope!.GetType().DeclaringType!.GetNestedType("Receipt",F)!;
            object receipt=Activator.CreateInstance(receiptType,true)!;Set(receipt,"Invalidated",true);Set(receipt,"Applied",true);
            ((IDictionary)scope.GetType().GetField("Targets",F)!.GetValue(scope)!).Add(Hero,receipt);
            await PowerCmd.Remove(ordinary);
            Check(Powers(Hero).Contains(ordinary),"nonnull buff removal stays blocked by an already-established Aoi nullification receipt");
        }
        finally{Call("ThirtyOneBEnemyActionHelper","Close",scope);}
        await PowerCmd.Remove(ordinary);
        Check(!Powers(Hero).Contains(ordinary),"the same buff removes normally after the protected action closes");
    }
    public static async Task RunRemovalNull(string path)
    {
        string? error=null;
        try{RemovalSetup();await RemovalCoreCases();if(RemovalNegative)await ChihayaPartialApplyNegativeCases();else await (Task)Invoke(typeof(EffectChecks).GetMethod("ChihayaRemovalCases",F)??throw new MissingMethodException("ChihayaRemovalCases is not ready"),null)!;}
        catch(Exception exception){error=exception.ToString();Console.Error.WriteLine(exception);}
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,negativeControl=RemovalNegative,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary="Real native PowerCmd.Remove factories/bodies with three actual production removal prefixes, real absent generic removal and Millsage hook, real non-null removal callback plus existing deferred-command replay. Aoi protected receipt is controlled state, not a claim about fresh defensive dice. Cmd waits/log/string-name engine terminals isolated; no full turn loop, Godot/UI or in-game test."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" power-removal-null checks");if(error!=null)throw new Exception("See "+path);
    }
}
