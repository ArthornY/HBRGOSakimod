using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Runs;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
static partial class EffectChecks
{
    static bool BSpiritCandidates(ref MonsterModel[] __result) { __result = [ModelDb.Monster<SmallHopper>()]; return false; }
    static bool BPetAdd(Creature creature, ref Task __result)
    {
        Combat.AddCreature(creature); creature.Monster!.SetUpForCombat();
        __result = Task.CompletedTask; return false;
    }
    static bool BPetKill(Creature creature, bool force, ref Task __result)
    {
        Set(creature, "_currentHp", 0);
        typeof(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState).GetMethod("OnPetDied", F)!.Invoke(Player.PlayerCombatState, [creature]);
        Combat.RemoveCreature(creature, unattach: false);
        __result = Task.CompletedTask; return false;
    }
    static async Task BKozueCases(Harmony h)
    {
        foreach (Type type in new[] { typeof(SmallHopper), typeof(AdaptablePower), typeof(TestSubject), typeof(PhantomPower), typeof(MinionPower), typeof(KozueSpiritPower), typeof(KozueSpiritShellPower), typeof(HbrCriticalRateUpPower), typeof(HbrCriticalDamageUpPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        h.Patch(Helper("KozueSpiritHelper").GetMethod("Candidates", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BSpiritCandidates), F)!));
        h.Patch(typeof(CreatureCmd).GetMethod("Add", [typeof(Creature)])!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BPetAdd), F)!));
        h.Patch(typeof(CreatureCmd).GetMethod("Kill", [typeof(Creature), typeof(bool)])!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BPetKill), F)!));
        foreach (string patch in new[] { "KozueSpiritDamageDefenderPatch", "KozueSpiritNoRosterDamagePatch", "KozueSpiritNoPendingRevivePatch", "KozueSpiritDeathRemovalPatch", "KozueSpiritNoRevivalPowerPatch", "KozueSpiritNoHealRevivalPatch", "CreatureDamageBlockInternalPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + patch, true)!).Patch().Count > 0, "installs actual spirit patch " + patch);
        BReset(); var card = GardenCard<InnocentRequiemHbrgoSaki>(0, 3, 5);
        string seed = "31B-SPIRIT-HP";
        Set(RunState, "<Rng>k__BackingField", new RunRngSet(seed)); RunState.Rng.Niche.NextInt(1);
        decimal normal = (decimal)Call("KozueSpiritHelper", "CalculateSpawnHealth", Player, ModelDb.Monster<SmallHopper>(), 1m)!;
        Set(RunState, "<Rng>k__BackingField", new RunRngSet(seed));
        await GardenPlay(card);
        Creature spirit = (Creature)Call("KozueSpiritHelper", "GetLivingSpirit", Player)!;
        Equal(spirit.MaxHp, decimal.Floor(normal * .3m), "actual summon has30% of ordinary enemy-side HBR/native spawnHP");
        var shell = spirit.GetPower<KozueSpiritShellPower>()!;
        Equal(shell.CurrentDp, decimal.Floor(spirit.MaxHp * .2m), "actual spirit shell receives20% of spiritHP as DP");
        Check(spirit.PetOwner == Player && spirit.GetPower<PhantomPower>()!.IsSummonedMinion && spirit.HasPower<MinionPower>(), "31O owns the spirit as an independent non-roster phantom pet");
        Equal((decimal)Call("KozueSpiritHelper", "GetDamageAmplificationPercent", Hero)!, 15m, "Inspiration adds15 in the damage-amplification bucket while spirit lives");
        var rate = Hero.GetPower<HbrCriticalRateUpPower>()!; var critical = Hero.GetPower<HbrCriticalDamageUpPower>()!;
        Check(rate.Applier == spirit && critical.Applier == spirit, "Smile's two real uncapped effect instances are attributed to this spirit");
        Check(Enemy.GetPower<FragilePower>()?.Amount == 2, "Freezing Curse applies actual two-turn fragile on entry");
        decimal before = shell.CurrentDp;
        Equal(spirit.DamageBlockInternal(10m, ValueProp.Move), 10m, "real native block/DP boundary absorbs one spirit hit");
        Equal(shell.CurrentDp, before - 10m, "spirit DP decreases only once at the shared absorption boundary");
        before = shell.CurrentDp;
        Equal(spirit.DamageBlockInternal(10m, ValueProp.Unblockable), 0m, "Unblockable uses the existing phantom DP bypass rule");
        Equal(shell.CurrentDp, before, "Unblockable never spends spiritDP");
        Check(ReferenceEquals(Call("PhantomHelper", "GetDamageDefender", Player), spirit), "spirit redirects attacks against a31O owner");
        var rosterBefore = ((System.Collections.IEnumerable)Call("PhantomHelper", "GetLivingPhantoms", Player)!).Cast<Creature>().ToArray();
        Check(!rosterBefore.Contains(spirit), "spirit never enters the main phantom list");
        var damageBonus = Call("PhantomHelper", "GetDamageBonusBreakdown", spirit)!;
        Equal((decimal)damageBonus.GetType().GetField("Item1")!.GetValue(damageBonus)!, 0m, "spirit has no main-phantom per-level damage growth");
        // A second physical card replaces the pet, removes exactly its aura
        // instances and leaves the player's unrelated critical grants intact.
        await ThirtyOneBTestCriticals(); var unrelated = Hero.Powers.OfType<HbrCriticalRateUpPower>().Single(power => power != rate);
        var replacement = GardenCard<InnocentRequiemHbrgoSaki>(6);
        Set(RunState, "<Rng>k__BackingField", new RunRngSet(seed)); RunState.Rng.Niche.NextInt(1);
        decimal expectedSixty = (decimal)Call("KozueSpiritHelper", "CalculateSpawnHealth", Player, ModelDb.Monster<SmallHopper>(), .6m)!;
        Set(RunState, "<Rng>k__BackingField", new RunRngSet(seed));
        await GardenPlay(replacement);
        Creature second = (Creature)Call("KozueSpiritHelper", "GetLivingSpirit", Player)!;
        Equal(second.MaxHp, expectedSixty, "Soul Evocare actual replacement has60% normal spawnHP");
        Check(second != spirit && Player.PlayerCombatState!.Pets.Count(pet => pet.HasPower<KozueSpiritPower>() && pet.IsAlive) == 1, "same-name replacement across physical cards leaves exactly one living spirit");
        Check(!Hero.Powers.Contains(rate) && !Hero.Powers.Contains(critical) && Hero.Powers.Contains(unrelated), "replacement removes only disappearing spirit's critical pair");
        Equal((decimal)Call("KozueSpiritHelper", "GetDamageAmplificationPercent", Hero)!, 0m, "replacement without Inspiration drops the15% aura immediately");
        Check(Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(Combat, second), "spirit death always permits combat removal");
        int actions = second.GetPower<PhantomPower>()!.ActionsPerformed;
        await second.GetPower<PhantomPower>()!.AfterSideTurnEndLate(new ThrowingPlayerChoiceContext(), MegaCrit.Sts2.Core.Combat.CombatSide.Player, [Hero]);
        Equal(second.GetPower<PhantomPower>()!.ActionsPerformed, actions + 1, "real phantom late-turn hook advances the spirit's own native move table exactly once");
        // A native TestSubject revival power on the spirit must never start its
        // dead-state animation/state-machine transition.
        if (!ModelDb.Contains(typeof(TestSubject))) ModelDb.Inject(typeof(TestSubject));
        var subject = (TestSubject)ModelDb.Monster<TestSubject>().MutableClone(); Set(subject, "_creature", second); Set(second, "<Monster>k__BackingField", subject);
        await PowerCmd.Apply<AdaptablePower>(new ThrowingPlayerChoiceContext(), second, 1m, second, null);
        var adaptable = second.GetPower<AdaptablePower>()!;
        Set(second, "_currentHp", 0);
        MethodInfo death = typeof(AdaptablePower).GetMethods(F).Single(method => method.Name == "AfterDeath" && method.DeclaringType == typeof(AdaptablePower));
        object?[] deathArgs = death.GetParameters().Select(parameter => parameter.ParameterType == typeof(PlayerChoiceContext) ? (object)new ThrowingPlayerChoiceContext() :
            parameter.ParameterType == typeof(Creature) ? second : parameter.ParameterType == typeof(bool) ? false : parameter.ParameterType == typeof(float) ? 0f : null).ToArray();
        await (Task)Invoke(death, adaptable, deathArgs)!;
        Check(!(bool)typeof(AdaptablePower).GetProperty("IsReviving", F)!.GetValue(adaptable)!, "real native Adaptable.AfterDeath cannot revive a marked spirit");
        Check(!(bool)Call("PhantomHelper", "IsPendingSceneBossRevive", second)!, "a dead spirit never enters pending scene-boss revival");
        // The independent spirit cannot replace a30A main pet or alter its list.
        BReset(); Set(Player, "<Character>k__BackingField", Blank<HBRGOSakiMod.Core.Models.Characters.HbrgoSaki30A>());
        Creature main = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Player.PlayerCombatState!.AddPetInternal(main); Combat.AddCreature(main); Power<PhantomPower>(main, 1);
        var thirtyA = GardenCard<InnocentRequiemHbrgoSaki>(); await GardenPlay(thirtyA);
        Creature companion = (Creature)Call("KozueSpiritHelper", "GetLivingSpirit", Player)!;
        Check(ReferenceEquals(Call("PhantomHelper", "GetLivingPhantom", Player), main), "30A's living main phantom remains unchanged after spirit entry");
        Check(((System.Collections.IEnumerable)Call("PhantomHelper", "GetLivingPhantoms", Player)!).Cast<Creature>().SequenceEqual([main]), "30A main phantom list contains only the original main pet");
        Check(ReferenceEquals(Call("PhantomHelper", "GetDamageDefender", Player), companion), "independent spirit takes incoming attacks while both pets coexist");
    }
    static Task ThirtyOneBTestCriticals() => (Task)Call("ThirtyOneAMemoryUpgradeHelper", "GrantCriticals", new ThrowingPlayerChoiceContext(), Hero, Hero, null)!;
}
