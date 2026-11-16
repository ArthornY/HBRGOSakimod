using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// What a roll does after a fragment has spoken: draws, self-damage and Fragile wait
// until the card that rolled has resolved, like the Fragile card's own trigger.
internal readonly record struct HbrFateDicePendingEffects(int Draws, int SelfDamageHits, int FragileBursts)
{
    internal bool IsEmpty => Draws <= 0 && SelfDamageHits <= 0 && FragileBursts <= 0;

    public static HbrFateDicePendingEffects operator +(HbrFateDicePendingEffects a, HbrFateDicePendingEffects b) =>
        new(a.Draws + b.Draws, a.SelfDamageHits + b.SelfDamageHits, a.FragileBursts + b.FragileBursts);
}

// The equipped fragments as the roll pipeline reads them. Only 31O carries a fate die.
internal static class HbrFateDiceRollHelper
{
    private sealed class CombatMarks
    {
        internal object Combat;
        internal int LastRollRound = int.MinValue;
        internal bool RerollUsed;
    }

    private static readonly ConditionalWeakTable<Player, CombatMarks> Marks = new();

    internal static bool Has(Player player, string fragment) =>
        player?.GetRelic<ElectronicSoldierManual>()?.HasFateFragment(fragment) == true;

    // How far the critical range reaches below its usual minimum.
    internal static int CriticalRangeBonus(Player player)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { UsesFateDice: true }) return 0;
        int bonus = 0;
        if (Has(player, HbrFateDiceCatalog.WideEdge)) bonus += HbrFateDiceCatalog.WideEdgeRange;
        if (Has(player, HbrFateDiceCatalog.WildDie)) bonus += HbrFateDiceCatalog.WildDieRange;
        Creature creature = player.Creature;
        if (Has(player, HbrFateDiceCatalog.LastStand) && creature != null && creature.MaxHp > 0 &&
            creature.CurrentHp * 2m < creature.MaxHp)
            bonus += HbrFateDiceCatalog.LastStandRange;
        return bonus;
    }

    internal static int RawPenalty(Player player) =>
        player?.GetRelic<ElectronicSoldierManual>()?.FateDiceRawPenalty ?? 0;

    private static CombatMarks MarksFor(Player player)
    {
        CombatMarks marks = Marks.GetOrCreateValue(player);
        object combat = player.Creature?.CombatState;
        if (!ReferenceEquals(marks.Combat, combat))
        {
            marks.Combat = combat;
            marks.LastRollRound = int.MinValue;
            marks.RerollUsed = false;
        }
        return marks;
    }

    // Every roll of the turn passes here once; only the first one with 先手 gains a die.
    internal static bool TakeFirstRollOfTurn(Player player)
    {
        if (player?.Creature?.CombatState is not { } combat) return false;
        CombatMarks marks = MarksFor(player);
        bool first = marks.LastRollRound != combat.RoundNumber;
        marks.LastRollRound = combat.RoundNumber;
        return first && Has(player, HbrFateDiceCatalog.FirstMove);
    }

    // 再来: the first raw roll of ten or less in each combat rolls once more.
    internal static bool TryUseReroll(Player player, int raw)
    {
        if (raw > HbrFateDiceCatalog.RerollThreshold || !Has(player, HbrFateDiceCatalog.Reroll) ||
            player.Creature?.CombatState == null)
            return false;
        CombatMarks marks = MarksFor(player);
        if (marks.RerollUsed) return false;
        marks.RerollUsed = true;
        return true;
    }

    internal static int ApplyRawPenalty(Player player, int raw) =>
        System.Math.Max(RollKeywordRules.RollMinimum, raw - RawPenalty(player));

    internal static bool AppliesFloor(Player player, int raw) =>
        raw < HbrFateDiceCatalog.FloorValue && Has(player, HbrFateDiceCatalog.Floor);

    internal static bool AppliesLuckyNumbers(Player player, int raw) =>
        raw is 7 or 77 && player?.Creature?.HasPower<ReptureDualSwordsPower>() == true &&
        Has(player, HbrFateDiceCatalog.LuckyNumbers);

    internal static bool IsRepdigit(int raw) => raw is >= 11 and <= 99 && raw % 11 == 0;

    internal static bool DoublesValues(Player player, int raw) =>
        IsRepdigit(raw) && Has(player, HbrFateDiceCatalog.Mirror);

    internal static bool BlocksAdvantage(Player player) => Has(player, HbrFateDiceCatalog.AllIn);

    // The percentage modifiers the fragments add to every non-advantage roll.
    internal static IEnumerable<(int Percent, string Source)> PercentModifiers(Player player)
    {
        if (Has(player, HbrFateDiceCatalog.Acceleration))
            yield return (HbrFateDiceCatalog.AccelerationPercent, "加速（命运碎片）");
        if (Has(player, HbrFateDiceCatalog.AllIn))
            yield return (HbrFateDiceCatalog.AllInPercent, "孤注（命运碎片）");
        if (Has(player, HbrFateDiceCatalog.ProtagonistPrivilege))
            yield return (HbrFateDiceCatalog.ProtagonistPrivilegePercent, "主人公特典（命运碎片）");
    }

    internal static HbrFateDicePendingEffects EffectsOf(Player player, int raw, bool critical)
    {
        if (player?.GetRelic<ElectronicSoldierManual>() is not { UsesFateDice: true }) return default;
        int draws = raw > 0 && raw % 10 == 0 && Has(player, HbrFateDiceCatalog.RoundNumber) ? 1 : 0;
        int selfDamage = raw <= HbrFateDiceCatalog.WildDieThreshold && Has(player, HbrFateDiceCatalog.WildDie) ? 1 : 0;
        int fragile = critical && player.Creature?.HasPower<ReptureBowPower>() == true &&
            Has(player, HbrFateDiceCatalog.HawkEye) ? 1 : 0;
        return new HbrFateDicePendingEffects(draws, selfDamage, fragile);
    }

    internal static async Task Resolve(PlayerChoiceContext choiceContext, Player player, HbrFateDicePendingEffects effects)
    {
        Creature creature = player?.Creature;
        if (effects.IsEmpty || creature?.CombatState == null || !creature.IsAlive) return;
        // 狂骰: fixed self-damage, ×100 under HBR numerics like every fixed HP cost.
        for (int hit = 0; hit < effects.SelfDamageHits && creature.IsAlive; hit++)
            await CreatureCmd.Damage(choiceContext, creature,
                HbrResourceValueHelper.Fixed(creature, HbrFateDiceCatalog.WildDieSelfDamage), ValueProp.Unpowered, (Creature)null);
        // 鹰眼: every enemy gets Fragile.
        for (int burst = 0; burst < effects.FragileBursts && creature.IsAlive; burst++)
            foreach (Creature enemy in creature.CombatState.GetOpponentsOf(creature).Where(enemy => enemy.IsAlive).ToList())
                await PowerCmd.Apply<FragilePower>(choiceContext, enemy, HbrFateDiceCatalog.HawkEyeFragile, creature, null);
        if (effects.Draws > 0 && creature.IsAlive)
            await CardPileCmd.Draw(choiceContext, effects.Draws, player);
    }
}
