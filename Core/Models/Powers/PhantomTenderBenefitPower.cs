using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomTenderBenefitPower : PowerModel, ICustomPower
{
    public string CustomPackedIconPath => ModelDb.Power<StrengthPower>().IconPath;
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    private int _cardsPlayed;
    private long _strengthGained;
    private long _dexterityGained;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => _cardsPlayed;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _cardsPlayed = 0;
        _strengthGained = 0;
        _dexterityGained = 0;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Player == null || cardPlay.Card.Owner != Owner.Player)
            return;

        AssertMutable();
        _cardsPlayed++;
        InvokeDisplayAmountChanged();
        int previousStrength = Owner.GetPowerAmount<StrengthPower>();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, 1, Applier, null, silent: true);
        _strengthGained += Math.Max(0L, (long)Owner.GetPowerAmount<StrengthPower>() - previousStrength);
        int previousDexterity = Owner.GetPowerAmount<DexterityPower>();
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, 1, Applier, null, silent: true);
        _dexterityGained += Math.Max(0L, (long)Owner.GetPowerAmount<DexterityPower>() - previousDexterity);
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants) =>
        side == Owner.Side && participants.Contains(Owner)
            ? RemoveTemporaryGains(Owner) : Task.CompletedTask;

    public override Task AfterRemoved(Creature oldOwner) => RemoveTemporaryGains(oldOwner);

    internal Action SnapshotCounterDuplication()
    {
        int cardsPlayed = _cardsPlayed;
        long strengthGained = _strengthGained;
        long dexterityGained = _dexterityGained;
        return () =>
        {
            AssertMutable();
            _cardsPlayed = (int)Math.Min(int.MaxValue, (long)_cardsPlayed + cardsPlayed);
            // Each stat records its actual gain independently of the number
            // of cards played. Copy the expiry ledger without granting it again.
            _strengthGained += Math.Min(strengthGained, long.MaxValue - _strengthGained);
            _dexterityGained += Math.Min(dexterityGained, long.MaxValue - _dexterityGained);
            InvokeDisplayAmountChanged();
        };
    }

    private async Task RemoveTemporaryGains(Creature creature)
    {
        long strength = _strengthGained;
        long dexterity = _dexterityGained;
        AssertMutable();
        _cardsPlayed = 0;
        _strengthGained = 0;
        _dexterityGained = 0;
        InvokeDisplayAmountChanged();
        await RemoveGain<StrengthPower>(creature, strength);
        await RemoveGain<DexterityPower>(creature, dexterity);
    }

    private static async Task RemoveGain<T>(Creature creature, long amount) where T : PowerModel
    {
        if (amount <= 0 || creature?.GetPower<T>() is not { } power)
            return;
        // Expiring temporary gains is bookkeeping, so Artifact must not block
        // this subtraction as though an opponent had applied a new debuff.
        power.SetAmount((int)Math.Clamp((long)power.Amount - amount, int.MinValue, int.MaxValue), silent: true);
        if (power.ShouldRemoveDueToAmount())
            await PowerCmd.Remove(power);
    }
}
