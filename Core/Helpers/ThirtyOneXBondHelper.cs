using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneXBondHelper
{
    private sealed class Battle
    {
        internal bool Closed;
        internal readonly Dictionary<Player, uint> Members = new();
        internal readonly HashSet<Player> Started = new();
        internal readonly HashSet<Creature> AuraSeen = new();
        internal readonly HashSet<Player> Victories = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static bool Eligible(Player player) => player?.Creature is { IsAlive: true } &&
        player.GetRelic<ThirtyOneXCollection>() is { IsMelted: false } && HbrBattleStatsHelper.IsEnabled(player);
    private static Battle State(ICombatState combat) => Battles.GetValue(combat, state =>
    {
        Battle battle = new();
        foreach (Player player in state.Players)
            if (Eligible(player)) battle.Members[player] = ThirtyOneXKeywordRules.GetPermanentMemberMask(player);
        return battle;
    });
    internal static uint Members(Player player)
    {
        if (!Eligible(player)) return 0;
        if (player.Creature.CombatState is not { } combat) return ThirtyOneXKeywordRules.GetPermanentMemberMask(player);
        if (!Battles.TryGetValue(combat, out Battle battle)) return ThirtyOneXKeywordRules.GetPermanentMemberMask(player);
        return battle.Closed ? ThirtyOneXKeywordRules.GetPermanentMemberMask(player) :
            battle.Members.TryGetValue(player, out uint value) ? value : 0;
    }
    internal static int Tier(uint members) => BitOperations.PopCount(members) switch { >= 6 => 3, >= 4 => 2, >= 2 => 1, _ => 0 };
    internal static bool Has(uint members, ThirtyOneXMember member) => (members & (1u << (int)member)) != 0;
    internal static decimal MemberPercent(Player player, ThirtyOneXMember member, decimal basis)
    {
        uint members = Members(player);
        return Has(members, member) ? basis * Tier(members) : 0m;
    }
    internal static decimal GetDamageMultiplier(Creature target, Creature dealer, ValueProp props)
    {
        Player owner = dealer?.Player ?? dealer?.PetOwner;
        if (target == null || dealer == null || target.Side == dealer.Side || !props.IsPoweredAttack()) return 1m;
        if (dealer.CombatState is { } combat && Battles.TryGetValue(combat, out Battle battle) && battle.Closed) return 1m;
        bool dp = !HpOnlyDamageContext.BypassesDpForBlockReceiver(target) && RanaByakkoCombatHelper.HasDp(target);
        return 1m + MemberPercent(owner, dp ? ThirtyOneXMember.IreneRedmayne : ThirtyOneXMember.CarolReaper, 10m) / 100m;
    }
    internal static decimal ScaleMaximumDp(Creature target, decimal maximum)
    {
        Player owner = target?.Player ?? target?.PetOwner;
        if (target?.CombatState is not { } combat || !Eligible(owner) ||
            target.Player == null && !PhantomHelper.IsOwnedMainPhantom(target, owner)) return maximum;
        if (!Battles.TryGetValue(combat, out Battle battle) || battle.Closed || !battle.Started.Contains(owner)) return maximum;
        return decimal.Floor(BigCombatNumbers.ClampStat(DamageAmplificationLimits.MultiplySaturated(maximum,
            1m + MemberPercent(owner, ThirtyOneXMember.CharlottaSkopovskaya, 5m) / 100m)));
    }
    internal static async Task Start(Player owner)
    {
        if (!Eligible(owner) || owner.Creature.CombatState is not { } combat) return;
        Battle battle = State(combat);
        if (battle.Closed || battle.Started.Contains(owner)) return;
        // Enable the cap only after taking the old value. This is independent
        // of the ordering of DeflectorProtection's own combat-start callback.
        DeflectorProtection protection = owner.GetRelic<DeflectorProtection>();
        protection?.EnsureDpInitialized();
        int oldMaximum = protection?.MaxDp ?? 0;
        Creature phantom = PhantomHelper.GetLivingPhantom(owner);
        HighDimensionalShellPower shell = phantom?.GetPower<HighDimensionalShellPower>();
        decimal oldShellMaximum = shell?.MaximumDp ?? 0m;
        battle.Started.Add(owner);
        if (protection != null) protection.AddDpForMaximumIncrease(Math.Max(0, protection.MaxDp - oldMaximum));
        if (shell != null && shell.MaximumDp > oldShellMaximum)
            shell.SetDpAmount(DamageAmplificationLimits.AddSaturated(shell.CurrentDp, shell.MaximumDp - oldShellMaximum));
        await ApplyAura(combat);
    }
    internal static async Task ApplyAura(ICombatState combat, Creature added = null)
    {
        if (combat == null) return;
        if (!Battles.TryGetValue(combat, out Battle battle) || battle.Closed || battle.Started.Count == 0) return;
        Player[] sources = battle.Members.Keys.Where(player => Eligible(player) &&
            MemberPercent(player, ThirtyOneXMember.VritikaBalakrishnan, 5m) > 0m).OrderBy(player => player.NetId).ToArray();
        if (sources.Length == 0) return;
        IEnumerable<Creature> targets = added == null ? combat.Creatures : new[] { added };
        foreach (Creature target in targets.Where(target => target.IsAlive && target.Side != sources[0].Creature.Side).ToArray())
        {
            if (!battle.AuraSeen.Add(target)) continue;
            Player best = null; decimal potency = decimal.MinValue;
            foreach (Player source in sources)
            {
                decimal effect = HbrPercentDebuffHelper.ResolveBasis(ModelDb.Power<HbrAttackDownPower>(),
                    MemberPercent(source, ThirtyOneXMember.VritikaBalakrishnan, 5m), source.Creature, target);
                if (effect > potency) { potency = effect; best = source; }
            }
            await HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(new ThrowingPlayerChoiceContext(), target,
                MemberPercent(best, ThirtyOneXMember.VritikaBalakrishnan, 5m), best.Creature, null,
                new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1));
        }
    }
    internal static decimal OpeningDraw(Player owner) => owner?.PlayerCombatState?.TurnNumber == 1 &&
        OverclockImmediateTurnCoordinator.GetTurnToken(owner) == null
            ? MemberPercent(owner, ThirtyOneXMember.LiYingxia, 1m) : 0m;
    internal static async Task Victory(Player owner)
    {
        if (!Eligible(owner) || owner.Creature.CombatState is not { } combat) return;
        Battle battle = State(combat);
        if (battle.Closed || !battle.Victories.Add(owner)) return;
        decimal percent = MemberPercent(owner, ThirtyOneXMember.MariaDeAngelis, 3m);
        if (percent > 0m) await CreatureCmd.Heal(owner.Creature,
            decimal.Floor(LargeCreatureHealthHelper.GetMaxHp(owner.Creature) * percent / 100m));
    }
    internal static void End(Player owner)
    {
        if (owner?.Creature.CombatState is not { } combat || !Battles.TryGetValue(combat, out Battle battle)) return;
        battle.Closed = true;
        owner.GetRelic<DeflectorProtection>()?.SyncAfterEquipmentDpChanged();
        PhantomHelper.GetLivingPhantom(owner)?.GetPower<HighDimensionalShellPower>()?.ClampToMaximum();
    }
}
