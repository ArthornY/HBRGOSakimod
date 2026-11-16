using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static readonly List<(Player Owner, int Count)> NinjaTurns = [];
    static bool NinjaRequestLeaf(Player owner, int count) { NinjaTurns.Add((owner, count)); return false; }
    static decimal NinjaAmp(CardModel card) => (decimal)Call("KanzakiShowtimeCombatHelper", "GetDamageAmplificationPercent", card, Hero, Enemy, ValueProp.Move)!;
    static decimal NinjaDestruction(CardModel card) => (decimal)Call("KanzakiShowtimeCombatHelper", "GetDestructionGainMultiplier", card, Enemy, Hero, ValueProp.Move)!;
    static async Task BNinjaCases(Harmony h)
    {
        if (!ModelDb.Contains(typeof(ItsNinjaShowtimeHbrgoSaki))) ModelDb.Inject(typeof(ItsNinjaShowtimeHbrgoSaki));
        Set(ModelDb.Card<ItsNinjaShowtimeHbrgoSaki>(), "<Id>k__BackingField", ModelDb.GetId(typeof(ItsNinjaShowtimeHbrgoSaki)));
        BReset(); var card = GardenCard<ItsNinjaShowtimeHbrgoSaki>();
        await GardenPlay(card);
        Check(BAttacks.Single().Raw == 3m && BAttacks.Single().Hits == 3, "Ninja base attacks all enemies three times at50%each");
        Equal(NinjaDestruction(card), 1.6m, "Ninja base destruction4.8 is normalized by the existing default coefficient3 exactly once");
        BReset(); card = GardenCard<ItsNinjaShowtimeHbrgoSaki>(1, 2, 3);
        await GardenPlay(card);
        Check(BAttacks.Single().Raw == 3m && BAttacks.Single().Hits == 6, "Shadow Clone replaces base3 with6 without changing each50% skill coefficient");
        Equal(NinjaDestruction(card), 5.6m, "Shadow Clone destruction16.8 is normalized once by3 rather than multiplied by16.8");
        Check(Hero.GetPower<HbrComboLargePower>() is { Amount: 1 }, "Will Activation grants the real large combo before this card's attack leaf runs");
        Check(Enemy.GetPower<HbrAttackDownPower>() is { Amount: 1 }, "Ice Crystal creates one native received HBR attack-down instance with one enemy-turn duration");
        Equal((decimal)Call("HbrPercentDebuffHelper", "GetEffectPercent", Enemy.GetPower<HbrAttackDownPower>())!, 19m, "Ice Crystal20%basis resolves once at INT/LUCK100 versus enemyE150");
        BReset(); card = GardenCard<ItsNinjaShowtimeHbrgoSaki>(6);
        Check(card.Keywords.Contains(CardKeyword.Retain), "Faint Presence adds the real native Retain keyword through its garden upgrade");
        await card.BeforeSideTurnEndEarly(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]);
        Equal(NinjaAmp(card), 25m, "one own normal-turn hand retention stores25 normal amplification");
        await card.BeforeSideTurnEndEarly(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]);
        Equal(NinjaAmp(card), 25m, "duplicate turn-end dispatch cannot count the same physical card twice");
        object personalToken = new(); Call("KanzakiShowtimeCombatHelper", "OnTurnEnd", Player, personalToken);
        Equal(NinjaAmp(card), 50m, "an inserted personal turn owns a separate retained-card receipt");
        Call("KanzakiShowtimeCombatHelper", "OnTurnEnd", Player, personalToken);
        Equal(NinjaAmp(card), 50m, "duplicate inserted-turn flush also remains idempotent");
        await card.BeforeSideTurnEndEarly(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]);
        Equal(NinjaAmp(card), 50m, "resuming the interrupted normal turn cannot count its already-recorded retention again");
        Player.PlayerCombatState!.IncrementTurnNumber(); Call("KanzakiShowtimeCombatHelper", "OnTurnEnd", Player, null);
        Equal(NinjaAmp(card), 75m, "a real native extra turn increments the owner's turn counter even within the same combat round and stores another25");
        for (int turn = 0; turn < 5; turn++) { Player.PlayerCombatState.IncrementTurnNumber(); Call("KanzakiShowtimeCombatHelper", "OnTurnEnd", Player, null); }
        Equal(NinjaAmp(card), 100m, "retained normal amplification stops at100%after four own turns");
        using ((IDisposable)Call("KanzakiShowtimeCombatHelper", "BeginPlay", card)!)
        {
            Equal(NinjaAmp(card), 100m, "play receipt keeps the consumed retained bonus through every native attack segment");
            Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetStoredAmplification", card)!, 0m, "retained storage clears when the physical card is played");
            using ((IDisposable)Call("KanzakiShowtimeCombatHelper", "BeginPlay", card)!) Equal(NinjaAmp(card), 0m, "nested replay cannot borrow its parent's consumed retention");
            Equal(NinjaAmp(card), 100m, "nested replay restores the outer retained-bonus receipt");
        }
        Equal(NinjaAmp(card), 0m, "later plays do not retain a spent damage amplification receipt");
        Call("KanzakiShowtimeCombatHelper", "RecordRetained", card, new object());
        var closedReceipt = (IDisposable)Call("KanzakiShowtimeCombatHelper", "BeginPlay", card)!;
        ExecutionContext captured = ExecutionContext.Capture()!; closedReceipt.Dispose();
        decimal staleBonus = -1m; ExecutionContext.Run(captured, _ => staleBonus = NinjaAmp(card), null);
        Equal(staleBonus, 0m, "a captured asynchronous context cannot reuse a disposed retained-bonus receipt");
        BReset(); card = GardenCard<ItsNinjaShowtimeHbrgoSaki>(4, 6); GardenPermanent(card);
        var ordinary = (ItsNinjaShowtimeHbrgoSaki)card.MutableClone(); Set(ordinary, "_owner", Player); Set(ordinary, "_deckVersion", card); Player.PlayerCombatState!.Hand.AddInternal(ordinary);
        var od = (ItsNinjaShowtimeHbrgoSaki)card.MutableClone(); Set(od, "_owner", Player); Set(od, "_deckVersion", card); Player.PlayerCombatState.Hand.AddInternal(od);
        Call("KanzakiShowtimeCombatHelper", "RecordRetained", ordinary, null);
        Equal(NinjaAmp(od), 25m, "ordinary and OD mirrors share the permanent physical card's retained growth");
        MethodInfo request = Helper("PersonalImmediateTurnCoordinator").GetMethod("Request", F)!;
        MethodInfo requestLeaf = typeof(EffectChecks).GetMethod(nameof(NinjaRequestLeaf), F)!;
        h.Patch(request, prefix: new HarmonyMethod(requestLeaf)); NinjaTurns.Clear();
        try
        {
            await GardenPlay(ordinary); await GardenPlay(od);
            Check(NinjaTurns.Count == 1 && NinjaTurns[0].Owner == Player && NinjaTurns[0].Count == 1, "Wild Roar dispatches exactly one real personal-turn entry for the physical card across ordinary and OD mirrors");
            var independent = GardenCard<ItsNinjaShowtimeHbrgoSaki>(4); await GardenPlay(independent);
            Equal(NinjaTurns.Count, 2, "a separate physical Ninja card retains its own once-per-combat personal turn");
        }
        finally { h.Unpatch(request, requestLeaf); }
        await BNinjaOdCases();
        BReset(); card = GardenCard<ItsNinjaShowtimeHbrgoSaki>();
        foreach (int id in Enumerable.Range(0, 7).Reverse()) GardenUpgrade(card, id);
        Equal(card.CurrentUpgradeLevel, 7, "Ninja receives all seven distinct garden upgrades");
        Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local), 0, "Ninja Flash reduces the native energy cost exactly once");
        Check(!DesuwaFace(card, false).Contains('{'), "Ninja upgraded face resolves stored amplification, hits and destruction coefficient");
        BRegisterSaveType(card.GetType()); var options = new JsonSerializerOptions { IncludeFields = true };
        var restored = (ItsNinjaShowtimeHbrgoSaki)ModelDb.Card<ItsNinjaShowtimeHbrgoSaki>().MutableClone(); Set(restored, "_owner", Player);
        JsonSerializer.Deserialize<SavedProperties>(JsonSerializer.Serialize(SavedProperties.From(card), options), options)!.Fill(restored);
        for (int i = 0; i < 7; i++) { restored.UpgradeInternal(); restored.FinalizeUpgradeInternal(); }
        Check(restored.UpgradeEffectOrder.SequenceEqual(card.UpgradeEffectOrder) && restored.Keywords.Contains(CardKeyword.Retain), "true native save metadata restores the inherited seven-upgrade ledger and its real Retain replay");
    }
    static async Task BNinjaOdCases()
    {
        BReset(); var card = GardenCard<ItsNinjaShowtimeHbrgoSaki>(5);
        Equal(NinjaDestruction(card), 1.6m, "Beast Fang gives no destruction bonus in the ordinary turn");
        Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetCriticalDamageBonusPercent", Enemy, Hero, card, ValueProp.Move)!, 0m, "Beast Fang gives no critical-damage points in an ordinary turn");
        Type od = Helper("OverclockImmediateTurnCoordinator"), personal = Helper("PersonalImmediateTurnCoordinator");
        FieldInfo odCurrent = od.GetField("_current", F)!, personalCurrent = personal.GetField("_current", F)!, personalCombat = personal.GetField("_combat", F)!;
        object? oldOd = odCurrent.GetValue(null), oldPersonal = personalCurrent.GetValue(null), oldCombat = personalCombat.GetValue(null);
        try
        {
            Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player);
            object session = Activator.CreateInstance(personal.GetNestedType("Session", F)!, true)!;
            Set(session, "Owner", Player); Set(session, "Combat", Combat); personalCurrent.SetValue(null, session); personalCombat.SetValue(null, Combat);
            Check((bool)Call("PersonalImmediateTurnCoordinator", "IsActiveFor", Player)!, "fixture owns a real active nonOD personal session");
            Equal(NinjaDestruction(card), 1.6m, "Beast Fang does not misclassify another personal extra turn as OD");
            Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetCriticalDamageBonusPercent", Enemy, Hero, card, ValueProp.Move)!, 0m, "personal extra turn gives no OD-only critical points");
            personalCurrent.SetValue(null, null);
            object odSession = od.GetNestedType("Session", F)!.GetConstructors(F).Single().Invoke([Blank<Overclock>(), Combat, new[] { Player }]); odCurrent.SetValue(null, odSession);
            Check((bool)Call("OverclockImmediateTurnCoordinator", "IsActiveFor", Player)!, "fixture owns a real active OD session");
            Equal(NinjaDestruction(card), 1.92m, "OD multiplies the normalized4.8 destruction gain by1.2");
            Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetCriticalDamageBonusPercent", Enemy, Hero, card, ValueProp.Move)!, 20m, "OD adds exactly20 critical-damage percentage points rather than a20%multiplicative factor");
            var peer = SupportTeammate();
            Equal((decimal)Call("KanzakiShowtimeCombatHelper", "GetCriticalDamageBonusPercent", Enemy, peer.Creature, card, ValueProp.Move)!, 0m, "another player's damage cannot borrow the owner's Ninja source bonus");
            await BEffect<HbrCriticalRateUpPower>(Hero, 100m, Hero);
            await Command(Hero, 3m, ValueProp.Move, card);
            decimal critBase = (decimal)Call("HbrSkillPower", "EvaluateDamageFromUnits", 3m, 100m, 150m, true)!;
            Equal(Hits.Last().Final, decimal.Floor(critBase * 1.7m), "actual shared damage pipeline adds20points to the native1.5critical multiplier only inOD");
        }
        finally { odCurrent.SetValue(null, oldOd); personalCurrent.SetValue(null, oldPersonal); personalCombat.SetValue(null, oldCombat); }
    }
}
