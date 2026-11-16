using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("NARARA_LAYERED_HP_POWER")]
public sealed class NararaLayeredHpPower : PowerModel, ICustomPower
{
    [SavedProperty] public string PartitionMaximumHpData { get; set; } = string.Empty;
    [SavedProperty] public string SpentMaximumHpData { get; set; } = string.Empty;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => IsMutable ? NararaLayeredHpHelper.GetRemainingLayers(Owner) : NararaLayeredHpHelper.MaximumLayers;
    public string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/high_dimensional_shell_power.tres";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("Layers", IsMutable ? DisplayAmount : NararaLayeredHpHelper.MaximumLayers);
            return text;
        }
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        // Give small and large versions the same exact ledger/write boundary.
        // This preserves HP and lets native commands report the actual delta.
        if (NararaLayeredHpHelper.IsEnabled(Owner))
        {
            NararaLayeredHpHelper.InitializePartitions(Owner);
            LargeCreatureHealthHelper.SetCurrentHp(Owner, LargeCreatureHealthHelper.GetCurrentHp(Owner));
        }
        return Task.CompletedTask;
    }
}
