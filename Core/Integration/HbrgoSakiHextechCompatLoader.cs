using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Rewards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;

namespace HBRGOSakiMod.Core.Integration;

internal static class HbrgoSakiHextechCompatLoader
{
    private const string HextechAssemblyName = "HextechRunes";
    private const string CompatAssemblyName = "HBRGOSakiMod.HextechCompat";
    private const string CompatAssemblyResourceName =
        CompatAssemblyName + ".dll";
    private const string SwordheartForgeTypeName =
        "HBRGOSakiMod.HextechCompat.SwordheartForge";
    private const string DoubleSwordsDoubleRuneTypeName =
        "HBRGOSakiMod.HextechCompat.DoubleSwordsDoubleRune";
    private const string ChangingSkyAspectRuneTypeName =
        "HBRGOSakiMod.HextechCompat.ChangingSkyAspectRune";
    private const string AnokoRuneTypeName =
        "HBRGOSakiMod.HextechCompat.AnokoRune";
    private const string SuperTropicalHellRuneTypeName =
        "HBRGOSakiMod.HextechCompat.SuperTropicalHellRune";
    private const string ActuallyCancerRuneTypeName =
        "HBRGOSakiMod.HextechCompat.ActuallyCancerRune";
    private const string HextechAssetsTypeName =
        "HextechRunes.HextechAssets";
    private const string HextechIconResolverMethodName =
        "TryGetCustomRelicIconPath";
    private const string HextechAssetHooksTypeName =
        "HextechRunes.HextechAssetHooks";
    private const string HextechPortableTextureLoaderMethodName =
        "LoadPortableTexture";
    private const string CompatEntryTypeName =
        "HBRGOSakiMod.HextechCompat.HextechCompatEntry";
    private const string CompatEntryMethodName = "RegisterAll";

    private static readonly object Gate = new();
    private static readonly List<Assembly> CompatAssemblies = new();

    private static bool _isLoading;
    private static bool _isRegistered;
    private static bool _isSubscribed;
    private static bool _reflectionBridgeInstalled;
    private static bool _iconPathBridgeInstalled;

    public static void Initialize()
    {
        Assembly hextechAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(IsHextechAssembly);
        if (hextechAssembly != null)
        {
            TryLoadAndRegister();
            return;
        }

        lock (Gate)
        {
            if (_isRegistered || _isSubscribed)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            _isSubscribed = true;
        }

        Log.Info(
            "[HBRGOSakiMod][HextechCompat] Waiting for HextechRunes.");
    }

    private static void OnAssemblyLoad(
        object sender,
        AssemblyLoadEventArgs eventArgs)
    {
        if (IsHextechAssembly(eventArgs.LoadedAssembly))
        {
            TryLoadAndRegister();
        }
    }

    private static bool IsHextechAssembly(Assembly assembly)
    {
        return string.Equals(
            assembly?.GetName().Name,
            HextechAssemblyName,
            StringComparison.Ordinal);
    }

    private static void TryLoadAndRegister()
    {
        lock (Gate)
        {
            if (_isRegistered || _isLoading)
            {
                return;
            }

            _isLoading = true;
        }

        try
        {
            Assembly hextechAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(IsHextechAssembly) ??
                throw new InvalidOperationException(
                    $"Assembly {HextechAssemblyName} is not loaded.");
            Assembly compatAssembly = FindLoadedCompatAssembly() ??
                LoadCompatAssembly();
            RegisterCompatAssembly(compatAssembly);
            InstallIconPathBridge(hextechAssembly);
            InvokeCompatEntry(compatAssembly);

            lock (Gate)
            {
                _isRegistered = true;
                UnsubscribeNoLock();
            }

            Log.Info(
                "[HBRGOSakiMod][HextechCompat] Registered 31O Hextech content.");
        }
        catch (Exception exception)
        {
            Log.Error(
                "[HBRGOSakiMod][HextechCompat] Failed to load or register " +
                $"{CompatAssemblyName}: {exception}");
        }
        finally
        {
            lock (Gate)
            {
                _isLoading = false;
            }
        }
    }

