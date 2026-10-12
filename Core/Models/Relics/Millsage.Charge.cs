using System;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed partial class Millsage
{
    public const int InitialLayers = 1;
    public const int MaximumLayers = 6;
    private const int CurrentChargeStateVersion = 1;
    private const int LegacyInitialLayers = 5;
    private int _layers = InitialLayers;
    private int _chargeStateVersion;

    [SavedProperty]
    public int Layers
    {
        // Previously five was the initial cannon count, and six meant one
        // extra acquisition. Preserve those acquisitions without reading
        // or rewriting any cannon upgrades or soul-power values.
        get => _chargeStateVersion < CurrentChargeStateVersion && _layers >= LegacyInitialLayers
            ? _layers - LegacyInitialLayers + InitialLayers
            : _layers;
        private set
        {
            AssertMutable();
            _layers = Math.Clamp(value, InitialLayers, MaximumLayers);
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty]
    public int ChargeStateVersion
    {
        get => _chargeStateVersion;
        private set
        {
            AssertMutable();
            _chargeStateVersion = Math.Clamp(value, 0, CurrentChargeStateVersion);
        }
    }

    public void SetLayers(int amount)
    {
        AssertMutable();
        // Both saved fields may arrive in either order. Only explicit game
        // mutations materialize the legacy projection, never a getter.
        _layers = Layers;
        _chargeStateVersion = CurrentChargeStateVersion;
        Layers = amount;
    }

    internal bool GainCharge()
    {
        int current = Layers;
        if (current >= MaximumLayers) return false;
        SetLayers(current + 1);
        Flash();
        return true;
    }
}
