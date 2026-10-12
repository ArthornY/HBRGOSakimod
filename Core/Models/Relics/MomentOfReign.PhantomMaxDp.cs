using System;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class MomentOfReign
{
    private decimal _phantomLostMaxDp;
    private bool _hasExactPhantomLostMaxDp;

    // A permanent cost belongs to the summoner, outside roster/fusion bonuses.
    // Switching or resummoning a phantom must not erase or multiply it.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PhantomLostMaxDp
    {
        get => DpAmountCodec.ToNative(_phantomLostMaxDp);
        protected set
        {
            AssertMutable();
            _phantomLostMaxDp = Math.Max(0m, value);
            _hasExactPhantomLostMaxDp = true;
        }
    }

    internal decimal ExactPhantomLostMaxDp
    {
        get => _phantomLostMaxDp;
        private set
        {
            AssertMutable();
            _phantomLostMaxDp = BigCombatNumbers.ClampStat(value);
            _hasExactPhantomLostMaxDp = true;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PhantomLostMaxDpData
    {
        get => _hasExactPhantomLostMaxDp ? DpAmountCodec.Format(_phantomLostMaxDp) : string.Empty;
        protected set
        {
            if (string.IsNullOrEmpty(value)) return;
            _phantomLostMaxDp = DpAmountCodec.Parse(value);
            _hasExactPhantomLostMaxDp = true;
        }
    }

    internal decimal LosePhantomMaxDp(decimal amount)
    {
        if (!IsMutable || amount <= 0) return 0;
        var phantom = PhantomHelper.GetLivingPhantom(Owner);
        if (phantom == null) return 0;

        // A broken current shield still has a maximum. Its removal must not
        // turn an ordinary DP cost into a max-HP cost prematurely.
        decimal maximum = phantom.GetPower<HighDimensionalShellPower>()?.MaxAmount ??
            (phantom.Monster is NararaShibaBossMonster
                ? NararaLayeredDpPower.CalculateInitialAmount(phantom)
                : PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom));
        decimal lost = Math.Min(Math.Max(0m, maximum), BigCombatNumbers.ClampStat(amount));
        if (lost <= 0) return 0;

        ExactPhantomLostMaxDp = DpAmountCodec.AddOffset(ExactPhantomLostMaxDp, lost);
        foreach (HighDimensionalShellPower shell in phantom.Powers
                     .OfType<HighDimensionalShellPower>().ToArray())
            shell.ClampToMaximum();
        return lost;
    }
}