    private static Assembly FindLoadedCompatAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                CompatAssemblyName,
                StringComparison.Ordinal));
    }

    private static Assembly LoadCompatAssembly()
    {
        Assembly mainAssembly =
            typeof(HbrgoSakiHextechCompatLoader).Assembly;
        using Stream resourceStream =
            mainAssembly.GetManifestResourceStream(
                CompatAssemblyResourceName);
        if (resourceStream == null)
        {
            throw new FileNotFoundException(
                "The embedded Hextech compatibility assembly is missing.",
                CompatAssemblyResourceName);
        }

        using MemoryStream assemblyStream = new();
        resourceStream.CopyTo(assemblyStream);
        assemblyStream.Position = 0;

        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(
            mainAssembly) ??
            AssemblyLoadContext.Default;
        return context.LoadFromStream(assemblyStream);
    }

    internal static bool Is31OPlayer(Player player)
    {
        return player?.Character is HbrgoSaki31O;
    }

    internal static int GetSwordheartMaximumBonus(Creature creature)
    {
        return creature?.Player?.Relics
            .Where(relic => string.Equals(
                relic.GetType().FullName,
                SwordheartForgeTypeName,
                StringComparison.Ordinal))
            .Sum(relic =>
                relic.GetType().GetProperty(
                    "SwordheartMaximumBonus",
                    BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(relic) is int bonus
                    ? Math.Max(0, bonus)
                    : 0) ?? 0;
    }

    internal static bool HasDoubleSwordsDoubleRune(Creature creature)
    {
        return HasCompatRelic(
            creature?.Player,
            DoubleSwordsDoubleRuneTypeName);
    }

    internal static bool HasChangingSkyAspectRune(Creature creature)
    {
        return HasCompatRelic(
            creature?.Player,
            ChangingSkyAspectRuneTypeName);
    }

    internal static IHoverTip GetChargeHoverTip()
    {
        return HoverTipFactory.FromPower<ChargePower>();
    }

    internal static Task ApplyCharge(Player player, decimal amount)
    {
        if (player?.Creature == null ||
            player.Creature.IsDead ||
            amount <= 0m)
        {
            return Task.CompletedTask;
        }

        return PowerCmd.Apply<ChargePower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            amount,
            player.Creature,
            null);
    }

    internal static bool GainMaxDp(Player player, int amount)
    {
        return amount > 0 &&
            player?.GetRelic<DeflectorProtection>()?.GainMaxDp(amount) > 0;
    }

    internal static bool CanOfferSixSenseReward(Player player)
    {
        return player?.GetRelic<ElectronicSoldierManual>() != null;
    }

    internal static Task OfferSixSenseReward(Player player)
    {
        ElectronicSoldierManual manual =
            player?.GetRelic<ElectronicSoldierManual>();
        manual?.GrantSixSensePoints(1);
        return Task.CompletedTask;
    }

    internal static Task EnterDualSwords(Player player)
    {
        Creature creature = player?.Creature;
        if (creature == null ||
            creature.IsDead ||
            creature.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding ||
            creature.HasPower<ReptureDualSwordsPower>())
        {
            return Task.CompletedTask;
        }

        return PowerCmd.Apply<ReptureDualSwordsPower>(
            new ThrowingPlayerChoiceContext(),
            creature,
            1m,
            creature,
            null);
    }

    internal static bool HasBowMode(Player player)
    {
        return player?.Creature?.HasPower<ReptureBowPower>() == true;
    }

    internal static bool IsBowFormCard(CardModel card)
    {
        return ModeTransitionHelper.IsBowRestrictedCard(card);
    }

    internal static async Task TransformChihayaToAnoko(Player player)
    {
        if (player?.RunState == null)
        {
            return;
        }

        ChihayaClassmateHbrgoSaki[] originals = player.Deck.Cards
            .OfType<ChihayaClassmateHbrgoSaki>()
            .ToArray();
        foreach (ChihayaClassmateHbrgoSaki original in originals)
        {
            AnokoHbrgoSaki replacement =
                player.RunState.CreateCard<AnokoHbrgoSaki>(player);
            while (replacement.CurrentUpgradeLevel <
                   original.CurrentUpgradeLevel &&
                   replacement.IsUpgradable)
            {
                CardCmd.Upgrade(replacement, CardPreviewStyle.None);
            }

            await CardCmd.Transform(
                original,
                replacement,
                CardPreviewStyle.HorizontalLayout);
        }
    }

    internal static async Task TransformCurseAndStatusCardsToEternalYo(
        Player player)
    {
        if (player?.RunState == null)
        {
            return;
        }

        CardModel[] originals = player.Deck.Cards
            .Where(card =>
                card.Type is CardType.Curse or CardType.Status)
            .ToArray();
        if (originals.Length == 0)
        {
            return;
        }

        foreach (CardModel original in originals)
        {
            if (!original.IsTransformable &&
                original.Keywords.Contains(CardKeyword.Eternal))
            {
                original.RemoveKeyword(CardKeyword.Eternal);
            }
        }

        List<CardTransformation> transformations = originals
            .Where(card => card.IsTransformable)
            .Select(card => new CardTransformation(
                card,
                CreateEternalYo(player)))
            .ToList();
        if (transformations.Count > 0)
        {
            await CardCmd.Transform(
                transformations,
                null,
                CardPreviewStyle.GridLayout);
        }

        foreach (CardModel original in originals
                     .Where(card => !card.HasBeenRemovedFromState))
        {
            await CardPileCmd.RemoveFromDeck(
                original,
                showPreview: false);
            CardPileAddResult result = await CardPileCmd.Add(
                CreateEternalYo(player),
                PileType.Deck);
            CardCmd.PreviewCardPileAdd(
                result,
                2f,
                CardPreviewStyle.GridLayout);
        }
    }

    internal static CardModel CreateEternalYo(Player player)
    {
        if (player?.RunState == null)
        {
            return null;
        }

        YoHbrgoSaki card =
            player.RunState.CreateCard<YoHbrgoSaki>(player);
        card.AddKeyword(CardKeyword.Eternal);
        return card;
    }

    internal static async Task SwapMaxHpAndDp(Player player)
    {
        Creature creature = player?.Creature;
        DeflectorProtection protection =
            player?.GetRelic<DeflectorProtection>();
        if (creature == null || protection == null)
        {
            return;
        }

        protection.EnsureDpInitialized();
        int oldMaxHp = creature.MaxHp;
        int oldMaxDp = protection.MaxDp;
        if (oldMaxHp <= 0 || oldMaxDp <= 0)
        {
            return;
        }

        protection.ActivateCancerMaxDp(oldMaxHp);
        await CreatureCmd.SetMaxHp(creature, oldMaxDp);
    }

    private static bool HasCompatRelic(
        Player player,
        string relicTypeName)
    {
        return player?.Relics.Any(relic => string.Equals(
            relic.GetType().FullName,
            relicTypeName,
            StringComparison.Ordinal)) == true;
    }

    private static void RegisterCompatAssembly(Assembly assembly)
    {
        lock (Gate)
        {
            if (!CompatAssemblies.Contains(assembly))
            {
                CompatAssemblies.Add(assembly);
            }
        }

        InstallReflectionBridge();
    }

    private static void InstallReflectionBridge()
    {
        lock (Gate)
        {
            if (_reflectionBridgeInstalled)
            {
                return;
            }

            MethodInfo getter = AccessTools.PropertyGetter(
                typeof(ReflectionHelper),
                nameof(ReflectionHelper.ModTypes));
            if (getter == null)
            {
                throw new MissingMethodException(
                    nameof(ReflectionHelper),
                    "get_ModTypes");
            }

            new Harmony("HBRGOSakiMod.HextechCompat.ReflectionBridge").Patch(
                getter,
                postfix: new HarmonyMethod(
                    typeof(HbrgoSakiHextechCompatLoader),
                    nameof(AppendCompatTypes)));
            _reflectionBridgeInstalled = true;
        }
    }

    private static void InstallIconPathBridge(Assembly hextechAssembly)
    {
        lock (Gate)
        {
            if (_iconPathBridgeInstalled)
            {
                return;
            }

            Type assetsType = hextechAssembly.GetType(
                HextechAssetsTypeName,
                throwOnError: true);
            MethodInfo resolver = AccessTools.Method(
                assetsType,
                HextechIconResolverMethodName,
                new[] { typeof(RelicModel) });
            if (resolver == null)
            {
                throw new MissingMethodException(
                    HextechAssetsTypeName,
                    HextechIconResolverMethodName);
            }

            Type assetHooksType = hextechAssembly.GetType(
                HextechAssetHooksTypeName,
                throwOnError: true);
            MethodInfo portableTextureLoader = AccessTools.Method(
                assetHooksType,
                HextechPortableTextureLoaderMethodName,
                new[] { typeof(string) });
            if (portableTextureLoader == null)
            {
                throw new MissingMethodException(
                    HextechAssetHooksTypeName,
                    HextechPortableTextureLoaderMethodName);
            }

            Harmony harmony = new(
                "HBRGOSakiMod.HextechCompat.IconPathBridge");
            harmony.Patch(
                resolver,
                prefix: new HarmonyMethod(
                    typeof(HbrgoSakiHextechCompatLoader),
                    nameof(TryResolveCompatRelicIconPath)));
            harmony.Patch(
                portableTextureLoader,
                prefix: new HarmonyMethod(
                    typeof(HbrgoSakiHextechCompatLoader),
                    nameof(TryLoadCompatRelicTexture)));
            _iconPathBridgeInstalled = true;
        }
    }

    private static bool TryResolveCompatRelicIconPath(
        RelicModel __0,
        ref string __result)
    {
        string path = __0?.GetType().FullName switch
        {
            DoubleSwordsDoubleRuneTypeName =>
                ImageHelper.GetImagePath(
                    "relics/doubleSwordsDoubleRune.png"),
            ChangingSkyAspectRuneTypeName =>
                ImageHelper.GetImagePath(
                    "relics/changingSkyAspectRune.png"),
            AnokoRuneTypeName =>
                ImageHelper.GetImagePath("relics/anokoRune.png"),
            SuperTropicalHellRuneTypeName =>
                "res://HextechRunes/images/relics/finalFormRune.png",
            ActuallyCancerRuneTypeName =>
                ImageHelper.GetImagePath("relics/actuallyCancerRune.png"),
            _ => null
        };
        if (path == null)
        {
            return true;
        }

        __result = path;
        return false;
    }

    private static bool TryLoadCompatRelicTexture(
        string __0,
        ref Texture2D __result)
    {
        if (!IsCompatRuneIconPath(__0))
        {
            return true;
        }

        Texture2D texture = ResourceLoader.Load<Texture2D>(
            __0,
            null,
            ResourceLoader.CacheMode.Reuse);
        if (texture == null ||
            !GodotObject.IsInstanceValid(texture) ||
            texture.GetWidth() <= 0 ||
            texture.GetHeight() <= 0)
        {
            return true;
        }

        __result = texture;
        return false;
    }

    private static bool IsCompatRuneIconPath(string path)
    {
        return path is
            "res://images/relics/doubleSwordsDoubleRune.png" or
            "res://images/relics/changingSkyAspectRune.png" or
            "res://images/relics/anokoRune.png" or
            "res://images/relics/actuallyCancerRune.png";
    }

    private static void AppendCompatTypes(ref Type[] __result)
    {
        Type[] compatTypes;
        lock (Gate)
        {
            compatTypes = CompatAssemblies
                .SelectMany(GetLoadableTypes)
                .Distinct()
                .ToArray();
        }

        if (compatTypes.Length > 0)
        {
            __result = __result
                .Concat(compatTypes)
                .Distinct()
                .ToArray();
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            Log.Warn(
                "[HBRGOSakiMod][HextechCompat] Partial type load: " +
                exception.Message);
            return exception.Types.OfType<Type>();
        }
    }

    private static void InvokeCompatEntry(Assembly assembly)
    {
        Type entryType = assembly.GetType(
            CompatEntryTypeName,
            throwOnError: true);
        MethodInfo registerMethod = entryType.GetMethod(
            CompatEntryMethodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (registerMethod == null)
        {
            throw new MissingMethodException(
                CompatEntryTypeName,
                CompatEntryMethodName);
        }

        registerMethod.Invoke(null, null);
    }

    private static void UnsubscribeNoLock()
    {
        if (!_isSubscribed)
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        _isSubscribed = false;
    }
}
