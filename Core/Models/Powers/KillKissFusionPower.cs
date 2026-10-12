using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class KillKissFusionPower : PowerModel
{
    [SavedProperty]
    public ModelId TargetMonsterId { get; private set; } = ModelId.none;

    [SavedProperty]
    public int CopyLevel { get; private set; } = 1;

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("CopyLevel", CopyLevel);
            return description;
        }
    }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override int DisplayAmount => CopyLevel;

    // Each pending fusion remains independent; its badge shows the level rather than Amount.
    public override PowerInstanceType InstanceType =>
        PowerInstanceType.Instanced;

    internal void Configure(ModelId targetMonsterId, int copyLevel)
    {
        AssertMutable();
        int previousLevel = CopyLevel;
        TargetMonsterId = targetMonsterId;
        CopyLevel = System.Math.Max(1, copyLevel);
        if (CopyLevel != previousLevel)
            InvokeDisplayAmountChanged();
    }
}
