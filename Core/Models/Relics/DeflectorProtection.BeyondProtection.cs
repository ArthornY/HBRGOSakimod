using System;
using HBRGOSakiMod.Core.Helpers;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class DeflectorProtection
{
    private static readonly AsyncLocal<int> TemporaryMaxHpChangeDepth = new();

    // The ledger belongs to the permanent relic so removing the visible buff cannot
    // leave temporary maximum HP behind or lose the DP owed at combat end.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BeyondProtectionDpToReturn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BeyondProtectionPlayerMaxHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BeyondProtectionPhantomMaxHp { get; private set; }

    internal async Task<int> ConvertDpToTemporaryMaxHp()
    {
        AssertMutable();
        EnsureDpInitialized();
        int removedDp = LoseDp(Dp);
        if (removedDp <= 0) return 0;

        BeyondProtectionDpToReturn = AddClamped(BeyondProtectionDpToReturn, removedDp);
        int maxHp = removedDp / 2;
        if (maxHp <= 0) return removedDp;

        MomentOfReign reign = Owner.GetRelic<MomentOfReign>();
        int previousMaxHp = Owner.Creature.MaxHp;
        decimal previousPhantomAdjustment = reign?.ExactPhantomMaxHpAdjustment ?? 0m;
        TemporaryMaxHpChangeDepth.Value++;
        try
        {
            // Set leaves the player's current HP unchanged. When redirected,
            // phantoms also recover the actual maximum HP gained. Suppress the
            // player's DP refill normally caused by maximum-HP growth.
            await CreatureCmd.SetMaxHp(Owner.Creature,
                Math.Min(999_999_999L, (long)previousMaxHp + maxHp));
        }
        finally
        {
            BeyondProtectionPlayerMaxHp = AddClamped(BeyondProtectionPlayerMaxHp,
                Math.Max(0, Owner.Creature.MaxHp - previousMaxHp));
            BeyondProtectionPhantomMaxHp = AddClamped(BeyondProtectionPhantomMaxHp,
                (int)Math.Min(BigCombatNumbers.NativeStatCap,
                    Math.Max(0m, (reign?.ExactPhantomMaxHpAdjustment ?? 0m) - previousPhantomAdjustment)));
            TemporaryMaxHpChangeDepth.Value--;
        }

        return removedDp;
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await FinishBeyondProtection();
        ApplyHotaruCombatWear(room);
    }

    internal async Task FinishBeyondProtection()
    {
        AssertMutable();
        if (BeyondProtectionDpToReturn == 0 && BeyondProtectionPlayerMaxHp == 0 &&
            BeyondProtectionPhantomMaxHp == 0) return;

        // Clear each entry as it is settled; both this relic and MomentOfReign's
        // end hook call us, and re-entry must never refund twice.
        if (BeyondProtectionPlayerMaxHp > 0)
        {
            int temporaryHp = BeyondProtectionPlayerMaxHp;
            BeyondProtectionPlayerMaxHp = 0;
            TemporaryMaxHpChangeDepth.Value++;
            try
            {
                // Remove from the original recipient even if a redirect relic was
                // acquired later. This only changes the cap and cannot deal damage.
                Owner.Creature.SetMaxHpInternal(Math.Max(1, Owner.Creature.MaxHp - temporaryHp));
            }
            finally
            {
                TemporaryMaxHpChangeDepth.Value--;
            }
        }

        if (BeyondProtectionPhantomMaxHp > 0)
        {
            int temporaryHp = BeyondProtectionPhantomMaxHp;
            BeyondProtectionPhantomMaxHp = 0;
            if (Owner.GetRelic<MomentOfReign>() is { } reign)
                await reign.RedirectMaxHpChange(-temporaryHp, PhantomMaxHpChange.Set);
        }

        int refund = BeyondProtectionDpToReturn;
        BeyondProtectionDpToReturn = 0;
        // This is a refund, not healing: recovery reductions and Last Stand must
        // not eat it. The normal DP cap is restored before the refund is added.
        SetDpClamped(AddClamped(Dp, refund));
        InvokeDisplayAmountChanged();
    }

    private static int AddClamped(int first, int second) =>
        (int)Math.Clamp((long)first + second, 0, int.MaxValue);
}
