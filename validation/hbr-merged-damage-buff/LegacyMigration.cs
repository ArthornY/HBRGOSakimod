using System.Collections;
using System.Reflection;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class MergedDamageBuffChecks
{
    static PhantomDamageInheritancePower FreshLegacy(Creature owner)
    {
        var power = (PhantomDamageInheritancePower)ModelDb.Power<PhantomDamageInheritancePower>().ToMutable();
        Set(power, "_owner", owner); return power;
    }

    static partial void LegacyMigrationCases()
    {
        Check(typeof(HotaruDamageAmplificationPower).IsAssignableFrom(typeof(PhantomDamageInheritancePower)),
            "legacy inheritance model is a compatibility subtype of canonical damage buff");
        foreach (bool hbr in new[] { true, false })
        foreach (bool legacyFirst in new[] { true, false })
        {
            Reset(); Mode(hbr); decimal basis = Preview(Hero);
            if (legacyFirst)
            {
                PowerCmd.Apply<PhantomDamageInheritancePower>(null!, Hero, 25.125m, Hero, null, true).GetAwaiter().GetResult();
                Apply(12.375m);
            }
            else
            {
                Apply(25.125m);
                // The native generic result may be null when its as-T cast
                // targets the old alias but stacking selected a canonical T.
                PowerCmd.Apply<PhantomDamageInheritancePower>(null!, Hero, 12.375m, Hero, null, true).GetAwaiter().GetResult();
            }
            var instances = Powers(Hero).Where(p => p is HotaruDamageAmplificationPower).ToArray();
            Check(instances.Length == 1, $"mode{hbr} legacyFirst{legacyFirst} mixed-ID awards share one native instance");
            var unified = (HotaruDamageAmplificationPower)instances.Single();
            Equal(unified.DamagePercent, 37.5m, $"mode{hbr} legacyFirst{legacyFirst} mixed awards sum exact fractional amounts");
            Check(unified.Amount == 37, $"mode{hbr} legacyFirst{legacyFirst} exact sum controls one native integer proxy");
            Check((unified is PhantomDamageInheritancePower) == legacyFirst,
                $"mode{hbr} legacyFirst{legacyFirst} stacking preserves existing ModelId for snapshot identity");
            Equal(Preview(Hero), hbr ? decimal.Floor(basis * 1.375m) : basis * 1.375m,
                $"mode{hbr} legacyFirst{legacyFirst} mixed-ID total enters damage aggregation once");
            Equal(Actual(Hero), Preview(Hero), $"mode{hbr} legacyFirst{legacyFirst} actual wrapper matches preview");
            Equal((decimal)Call("TemporaryDamageBonusHelper", "CalculateEffectiveBonusPercent", Hero)!, 37.5m,
                $"mode{hbr} legacyFirst{legacyFirst} outgoing/death-inheritance summary has no duplicate alias bonus");
            Check(unified.Title.GetFormattedText() == "伤害增幅", $"mode{hbr} legacyFirst{legacyFirst} active old and new IDs display canonical title");
            SnapshotCase(unified);
        }
        Reset();

        // The old property name is an actual declared property consumed by
        // native SavedProperties, not a hand-set amount or ad-hoc net field.
        var current = Apply(87654.321125m)!;
        var saved = SavedProperties.From(current)!;
        var options = new JsonSerializerOptions { IncludeFields = true };
        string oldJson = JsonSerializer.Serialize(saved, options).Replace("AmplificationPercentData", "DamagePercentData", StringComparison.Ordinal);
        var oldFields = JsonSerializer.Deserialize<SavedProperties>(oldJson, options)!;
        var legacy = FreshLegacy(Hero); oldFields.FillInternal(legacy);
        Equal(((HotaruDamageAmplificationPower)(object)legacy).DamagePercent, 87654.321125m,
            "old DamagePercentData JSON property loads into unified exact state");
        Check(legacy.Amount == 87654, "old decimal property import synchronizes native proxy");
        Check(legacy.Id == ModelDb.GetId(typeof(PhantomDamageInheritancePower)), "old property import preserves original model identity");
        var normalized = SavedProperties.From(legacy)!;
        Check(normalized.strings?.Count(s => s.name == "AmplificationPercentData" && s.value == "87654.321125") == 1,
            "legacy model re-save emits canonical exact property once");
        Check(normalized.strings?.Any(s => s.name == "DamagePercentData") != true,
            "legacy load-only alias is omitted from new saved-property output");

        var writer = new PacketWriter(); oldFields.Serialize(writer); var reader = new PacketReader(); reader.Reset(writer.Buffer);
        var packet = new SavedProperties(); packet.Deserialize(reader); var fromPacket = FreshLegacy(Hero); packet.FillInternal(fromPacket);
        Equal(((HotaruDamageAmplificationPower)(object)fromPacket).DamagePercent, 87654.321125m,
            "registered old alias survives actual native packet field roundtrip");
        Check(reader.BitPosition == writer.BitPosition, "legacy alias packet consumes full registered payload");
        var clone = (PhantomDamageInheritancePower)legacy.ClonePreservingMutability();
        Equal(((HotaruDamageAmplificationPower)(object)clone).DamagePercent, 87654.321125m, "native legacy clone preserves canonical exact state");
        typeof(HotaruDamageAmplificationPower).GetProperty("AmplificationPercentData", F)!.SetValue(clone, "0.375");
        Equal(((HotaruDamageAmplificationPower)(object)legacy).DamagePercent, 87654.321125m, "legacy clone state is independent");
        Reset();

        // Simulate old whole-number-only power data: no exact property was
        // serialized by the historical model. New award must retain old total.
        var integerLegacy = FreshLegacy(Hero);
        PowerCmd.Apply(null!, integerLegacy, Hero, 17m, Hero, null, true).GetAwaiter().GetResult();
        Apply(.125m);
        Equal(((HotaruDamageAmplificationPower)(object)integerLegacy).DamagePercent, 17.125m,
            "integer-only legacy instance is not reset when receiving canonical fractional reward");
        Check(Powers(Hero).Count == 1, "integer-only legacy award also remains one active buff");
        Reset();
    }

    static void SnapshotCase(HotaruDamageAmplificationPower power)
    {
        Type snapshot = Helper("LargeCombatSnapshotHelper");
        Type creatureType = snapshot.GetNestedType("ExactCreature", F)!;
        object row = Activator.CreateInstance(creatureType)!;
        Invoke(snapshot.GetMethod("CaptureTemporaryDamage", F)!, null, row, power, 7);
        var pools = (IEnumerable?)creatureType.GetProperty("TemporaryDamagePools")!.GetValue(row);
        Check(pools != null, "snapshot captures unified damage percent for " + power.GetType().Name);
        var entries = pools!.Cast<object>().ToArray();
        Check(entries.Length == 1, "snapshot stores one unified percent pool per model");
        object pool = entries.Single(); Type poolType = pool.GetType();
        Check((int)poolType.GetProperty("PowerIndex")!.GetValue(pool)! == 7 &&
            (string)poolType.GetProperty("PowerId")!.GetValue(pool)! == power.Id.ToString(),
            "snapshot retains original native ID and power index");
        object[] values = ((IEnumerable)poolType.GetProperty("Values")!.GetValue(pool)!).Cast<object>().ToArray();
        Check(values.Length == 1 && (string)values[0].GetType().GetProperty("Name")!.GetValue(values[0])! == "DamagePercent" &&
            (string)values[0].GetType().GetProperty("Percent")!.GetValue(values[0])! == "37.5",
            "snapshot records exact canonical percent once without alias duplicate");
    }
}
