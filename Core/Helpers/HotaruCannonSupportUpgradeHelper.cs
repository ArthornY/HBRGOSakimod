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
        internal SoulRollBreakdown WinningRoll => _winningRoll ??= Winning?.ToRoll();
        internal SoulRollBreakdown[] DiscardedRolls => _discardedRolls ??=
            (Discarded ?? Array.Empty<RollDto>()).Select(roll => roll.ToRoll()).ToArray();
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

    internal static PhaseScope BeginPhase(Creature cannon, HotaruCannonPhase phase)
    {
        PhaseScope scope = new() { Previous = CurrentPhase.Value, Cannon = cannon, Phase = phase };
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
        if (resolve && !cache.Committed)
        {
            cache.Committed = true;
            WriteCache(state, phase, cache);
            RollGlossaryCapability.CommitCannonRoll(owner, cache.WinningRoll, cache.DiscardedRolls);
        }
        return RollKeywordRules.GetAttackOrSkillValueMultiplier(owner, cache.WinningRoll, type);
    }

    internal static int GetRollPercentBonus(Player player, CardType? type)
    {
        Millsage relic = player?.GetRelic<Millsage>();
        if (relic == null) return 0;
        int bonus = relic.HasCannonUpgrade(HotaruCannonKind.Longing, 3) ? 20 : 0;
        if (type == CardType.Skill && relic.HasCannonUpgrade(HotaruCannonKind.Coffin, 4)) bonus += 30;
        if (type == CardType.Attack && relic.HasCannonUpgrade(HotaruCannonKind.Absolution, 2)) bonus += 30;
        return bonus;
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
            decimal overflow = Math.Max(0m, 6m - Math.Max(0m, creature.MaxHp - creature.CurrentHp));
            await CreatureCmd.Heal(creature, 6m);
            if (overflow > 0m && !creature.HasPower<LastStandPower>())
                owner.GetRelic<DeflectorProtection>()?.RestoreDp((int)decimal.Floor(overflow));
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
            await CardPileCmd.Draw(context, 1m, relic.Owner);
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
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 5))
            await PowerCmd.Apply<DodgePower>(context, owner.Creature, 1m, owner.Creature, receipt.CardSource);
        if (relic.HasCannonUpgrade(HotaruCannonKind.Longing, 6))
            await SixSenseCombatHelper.ResolvePhantomAttack(context, owner, receipt.Target, receipt.CardSource);
    }

    internal static void OnDpRecoveryOverflow(DeflectorProtection deflector, int overflow)
    {
        if (overflow <= 0 || deflector?.Owner?.GetRelic<Millsage>()
            ?.HasCannonUpgrade(HotaruCannonKind.Longing, 4) != true) return;
        int gain = (int)decimal.Floor(overflow * 0.2m);
        if (gain > 0) deflector.GainMaxDp(gain);
    }
}
