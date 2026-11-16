using System.Collections;
using System.Reflection;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.ValueProps;

// Real exact-bank application, damage hooks, doubling and snapshot extension.
// DP/max-HP conversion commands remain covered by the separate ledger fixture.
static partial class EffectChecks
{
    static HotaruDamageAmplificationPower BeyondMergedBank() =>
        Hero.Powers.OfType<HotaruDamageAmplificationPower>().Single();

    static async Task BeyondProtectionMergeCases()
    {
        foreach (Type type in new[] { typeof(HotaruDamageAmplificationPower), typeof(BeyondProtectionPower) })
            if (!ModelDb.Contains(type))
            {
                ModelDb.Inject(type);
                Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
        var context = new ThrowingPlayerChoiceContext();
        try
        {
            foreach (bool hbr in new[] { true, false })
            {
                Reset(hbr); As31O(true);
                var source = Owned<BeyondProtectionHbrgoSaki>();
                var attack = Owned<StrikeIronclad>();
                await PowerCmd.Apply<HotaruDamageAmplificationPower>(context, Hero, 7.125m, Hero, null, true);
                var beyond = Power<BeyondProtectionPower>(Hero, 1);
                await (Task)Invoke(typeof(BeyondProtectionPower).GetMethod("AddConvertedDp", F)!,
                    beyond, context, 1001, source)!;
                HotaruDamageAmplificationPower bank = BeyondMergedBank();
                Equal(bank.DamagePercent, 12.13m, "Beyond adds exactly 5.005% to an existing 7.125% unified bank (HBR " + hbr + ")");
                Check(beyond.ConvertedDp == 0 && beyond.DamagePercentData == string.Empty && beyond.DamagePercent == 0m,
                    "new Beyond conversions leave no second serialized damage bank");
                Equal(beyond.ModifyDamageMultiplicative(Enemy, 6m, ValueProp.Move, Hero, attack), 1m,
                    "Beyond itself no longer contributes a damage modifier");
                decimal expected = hbr ? decimal.Floor(Base(6m) * 1.1213m) : 6m * 1.1213m;
                Equal(Preview(Enemy, Hero, 6m, ValueProp.Move, attack), expected,
                    "merged Beyond preview retains the old additive 12.13% result (HBR " + hbr + ")");
                Equal(Actual(Enemy, Hero, 6m, ValueProp.Move, attack), expected,
                    "merged Beyond real damage matches its preview (HBR " + hbr + ")");

                Call("HbrgoSakiDamageMultiplierHelper", "Begin", Hero, Enemy, hbr);
                bank.ModifyDamageMultiplicative(Enemy, 6m, ValueProp.Move, Hero, attack);
                object group = Call("HbrgoSakiDamageMultiplierHelper", "Complete")!;
                Equal((decimal)group.GetType().GetProperty("TemporaryPositiveMultiplierOffset", F)!.GetValue(group)!, .1213m,
                    "source-null award is still the holder's temporary attack bucket");
                Equal((decimal)group.GetType().GetProperty("PermanentMultiplierOffset", F)!.GetValue(group)!, 0m,
                    "Beyond conversion does not become permanent amplification");

                var doubler = Owned<DoublePowersHbrgoSaki>();
                await (Task)Native(doubler, "OnPlay", context, Play(doubler))!;
                Equal(bank.DamagePercent, 24.26m, "Double Powers now doubles the Beyond portion with the unified exact bank");
                await (Task)Invoke(typeof(BeyondProtectionPower).GetMethod("AddConvertedDp", F)!,
                    beyond, context, 1, source)!;
                Equal(bank.DamagePercent, 24.265m, "later DP conversion retains the fractional five-per-mille increment");
                BeyondSnapshotBankCase(beyond, bank);
            }

            Reset(); As31O(true);
            var legacy = Power<BeyondProtectionPower>(Hero, 1);
            typeof(BeyondProtectionPower).GetProperty(nameof(BeyondProtectionPower.ConvertedDp), F)!.SetValue(legacy, 9999);
            typeof(BeyondProtectionPower).GetProperty(nameof(BeyondProtectionPower.DamagePercentData), F)!.SetValue(legacy, "0.375");
            await legacy.AfterApplied(Hero, null!);
            Equal(BeyondMergedBank().DamagePercent, .375m, "legacy exact percentage takes priority over the old converted-DP field");
            await legacy.BeforeCombatStart();
            await legacy.BeforeCardPlayed(Play(Owned<StrikeIronclad>()));
            Equal(BeyondMergedBank().DamagePercent, .375m, "load, combat-start and first-card migration cannot credit the same legacy bank twice");
            Check(legacy.ConvertedDp == 0 && legacy.DamagePercentData == string.Empty,
                "successful migration clears both old serialized inputs");

            Reset(); As31O(true);
            legacy = Power<BeyondProtectionPower>(Hero, 1);
            typeof(BeyondProtectionPower).GetProperty(nameof(BeyondProtectionPower.ConvertedDp), F)!.SetValue(legacy, 1001);
            await legacy.BeforeCombatStart();
            Equal(BeyondMergedBank().DamagePercent, 5.005m, "integer-only old saves migrate before the first action without rounding");
            await legacy.BeforeCardPlayed(Play(Owned<StrikeIronclad>()));
            Equal(BeyondMergedBank().DamagePercent, 5.005m, "the first card leaves already migrated DP-derived amplification unchanged");
            await legacy.AfterCombatEnd(null!);
            Check(legacy.ConvertedDp == 0 && legacy.DamagePercentData == string.Empty,
                "Beyond combat cleanup has no legacy damage bank left to serialize");
            BeyondLegacySnapshotCase();
        }
        finally { As31O(false); Reset(); }
    }

    static void BeyondSnapshotBankCase(BeyondProtectionPower beyond, HotaruDamageAmplificationPower bank)
    {
        Type snapshot = Helper("LargeCombatSnapshotHelper");
        Type rowType = snapshot.GetNestedType("ExactCreature", F)!;
        object row = Activator.CreateInstance(rowType)!;
        MethodInfo capture = snapshot.GetMethod("CaptureTemporaryDamage", F)!;
        Invoke(capture, null, row, beyond, 0);
        Check(rowType.GetProperty("TemporaryDamagePools")!.GetValue(row) == null,
            "new snapshots do not emit a second Beyond damage pool");
        Invoke(capture, null, row, bank, 1);
        object[] pools = ((IEnumerable)rowType.GetProperty("TemporaryDamagePools")!.GetValue(row)!).Cast<object>().ToArray();
        Check(pools.Length == 1, "new snapshots contain only the unified amplification pool");
        object[] values = ((IEnumerable)pools[0].GetType().GetProperty("Values")!.GetValue(pools[0])!).Cast<object>().ToArray();
        Check(values.Length == 1 &&
              (string)values[0].GetType().GetProperty("Name")!.GetValue(values[0])! == "DamagePercent" &&
              (string)values[0].GetType().GetProperty("Percent")!.GetValue(values[0])! == "24.265",
            "the unified snapshot bank preserves the exact converted and doubled total once");
    }

    static void BeyondLegacySnapshotCase()
    {
        Type snapshot = Helper("LargeCombatSnapshotHelper");
        string id = ModelDb.GetId(typeof(BeyondProtectionPower)).ToString();
        string json = JsonSerializer.Serialize(new
        {
            Protocol = "HBRGO_EXACT_COMBAT", Version = 1,
            Creatures = new[] { new
            {
                Index = 0, CombatIndex = 0, CurrentHp = "100", MaxHp = "100", DpPowers = Array.Empty<object>(),
                TemporaryDamagePools = new[] { new
                {
                    PowerIndex = 0, PowerId = id,
                    Values = new[] { new { Name = "DamagePercent", Percent = "5.005" } }
                } }
            } }
        });
        NetFullCombatState State()
        {
            var state = new NetFullCombatState();
            state.Creatures.Add(new NetFullCombatState.CreatureState
            {
                currentHp = 100, maxHp = 100,
                powers = new() { new NetFullCombatState.PowerState { id = ModelDb.GetId(typeof(BeyondProtectionPower)), amount = 1 } }
            });
            return state;
        }
        object envelope = JsonSerializer.Deserialize(json, snapshot.GetNestedType("Envelope", F)!)!;
        NetFullCombatState oldState = State();
        Invoke(snapshot.GetMethod("Attach", F)!, null, oldState, envelope);
        string frozen = (string)Invoke(snapshot.GetMethod("GetFrozenJson", F)!, null, oldState)!;
        var writer = new PacketWriter();
        Invoke(snapshot.GetMethod("Serialize", F)!, null, oldState, writer);
        var reader = new PacketReader(); reader.Reset(writer.Buffer);
        NetFullCombatState readState = State();
        Invoke(snapshot.GetMethod("Deserialize", F)!, null, readState, reader);
        Check((string)Invoke(snapshot.GetMethod("GetFrozenJson", F)!, null, readState)! == frozen &&
              frozen.Contains(id, StringComparison.Ordinal) && frozen.Contains("5.005", StringComparison.Ordinal),
            "old Beyond exact snapshot extensions remain readable without rewriting native identities or checksum input");
        Check(reader.BitPosition == writer.BitPosition, "old snapshot extension roundtrip consumes the complete native string payload");
    }
}
