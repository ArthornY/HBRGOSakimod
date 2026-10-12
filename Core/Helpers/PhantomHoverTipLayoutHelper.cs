using System;
using Godot;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct PhantomHoverTipPlacement(Vector2 Position, float Scale);

internal static class PhantomHoverTipLayoutHelper
{
    internal static bool ShouldAvoidHud(Creature creature) =>
        creature?.PetOwner != null && creature.CombatState?.Players.Count > 1 &&
        creature.HasPower<PhantomPower>();

    internal static NHoverTipSet Track(NHoverTipSet tips, NCreature creature)
    {
        if (tips == null || !ShouldAvoidHud(creature?.Entity) ||
            !GodotObject.IsInstanceValid(tips) || !GodotObject.IsInstanceValid(creature)) return tips;
        tips.AddChildSafely(new NPhantomHoverTipPlacement { Creature = creature });
        return tips;
    }

    internal static PhantomHoverTipPlacement ComputePlacement(
        Rect2 content, Rect2 hud, Rect2 viewport, float gap)
    {
        if (!content.HasArea() || !viewport.HasArea()) return new(content.Position, 1f);
        Rect2 blocked = hud.Grow(Math.Max(0f, gap)).Intersection(viewport);
        float viewportScale = Math.Min(1f,
            Math.Min(viewport.Size.X / content.Size.X, viewport.Size.Y / content.Size.Y));
        Vector2 fittedPosition = ClampPosition(content.Position, content.Size * viewportScale, viewport);
        if (!blocked.HasArea() ||
            !new Rect2(fittedPosition, content.Size * viewportScale).Intersects(blocked))
            return new(fittedPosition, viewportScale);

        // Keeping the complete set together preserves all descriptions and the
        // relative text/card layout. Prefer full size; shrink only if no side
        // can contain it, rather than clipping tips or covering the status row.
        Rect2[] freeAreas =
        {
            new(viewport.Position, new(blocked.Position.X - viewport.Position.X, viewport.Size.Y)),
            new(new(blocked.End.X, viewport.Position.Y), new(viewport.End.X - blocked.End.X, viewport.Size.Y)),
            new(viewport.Position, new(viewport.Size.X, blocked.Position.Y - viewport.Position.Y)),
            new(new(viewport.Position.X, blocked.End.Y), new(viewport.Size.X, viewport.End.Y - blocked.End.Y))
        };
        PhantomHoverTipPlacement best = new(fittedPosition, viewportScale);
        float bestScale = 0f;
        float bestDistance = float.PositiveInfinity;
        foreach (Rect2 area in freeAreas)
        {
            if (!area.HasArea()) continue;
            float scale = Math.Min(1f,
                Math.Min(area.Size.X / content.Size.X, area.Size.Y / content.Size.Y));
            Vector2 position = ClampPosition(content.Position, content.Size * scale, area);
            float distance = position.DistanceSquaredTo(content.Position);
            if (scale > bestScale || scale == bestScale && distance < bestDistance)
            {
                best = new(position, scale);
                bestScale = scale;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static Vector2 ClampPosition(Vector2 position, Vector2 size, Rect2 area) => new(
        Math.Clamp(position.X, area.Position.X, Math.Max(area.Position.X, area.End.X - size.X)),
        Math.Clamp(position.Y, area.Position.Y, Math.Max(area.Position.Y, area.End.Y - size.Y)));
}
