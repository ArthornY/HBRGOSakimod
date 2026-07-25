using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NParticlesContainer), nameof(NParticlesContainer.Restart))]
internal static class NParticlesContainerRestartPatch
{
    private static bool Prefix(Godot.Collections.Array<GpuParticles2D> ____particles)
    {
        if (____particles == null)
        {
            return false;
        }

        bool hasInvalidParticle = false;
        foreach (GpuParticles2D particle in ____particles)
        {
            if (particle == null || !GodotObject.IsInstanceValid(particle))
            {
                hasInvalidParticle = true;
                break;
            }
        }

        if (!hasInvalidParticle)
        {
            return true;
        }

        foreach (GpuParticles2D particle in ____particles)
        {
            if (particle != null && GodotObject.IsInstanceValid(particle))
            {
                particle.Restart();
            }
        }

        return false;
    }
}
