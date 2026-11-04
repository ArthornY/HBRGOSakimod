using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HOTARU_MY_HAPPINESS_POWER")]
public sealed class HotaruMyHappinessPower : PowerModel, ICustomPower
{
    private decimal _baseValueGain;
    private decimal _attackBonus;
    private decimal _skillBonus;
    private int _lastCardType = -1;
    private long _cardStreak;
    private Dictionary<int, decimal> _cannonDamagePercent = new();
    private Dictionary<int, long> _cannonStreaks = new();
    private Dictionary<CardPlay, PendingCardReward> _pendingCards = new();

    public decimal BaseValueGain => _baseValueGain;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    // Exact decimal ledgers and consecutive-action history survive combat saves.
    // Cannon bonuses belong to a kind, so rebuilding an entity does not erase them.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HappinessStateData
    {
        get => JsonSerializer.Serialize(new SavedState
        {
            BaseValueGain = _baseValueGain,
            AttackBonus = _attackBonus,
            SkillBonus = _skillBonus,
            LastCardType = _lastCardType,
            CardStreak = _cardStreak,
            CannonDamagePercent = _cannonDamagePercent,
            CannonStreaks = _cannonStreaks
        });
        private set
        {
            SavedState state;
            try { state = JsonSerializer.Deserialize<SavedState>(value ?? "") ?? new SavedState(); }
            catch (JsonException) { state = new SavedState(); }
            _baseValueGain = Math.Max(0m, state.BaseValueGain);
            _attackBonus = Math.Max(0m, state.AttackBonus);
            _skillBonus = Math.Max(0m, state.SkillBonus);
            _lastCardType = state.LastCardType == (int)CardType.Attack || state.LastCardType == (int)CardType.Skill
                ? state.LastCardType : -1;
            _cardStreak = _lastCardType < 0 ? 0 : Math.Max(0, state.CardStreak);
            _cannonDamagePercent = (state.CannonDamagePercent ?? new())
                .Where(entry => IsCannonKind(entry.Key) && entry.Value > 0m)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            _cannonStreaks = (state.CannonStreaks ?? new())
                .Where(entry => IsCannonKind(entry.Key) && entry.Value > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            _pendingCards = new();
        }
    }

    private HotaruCannonRetirementChoice Form => IsMutable
        ? Owner?.Player?.GetRelic<Millsage>()?.GetCannonRetirementChoice(HotaruCannonKind.Happiness)
            ?? HotaruCannonRetirementChoice.None
        : HotaruCannonRetirementChoice.None;

    public override LocString Title => new("powers", "HOTARU_MY_HAPPINESS_POWER." + (Form switch
    {
        HotaruCannonRetirementChoice.Sacrifice => "sacrificeTitle",
        HotaruCannonRetirementChoice.Redemption => "redemptionTitle",
        _ => "title"
    }));

    public override LocString Description
    {
        get
        {
            LocString text = new("powers", "HOTARU_MY_HAPPINESS_POWER." + (Form switch
            {
                HotaruCannonRetirementChoice.Sacrifice => "sacrificeDescription",
                HotaruCannonRetirementChoice.Redemption => "redemptionDescription",
                _ => "normalDescription"
            }));
            text.Add("BaseValueGain", IsMutable ? BaseValueGain : 5m);
            text.Add("Amount", IsMutable ? Amount : 1);
            text.Add("AttackBonus", _attackBonus);
            text.Add("SkillBonus", _skillBonus);
            text.Add("CannonBonuses", GetCannonBonusesText());
            return text;
        }
    }

    public static async Task Apply(PlayerChoiceContext context, Creature owner,
        int baseValueGain, CardModel source)
    {
        if (baseValueGain <= 0 || owner?.IsAlive != true || owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding) return;

        HotaruMyHappinessPower power = owner.GetPower<HotaruMyHappinessPower>();
        if (power == null)
        {
            power = (HotaruMyHappinessPower)ModelDb.Power<HotaruMyHappinessPower>().ToMutable();
            power._baseValueGain = baseValueGain;
            await PowerCmd.Apply(context, power, owner, 1m, owner, source);
            if (power.Owner == owner && power.Amount > 0)
            {
                power._baseValueGain = DamageAmplificationLimits.MultiplySaturated(baseValueGain, power.Amount);
                power.Refresh();
            }
            return;
        }

        power.AssertMutable();
        int previousAmount = power.Amount;
        decimal previousGain = power.BaseValueGain;
        power._baseValueGain = DamageAmplificationLimits.AddSaturated(previousGain, baseValueGain);
        try { await PowerCmd.ModifyAmount(context, power, 1m, owner, source); }
        finally
        {
            int gainedCopies = Math.Max(0, power.Amount - previousAmount);
            power._baseValueGain = DamageAmplificationLimits.AddSaturated(previousGain,
                DamageAmplificationLimits.MultiplySaturated(baseValueGain, gainedCopies));
            power.Refresh();
        }
    }

    public decimal GetCardBaseValueBonus(CardModel card) =>
        Amount > 0 && card?.Owner?.Creature == Owner
            ? card.Type switch
            {
                CardType.Attack => _attackBonus,
                CardType.Skill => _skillBonus,
                _ => 0m
            }
            : 0m;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay?.Card;
        if (card?.Owner?.Creature != Owner || !CanResolve() || HotaruCannonStatsHelper.IsDamagePreview ||
            _pendingCards.ContainsKey(cardPlay)) return Task.CompletedTask;

        if (Form != HotaruCannonRetirementChoice.Sacrifice)
        {
            _lastCardType = -1;
            _cardStreak = 0;
            return Task.CompletedTask;
        }

        decimal reward = 0m;
        if (card.Type is CardType.Attack or CardType.Skill)
        {
            int type = (int)card.Type;
            long repetitions = _lastCardType == type ? _cardStreak : 0;
            _lastCardType = type;
            _cardStreak = NextCount(repetitions);
            reward = DamageAmplificationLimits.MultiplySaturated(BaseValueGain, repetitions);
        }
        else
        {
            _lastCardType = -1;
            _cardStreak = 0;
        }

        // CardPlay identity, rather than CardModel identity, distinguishes replays.
        // Capture the chronological streak before nested automatic plays start.
        _pendingCards.Add(cardPlay, new PendingCardReward(card.Type, reward));
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay == null || !_pendingCards.Remove(cardPlay, out PendingCardReward reward) ||
            reward.Amount <= 0m || !CanResolve()) return Task.CompletedTask;

