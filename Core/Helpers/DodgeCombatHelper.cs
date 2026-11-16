using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DodgeCombatHelper
{
    private static readonly AccessTools.FieldRef<Creature, int> NativeBlock =
        AccessTools.FieldRefAccess<Creature, int>("_block");
    private static readonly FieldInfo BlockChanged = AccessTools.Field(typeof(Creature), "BlockChanged");
    [ThreadStatic] private static Dictionary<Creature, int> RawReads;

    internal static ThirtyOneACollection Collection(Creature creature) =>
        creature?.Player?.GetRelic<ThirtyOneACollection>();
    internal static bool ConvertsBlock(Creature creature) => Collection(creature)?.HasBlockConversion == true;
    internal static bool CountsAsBlock(Creature creature) => Collection(creature)?.CountsDodgeAsBlock == true;
    internal static bool GrantsStats(Creature creature) => Collection(creature)?.GrantsDodgeStats == true;
    internal static int GetMaximum(Creature creature) => Collection(creature)?.HasExpandedDodge == true
        ? DodgePower.ExpandedMaximumAmount : DodgePower.MaximumAmount;
    internal static bool RetainsAtTurnStart(Creature creature) => Collection(creature)?.KeepsDodgeAtTurnStart == true;
    internal static int GetAmount(Creature creature) =>
        Math.Clamp(creature?.GetPower<DodgePower>()?.Amount ?? 0, 0, GetMaximum(creature));
    // Under HBR one layer stands for 100 points, the fixed scale of player HP and Block.
    // Layers, caps and turn-start halving stay native.
    internal static decimal PointsPerLayer(Creature creature) => HbrResourceValueHelper.Fixed(creature, 1m);

    // The 31A relic's Strength and Dexterity from the current layers: 2 each per layer under
    // HBR, 1 each per 5 layers natively. Read wherever Strength and Dexterity are read, never
    // stored as their powers, so doubling or rewriting those powers neither keeps nor loses it.
    internal static int GetStatBonus(Creature creature)
    {
        if (!GrantsStats(creature)) return 0;
        int layers = GetAmount(creature);
        return HbrBattleStatsHelper.IsEnabled(creature)
            ? layers * DodgePower.HbrStatsPerLayer : layers / DodgePower.NativeLayersPerStat;
    }
    internal static int GetBlockValue(Creature creature) =>
        (int)Math.Min(int.MaxValue, GetAmount(creature) * PointsPerLayer(creature));
    internal static int GetNativeBlock(Creature creature) => creature == null ? 0 : Math.Max(0, NativeBlock(creature));
    internal static int GetEffectiveBlock(Creature creature) => (int)Math.Min(int.MaxValue,
        (long)GetNativeBlock(creature) + (CountsAsBlock(creature) ? GetBlockValue(creature) : 0));
    internal static bool ReadsNativeBlock(Creature creature) =>
        creature != null && RawReads?.TryGetValue(creature, out int depth) == true && depth > 0;

    internal static RawBlockScope BeginNativeBlock(Creature creature) => new(creature);

    internal sealed class RawBlockScope : IDisposable
    {
        private Creature _creature;
        private readonly int _previousBlock;
        private readonly int _previousEffective;
        internal RawBlockScope(Creature creature)
        {
            _creature = creature;
            _previousBlock = GetNativeBlock(creature);
            _previousEffective = GetEffectiveBlock(creature);
            RawReads ??= new();
            RawReads.TryGetValue(creature, out int depth);
            RawReads[creature] = depth + 1;
        }
        public void Dispose()
        {
            Creature creature = _creature;
            if (creature == null) return;
            _creature = null;
            if (!RawReads.TryGetValue(creature, out int depth)) return;
            if (depth <= 1)
            {
                RawReads.Remove(creature);
                // Native storage events report raw Block. Once raw reads end,
                // finish with the displayed value so subscribers also see the
                // same effective value when they read Creature.Block.
                if (GetNativeBlock(creature) != _previousBlock && CountsAsBlock(creature))
                    NotifyBlockChanged(creature, _previousEffective);
            }
            else RawReads[creature] = depth - 1;
        }
    }

    internal static bool ConvertBlockGain(Creature creature, decimal amount)
    {
        int maximum = GetMaximum(creature);
        if (amount < 0m || !ConvertsBlock(creature) || GetAmount(creature) >= maximum) return false;
        // One layer per layer point of Block: 1 native Block, or 100 HBR Block.
        int gain = (int)Math.Min(maximum, decimal.Floor(amount / PointsPerLayer(creature)));
        if (gain <= 0) return true;
        DodgePower power = creature.GetPower<DodgePower>();
        if (power == null)
        {
            power = (DodgePower)ModelDb.Power<DodgePower>().ToMutable();
            power.ApplyInternal(creature, gain);
        }
        else power.SetAmount(Math.Min(maximum, GetAmount(creature) + gain));
        // A grant that fills the remaining layers still converts in full.
        // Once already capped, subsequent grants use native Block instead.
        return true;
    }

    internal static bool TryDodge(Creature receiver, decimal fullDamage)
    {
        if (receiver == null || fullDamage <= 0m) return false;
        DodgePower power = receiver.GetPower<DodgePower>();
        int amount = GetAmount(receiver);
        decimal points = PointsPerLayer(receiver);
        if (power == null || amount * points < fullDamage) return false;
        // Layers are integral. Compare the whole decimal hit first, then round
        // consumption up, so 22 cannot evade 22.5 while 23 can evade it fully;
        // under HBR 9 layers evade 814 and spend all 9.
        int spent = (int)decimal.Ceiling(fullDamage / points);
        power.SetAmount(amount - spent);
        if (power.Amount <= 0) power.RemoveInternal();
        HpOnlyDamageContext.RecordDodge(receiver);
        return true;
    }

    internal static void NotifyBlockChanged(Creature creature, int previousEffective)
    {
        if (creature == null) return;
        int current = GetEffectiveBlock(creature);
        if (current == previousEffective) return;
        (BlockChanged.GetValue(creature) as Action<int, int>)?.Invoke(previousEffective, current);
    }
}
