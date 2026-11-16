using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// 1.3.0.11 report "卡结算了" (2026-11-06). Mimic Moonlight's replay stacks are a power too, so
// Double Powers doubled them and every later manual card was replayed: each Double Powers ran
// twice, and seven plays multiplied every power by 16384. Magic Sword Mode's Simplicity block
// then passed int range, and CreatureCmd.GainBlock's (int) history cast faulted the play,
// leaving the card on screen. The same replay ran High Song a second time.
// The six-sense card effects, Simplicity's block among them, were removed on 2026-11-09; the
// gain limit is now checked with a skill block of the same size through the same command.
static partial class EffectChecks
{
    static bool OverflowIsolated;
    static decimal ReceivedScale = 1m;
    static void ScaleReceived(ref decimal __result) => __result *= ReceivedScale;

    static void IsolateOverflowCommands()
    {
        if (OverflowIsolated) return;
        IsolateCenterVisuals();
        var harmony = new Harmony(ProductionId + ".overflow");
        foreach (string name in new[] { "BlockGainLimitPatch", "IntegerStatAmountPatch" })
            Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "installs production " + name);
        // History recording needs a live combat log; its argument casts still run in the commands.
        Type history = typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory", true)!;
        foreach (string record in new[] { "BlockGained", "PowerReceived" })
            harmony.Patch(history.GetMethod(record, F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
        // A stand-in receiving amplifier, applied before production normalization; 1 by default.
        harmony.Patch(typeof(Hook).GetMethod(nameof(Hook.ModifyPowerAmountReceived), F)!,
            postfix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ScaleReceived), F)!));
        // PowerCmd.Apply<T> starts from the canonical model, which this host registers on demand.
        if (!ModelDb.Contains(typeof(StrengthPower)))
        {
            ModelDb.Inject(typeof(StrengthPower));
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(StrengthPower))), "<Id>k__BackingField", ModelDb.GetId(typeof(StrengthPower)));
        }
        OverflowIsolated = true;
    }

    static void ClearHeroBlock() =>
        typeof(Creature).GetFields(F).Single(field => field.Name is "_block" or "<Block>k__BackingField").SetValue(Hero, 0);

    // The block Simplicity gave (3 powered units for its owner), through the real GainBlock command.
    static Task SkillBlock(decimal units) => CreatureCmd.GainBlock(Hero,
        (decimal)Call("HbrSkillBlockHelper", "ResolveUnpowered", Hero, units)!, ValueProp.Unpowered, null);

    static async Task SixSenseOverflowCases()
    {
        IsolateOverflowCommands();
        // GainBlock returns at once outside a combat in progress; earlier cases leave the flag off.
        CombatInProgressWithTurn();
        try { await OverflowCases(); }
        finally { CombatInProgress(false); }
    }

    // v0.111.0 asks the turn state whether combat is ending, so it needs the fields the stat
    // host's setup gives it, not only the in-progress flag.
    static void CombatInProgressWithTurn()
    {
#if HBR_BETA_NATIVE
        Type turnType = typeof(MegaCrit.Sts2.Core.Combat.CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState", true)!;
        object turn = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(turnType);
        Set(turn, "_cts", new CancellationTokenSource()); Set(turn, "<State>k__BackingField", Combat);
        Set(turn, "<IsInProgress>k__BackingField", true);
        Set(turn, "<PlayersTakingExtraTurn>k__BackingField", new List<MegaCrit.Sts2.Core.Entities.Players.Player>());
        Set(turn, "<ReadyLock>k__BackingField", Activator.CreateInstance(typeof(System.Threading.Lock)));
        Set(turn, "<PlayersReadyToEndTurn>k__BackingField", new HashSet<MegaCrit.Sts2.Core.Entities.Players.Player>());
        Set(turn, "<PlayersReadyToBeginEnemyTurn>k__BackingField", new HashSet<MegaCrit.Sts2.Core.Entities.Players.Player>());
        Set(MegaCrit.Sts2.Core.Combat.CombatManager.Instance, "_turnState", turn);
#else
        CombatInProgress(true);
#endif
    }

    static async Task OverflowCases()
    {
        const int cap = 999_999_999;

        // The reported crash: a 3-unit skill block from maximal Dexterity, through the real command.
        Reset(); ClearHeroBlock();
        await SkillBlock(3m);
        Equal(Hero.Block, 300m, "an ordinary 3-unit skill block is unchanged by the gain limit at Intelligence 100");
        Reset(); ClearHeroBlock(); Power<DexterityPower>(Hero, cap);
        decimal raw = (decimal)Call("HbrSkillBlockHelper", "ResolveUnpowered", Hero, 3m)!;
        Check(raw > int.MaxValue, $"a 3-unit skill block from {cap} Dexterity resolves to {raw}, past int range");
        await SkillBlock(3m);
        Equal(Hero.Block, cap, "the real GainBlock command gains that block at the native cap instead of faulting its history cast");

        // Additional stacks: doubling a capped power through an amplifier used to wrap the int sum negative.
        Reset(); var dexterity = Power<DexterityPower>(Hero, cap);
        ReceivedScale = 1.25m;
        try { await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), dexterity, cap, Hero, null); }
        finally { ReceivedScale = 1m; }
        Equal(dexterity.Amount, cap, "doubling capped Dexterity through a 125% receiving modifier stays at the cap instead of wrapping to the negative cap");
        Reset(); dexterity = Power<DexterityPower>(Hero, 600_000_000);
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), dexterity, 600_000_000m, Hero, null);
        Equal(dexterity.Amount, cap, "doubling 600000000 Dexterity reaches the same cap SetAmount stored before");
        Reset(); dexterity = Power<DexterityPower>(Hero, 40);
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), dexterity, 40m, Hero, null);
        Equal(dexterity.Amount, 80m, "ordinary doubling 40 to 80 is unchanged");
        // First application: ApplyInternal casts the whole amount.
        Reset();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Hero, 3_000_000_000m, Hero, null);
        Equal(Hero.GetPowerAmount<StrengthPower>(), cap, "a 3000000000 Strength application is stored at the cap instead of overflowing ApplyInternal's cast");

        // High Song under a replay: the first run replaced and removed it, so the second does nothing.
        Reset();
        var song = Owned<HighSongHbrgoSaki>();
        int hand = Player.PlayerCombatState!.Hand.Cards.Count;
        await (Task)Native(song, "OnPlay", new ThrowingPlayerChoiceContext(), Play(song))!;
        Check(Player.PlayerCombatState.Hand.Cards.Count == hand && song.Pile == null,
            "a replayed High Song outside combat piles neither throws nor adds a second song");
        Player.PlayerCombatState.PlayPile.AddInternal(song, -1, true);
        Check(song.Pile?.IsCombatPile == true, "during its first run a played High Song is in the play pile, so the guard lets it choose");
        Player.PlayerCombatState.PlayPile.RemoveInternal(song, true);
    }
}
