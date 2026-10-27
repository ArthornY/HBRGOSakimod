using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruCannonSupportUpgradeHelper
{
    internal sealed class PhaseScope : IDisposable
    {
        internal PhaseScope Previous;
        internal Creature Cannon;
        internal HotaruCannonPhase Phase;
        internal bool SuppressCritical;
        internal bool Active = true;
        public void Dispose()
        {
            Active = false;
            if (ReferenceEquals(CurrentPhase.Value, this)) CurrentPhase.Value = Previous;
        }
    }

    private sealed class RollDto
    {
        public RollDto() { }
        public int Base { get; set; }
        public int Final { get; set; }
        public string Source { get; set; }
        public bool Forced { get; set; }
        public SoulRollTier? Tier { get; set; }
        public BonusDto[] Bonuses { get; set; } = Array.Empty<BonusDto>();
        internal static RollDto From(SoulRollBreakdown roll) => new()
        {
            Base = roll.BaseRoll, Final = roll.FinalRoll, Source = roll.DiceSource,
            Forced = roll.IsForcedCritical, Tier = roll.ResolvedTier,
            Bonuses = roll.Bonuses.Select(b => new BonusDto { Amount = b.Amount, Source = b.Source, Percent = b.IsPercentage }).ToArray()
        };
        internal SoulRollBreakdown ToRoll() => new(Base, Final, Source, (Bonuses ?? Array.Empty<BonusDto>())
            .Select(b => new SoulRollBonusDetail(b.Amount, b.Source, b.Percent)))
            { IsForcedCritical = Forced, ResolvedTier = Tier };
    }
    private sealed class BonusDto
    {
        public BonusDto() { }
        public int Amount { get; set; }
        public string Source { get; set; }
        public bool Percent { get; set; }
    }
    private sealed class CacheDto
    {
        public CacheDto() { }
        public RollDto Winning { get; set; }
        public RollDto[] Discarded { get; set; } = Array.Empty<RollDto>();
        public bool Committed { get; set; }

        // These immutable breakdowns are derived from saved dice data, never
        // from the owner's current powers or resulting damage multiplier.
        private SoulRollBreakdown _winningRoll;
        private SoulRollBreakdown[] _discardedRolls;
        private (SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded)? _nonCriticalResult;
        internal SoulRollBreakdown WinningRoll => _winningRoll ??= Winning?.ToRoll();
        internal SoulRollBreakdown[] DiscardedRolls => _discardedRolls ??=
            (Discarded ?? Array.Empty<RollDto>()).Select(roll => roll.ToRoll()).ToArray();

        // The reward action reads its own derived result. Keep the saved dice
        // and next normal phase intact; compute this once per cached phase.
        internal (SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded) NonCriticalResult
        {
            get
            {
                if (_nonCriticalResult.HasValue) return _nonCriticalResult.Value;
                SoulRollBreakdown[] rolls = new[] { WinningRoll }.Concat(DiscardedRolls)
                    .Select(RollKeywordRules.WithoutCritical).ToArray();
                int winner = 0;
                for (int index = 1; index < rolls.Length; index++)
                    if (rolls[index].FinalRoll > rolls[winner].FinalRoll) winner = index;
                _nonCriticalResult = (rolls[winner], rolls.Where((_, index) => index != winner).ToArray());
                return _nonCriticalResult.Value;
            }
        }
    }

    private sealed class ParsedPhaseCache
    {
        internal bool Initialized;
        internal string Json;
        internal CacheDto Value;
    }
    private sealed class ParsedStateCache
    {
        internal readonly ParsedPhaseCache Start = new();
        internal readonly ParsedPhaseCache End = new();
        internal ParsedPhaseCache For(HotaruCannonPhase phase) => phase == HotaruCannonPhase.Start ? Start : End;
    }

    private static readonly AsyncLocal<PhaseScope> CurrentPhase = new();
    private static readonly ConditionalWeakTable<HotaruCannonState, ParsedStateCache> ParsedRolls = new();

    internal static PhaseScope BeginPhase(Creature cannon, HotaruCannonPhase phase, bool suppressCritical = false)
    {
        PhaseScope scope = new() { Previous = CurrentPhase.Value, Cannon = cannon, Phase = phase,
            SuppressCritical = suppressCritical };
        CurrentPhase.Value = scope;
        return scope;
    }

    private static CacheDto ReadCache(HotaruCannonState state, HotaruCannonPhase phase)
    {
        ParsedPhaseCache parsed = ParsedRolls.GetValue(state, static _ => new ParsedStateCache()).For(phase);
        string json = state.GetRollData(phase);
        if (parsed.Initialized && string.Equals(parsed.Json, json, StringComparison.Ordinal)) return parsed.Value;
        CacheDto value;
        try { value = JsonSerializer.Deserialize<CacheDto>(json); }
        catch (JsonException) { value = null; }
        parsed.Json = json;
        parsed.Value = value;
        parsed.Initialized = true;
        return value;
    }

    private static void WriteCache(HotaruCannonState state, HotaruCannonPhase phase, CacheDto cache)
    {
        string json = JsonSerializer.Serialize(cache);
        state.SetRollData(phase, json);
        ParsedPhaseCache parsed = ParsedRolls.GetValue(state, static _ => new ParsedStateCache()).For(phase);
        parsed.Json = json;
        parsed.Value = cache;
        parsed.Initialized = true;
    }

    private static async Task<CacheDto> PrepareCache(PlayerChoiceContext context, Creature cannon, HotaruCannonPhase phase, CardType type)
    {
        Player owner = cannon.PetOwner;
        var roll = await RollGlossaryCapability.RollForCannon(context, owner, type);
        CacheDto cache = new() { Winning = RollDto.From(roll.Winning),
            Discarded = roll.Discarded.Select(RollDto.From).ToArray() };
        WriteCache(owner.GetRelic<Millsage>().StateFor(((HotaruCannonModel)cannon.Monster).Kind), phase, cache);
        return cache;
    }

    internal static async Task PreparePhase(PlayerChoiceContext context, Creature cannon, HotaruCannonPhase phase)
    {
        Millsage relic = cannon?.PetOwner?.GetRelic<Millsage>();
        if (HotaruUncommonCannonHelper.CurrentAction(cannon) is { } action &&
            (action.RollBonuses.Length > 0 || action.Modifier?.InheritedRoll != null ||
             action.Modifier?.AdvantageRoll == true || (action.Modifier?.RollAmplificationMultiplier ?? 1m) != 1m))
        {
            await PrepareEnhancedRoll(context, cannon, phase, action);
            return;
        }
        if (cannon?.Monster is not HotaruCannonModel model ||
            relic?.HasCannonUpgrade(HotaruCannonKind.Longing, 7) != true ||
            ReadCache(relic.StateFor(model.Kind), phase)?.Winning != null) return;
        await PrepareCache(context, cannon, phase, PhaseRollType(model.Kind, phase));
    }

    internal static decimal GetCannonRollMultiplier(Creature cannon, CardType type,
        bool resolve = false, HotaruCannonPhase? previewPhase = null)
    {
        Player owner = cannon?.PetOwner;
        Millsage relic = owner?.GetRelic<Millsage>();
        if (!HotaruCannonStatsHelper.IsDamagePreview && !HotaruCannonBlockHelper.IsBlockPreview &&
            HotaruUncommonCannonHelper.CurrentAction(cannon) is { Winning: not null } enhanced)
            return EnhancedMultiplier(cannon, type, resolve, enhanced);
        if (cannon?.Monster is not HotaruCannonModel model ||
            relic?.HasCannonUpgrade(HotaruCannonKind.Longing, 7) != true) return 1m;
        PhaseScope scope = CurrentPhase.Value;
        HotaruCannonPhase phase = previewPhase ?? (scope?.Active == true && ReferenceEquals(scope.Cannon, cannon)
            ? scope.Phase : type == CardType.Skill ? HotaruCannonPhase.Start : HotaruCannonPhase.End);
        HotaruCannonState state = relic.StateFor(model.Kind);
        CacheDto cache = ReadCache(state, phase);
        if (cache?.Winning == null)
        {
            // Preview never advances RNG. Combat start or the real phase
            // supplies the cache, including one-time old-save migration.
            return 1m;
        }
        bool suppressCritical = scope?.Active == true && ReferenceEquals(scope.Cannon, cannon) &&
            scope.Phase == phase && scope.SuppressCritical && !HotaruCannonStatsHelper.IsDamagePreview &&
            !HotaruCannonBlockHelper.IsBlockPreview;
        SoulRollBreakdown winning = suppressCritical ? cache.NonCriticalResult.Winning : cache.WinningRoll;
        IReadOnlyList<SoulRollBreakdown> discarded = suppressCritical ? cache.NonCriticalResult.Discarded : cache.DiscardedRolls;
        if (resolve && !cache.Committed)
        {
            cache.Committed = true;
            WriteCache(state, phase, cache);
            RollGlossaryCapability.CommitCannonRoll(owner, winning, discarded);
        }
        decimal multiplier = RollKeywordRules.GetAttackOrSkillValueMultiplier(owner, winning, type);
        return DamageAmplificationLimits.AddSaturated(1m, DamageAmplificationLimits.MultiplySaturated(
            multiplier - 1m, HotaruSeeYouAgainHelper.GetMultiplier(owner)));
    }

    private static async Task PrepareEnhancedRoll(PlayerChoiceContext context, Creature cannon,
        HotaruCannonPhase phase, HotaruUncommonCannonHelper.ActionScope action)
    {
        if (action.Winning != null) return;
        var owner = cannon.PetOwner;
        var relic = owner.GetRelic<Millsage>();
        var model = (HotaruCannonModel)cannon.Monster;
        action.RollWasNative = relic.HasCannonUpgrade(HotaruCannonKind.Longing, 7);
        SoulRollBreakdown winning;
        IReadOnlyList<SoulRollBreakdown> discarded;
        if (action.Modifier?.InheritedRoll is { } inherited)
        {
            var rolled = await RollGlossaryCapability.RollForCannon(context, owner,
                PhaseRollType(model.Kind, phase), inheritedRoll: inherited);
            winning = rolled.Winning;
            discarded = rolled.Discarded;
        }
        else if (action.Modifier?.AdvantageRoll == true)
        {
            var rolled = await RollGlossaryCapability.RollForCannon(context, owner, PhaseRollType(model.Kind, phase), forceAdvantage: true);
            winning = rolled.Winning;
            discarded = rolled.Discarded;
        }
        else if (action.RollWasNative)
        {
            CacheDto cached = ReadCache(relic.StateFor(model.Kind), phase);
            if (cached?.Winning == null || cached.Committed)
                cached = await PrepareCache(context, cannon, phase, PhaseRollType(model.Kind, phase));
            winning = cached.WinningRoll;
            discarded = cached.DiscardedRolls;
            action.BorrowedPhase = phase;
        }
        else
        {
            var rolled = await RollGlossaryCapability.RollForCannon(context, owner, PhaseRollType(model.Kind, phase));
            winning = rolled.Winning;
            discarded = rolled.Discarded;
        }
        action.Winning = action.RollBonuses.Length == 0 ? winning : Enhance(winning, owner, action);
        action.Discarded = action.RollBonuses.Length == 0 ? discarded : discarded.Select(roll => Enhance(roll, owner, action)).ToArray();
    }

    private static SoulRollBreakdown Enhance(SoulRollBreakdown roll, Player owner,
        HotaruUncommonCannonHelper.ActionScope action)
    {
        long final = roll.FinalRoll;
        foreach (int bonus in action.RollBonuses)
            final = action.RollWasNative ? Math.Min(int.MaxValue, final * 2L)
                : Math.Min(int.MaxValue, final + bonus);
        var details = roll.Bonuses.Concat(new[] { new SoulRollBonusDetail(
            (int)final - roll.FinalRoll, "交给那束光") });
        return RollKeywordRules.ResolveRollTier(owner, new SoulRollBreakdown(roll.BaseRoll,
            (int)final, roll.DiceSource, details) { IsForcedCritical = roll.IsForcedCritical });
    }

    private static decimal EnhancedMultiplier(Creature cannon, CardType type, bool resolve,
        HotaruUncommonCannonHelper.ActionScope action)
    {
        var owner = cannon.PetOwner;
        bool suppress = CurrentPhase.Value is { Active: true, SuppressCritical: true } phase &&
            ReferenceEquals(phase.Cannon, cannon);
        SoulRollBreakdown winning = suppress ? RollKeywordRules.WithoutCritical(action.Winning) : action.Winning;
        var discarded = suppress ? action.Discarded.Select(RollKeywordRules.WithoutCritical).ToArray() : action.Discarded;
        if (resolve && !action.Committed)
        {
            action.Committed = true;
            if (action.BorrowedPhase is { } borrowed)
            {
                var state = owner.GetRelic<Millsage>().StateFor(((HotaruCannonModel)cannon.Monster).Kind);
                var cache = ReadCache(state, borrowed);
                if (cache != null) { cache.Committed = true; WriteCache(state, borrowed, cache); }
            }
            RollGlossaryCapability.CommitCannonRoll(owner, winning, discarded);
        }
        decimal multiplier = RollKeywordRules.GetAttackOrSkillValueMultiplier(owner, winning, type);
        decimal scale = DamageAmplificationLimits.MultiplySaturated(HotaruSeeYouAgainHelper.GetMultiplier(owner),
            action.Modifier?.RollAmplificationMultiplier ?? 1m);
        return DamageAmplificationLimits.AddSaturated(1m, DamageAmplificationLimits.MultiplySaturated(multiplier - 1m, scale));
    }

    internal static int GetRollPercentBonus(Player player, CardType? type)
    {
        Millsage relic = player?.GetRelic<Millsage>();
        if (relic == null) return 0;
        decimal bonus = relic.HasCannonUpgrade(HotaruCannonKind.Longing, 3)
            ? DamageAmplificationLimits.MultiplySaturated(20m, HotaruSeeYouAgainHelper.GetMultiplier(player)) : 0m;
        if (type == CardType.Skill && relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 4))
            bonus = DamageAmplificationLimits.AddSaturated(bonus, 30m);
        if (type == CardType.Attack && relic.HasCannonUpgrade(HotaruCannonKind.Absolution, 2))
            bonus = DamageAmplificationLimits.AddSaturated(bonus, 30m);
        return BigCombatNumbers.ToNativeSigned(bonus);
    }

    internal static decimal GetBlockMultiplier(Creature cannon) =>
        cannon?.Monster is HotaruCoffinCannon && cannon.PetOwner?.GetRelic<Millsage>()
            ?.HasCannonUpgrade(HotaruCannonKind.Coffin, 1) == true ? 1.5m : 1m;

    internal static async Task OnCombatStart(Millsage relic)
    {
        Player owner = relic?.Owner;
        Creature creature = owner?.Creature;
        if (creature?.IsAlive != true || creature.CombatState == null) return;
        PlayerChoiceContext context = new BlockingPlayerChoiceContext();
        if (relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 3))
        {
            decimal recovery = HbrResourceValueHelper.ConvertFixedRecovery(creature, 6m);
            decimal overflow = Math.Max(0m, recovery - Math.Max(0m, creature.MaxHp - creature.CurrentHp));
            await CreatureCmd.Heal(creature, recovery);
            if (overflow > 0m && !creature.HasPower<LastStandPower>())
                owner.GetRelic<DeflectorProtection>()?.RestoreDp((int)Math.Min(int.MaxValue, decimal.Floor(overflow)));
        }
        if (relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 5) &&
            owner.GetRelic<DeflectorProtection>() is { } deflector)
        {
            deflector.EnsureDpInitialized();
            if (deflector.MaxDp > 0)
                await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, creature, deflector.MaxDp, creature, null);
        }
        if (relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 7))
            await PowerCmd.Apply<BarricadePower>(context, creature, 1m, creature, null);
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 2))
            await PowerCmd.Apply<DexterityPower>(context, creature, 2m, creature, null);
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 7))
            foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
            {
                Creature cannon = relic.GetCannon(kind);
                if (cannon == null) continue;
                foreach (HotaruCannonPhase phase in Enum.GetValues<HotaruCannonPhase>())
                    if (HotaruCannonState.HasEffect(kind, phase))
                        await PrepareCache(context, cannon, phase, PhaseRollType(kind, phase));
            }
    }

    internal static async Task AfterPhase(PlayerChoiceContext context, Millsage relic,
        HotaruCannonKind kind, HotaruCannonPhase phase, CardPlay cardPlay = null)
    {
        // Drawing can auto-play a card that stimulates this same cannon. Give
        // that next phase its own cache before entering the draw command.
        if (relic?.Owner?.Creature?.IsAlive == true && relic.HasCannonUpgrade(HotaruCannonKind.Longing, 7) &&
            relic.GetCannon(kind) is { } cannon && ReadCache(relic.StateFor(kind), phase)?.Committed == true)
            await PrepareCache(context, cannon, phase, PhaseRollType(kind, phase));
        if (kind == HotaruCannonKind.Longing && HotaruCannonState.HasEffect(kind, phase) &&
            relic?.Owner?.Creature?.IsAlive == true && relic.HasCannonUpgrade(HotaruCannonKind.Longing, 1))
            await CardPileCmd.Draw(context, BigCombatNumbers.ToNativeSigned(
                HotaruSeeYouAgainHelper.GetMultiplier(relic.Owner)), relic.Owner);
    }

    private static CardType PhaseRollType(HotaruCannonKind kind, HotaruCannonPhase phase) =>
        phase == HotaruCannonPhase.Start && kind is HotaruCannonKind.Coffin or HotaruCannonKind.Longing
            ? CardType.Skill : CardType.Attack;

    internal static async Task AfterOwnerDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        Player owner = receipt?.Target?.Player;
        if (receipt == null || receipt.IsReaction || !receipt.IsPrimary || receipt.ActualDamage <= 0m ||
            owner?.GetRelic<Millsage>()?.HasCannonUpgrade(HotaruCannonKind.Coffin, 2) != true ||
            !Millsage.IsEnemy(receipt.Dealer, owner)) return;
        await PowerCmd.Apply<DoomPower>(context, receipt.Dealer, receipt.ActualDamage,
            owner.Creature, receipt.CardSource);
    }

    internal static async Task AfterCannonDamage(PlayerChoiceContext context, CannonHitReceipt receipt)
    {
        Player owner = receipt?.Cannon?.PetOwner;
        Millsage relic = owner?.GetRelic<Millsage>();
        if (receipt == null || receipt.ActualDamage <= 0m ||
            owner?.Creature?.IsAlive != true || relic == null) return;
        int effectAmount = BigCombatNumbers.ToNativeSigned(HotaruSeeYouAgainHelper.GetMultiplier(owner));
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 5))
            await PowerCmd.Apply<DodgePower>(context, owner.Creature, effectAmount, owner.Creature, receipt.CardSource);
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 6))
            await SixSenseCombatHelper.ResolvePhantomAttack(context, owner, receipt.Target, receipt.CardSource, effectAmount);
    }

    internal static void OnDpRecoveryOverflow(DeflectorProtection deflector, int overflow)
    {
        if (overflow <= 0 || deflector?.Owner?.GetRelic<Millsage>()
            ?.HasCannonUpgrade(HotaruCannonKind.Longing, 4) != true) return;
        int gain = (int)Math.Min(int.MaxValue, decimal.Floor(DamageAmplificationLimits.MultiplySaturated(
            overflow * 0.2m, HotaruSeeYouAgainHelper.GetMultiplier(deflector.Owner))));
        if (gain > 0) deflector.GainMaxDp(gain);
    }
}
