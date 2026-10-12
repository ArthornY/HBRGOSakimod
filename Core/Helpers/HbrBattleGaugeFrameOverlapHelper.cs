using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

// Each frame has its own material. Supply the other gauges' full capsule
// geometry without changing fill masks, remaining values or shared resources.
internal static class HbrBattleGaugeFrameOverlapHelper
{
    internal const int MaxOccluders = 4;
    private sealed class State
    {
        internal ShaderMaterial Material;
        internal int Count = -1;
        internal readonly Vector4[] Transforms = new Vector4[MaxOccluders];
        internal readonly Vector4[] Geometry = new Vector4[MaxOccluders];
    }

    private static readonly ConditionalWeakTable<NinePatchRect, State> States = new();

    internal static void SetOccluders(NinePatchRect frame, IReadOnlyList<NinePatchRect> occluders)
    {
        if (!IsUsable(frame) || frame.Material is not ShaderMaterial material) return;
        State state = States.GetOrCreateValue(frame);
        if (!ReferenceEquals(state.Material, material))
        {
            state.Material = material;
            state.Count = -1;
        }

        int count = 0;
        Transform2D frameTransform = frame.GetGlobalTransformWithCanvas();
        if (frame.IsVisibleInTree() && !Mathf.IsZeroApprox(frameTransform.Determinant()))
        {
            foreach (NinePatchRect other in occluders)
            {
                if (count >= MaxOccluders) break;
                if (!IsUsable(other) || ReferenceEquals(frame, other) ||
                    !other.IsVisibleInTree() || other.Size.X <= 0f || other.Size.Y <= 0f) continue;
                Transform2D otherTransform = other.GetGlobalTransformWithCanvas();
                if (Mathf.IsZeroApprox(otherTransform.Determinant())) continue;
                // Convert this frame's shader point directly into the other
                // frame's local coordinates, including any scale or rotation.
                Transform2D relative = otherTransform.AffineInverse() * frameTransform;
                Vector4 transform = new(relative.X.X, relative.Y.X, relative.X.Y, relative.Y.Y);
                Vector4 geometry = new(relative.Origin.X, relative.Origin.Y, other.Size.X, other.Size.Y);
                if (state.Count < 0 || !state.Transforms[count].IsEqualApprox(transform) ||
                    !state.Geometry[count].IsEqualApprox(geometry))
                {
                    material.SetShaderParameter($"frame_overlap_transform_{count}", transform);
                    material.SetShaderParameter($"frame_overlap_geometry_{count}", geometry);
                    state.Transforms[count] = transform;
                    state.Geometry[count] = geometry;
                }
                count++;
            }
        }
        // Stale slots are ignored by the shader. Clearing the count also
        // restores the complete HP outline when DP is removed or hidden.
        if (state.Count != count)
        {
            material.SetShaderParameter("frame_overlap_count", count);
            state.Count = count;
        }
    }

    internal static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