        if (reward.Type == CardType.Attack)
            _attackBonus = DamageAmplificationLimits.AddSaturated(_attackBonus, reward.Amount);
        else if (reward.Type == CardType.Skill)
            _skillBonus = DamageAmplificationLimits.AddSaturated(_skillBonus, reward.Amount);
        Refresh();
        return Task.CompletedTask;
    }

    internal Task OnCannonBaseDamageGrowth(PlayerChoiceContext context, Millsage relic,
        Creature cannon, decimal actualIncrease, CardModel source = null, CardPlay play = null)
    {
        if (actualIncrease <= 0m || !CanResolve() || HotaruCannonStatsHelper.IsDamagePreview ||
            Form != HotaruCannonRetirementChoice.None || relic?.Owner != Owner.Player ||
            cannon?.Monster is not HotaruCannonModel { Kind: HotaruCannonKind.Happiness } ||
            !Millsage.IsOwnedCannon(cannon, Owner.Player)) return Task.CompletedTask;

        // Growth receipts use native damage units; this reward counts skill percentage points.
        decimal increase = DamageAmplificationLimits.MultiplySaturated(
            HbrSkillPower.DamagePercent(actualIncrease), Amount);
        HotaruCannonKind[] kinds = Owner.CombatState.Allies
            .Where(ally => Millsage.IsOwnedCannon(ally, Owner.Player))
            .Select(ally => ((HotaruCannonModel)ally.Monster).Kind)
            .Where(kind => kind != HotaruCannonKind.Happiness).Distinct().ToArray();
        foreach (HotaruCannonKind kind in kinds) AddCannonPercent(kind, increase);
        if (kinds.Length > 0) Refresh();
        return Task.CompletedTask;
    }

    public Task OnCannonActions(PlayerChoiceContext context, Millsage relic, IReadOnlyList<Creature> cannons)
    {
        if (!CanResolve() || HotaruCannonStatsHelper.IsDamagePreview || relic?.Owner != Owner.Player)
            return Task.CompletedTask;
        if (Form != HotaruCannonRetirementChoice.Redemption)
        {
            _cannonStreaks.Clear();
            return Task.CompletedTask;
        }

        // One completed simultaneous round is one observation for each kind.
        // Replacing the whole ledger resets every kind absent from this round.
        int[] kinds = (cannons ?? Array.Empty<Creature>())
            .Where(cannon => Millsage.IsOwnedCannon(cannon, Owner.Player))
            .Select(cannon => (int)((HotaruCannonModel)cannon.Monster).Kind).Distinct().ToArray();
        // An empty phase did not perform an action and cannot break continuity.
        if (kinds.Length == 0) return Task.CompletedTask;
        var nextStreaks = new Dictionary<int, long>();
        bool awarded = false;
        foreach (int kind in kinds)
        {
            long repetitions = _cannonStreaks.GetValueOrDefault(kind);
            nextStreaks[kind] = NextCount(repetitions);
            if (repetitions == 0) continue;
            decimal increase = DamageAmplificationLimits.MultiplySaturated(
                DamageAmplificationLimits.MultiplySaturated(10m, repetitions), Amount);
            AddCannonPercent((HotaruCannonKind)kind, increase);
            awarded = true;
        }
        _cannonStreaks = nextStreaks;
        if (awarded) Refresh();
        return Task.CompletedTask;
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource)
    {
        if (Amount <= 0 || props.HasFlag(ValueProp.Unpowered) ||
            !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, cardSource, props, out Creature cannon) ||
            !Millsage.IsOwnedCannon(cannon, Owner?.Player) || cannon.Monster is not HotaruCannonModel model)
            return 1m;
        decimal percent = _cannonDamagePercent.GetValueOrDefault((int)model.Kind);
        return percent > 0m ? HbrgoSakiDamageMultiplierHelper.Add(1m + percent / 100m, this) : 1m;
    }

    private bool CanResolve() => Amount > 0 && Owner?.IsAlive == true && Owner.Player?.PlayerCombatState != null &&
        Owner.CombatState != null && !CombatManager.Instance.IsOverOrEnding;

    private void AddCannonPercent(HotaruCannonKind kind, decimal increase)
    {
        int key = (int)kind;
        _cannonDamagePercent[key] = DamageAmplificationLimits.AddSaturated(
            _cannonDamagePercent.GetValueOrDefault(key), increase);
    }

    private string GetCannonBonusesText()
    {
        if (!IsMutable || Owner?.CombatState == null || Owner.Player?.GetRelic<Millsage>() is not { } relic)
            return "无";
        var bonuses = new List<string>();
        foreach (HotaruCannonKind kind in Enum.GetValues<HotaruCannonKind>())
        {
            Creature cannon = relic.GetCannon(kind);
            if (!Millsage.IsOwnedCannon(cannon, Owner.Player) || cannon.Monster is not HotaruCannonModel model)
                continue;
            decimal percent = _cannonDamagePercent.GetValueOrDefault((int)kind);
            bonuses.Add(model.Title.GetFormattedText() + "：+" + percent.ToString(CultureInfo.InvariantCulture) + "%");
        }
        return bonuses.Count > 0 ? string.Join("\n", bonuses) : "无";
    }

    private void Refresh()
    {
        Flash();
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
    }

    private static bool IsCannonKind(int kind) => Enum.IsDefined(typeof(HotaruCannonKind), kind);
    private static long NextCount(long count) => count < long.MaxValue ? count + 1 : long.MaxValue;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _cannonDamagePercent = new(_cannonDamagePercent);
        _cannonStreaks = new(_cannonStreaks);
        _pendingCards = new();
    }

    private readonly record struct PendingCardReward(CardType Type, decimal Amount);

    private sealed class SavedState
    {
        public decimal BaseValueGain { get; set; }
        public decimal AttackBonus { get; set; }
        public decimal SkillBonus { get; set; }
        public int LastCardType { get; set; } = -1;
        public long CardStreak { get; set; }
        public Dictionary<int, decimal> CannonDamagePercent { get; set; } = new();
        public Dictionary<int, long> CannonStreaks { get; set; } = new();
    }
}
