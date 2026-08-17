using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Utils.Persistence;

namespace HBRGOSakiMod.Core.Integration;

public enum HbrgoSakiVariant
{
    ThirtyOneO = 0,
    ArtificialSavior = 1
}

public sealed class HbrgoSakiVariantPreferenceData
{
    public HbrgoSakiVariant PreferredVariant { get; set; } =
        HbrgoSakiVariant.ThirtyOneO;
}

internal static class HbrgoSakiVariantPreference
{
    private const string ModId = "HBRGOSakiMod";
    private const string ProfileKey = "character_variant_preference";
    private const string ProfileFileName = "character_variant_preference.json";

    private static ModDataStore _profileStore;
    private static IDisposable _runEndedSubscription;

    internal static void Register()
    {
        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            _profileStore = RitsuLibFramework.GetDataStore(ModId);
            _profileStore.Register<HbrgoSakiVariantPreferenceData>(
                ProfileKey,
                ProfileFileName,
                SaveScope.Profile,
                defaultFactory: static () =>
                    new HbrgoSakiVariantPreferenceData(),
                autoCreateIfMissing: true);
        }

        _runEndedSubscription ??=
            RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(
                OnRunEnded,
                replayCurrentState: false);
    }

    internal static HbrgoSakiVariant GetPreferredVariant()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load HBRGO Saki character preference: {exception.Message}");
            return HbrgoSakiVariant.ThirtyOneO;
        }

        if (_profileStore?.IsProfileInitialized != true)
        {
            return HbrgoSakiVariant.ThirtyOneO;
        }

        HbrgoSakiVariant variant = _profileStore
            .Get<HbrgoSakiVariantPreferenceData>(ProfileKey)
            ?.PreferredVariant ?? HbrgoSakiVariant.ThirtyOneO;
        return Enum.IsDefined(variant)
            ? variant
            : HbrgoSakiVariant.ThirtyOneO;
    }

    internal static CharacterModel GetCharacter(HbrgoSakiVariant variant)
    {
        return variant == HbrgoSakiVariant.ArtificialSavior
            ? ModelDb.Character<HbrgoSaki>()
            : ModelDb.Character<HbrgoSaki31O>();
    }

    internal static HbrgoSakiVariant FromCharacter(
        CharacterModel character)
    {
        return character is HbrgoSaki31O
            ? HbrgoSakiVariant.ThirtyOneO
            : HbrgoSakiVariant.ArtificialSavior;
    }

    private static void OnRunEnded(RunEndedEvent evt)
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            SerializablePlayer player = LocalContext.NetId.HasValue
                ? evt.Run?.Players?.FirstOrDefault(candidate =>
                    candidate.NetId == LocalContext.NetId.Value)
                : null;
            if (player == null && evt.Run?.Players?.Count == 1)
            {
                player = evt.Run.Players.First();
            }

            if (player == null || _profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            HbrgoSakiVariant? variant = null;
            if (player.CharacterId == ModelDb.Character<HbrgoSaki31O>().Id)
            {
                variant = HbrgoSakiVariant.ThirtyOneO;
            }
            else if (player.CharacterId == ModelDb.Character<HbrgoSaki>().Id)
            {
                variant = HbrgoSakiVariant.ArtificialSavior;
            }

            if (!variant.HasValue)
            {
                return;
            }

            _profileStore.Modify<HbrgoSakiVariantPreferenceData>(
                ProfileKey,
                data => data.PreferredVariant = variant.Value);
            _profileStore.Save(ProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save HBRGO Saki character preference: {exception.Message}");
        }
    }
}
