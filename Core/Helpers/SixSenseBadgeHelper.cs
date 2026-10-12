using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SixSenseBadgeHelper
{
    internal const string AtlasPath = "res://images/ui/six_sense_badges.png";

    internal static bool TryGetAtlas(ref Texture2D atlas)
    {
        // Room cache cleanup can dispose a resource while pooled UI still holds
        // its managed wrapper. Null checks alone cannot detect that stale handle.
        if (!GodotObject.IsInstanceValid(atlas))
            atlas = ResourceLoader.Load<Texture2D>(AtlasPath);
        return GodotObject.IsInstanceValid(atlas);
    }

    internal static readonly IReadOnlyList<SixSenseAttribute> DisplayOrder = Array.AsReadOnly(new[]
    {
        SixSenseAttribute.LeadershipCharm, SixSenseAttribute.MentalStrength,
        SixSenseAttribute.Madness, SixSenseAttribute.Gentleness,
        SixSenseAttribute.Talent, SixSenseAttribute.Simplicity
    });

    internal static SixSenseAttribute? CardAttribute(CardModel card) =>
        card?.Capability<SixSenseCardCapability>()?.BaseAttribute;

    internal static Rect2 AtlasRegion(SixSenseAttribute attribute)
    {
        int index = (int)attribute;
        if (index < 0 || index >= 6) throw new ArgumentOutOfRangeException(nameof(attribute));
        // UVs stay inside the silver rim. Only the six-sided mesh samples the source atlas.
        return new Rect2(160 + index % 3 * 448, 104 + index / 3 * 450, 316, 362);
    }

    internal static Vector2[] Hexagon(Vector2 size)
    {
        float height = Math.Max(0, Math.Min(size.Y, size.X / 0.8660254f));
        float width = height * 0.8660254f;
        Vector2 offset = (size - new Vector2(width, height)) / 2;
        return new[]
        {
            offset + new Vector2(width / 2, 0), offset + new Vector2(width, height / 4),
            offset + new Vector2(width, height * 3 / 4), offset + new Vector2(width / 2, height),
            offset + new Vector2(0, height * 3 / 4), offset + new Vector2(0, height / 4)
        };
    }

    internal static Vector2[] AtlasUvs(SixSenseAttribute attribute)
    {
        Rect2 r = AtlasRegion(attribute);
        Vector2[] points = { new(.5f, 0), new(1, .25f), new(1, .75f),
            new(.5f, 1), new(0, .75f), new(0, .25f) };
        return points.Select(p => (r.Position + p * r.Size) / new Vector2(1536, 1024)).ToArray();
    }

    internal static SixSenseAttribute[] VisibleResistances(Creature creature)
    {
        SixSenseResistancePower power = creature?.GetPower<SixSenseResistancePower>();
        if (power == null || !SixSenseCombatHelper.IsEligibleEnemy(creature))
            return Array.Empty<SixSenseAttribute>();
        NararaNullificationPower nullification = creature.GetPower<NararaNullificationPower>();
        return DisplayOrder.Where(sense => ShouldShowResistance(power, nullification, sense)).ToArray();
    }

    internal static bool ShouldShowResistance(SixSenseResistancePower power,
        NararaNullificationPower nullification, SixSenseAttribute attribute) =>
        power != null && (power.GetResistance(attribute) > 0 ||
            // A broken shield still identifies the current turn's restricted
            // attributes. Showing it must not restore any resistance layers.
            (power.IsBroken(attribute) && nullification?.IsResistanceLocked(attribute) == true));

    internal static bool CanShowResistanceHover(Creature creature, SixSenseAttribute attribute) =>
        SixSenseCombatHelper.IsEligibleEnemy(creature) &&
        ShouldShowResistance(creature.GetPower<SixSenseResistancePower>(),
            creature.GetPower<NararaNullificationPower>(), attribute);

    internal static LocString ResistanceDescription(SixSenseResistancePower power)
    {
        var description = new LocString("powers", "SIX_SENSE_RESISTANCE_POWER.badgeDescription");
        description.Add("conversion", ((power?.DamageConversion ?? .5m) * 100m)
            .ToString("0.##", CultureInfo.InvariantCulture));
        return description;
    }
}
