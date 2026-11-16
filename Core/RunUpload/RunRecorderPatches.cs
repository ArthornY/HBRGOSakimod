using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.RunUpload;

// Read-only taps for the run recorder (schema-v1.md 7). Every patch is a void
// prefix or postfix that only reads its arguments: none skips the original or
// changes an argument or a result. The recorder returns at once unless the
// player agreed to uploading and a run is being recorded. Prefixes on the
// After* hooks see the state at the moment of the event, before listeners react.

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
internal static class RunRecordSideTurnStartingPatch
{
    private static void Prefix(ICombatState combatState, CombatSide side) =>
        RunRecorder.OnSideTurnStarting(combatState, side);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeHandDraw))]
internal static class RunRecordHandDrawPatch
{
    private static void Prefix(ICombatState combatState, Player player) => RunRecorder.OnHandDraw(combatState, player);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class RunRecordSideTurnStartedPatch
{
    private static void Prefix(ICombatState combatState, CombatSide side) =>
        RunRecorder.OnSideTurnStarted(combatState, side);
}

// v0.107.1 names it AfterTurnEnd, v0.111.0 AfterSideTurnEnd; both take
// (combatState, side, participants).
[HarmonyPatch]
internal static class RunRecordSideTurnEndedPatch
{
    private static MethodBase Target() =>
        typeof(Hook).GetMethod("AfterTurnEnd", BindingFlags.Public | BindingFlags.Static) ??
        typeof(Hook).GetMethod("AfterSideTurnEnd", BindingFlags.Public | BindingFlags.Static);

    private static bool Prepare() => Target() != null;

    private static MethodBase TargetMethod() => Target();

    private static void Prefix(ICombatState combatState, CombatSide side) =>
        RunRecorder.OnSideTurnEnded(combatState, side);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
internal static class RunRecordCardDrawnPatch
{
    private static void Prefix(CardModel card) => RunRecorder.OnCardDrawn(card);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class RunRecordCardPlayingPatch
{
    private static void Prefix(CardPlay cardPlay) => RunRecorder.OnCardPlaying(cardPlay);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class RunRecordCardPlayedPatch
{
    private static void Prefix(CardPlay cardPlay) => RunRecorder.OnCardPlayed(cardPlay);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforePotionUsed))]
internal static class RunRecordPotionUsingPatch
{
    private static void Prefix(PotionModel potion, Creature target) => RunRecorder.OnPotionUsing(potion, target);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionProcured))]
internal static class RunRecordPotionProcuredPatch
{
    private static void Prefix(PotionModel potion) => RunRecorder.OnPotionProcured(potion);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeDamageReceived))]
internal static class RunRecordDamageIncomingPatch
{
    private static void Prefix(Creature target) => RunRecorder.OnDamageIncoming(target);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
internal static class RunRecordDamageReceivedPatch
{
    private static void Prefix(Creature target, DamageResult result, Creature dealer, CardModel cardSource) =>
        RunRecorder.OnDamageReceived(target, result, dealer, cardSource);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
internal static class RunRecordBlockGainedPatch
{
    private static void Prefix(Creature creature, decimal amount, CardModel cardSource) =>
        RunRecorder.OnBlockGained(creature, amount, cardSource);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
internal static class RunRecordPowerChangedPatch
{
    private static void Prefix(PowerModel power, decimal amount, Creature applier, CardModel cardSource) =>
        RunRecorder.OnPowerChanged(power, amount, applier, cardSource);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class RunRecordDeathPatch
{
    private static void Prefix(Creature creature) => RunRecorder.OnDeath(creature);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCreatureAddedToCombat))]
internal static class RunRecordCreatureAddedPatch
{
    private static void Prefix(ICombatState combatState, Creature creature) =>
        RunRecorder.OnCreatureAdded(combatState, creature);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
internal static class RunRecordCardExhaustedPatch
{
    private static void Prefix(CardModel card) => RunRecorder.OnCardExhausted(card);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class RunRecordCardGeneratedPatch
{
    private static void Prefix(CardModel card, Player creator) => RunRecorder.OnCardGenerated(card, creator);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterShuffle))]
internal static class RunRecordShufflePatch
{
    private static void Prefix(Player shuffler) => RunRecorder.OnShuffle(shuffler);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterItemPurchased))]
internal static class RunRecordItemPurchasedPatch
{
    private static void Prefix(Player player, MerchantEntry itemPurchased, int goldSpent) =>
        RunRecorder.OnItemPurchased(player, itemPurchased, goldSpent);
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class RunRecordMonsterMovePatch
{
    private static void Prefix(MonsterModel __instance) => RunRecorder.OnMove(__instance);
}

// Every developer console command, typed here or sent by a co-op teammate,
// passes through this private overload (cmdName in v0.107.1 and v0.111.0).
// A successful one excludes the run unless it is one of the game's harmless
// commands (RunUploadConstants.HarmlessConsoleCommands).
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.DevConsole.DevConsole), "ProcessCommand",
    new[] { typeof(Player), typeof(string), typeof(string[]) })]
internal static class RunRecordConsolePatch
{
    private static void Postfix(string cmdName, MegaCrit.Sts2.Core.DevConsole.CmdResult __result)
    {
        try
        {
            RunRecorder.OnConsoleCommand(cmdName, __result.success);
        }
        catch (Exception exception)
        {
            RunUploadService.Log("Console check failed: " + exception.Message);
        }
    }
}

// The mod's own roll sink: the entry it just stored carries the resolved tiers.
[HarmonyPatch(typeof(SoulOfAnchor), nameof(SoulOfAnchor.RecordRoll))]
internal static class RunRecordRollPatch
{
    private static void Postfix(SoulOfAnchor __instance) => RunRecorder.OnRoll(__instance);
}

[HarmonyPatch(typeof(ElectronicSoldierManual), nameof(ElectronicSoldierManual.ApplySixSenseAllocation))]
internal static class RunRecordSixSensePatch
{
    private static void Postfix(ElectronicSoldierManual __instance, IReadOnlyList<int> increases, bool __result)
    {
        if (__result) RunRecorder.OnSixSenseAllocated(__instance, increases);
    }
}

// The command runs synchronously up to its first gold change, so the stored
// state already differs from __state when the postfix runs.
[HarmonyPatch(typeof(ElectronicSoldierManual), nameof(ElectronicSoldierManual.ApplyEquipmentCommand))]
internal static class RunRecordEquipmentPatch
{
    private static void Prefix(ElectronicSoldierManual __instance, out HbrEquipmentState __state)
    {
        __state = null;
        if (!RunRecorder.IsRecording) return;
        try
        {
            __state = __instance.Equipment;
        }
        catch (Exception)
        {
            // Left unrecorded; the command itself runs as usual.
        }
    }

    private static void Postfix(ElectronicSoldierManual __instance, HbrEquipmentCommand command,
        HbrEquipmentState __state)
    {
        if (__state != null) RunRecorder.OnEquipmentCommand(__instance, command, __state);
    }
}
