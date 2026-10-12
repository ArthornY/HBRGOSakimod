using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
internal static class PhantomKaiserCrabVisualsPatch
{
    internal static string GetVisualsPath(MonsterModel monster, Creature creature)
    {
        // AddPet assigns the owner before creating visuals, whereas PhantomPower
        // is applied later. Enemy arms and bestiary models keep their native scene.
        if (creature?.PetOwner == null) return null;
        return monster switch
        {
            Rocket => "res://scenes/creature_visuals/phantom_rocket.tscn",
            Crusher => "res://scenes/creature_visuals/phantom_crusher.tscn",
            _ => null
        };
    }

    private static bool Prefix(MonsterModel __instance, Creature ____creature,
        ref NCreatureVisuals __result)
    {
        // Read the backing field: canonical and detached mutable models can have
        // no Creature, and MonsterModel.Creature throws instead of returning null.
        string path = GetVisualsPath(__instance, ____creature);
        if (path == null) return true;

        __result = ResourceLoader.Load<PackedScene>(path)
            .Instantiate<NCreatureVisuals>(PackedScene.GenEditState.Disabled);
        return false;
    }
}
