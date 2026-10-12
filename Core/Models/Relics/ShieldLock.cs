using System;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class ShieldLock : RelicModel
{
    private int _counter = 1;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount => Counter;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/shield_lock_power.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public int Counter
    {
        get => _counter;
        private set
        {
            AssertMutable();
            int nextValue = Math.Max(0, value);
            if (_counter == nextValue)
            {
                return;
            }

            _counter = nextValue;
            InvokeDisplayAmountChanged();
        }
    }

    internal void GainCounter(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int previous = Counter;
        Counter += amount;
        if (Counter != previous)
        {
            Flash();
        }
    }

}
