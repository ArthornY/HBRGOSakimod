using HextechRunes;

namespace HBRGOSakiMod.HextechCompat;

public static class HextechCompatEntry
{
    private const string AssetModId = "HBRGOSakiMod";

    private static readonly object Gate = new();
    private static bool _isRegistered;

    public static void RegisterAll()
    {
        lock (Gate)
        {
            if (_isRegistered)
            {
                return;
            }

            HextechRunesApi.RegisterForge<SwordheartForge>(
                HextechRarityTier.Prismatic,
                AssetModId);
            HextechRunesApi.RegisterForge<ChargeForge>(
                HextechRarityTier.Prismatic,
                AssetModId);
            HextechRunesApi.RegisterForge<DpForge>(
                HextechRarityTier.Gold,
                AssetModId);
            HextechRunesApi.RegisterForge<SixSenseForge>(
                HextechRarityTier.Silver,
                AssetModId);

            HextechRunesApi.RegisterPlayerRune<DoubleSwordsDoubleRune>(
                HextechRarityTier.Prismatic,
                tagKey: "OUTPUT",
                assetModId: AssetModId);
            HextechRunesApi.RegisterPlayerRune<ChangingSkyAspectRune>(
                HextechRarityTier.Prismatic,
                tagKey: "COMPREHENSIVE",
                assetModId: AssetModId);
            HextechRunesApi.RegisterPlayerRune<AnokoRune>(
                HextechRarityTier.Prismatic,
                tagKey: "COMPREHENSIVE",
                assetModId: AssetModId);
            HextechRunesApi.RegisterPlayerRune<SuperTropicalHellRune>(
                HextechRarityTier.Prismatic,
                tagKey: "COMPREHENSIVE",
                assetModId: AssetModId);
            HextechRunesApi.RegisterPlayerRune<ActuallyCancerRune>(
                HextechRarityTier.Silver,
                PlayerRuneFlags.AttributeConversionExclusive,
                tagKey: "SURVIVAL",
                assetModId: AssetModId);

            _isRegistered = true;
        }
    }
}
