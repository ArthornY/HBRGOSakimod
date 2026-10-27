using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public abstract class HotaruUncommonCardPower : PowerModel, ICustomPower
{
    private HashSet<string> _ordinaryClaims = new();
    private Dictionary<object, HashSet<string>> _overclockClaims = new(ReferenceEqualityComparer.Instance);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ClaimsTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ClaimedKinds
    {
        get => JsonSerializer.Serialize(_ordinaryClaims);
        private set
        {
            try { _ordinaryClaims = JsonSerializer.Deserialize<HashSet<string>>(value ?? "") ?? new(); }
            catch (JsonException) { _ordinaryClaims = new(); }
        }
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public string CustomPackedIconPath => "res://images/packed/card_portraits/hbrgo_hotaru/default_hotaru.jpg";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal void ShowTriggered() => Flash();

    internal bool TryClaimTurnKey(string key)
    {
        if (Amount <= 0 || !HotaruCommonCannonHelper.CanAct(Owner?.Player)) return false;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        if (token != null)
        {
            if (!_overclockClaims.TryGetValue(token, out HashSet<string> claims))
                _overclockClaims[token] = claims = new();
            return claims.Add(key);
        }
        int turn = Owner.Player.PlayerCombatState.TurnNumber;
        if (ClaimsTurn != turn)
        {
            ClaimsTurn = turn;
            _ordinaryClaims.Clear();
        }
        return _ordinaryClaims.Add(key);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _ordinaryClaims = new(_ordinaryClaims);
        _overclockClaims = new(ReferenceEqualityComparer.Instance);
    }
}

[CustomID("HOTARU_CONVERGING_FRAGMENTS_POWER")]
public sealed class HotaruConvergingFragmentsPower : HotaruUncommonCardPower
{
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel source) =>
        HotaruUncommonCardHelper.OnDebuffApplied(context, this, power, amount, applier, source);
}

[CustomID("HOTARU_LUCKY_FAVOR_POWER")]
public sealed class HotaruLuckyFavorPower : HotaruUncommonCardPower { }

[CustomID("HOTARU_STILL_COVERED_POWER")]
public sealed class HotaruStillCoveredPower : HotaruUncommonCardPower
{
    public override LocString Description => HbrSkillBlockDescription.ForPower(this,
        base.Description, "HOTARU_STILL_COVERED_POWER.hbrDescription", IsMutable ? Amount : 0m);
}

[CustomID("HOTARU_THREE_BREATHS_POWER")]
public sealed class HotaruThreeBreathsPower : HotaruUncommonCardPower { }

// These grants belong to the turn in which they were made. An OD turn has its
// own ledger; it neither spends nor expires the suspended ordinary-turn grants.
public abstract class HotaruTurnLedgerPower : HotaruUncommonCardPower
{
    public sealed class GrantEntry
    {
        public int Value { get; set; }
        public int Remaining { get; set; }
        public int Tag { get; set; }
    }

