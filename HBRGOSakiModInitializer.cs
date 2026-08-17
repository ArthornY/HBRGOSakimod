using System.Reflection;
using BaseLib.Config;
using HarmonyLib;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Integration;
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
            ModConfigRegistry.Register(ModId, new HBRGOSakiConfig());
            HBRGOSakiRewards.Register();
            SixSenseNetActions.Initialize();
            HBRGOSakiTargetTypes.Register();
            HbrgoSakiVariantPreference.Register();
            HBRGOSakiContentPack.Register();
            HBRGOSakiRightClickIntegration.Register();
            HBRGOSakiRunLifecycle.Register();
            HBRGOSakiCombatUiIntegration.Register();

            new Harmony(ModId).PatchAll();
            RitsuLibFramework.EnsureGodotScriptsRegistered(Assembly.GetExecutingAssembly());

            Log.Info($"{ModId} initialized.");
            GD.Print($"{ModId} initialized.");
        }

    }
}
