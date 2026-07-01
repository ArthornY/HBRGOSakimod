using System;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.PotionPools;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.RelicPools;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod{
    [ModInitializer(nameof(Initialize))]
    public static class HBRGOSakiModInitializer
    {
        private const string ModId = "HBRGOSakiMod";

        private static void Initialize()
        {
            ModHelper.AddModelToPool(typeof(HBRGOSakiRelicPool), typeof(ElectronicSoldierManual));
            ModHelper.AddModelToPool(typeof(HBRGOSakiRelicPool), typeof(SoulOfAnchor));
            ModHelper.AddModelToPool(typeof(HBRGOSakiRelicPool), typeof(DeflectorProtection));

            new Harmony(ModId).PatchAll();
            LookupScriptsInThisAssembly();

            Log.Info($"{ModId} initialized.");
            GD.Print($"{ModId} initialized.");
        }

        private static void LookupScriptsInThisAssembly()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type scriptManagerBridge = typeof(GD).Assembly.GetType("Godot.Bridge.ScriptManagerBridge");
            MethodInfo lookupScripts = scriptManagerBridge?.GetMethod(
                "LookupScriptsInAssembly",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            lookupScripts?.Invoke(null, new object[] { assembly });
        }
    }
}