    private List<GrantEntry> _ordinary = new();
    private Dictionary<object, List<GrantEntry>> _overclock = new(ReferenceEqualityComparer.Instance);
    private int _pendingRemoval;
    private const int Maximum = 999_999_999;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int OrdinaryTurnNumber { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string OrdinaryGrants
    {
        get => JsonSerializer.Serialize(_ordinary);
        private set
        {
            try { _ordinary = JsonSerializer.Deserialize<List<GrantEntry>>(value ?? "") ?? new(); }
            catch (JsonException) { _ordinary = new(); }
            _ordinary = _ordinary.Where(g => g != null && g.Value > 0 && g.Remaining > 0).ToList();
        }
    }

    public override int DisplayAmount => IsMutable ? ActiveAmount : 1;
    internal int ActiveAmount => Math.Min(Math.Max(0, Amount), Sum(GetActiveGrants()));
    internal int ActiveAmountForTag(int tag) => Math.Min(Math.Max(0, Amount),
        Sum(GetActiveGrants()?.Where(entry => entry.Tag == tag)));
    internal int RemainingUses => GetActiveGrants()?.Select(g => g.Remaining).DefaultIfEmpty(0).Max() ?? 0;
    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("ActiveAmount", IsMutable ? ActiveAmount : 1);
            description.Add("Remaining", IsMutable ? RemainingUses : 3);
            return description;
        }
    }

    private static int Sum(IEnumerable<GrantEntry> entries) =>
        (int)Math.Min(Maximum, entries?.Sum(g => (long)g.Value) ?? 0L);

    private List<GrantEntry> GetActiveGrants()
    {
        if (!IsMutable || !HotaruCommonCannonHelper.CanAct(Owner?.Player) || Amount <= 0) return null;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player);
        return token == null
            ? OrdinaryTurnNumber == Owner.Player.PlayerCombatState.TurnNumber ? _ordinary : null
            : _overclock.GetValueOrDefault(token);
    }

    internal static async Task Grant<T>(PlayerChoiceContext context, Creature owner, int value,
        int uses, CardModel source, int tag = 0) where T : HotaruTurnLedgerPower, new()
    {
        if (value <= 0 || uses <= 0 || !HotaruCommonCannonHelper.CanAct(owner?.Player)) return;
        var combat = owner.CombatState;
        object token = OverclockImmediateTurnCoordinator.GetTurnToken(owner.Player);
        int turn = owner.Player.PlayerCombatState.TurnNumber;
        T power = owner.GetPower<T>();
        if (power != null) await power.OnTurnStart(context, token);
        power = owner.GetPower<T>();
        int previous = power?.Amount ?? 0;
        if (power == null)
        {
            power = (T)ModelDb.Power<T>().ToMutable();
            await PowerCmd.Apply(context, power, owner, value, owner, source);
        }
        else await PowerCmd.ModifyAmount(context, power, value, owner, source);
        if (!ReferenceEquals(combat, owner.CombatState) || power.Owner != owner || power.Amount <= previous) return;
        int actual = Math.Min(Maximum, power.Amount - previous);
        List<GrantEntry> list;
        if (token == null)
        {
            power.OrdinaryTurnNumber = turn;
            list = power._ordinary;
        }
        else if (!power._overclock.TryGetValue(token, out list))
            power._overclock[token] = list = new();
        list.Add(new GrantEntry { Value = actual, Remaining = uses, Tag = tag });
        power.Refresh();
    }

    internal List<GrantEntry> SnapshotGrants() => GetActiveGrants()?.ToList() ?? new();

    internal int Claim(IReadOnlyCollection<GrantEntry> eligible = null)
    {
        List<GrantEntry> entries = GetActiveGrants();
        if (entries == null) return 0;
        List<GrantEntry> claimed = eligible == null ? entries : entries.Where(eligible.Contains).ToList();
        int reward = Math.Min(ActiveAmount, Sum(claimed));
        foreach (GrantEntry entry in claimed) entry.Remaining--;
        _pendingRemoval = (int)Math.Min(Maximum, (long)_pendingRemoval + Sum(entries.Where(g => g.Remaining <= 0)));
        entries.RemoveAll(g => g.Remaining <= 0);
        Refresh();
        return reward;
    }

    internal async Task FlushSpent(PlayerChoiceContext context)
    {
        // Claim before the await, so nested callbacks cannot consume twice.
        int remove = _pendingRemoval;
        _pendingRemoval = 0;
        if (remove > 0 && Amount > 0)
            await PowerCmd.ModifyAmount(context, this, -Math.Min(remove, Amount), Owner, null);
        Refresh();
    }

    internal async Task OnTurnStart(PlayerChoiceContext context, object token)
    {
        if (Owner?.Player?.PlayerCombatState == null) return;
        if (OrdinaryTurnNumber != Owner.Player.PlayerCombatState.TurnNumber)
            await OnTurnEnd(context, null);
        // OD data is deliberately not serialized. Remove any orphaned native
        // amount after loading while retaining the saved ordinary grants.
        int tracked = (int)Math.Min(Maximum, (long)Sum(_ordinary) + _overclock.Values.Sum(l => (long)Sum(l)) + _pendingRemoval);
        if (Amount > tracked) await PowerCmd.ModifyAmount(context, this, tracked - Amount, Owner, null);
        Refresh();
    }

    internal async Task OnTurnEnd(PlayerChoiceContext context, object token)
    {
        int expired;
        if (token == null)
        {
            expired = Sum(_ordinary);
            _ordinary.Clear();
        }
        else expired = _overclock.Remove(token, out List<GrantEntry> list) ? Sum(list) : 0;
        _pendingRemoval = (int)Math.Min(Maximum, (long)_pendingRemoval + expired);
        await FlushSpent(context);
    }

    public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants) =>
        Owner?.Player != null && side == Owner.Side && participants.Contains(Owner) &&
        OverclockImmediateTurnCoordinator.GetTurnToken(Owner.Player) == null
            ? OnTurnEnd(context, null) : Task.CompletedTask;

    internal void Refresh()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, true);
        CombatPreviewRefreshHelper.RefreshCombatState(Owner, GetType().Name);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _ordinary = _ordinary.Select(g => new GrantEntry { Value = g.Value, Remaining = g.Remaining, Tag = g.Tag }).ToList();
        _overclock = new(ReferenceEqualityComparer.Instance);
        _pendingRemoval = 0;
    }
}

[CustomID("HOTARU_TRAILING_SILENCE_POWER")]
public sealed class HotaruTrailingSilencePower : HotaruTurnLedgerPower
{
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card?.IsMutable != true || card.Owner?.Creature != Owner || card.Type != CardType.Skill || card.EnergyCost.CostsX ||
            CardPlayContextHelper.IsAutoPlay(card) || ActiveAmount <= 0) return false;
        modifiedCost = Math.Max(0m, originalCost - ActiveAmount);
        return true;
    }
}

[CustomID("HOTARU_BREATHING_RHYTHM_POWER")]
public sealed class HotaruBreathingRhythmPower : HotaruTurnLedgerPower
{
    public override LocString Description => HbrSkillBlockDescription.ForPower(this,
        base.Description, "HOTARU_BREATHING_RHYTHM_POWER.hbrDescription", IsMutable ? ActiveAmount : 0m,
        referenceUnits: "本次生效格挡原值");
}
