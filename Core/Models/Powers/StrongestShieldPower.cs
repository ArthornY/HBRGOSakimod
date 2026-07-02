using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class StrongestShieldPower : PowerModel
{
    private const int _rollMin = 1;
    private const int _rollMax = 100;
    private const int _rollExclusiveMax = _rollMax + 1;
    private const decimal _baseThreshold = 33m;
    private const decimal _thresholdMultiplier = 1.5m;
    private const decimal _damageMultiplier = 0.5m;

    private int _pendingSwordheartAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    private int PendingSwordheartAmount
    {
        get => _pendingSwordheartAmount;
        set
        {
            AssertMutable();
            _pendingSwordheartAmount = value;
        }
    }

    public static decimal ModifyIncomingAttackDamage(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource)
    {
        if (amount <= 0m || !props.IsPoweredAttack())
        {
            return amount;
        }

        StrongestShieldPower strongestShield = target.GetPower<StrongestShieldPower>();
        if (strongestShield == null || !strongestShield.RollSucceeds(target, dealer, amount, props, cardSource))
        {
            return amount;
        }

        strongestShield.Flash();
        return amount * _damageMultiplier;
    }

    private bool RollSucceeds(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource)
    {
        int roll = RollD100(target, dealer, amount, props, cardSource, 0);
        PendingSwordheartAmount += GetSwordheartAmount(roll);
        return roll > GetThreshold();
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || PendingSwordheartAmount <= 0)
        {
            return;
        }

        int swordheartAmount = GetSwordheartAmountToApply(PendingSwordheartAmount);
        PendingSwordheartAmount = 0;
        if (swordheartAmount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner, swordheartAmount, Owner, null);
    }

    private int RollD100(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        int roll = RollDeterministicD100(target, dealer, amount, props, cardSource, rollIndex);
        GuideLittleHand guideLittleHand = Owner.Player?.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            roll = guideLittleHand.TakeHigherRoll(roll, RollDeterministicD100(target, dealer, amount, props, cardSource, rollIndex + 1));
        }

        return roll;
    }

    private int RollDeterministicD100(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        return new Rng(BuildRollSeed(target, dealer, amount, props, cardSource, rollIndex)).NextInt(_rollMin, _rollExclusiveMax);
    }

    private uint BuildRollSeed(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        unchecked
        {
            int hash = (int)Owner.CombatState.RunState.Rng.Seed;
            hash = AppendHash(hash, Owner.Player?.RunState.GetPlayerSlotIndex(Owner.Player) ?? 0);
            hash = AppendHash(hash, RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.Id ?? 0u);
            hash = AppendHash(hash, CombatManager.Instance.History.Entries.Count());
            hash = AppendHash(hash, Owner.CombatState.RoundNumber);
            hash = AppendHash(hash, (int)Owner.CombatState.CurrentSide);
            hash = AppendHash(hash, target.CombatId ?? 0u);
            hash = AppendHash(hash, dealer?.CombatId ?? 0u);
            hash = AppendHash(hash, HashString(target.ModelId.Entry));
            hash = AppendHash(hash, HashString(dealer?.ModelId.Entry ?? string.Empty));
            hash = AppendHash(hash, HashString(cardSource?.Id.Entry ?? string.Empty));
            hash = AppendHash(hash, (int)(amount * 100m));
            hash = AppendHash(hash, (int)props);
            hash = AppendHash(hash, rollIndex);
            return (uint)hash;
        }
    }

    private static int AppendHash(int hash, int value)
    {
        unchecked
        {
            return hash * 397 ^ value;
        }
    }

    private static int AppendHash(int hash, uint value)
    {
        unchecked
        {
            return AppendHash(hash, (int)value);
        }
    }

    private static int HashString(string value)
    {
        unchecked
        {
            int hash = 352654597;
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 397 ^ value[i];
            }

            return hash;
        }
    }

    private decimal GetThreshold()
    {
        decimal threshold = _baseThreshold;
        for (int i = 1; i < Amount; i++)
        {
            threshold *= _thresholdMultiplier;
        }

        return threshold;
    }

    private int GetSwordheartAmount(int roll)
    {
        int amount = roll == _rollMax ? SwordheartPower.BaseMaxAmount : 0;
        amount += CountSevens(roll);
        return Math.Min(amount, SwordheartPower.BaseMaxAmount);
    }

    private int CountSevens(int roll)
    {
        int count = 0;
        for (int value = roll; value > 0; value /= 10)
        {
            if (value % 10 == 7)
            {
                count++;
            }
        }

        return count;
    }

    private int GetSwordheartAmountToApply(int amount)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        int current = swordheart?.Amount ?? 0;
        int remaining = Math.Max(0, SwordheartPower.GetMaxAmount(Owner) - current);
        return Math.Min(amount, remaining);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingSwordheartAmount = 0;
    }
}
