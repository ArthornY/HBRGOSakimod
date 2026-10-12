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
    ThirtyA = 1
}

public sealed class HbrgoSakiVariantPreferenceData
{
    public HbrgoSakiVariant PreferredVariant { get; set; } =
        HbrgoSakiVariant.ThirtyOneO;
}

public sealed class HbrgoBossUnlockData
{
    public const int DefaultNararaShibaFloor = 53997;

    public bool HasDefeatedFlatHand { get; set; }

    public bool HasUnlockedThirtyA { get; set; }

    public bool ForceSkullFeatherNextActFour { get; set; }

    public bool HasUnlockedNararaShiba { get; set; }

    public int NararaShibaFloor { get; set; } =
        DefaultNararaShibaFloor;

    internal bool NeedsSkullFeatherLinkedUnlock(HbrgoSakiVariant variant) =>
        HasDefeatedFlatHand && (variant switch
        {
            HbrgoSakiVariant.ThirtyOneO => !HasUnlockedThirtyA,
            HbrgoSakiVariant.ThirtyA => !HasUnlockedNararaShiba,
            _ => false
        });

    internal void ApplySkullFeatherLinkedUnlock(HbrgoSakiVariant variant)
    {
        if (!NeedsSkullFeatherLinkedUnlock(variant)) return;
        if (variant == HbrgoSakiVariant.ThirtyOneO) HasUnlockedThirtyA = true;
        else if (variant == HbrgoSakiVariant.ThirtyA) HasUnlockedNararaShiba = true;
    }
}

internal static class HbrgoSakiVariantPreference
{
    private const string ModId = "HBRGOSakiMod";
    private const string ProfileKey = "character_variant_preference";
    private const string ProfileFileName = "character_variant_preference.json";
    private const string BossUnlockProfileKey = "boss_unlocks";
    private const string BossUnlockProfileFileName = "boss_unlocks.json";

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
            _profileStore.Register<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                BossUnlockProfileFileName,
                SaveScope.Profile,
                defaultFactory: static () => new HbrgoBossUnlockData(),
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

