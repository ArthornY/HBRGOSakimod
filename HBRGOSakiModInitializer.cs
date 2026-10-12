using System.Reflection;
using BaseLib.Config;
using HarmonyLib;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;

namespace HBRGOSakiMod{
    [ModInitializer(nameof(Initialize))]
    public static class HBRGOSakiModInitializer
    {
        private const string ModId = "HBRGOSakiMod";

        private static void Initialize()
        {
            HbrgoSakiHextechCompatLoader.Initialize();
            // RitsuLib mirrors this registry when its settings menu opens; both UIs use this instance.
            ModConfigRegistry.Register(ModId, new HBRGOSakiConfig());
            HBRGOSakiRewards.Register();
            SixSenseNetActions.Initialize();
            PhantomRosterNetActions.Initialize();
            HBRGOSakiTargetTypes.Register();
            HbrgoSakiVariantPreference.Register();
            Harmony harmony = new(ModId);
            HbrStoryModelIdentityPatch.Install(harmony);
            HBRGOSakiContentPack.Register();
            HBRGOSakiRightClickIntegration.Register();
            HBRGOSakiRunLifecycle.Register();
            HBRGOSakiCombatUiIntegration.Register();

            BaseLibRuntimeCompatibility.Install(harmony);
            CardPlayRuntimeCompatibility.Install(harmony);
            harmony.PatchAll();
            HbrCursorInstallPatch.ApplyToExistingManager();
            RitsuLibFramework.EnsureGodotScriptsRegistered(Assembly.GetExecutingAssembly());
            HbrClickEffectHelper.EnsureInstalled();
            NararaShibaThreadPageSync.StartSessionRefresh(
                ProjectSettings.GlobalizePath("user://HBRGOSakiMod/narara_shiba_author_page.json"));
            OverworkPortraitHelper.Prewarm();

            Log.Info($"{ModId} initialized.");
            GD.Print($"{ModId} initialized.");
        }

    }
}
