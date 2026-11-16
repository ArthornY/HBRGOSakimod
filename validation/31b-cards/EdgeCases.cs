using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
static partial class EffectChecks
{
    static void BRegisterSaveType(Type type)
    {
        Assembly native = typeof(SavedProperties).Assembly;
        if (native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is { } legacy)
        { legacy.GetMethod("InjectTypeIntoCache", F)!.Invoke(null, [type]); return; }
        Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        // Beta folded save metadata into the model-id registry. Initialize only
        // its real save-property cache, not unrelated network/epoch registries.
        cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]);
        cache.GetField("_initialized", F)!.SetValue(null, true);
    }
    static async Task BSaveCases()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        foreach (Type type in New31BCards)
        {
            BReset(); var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
            foreach (int choice in new[] { 3, 0, 6 }) GardenUpgrade(card, choice);
            BRegisterSaveType(type);
            string payload = JsonSerializer.Serialize(SavedProperties.From(card), options);
            var restored = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(restored, "_owner", Player);
            JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(restored);
            Check(((int[])type.GetProperty("UpgradeEffectOrder")!.GetValue(restored)!).SequenceEqual(new[] { 3, 0, 6 }), type.Name + " inherited upgrade order survives true native SavedProperties From/JSON/Fill");
            for (int i = 0; i < 3; i++) { restored.UpgradeInternal(); restored.FinalizeUpgradeInternal(); }
            Equal(restored.EnergyCost.GetWithModifiers(CostModifiers.Local), card.EnergyCost.GetWithModifiers(CostModifiers.Local), type.Name + " saved ledger restores actual native upgrade/cost path");
        }
        BReset(); var aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(0, 3); await GardenPlay(aoi);
        var shield = Hero.GetPower<AoiInvincibleShieldPower>()!;
        BRegisterSaveType(typeof(AoiInvincibleShieldPower));
        var recovered = (AoiInvincibleShieldPower)ModelDb.Power<AoiInvincibleShieldPower>().MutableClone();
        JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(shield), options), options)!.Fill(recovered);
        Check(recovered.TwicePerTurn && recovered.GrantsAttackUp, "actual Aoi option flags survive native save JSON roundtrip");
        await RollBonusPower.Apply(new ThrowingPlayerChoiceContext(), Hero, 50, 3, Hero, null);
        var roll = Hero.GetPower<RollBonusPower>()!;
        BRegisterSaveType(typeof(RollBonusPower));
        var rollRestored = (RollBonusPower)ModelDb.Power<RollBonusPower>().MutableClone();
        JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(roll), options), options)!.Fill(rollRestored);
        Equal(rollRestored.RemainingUses, 3m, "inherited CountedRollPower remaining uses survives real native save JSON roundtrip");
    }
    static async Task BMultiplayerCases()
    {
        BReset(); var aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(5); await GardenPlay(aoi);
        Player peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        ShieldDice.Enqueue(0); object againstPeer = BBegin(); BCapture(peer.Creature); await BFinish(againstPeer);
        Equal(ShieldRolls, 0m, "an enemy hitting a teammate never rolls the holder's invincible shield");
        object againstHolder = BBegin(); BCapture(Hero); await BFinish(againstHolder);
        Equal(ShieldRolls, 1m, "holder's first actual hit still owns its unspent shield quota");
        var all = GardenCard<FreshDeathHbrgoSaki>(2, 3); await GardenPlay(all);
        Check(Hero.HasPower<HbrAttackUpPower>() && peer.Creature.HasPower<HbrAttackUpPower>(), "Higuchi saturated enhancement grants to both actual players");
        Check(peer.Creature.HasPower<HbrCriticalRateUpPower>() && peer.Creature.HasPower<HbrCriticalDamageUpPower>(), "Higuchi spectacle follows the same teammate recipient set");
        var owned = GardenCard<FreshDeathHbrgoSaki>(6); GardenPermanent(owned);
        Set(Hero, "_currentHp", 5000); Set(peer.Creature, "_currentHp", 1);
        Equal(BFactor(null, Enemy), 1m, "Die Together does not read lowHP from a teammate who lacks the permanent upgrade");
        Set(Hero, "_currentHp", 1000); Equal(BFactor(null, Enemy), 1.3m, "Die Together uses only its actual owning player's lowHP");
    }
    static async Task BDeferredCases()
    {
        BReset(); var aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
        ShieldDice.Enqueue(0); Set(Enemy.Monster!, "<Id>k__BackingField", ModelDb.GetId(Enemy.Monster!.GetType()));
        Set(Enemy.Monster!, "<NextMove>k__BackingField", new MoveState("PRE_DEBUFF", async _ =>
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
            await BBasis<HbrAttackDownPower>(Hero, 20m, Enemy);
            await BEffect<HbrVirusPower>(Hero, 1m, Enemy);
            Check(!Hero.HasPower<WeakPower>() && !Hero.HasPower<HbrVirusPower>(), "pre-hit attached effects are staged until this action's real target is known");
            await Command(Hero, 1m, ValueProp.Unpowered, null, [Hero]);
            Check(!Hero.HasPower<WeakPower>() && ShieldRolls == 0, "nested unrelated self-damage neither flushes the enemy's staged debuff nor rolls its attack shield");
            await Command(Enemy, 8m, ValueProp.Move, null, [Hero]);
        }));
        await Enemy.Monster!.PerformMove();
        Check(!Hero.HasPower<WeakPower>() && !Hero.HasPower<HbrAttackDownPower>() && !Hero.HasPower<HbrVirusPower>(), "successful shield discards both native and direct-HBR debuffs issued before first damage");
        Equal(ShieldRolls, 1m, "pre-hit debuff staging rolls exactly once at the actual damage target");
        Check(!(bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending", F)!.GetValue(null)!, "successful action leaves no staged effects");
        BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi); ShieldDice.Enqueue(99);
        Set(Enemy.Monster!, "<NextMove>k__BackingField", new MoveState("PRE_ALLOWED", async _ =>
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
            await Command(Enemy, 8m, ValueProp.Move, null, [Hero]);
        }));
        await Enemy.Monster!.PerformMove();
        Check(Hero.HasPower<WeakPower>() && Hero.Powers.OfType<RollBonusPower>().Any(power => power.Amount == 50 && power.RemainingUses == 1), "high raw roll flushes the pre-hit debuff normally and grants its one-use50 roll bonus");
        // The holder is debuffed, but the actual attack targets another player.
        BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
        Player peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer }); Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        Set(Enemy.Monster!, "<NextMove>k__BackingField", new MoveState("PRE_UNTARGETED", async _ =>
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
            await Command(Enemy, 8m, ValueProp.Move, null, [peer.Creature]);
        }));
        await Enemy.Monster!.PerformMove();
        Check(Hero.HasPower<WeakPower>() && ShieldRolls == 0, "untargeted holder's staged debuff applies normally at action completion without any shield roll");
        // Fault/cancel before a hit must close both the action and its queue.
        foreach (bool cancel in new[] { false, true })
        {
            BReset(); aoi = GardenCard<PeacefulMemoriesHbrgoSaki>(); await GardenPlay(aoi);
            Set(Enemy.Monster!, "<NextMove>k__BackingField", new MoveState("PRE_FAILED", async _ =>
            {
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
                if (cancel) throw new TaskCanceledException("controlled-before-hit");
                throw new InvalidOperationException("controlled-before-hit");
            }));
            bool original = false;
            try { await Enemy.Monster!.PerformMove(); }
            catch (Exception error) when ((cancel && error is TaskCanceledException) || (!cancel && error is InvalidOperationException)) { original = error.Message.Contains("controlled-before-hit"); }
            Check(original && !Hero.HasPower<WeakPower>() && !(bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending", F)!.GetValue(null)!, "failed/canceled action clears queued effects and preserves its original exception: " + cancel);
        }
    }
    static void BLocalizationCases(Harmony h)
    {
        foreach (string name in new[] { "CardDescriptionLocPatch", "ThirtyOneBCardDescriptionPatch" }) h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true)!).Patch();
        foreach (Type type in New31BCards)
        {
            BReset(); CardModel card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player); Player.PlayerCombatState!.Hand.AddInternal(card);
            Check(!DesuwaFace(card, false).Contains('{'), type.Name + " fresh card face has no unresolved formatting keys");
            foreach (int id in Enumerable.Range(0, 7)) GardenUpgrade(card, id);
            Check(!DesuwaFace(card, false).Contains('{'), type.Name + " fully upgraded card face renders all contextual counts/percentages");
        }
    }
}
