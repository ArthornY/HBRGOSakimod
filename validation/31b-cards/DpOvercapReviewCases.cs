using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

// A controllable maximum isolates the same maximum-change boundary used by
// main-phantom DP loss, without replacing Restore/Clamp/native amount writes.
public sealed class OvercapReviewShell : HighDimensionalShellPower
{
    public decimal TestMaximum { get; set; } = 100m;
    protected override decimal MaximumAmount => TestMaximum;
}

static partial class EffectChecks
{
    static async Task BDpOvercapReview(Harmony h)
    {
        BReset();
        if (!ModelDb.Contains(typeof(OvercapReviewShell))) ModelDb.Inject(typeof(OvercapReviewShell));
        Set(ModelDb.Power<OvercapReviewShell>(), "<Id>k__BackingField", ModelDb.GetId(typeof(OvercapReviewShell)));
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighDimensionalShellNativeAmountPatch", true)!).Patch();
        var shell = (OvercapReviewShell)ModelDb.Power<OvercapReviewShell>().ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), shell, Hero, 100m, Hero, null);
        Native(shell, "RestoreWithLimit", 20m, true);
        Equal(shell.CurrentDp, 120m, "overcap review starts with a real120/100 native shell pool");
        shell.TestMaximum = 0m;
        Native(shell, "ClampToMaximum");
        Equal(shell.CurrentDp, 0m, "reducing an overcap shell's maximum to0 cannot preserve a phantom1DP from the health-scaling floor");
    }
}
