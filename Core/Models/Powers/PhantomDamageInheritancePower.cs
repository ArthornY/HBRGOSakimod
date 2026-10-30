using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// Keep the legacy model ID and saved-property name readable. New sources use
// HotaruDamageAmplificationPower, and both IDs share its state and calculation.
public sealed class PhantomDamageInheritancePower : HotaruDamageAmplificationPower
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DamagePercentData
    {
        get => null;
        private set => AmplificationPercentData = value;
    }
}
