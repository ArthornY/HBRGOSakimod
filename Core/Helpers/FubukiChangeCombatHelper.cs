using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class FubukiChangeCombatHelper
{
    private sealed class Battle
    {
        internal readonly Dictionary<Player, Dictionary<(int Number, object Token), HashSet<CardType>>> Turns = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    internal sealed class Receipt : IDisposable
    {
        internal MakeAChangeKillYourselfHbrgoSaki Card;
        internal Receipt Previous;
        internal bool Active = true, Completed;
        internal decimal ResourceDamage;
        internal readonly HashSet<DamageResult> Results = new();
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    internal static Receipt Begin(MakeAChangeKillYourselfHbrgoSaki card)
    { Receipt receipt = new() { Card = card, Previous = Current.Value }; Current.Value = receipt; return receipt; }
    private static HashSet<CardType> GetTurn(Player owner)
    {
        if (owner?.Creature?.CombatState is not { } combat) return null;
        Battle battle = Battles.GetOrCreateValue(combat);
        if (!battle.Turns.TryGetValue(owner, out var turns)) battle.Turns.Add(owner, turns = new());
        int number = owner.PlayerCombatState?.TurnNumber ?? 0;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner);
        // Suspending a normal turn for OD/personal turns must not destroy the
        // normal turn's earlier types when it resumes with its original token.
        if (!turns.TryGetValue((number, token), out var types)) turns.Add((number, token), types = new());
        return types;
    }
    internal static int GetPriorTypes(Player owner) => GetTurn(owner)?.Count ?? 0;
    internal static void AfterPlay(CardPlay play)
    {
        if (play?.Card is not { Owner: not null } card || card.Type is not (CardType.Attack or CardType.Skill or CardType.Power)) return;
        GetTurn(card.Owner)?.Add(card.Type);
    }
    internal static Creature DpTarget(Player owner) => owner?.Character is HbrgoSaki30A
        ? PhantomHelper.GetLivingPhantom(owner) ?? owner.Creature : owner?.Creature;
    internal static (decimal Current, decimal Maximum) Dp(Player owner)
    {
        if (HbrCardEffectPreview.TryGetDp(owner, out decimal current, out decimal maximum)) return (current, maximum);
        Creature target = DpTarget(owner);
        if (target == null) return (0m, 0m);
        if (target.Player != null)
        {
            DeflectorProtection protection = owner.GetRelic<DeflectorProtection>();
            protection?.EnsureDpInitialized();
            return (protection?.Dp ?? 0, protection?.MaxDp ?? 0);
        }
        var shell = target.GetPower<HighDimensionalShellPower>();
        return (shell?.CurrentDp ?? 0m, shell?.MaxAmount ?? (target.Monster is HBRGOSakiMod.Core.Models.Monsters.NararaShibaBossMonster
            ? NararaLayeredDpPower.CalculateInitialAmount(target) : PhantomHighDimensionalShellPower.CalculateInitialAmount(target)));
    }
    private static decimal LoseDirect(Player owner, decimal requested)
    {
        Creature target = DpTarget(owner);
        if (target?.Player != null) return owner.GetRelic<DeflectorProtection>()?.LoseDpDirect(requested) ?? 0m;
        if (target?.GetPower<HighDimensionalShellPower>() is not { } shell) return 0m;
        decimal paid = Math.Min(shell.CurrentDp, Math.Max(0m, decimal.Floor(requested)));
        shell.SetDpAmount(shell.CurrentDp - paid);
        return paid;
    }
    internal static decimal PayBaseCost(MakeAChangeKillYourselfHbrgoSaki card)
    {
        var dp = Dp(card.Owner);
        return dp.Current <= 0m || dp.Maximum <= 0m ? 0m : LoseDirect(card.Owner,
            dp.Maximum * (card.HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.Flash) ? .05m : .1m));
    }
    internal static void PayCompensation(MakeAChangeKillYourselfHbrgoSaki card)
    {
        var dp = Dp(card.Owner);
        if (dp.Current <= 0m || dp.Maximum <= 0m) return;
        decimal paid = LoseDirect(card.Owner, dp.Current / 2m);
        if (paid > 0m) card.Owner.GetRelic<Overclock>()?.GainLevelProgress(paid / dp.Maximum * 200m);
    }
    internal static decimal GiftMultiplier(MakeAChangeKillYourselfHbrgoSaki card)
    {
        if (!card.HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.GiftForYou)) return 1m;
        var dp = Dp(card.Owner);
        return 1m + .75m * (1m - (dp.Maximum > 0m ? Math.Max(0m, dp.Current / dp.Maximum) : 0m));
    }
    internal static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is MakeAChangeKillYourselfHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static bool IsOwnAttack(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return false;
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, source)) return true;
        return false;
    }
    internal static void RecordDamage(CardModel source, DamageResult result)
    {
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && !receipt.Completed && ReferenceEquals(receipt.Card, source))
            {
                if (result != null && receipt.Results.Add(result)) receipt.ResourceDamage =
                    DamageAmplificationLimits.AddSaturated(receipt.ResourceDamage, CombatAttackHitHelper.GetResourceDamage(result));
                return;
            }
    }
    internal static Task CompleteAttack(PlayerChoiceContext context, Receipt receipt)
    {
        if (!receipt.Active || receipt.Completed) return Task.CompletedTask;
        receipt.Completed = true;
        return RestoreDamage(context, receipt.Card, receipt.ResourceDamage);
    }
    internal static decimal GetConditionalDamageMultiplier(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) ? GiftMultiplier((MakeAChangeKillYourselfHbrgoSaki)source) : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props) =>
        Applies(source, dealer, target, props) ? (((MakeAChangeKillYourselfHbrgoSaki)source)
            .HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.RestInPeace) ? 1.4m : 1m) /
            HbrDestructionGainHelper.DefaultSkillCoefficient : 1m;
    internal static async Task RestoreDamage(PlayerChoiceContext context, CardModel source, decimal resourceDamage)
    {
        if (resourceDamage <= 0m || source is not MakeAChangeKillYourselfHbrgoSaki card ||
            !card.HasUpgradeEffect(MakeAChangeKillYourselfHbrgoSaki.GiftForYou)) return;
        await SeiraDpRecoveryHelper.RestoreAmount(context, DpTarget(card.Owner),
            decimal.Floor(resourceDamage * .3m), false, card);
    }
}
