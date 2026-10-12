using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// These native moves assume every teammate is an enemy monster. A phantom's
// teammates also include players, and its reinforcements spawn on the player side.
internal static class PhantomNativeTeamMoves
{
    internal static bool IsPhantom(Creature creature) =>
        creature?.HasPower<PhantomPower>() == true;

    internal static string NextBackupSlot(TwoTailedRat rat)
    {
        var combat = rat.Creature.CombatState;
        var teammates = combat.GetTeammatesOf(rat.Creature);
        return combat.Encounter?.Slots.LastOrDefault(
            slot => teammates.All(teammate => teammate.SlotName != slot),
            string.Empty) ?? string.Empty;
    }

    internal static bool CanSummon(TwoTailedRat rat, int turnsUntilSummonable)
    {
        if (turnsUntilSummonable > 0 || rat.CallForBackupCount >= 3 ||
            string.IsNullOrEmpty(NextBackupSlot(rat)))
        {
            return false;
        }

        return !rat.Creature.CombatState.GetTeammatesOf(rat.Creature)
            .Any(teammate => teammate != rat.Creature &&
                teammate.Monster?.NextMove?.Id == "CALL_FOR_BACKUP_MOVE");
    }

    internal static void AdvanceBackupCount(TwoTailedRat rat)
    {
        // Include the caller even if a removal hook has just detached it. Never
        // derive the count from enemy rats or apply Max to an empty collection.
        var rats = rat.Creature.CombatState.GetTeammatesOf(rat.Creature)
            .Select(teammate => teammate.Monster)
            .OfType<TwoTailedRat>()
            .Append(rat)
            .Distinct()
            .ToArray();
        int nextCount = rats.Max(teammate => teammate.CallForBackupCount) + 1;
        foreach (TwoTailedRat teammate in rats)
        {
            teammate.CallForBackupCount = nextCount;
        }
    }

    internal static async Task CallForBackup(TwoTailedRat rat)
    {
        SfxCmd.Play("event:/sfx/enemy/enemy_attacks/two_tail_rats/two_tail_rats_summon");
        await CreatureCmd.TriggerAnim(rat.Creature, "Summon", 0.3f);
        var combat = rat.Creature.CombatState;
        if (!combat.IsLiveCombat())
        {
            return;
        }

        string nextSlot = NextBackupSlot(rat);
        if (!string.IsNullOrEmpty(nextSlot))
        {
            await Cmd.Wait(0.5f);
            // PerformPhantomIntent keeps the summon context alive through this
            // task, so the new rat receives the same side and phantom owner.
            await CreatureCmd.Add<TwoTailedRat>(combat, nextSlot);
        }
        AdvanceBackupCount(rat);
    }

    internal static async Task Guard(Guardbot guard)
    {
        SfxCmd.Play("event:/sfx/enemy/enemy_attacks/guardbot/guardbot_cast");
        await CreatureCmd.TriggerAnim(guard.Creature, "Cast", 0.6f);
        var fabricators = guard.Creature.CombatState
            .GetTeammatesOf(guard.Creature)
            .Where(teammate => teammate.Monster is Fabricator)
            .ToArray();
        foreach (Creature fabricator in fabricators)
        {
            await CreatureCmd.GainBlock(fabricator, 15m, ValueProp.Unpowered, null);
        }
    }
}

[HarmonyPatch(typeof(TwoTailedRat), "CanSummon")]
internal static class PhantomTwoTailedRatCanSummonPatch
{
    private static bool Prefix(TwoTailedRat __instance,
        int ____turnsUntilSummonable, ref bool __result)
    {
        if (!PhantomNativeTeamMoves.IsPhantom(__instance.Creature)) return true;
        __result = PhantomNativeTeamMoves.CanSummon(__instance, ____turnsUntilSummonable);
        return false;
    }
}

[HarmonyPatch(typeof(TwoTailedRat), "CallForBackup")]
internal static class PhantomTwoTailedRatBackupPatch
{
    private static bool Prefix(TwoTailedRat __instance, ref Task __result)
    {
        if (!PhantomNativeTeamMoves.IsPhantom(__instance.Creature)) return true;
        __result = PhantomNativeTeamMoves.CallForBackup(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(Guardbot), "GuardMove")]
internal static class PhantomGuardbotTeamPatch
{
    private static bool Prefix(Guardbot __instance, ref Task __result)
    {
        if (!PhantomNativeTeamMoves.IsPhantom(__instance.Creature)) return true;
        __result = PhantomNativeTeamMoves.Guard(__instance);
        return false;
    }
}