    internal static void SetPreferredVariant(HbrgoSakiVariant variant)
    {
        if (!Enum.IsDefined(variant) ||
            variant == HbrgoSakiVariant.ThirtyA &&
            !IsThirtyAUnlocked())
        {
            return;
        }

        try
        {
            EnsureSkullFeatherLinkedUnlocks(variant);
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            _profileStore.Modify<HbrgoSakiVariantPreferenceData>(
                ProfileKey,
                data => data.PreferredVariant = variant);
            _profileStore.Save(ProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save HBRGO Saki character preference: {exception.Message}");
        }
    }

    internal static bool IsThirtyAUnlocked()
    {
        try
        {
            // The 31O selector must also repair profiles created before the
            // linked character unlock existed, without requiring another kill.
            EnsureSkullFeatherLinkedUnlocks(HbrgoSakiVariant.ThirtyOneO);
            RitsuLibFramework.EnsureProfileServicesInitialized();
            return _profileStore?.IsProfileInitialized == true &&
                (_profileStore.Get<HbrgoBossUnlockData>(
                    BossUnlockProfileKey)?.HasUnlockedThirtyA ?? false);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load HBRGO 30A unlock: {exception.Message}");
            return false;
        }
    }

    internal static void UnlockThirtyA()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            HbrgoBossUnlockData data = _profileStore
                .Get<HbrgoBossUnlockData>(BossUnlockProfileKey);
            if (data?.HasUnlockedThirtyA == true)
            {
                return;
            }

            _profileStore.Modify<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                unlocks => unlocks.HasUnlockedThirtyA = true);
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save HBRGO 30A unlock: {exception.Message}");
        }
    }

    internal static void EnsureSkullFeatherLinkedUnlocks(CharacterModel character)
    {
        if (character is HbrgoSaki31O)
            EnsureSkullFeatherLinkedUnlocks(HbrgoSakiVariant.ThirtyOneO);
        else if (character is HbrgoSaki30A)
            EnsureSkullFeatherLinkedUnlocks(HbrgoSakiVariant.ThirtyA);
    }

    private static void EnsureSkullFeatherLinkedUnlocks(HbrgoSakiVariant variant)
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true ||
                _profileStore.Get<HbrgoBossUnlockData>(BossUnlockProfileKey)
                    ?.NeedsSkullFeatherLinkedUnlock(variant) != true) return;

            _profileStore.Modify<HbrgoBossUnlockData>(BossUnlockProfileKey,
                unlocks => unlocks.ApplySkullFeatherLinkedUnlock(variant));
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn($"Failed to save SkullFeather linked unlocks: {exception.Message}");
        }
    }

    internal static bool IsNararaShibaUnlocked()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            return _profileStore?.IsProfileInitialized == true &&
                (_profileStore.Get<HbrgoBossUnlockData>(
                    BossUnlockProfileKey)?.HasUnlockedNararaShiba ?? false);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load Narara Shiba unlock: {exception.Message}");
            return false;
        }
    }

    internal static void UnlockNararaShiba()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            HbrgoBossUnlockData data = _profileStore
                .Get<HbrgoBossUnlockData>(BossUnlockProfileKey);
            if (data?.HasUnlockedNararaShiba == true)
            {
                return;
            }

            _profileStore.Modify<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                unlocks => unlocks.HasUnlockedNararaShiba = true);
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save Narara Shiba unlock: {exception.Message}");
        }
    }

    internal static int GetNararaShibaFloor()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            int floor = _profileStore?.IsProfileInitialized == true
                ? _profileStore.Get<HbrgoBossUnlockData>(
                    BossUnlockProfileKey)?.NararaShibaFloor ?? 0
                : 0;
            return floor > 0
                ? floor
                : HbrgoBossUnlockData.DefaultNararaShibaFloor;
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load Narara Shiba floor: {exception.Message}");
            return HbrgoBossUnlockData.DefaultNararaShibaFloor;
        }
    }

    internal static void SetNararaShibaFloor(int floor)
    {
        if (floor <= 0)
        {
            return;
        }

        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            _profileStore.Modify<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                unlocks => unlocks.NararaShibaFloor = floor);
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save Narara Shiba floor: {exception.Message}");
        }
    }

    internal static bool IsSkullFeatherUnlocked()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            return _profileStore?.IsProfileInitialized == true &&
                (_profileStore.Get<HbrgoBossUnlockData>(
                    BossUnlockProfileKey)?.HasDefeatedFlatHand ?? false);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load HBRGO boss unlocks: {exception.Message}");
            return false;
        }
    }

    internal static void UnlockSkullFeather()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            HbrgoBossUnlockData data = _profileStore
                .Get<HbrgoBossUnlockData>(BossUnlockProfileKey);
            if (data?.HasDefeatedFlatHand == true)
            {
                return;
            }

            _profileStore.Modify<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                unlocks =>
                {
                    unlocks.HasDefeatedFlatHand = true;
                    unlocks.ForceSkullFeatherNextActFour = true;
                });
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save HBRGO boss unlocks: {exception.Message}");
        }
    }

    internal static bool ShouldForceSkullFeatherNextActFour()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            return _profileStore?.IsProfileInitialized == true &&
                (_profileStore.Get<HbrgoBossUnlockData>(
                    BossUnlockProfileKey)
                    ?.ForceSkullFeatherNextActFour ?? false);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to load forced HBRGO Act 4 boss: {exception.Message}");
            return false;
        }
    }

    internal static void ConsumeForcedSkullFeatherActFour()
    {
        try
        {
            RitsuLibFramework.EnsureProfileServicesInitialized();
            if (_profileStore?.IsProfileInitialized != true)
            {
                return;
            }

            HbrgoBossUnlockData data = _profileStore
                .Get<HbrgoBossUnlockData>(BossUnlockProfileKey);
            if (data?.ForceSkullFeatherNextActFour != true)
            {
                return;
            }

            _profileStore.Modify<HbrgoBossUnlockData>(
                BossUnlockProfileKey,
                unlocks =>
                    unlocks.ForceSkullFeatherNextActFour = false);
            _profileStore.Save(BossUnlockProfileKey);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to consume forced HBRGO Act 4 boss: {exception.Message}");
        }
    }

    internal static CharacterModel GetCharacter(HbrgoSakiVariant variant)
    {
        return variant == HbrgoSakiVariant.ThirtyOneO
            ? ModelDb.Character<HbrgoSaki31O>()
            : ModelDb.Character<HbrgoSaki30A>();
    }

    internal static HbrgoSakiVariant FromCharacter(
        CharacterModel character)
    {
        return character is HbrgoSaki31O
            ? HbrgoSakiVariant.ThirtyOneO
            : HbrgoSakiVariant.ThirtyA;
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
            else if (player.CharacterId == ModelDb.Character<HbrgoSaki30A>().Id)
            {
                variant = HbrgoSakiVariant.ThirtyA;
            }

            if (!variant.HasValue)
            {
                return;
            }

            SetPreferredVariant(variant.Value);
        }
        catch (Exception exception)
        {
            Log.Warn(
                $"Failed to save HBRGO Saki character preference: {exception.Message}");
        }
    }
}
