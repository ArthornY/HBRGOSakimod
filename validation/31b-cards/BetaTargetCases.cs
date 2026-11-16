using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static object?[] BLastTargetRecords = [];
    static void BObserveTargetRecords(object[] __args) => BLastTargetRecords = ((System.Collections.IEnumerable)__args[1]).Cast<object>().ToArray();
    static async Task BBetaTargetCases(Harmony h)
    {
        Type? targetType = typeof(CreatureCmd).Assembly.GetType("MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData");
        if (targetType == null) return;
        h.Patch(Canonical, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BObserveTargetRecords), F)!) { priority = -50 });
        BReset(); var aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
        Creature spirit = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Player.PlayerCombatState!.AddPetInternal(spirit); Combat.AddCreature(spirit);
        Power<KozueSpiritPower>(spirit, 1); var phantom = Power<PhantomPower>(spirit, 1); Native(phantom, "ConfigureAsSummonedMinion", 1);
        object scope = BBegin();
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
        object record = Activator.CreateInstance(targetType, Hero)!;
        targetType.GetField("hitVfx")!.SetValue(record, "31b-vfx-preservation");
        targetType.GetField("playVfxOnCreatureCenter")!.SetValue(record, true);
        Array records = Array.CreateInstance(targetType, 1); records.SetValue(record, 0);
        Type adapter = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ThirtyOneBDamageCallsitePatch", true)!;
        var call = adapter.GetMethod("DamageEight", F)!.MakeGenericMethod(targetType);
        await (Task)Invoke(call, null, new ThrowingPlayerChoiceContext(), records, 8m, ValueProp.Move, Enemy, null, null, MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType.All)!;
        Check(BLastTargetRecords.Length == 1 && ReferenceEquals(targetType.GetField("target")!.GetValue(BLastTargetRecords[0]), spirit), "beta typed adapter redirects the real DamageTargetData target to the spirit");
        Check((string)targetType.GetField("hitVfx")!.GetValue(BLastTargetRecords[0])! == "31b-vfx-preservation" && (bool)targetType.GetField("playVfxOnCreatureCenter")!.GetValue(BLastTargetRecords[0])!, "beta typed pending adapter preserves both per-target VFX fields");
        await BFinish(scope);
        Check(Hero.HasPower<WeakPower>() && ShieldRolls == 0, "beta VFX redirect does not incorrectly roll the shield on an unhit owner");
    }
}
