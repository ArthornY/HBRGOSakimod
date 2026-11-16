using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using LostWisp = MegaCrit.Sts2.Core.Models.Relics.LostWisp;

// Fixed damage on the enemy side (2026-11-05): shared and event relics, and damage over
// time. Both read native amounts as skill units, six to 100%: a relic as its holder's skill,
// damage over time as its applier's (the incoming curve for an enemy applier).
static partial class EffectChecks
{
    static bool CenterVisualsIsolated;
    static void IsolateCenterVisuals()
    {
        if (CenterVisualsIsolated) return;
        var harmony = new Harmony(ProductionId + ".center-visuals");
        foreach (MethodInfo vfx in typeof(VfxCmd).GetMethods(F).Where(method => method.Name is "PlayOnCreatureCenter" or "PlayOnCreatureCenters"))
            harmony.Patch(vfx, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(Skip), F)!));
        CenterVisualsIsolated = true;
    }
    // Stable keeps the flag on CombatManager; v0.111.0 moved it to the manager's CombatTurnState.
    static void CombatInProgress(bool value)
    {
        FieldInfo? turnState = typeof(CombatManager).GetField("_turnState", F);
        if (turnState == null) { Set(CombatManager.Instance, "<IsInProgress>k__BackingField", value); return; }
        object state = turnState.GetValue(CombatManager.Instance) ?? RuntimeHelpers.GetUninitializedObject(turnState.FieldType);
        Set(state, "<IsInProgress>k__BackingField", value);
        turnState.SetValue(CombatManager.Instance, value ? state : null);
    }
    static CardPlay Play(CardModel card) { var play = Blank<CardPlay>(); Set(play, "<Card>k__BackingField", card); return play; }

    static async Task NativeRelicDamageCases()
    {
        IsolateCenterVisuals();
        async Task<Hit> Trigger<T>(decimal raw, Func<T, Task> hook, bool hbr = true) where T : RelicModel, new()
        {
            Reset(hbr); var relic = Owned<T>(); Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), relic });
            await hook(relic);
            Check(Hits.Count == 1, $"real native {typeof(T).Name} hook issues one damage command (HBR {hbr})");
            Hit hit = Hits.Single();
            Cases.Add(new { source = typeof(T).Name, hit.Raw, hit.Final, props = hit.Props.ToString(), hit.Enabled, hit.Sample });
            Check(hit.Raw == raw && hit.Props == ValueProp.Unpowered && ReferenceEquals(hit.Target, Enemy) && ReferenceEquals(hit.Dealer, Hero) && hit.Card == null,
                $"{typeof(T).Name} keeps raw{raw}, Unpowered, the enemy target, its holder as dealer and no card");
            Clean("native " + typeof(T).Name);
            return hit;
        }
        // Each of these reaches the canonical command through the typed DamageVar adapter.
        Equal((await Trigger<MercuryHourglass>(3m, relic => relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player))).Final,
            decimal.Floor(Base(3m)), "real Mercury Hourglass3 resolves a50percent skill through its holder's stats");
        Equal((await Trigger<FestivePopper>(9m, relic => relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player))).Final,
            decimal.Floor(Base(9m)), "real first-turn Festive Popper9 resolves a150percent skill");
        Equal((await Trigger<ScreamingFlagon>(20m, relic => relic.BeforeSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero }))).Final,
            decimal.Floor(Base(20m)), "real empty-hand Screaming Flagon20 resolves a333point33percent skill");
        Equal((await Trigger<ForgottenSoul>(1m, relic => relic.AfterCardExhausted(new ThrowingPlayerChoiceContext(), Owned<StrikeIronclad>(), false))).Final,
            decimal.Floor(Base(1m)), "real Forgotten Soul1 on exhaust resolves a16point67percent skill");
        // Parrying Shield's Block threshold is a fixed resource: 10 native, 1000 in HBR.
        Reset(); var shield = Owned<ParryingShield>(); Set(Hero, "_block", 999);
        await shield.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
        Check(Hits.Count == 0, "real Parrying Shield ignores 999 Block below its HBR threshold of 1000");
        Equal((await Trigger<ParryingShield>(6m, relic => { Set(Hero, "_block", 1000); return relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero }); })).Final,
            decimal.Floor(Base(6m)), "real Parrying Shield at 1000 Block deals a100percent skill");
        Set(Hero, "_block", 0);
        // Lost Wisp and Mr. Struggles pass a decimal; their own wrapper declares the skill.
        Equal((await Trigger<LostWisp>(8m, async relic =>
            {
                CombatInProgress(true);
                try { await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), Play(Owned<Inflame>())); }
                finally { CombatInProgress(false); }
            })).Final, decimal.Floor(Base(8m)), "real Lost Wisp8 after a power card resolves a133point33percent skill");
        Equal((await Trigger<MrStruggles>(1m, relic => relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player))).Final,
            decimal.Floor(Base(1m)), "real turn-1 Mr. Struggles resolves the turn number as a16point67percent skill");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 3);
        Equal((await Trigger<MrStruggles>(3m, relic => relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player))).Final,
            decimal.Floor(Base(3m)), "real turn-3 Mr. Struggles resolves a50percent skill");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        Equal((await Trigger<MercuryHourglass>(3m, relic => relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), Player), hbr: false)).Final,
            3m, "disabled HBR Mercury Hourglass keeps native3 damage");
        Equal((await Trigger<ParryingShield>(6m, relic => { Set(Hero, "_block", 10); return relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero }); }, hbr: false)).Final,
            6m, "disabled HBR Parrying Shield fires at native 10 Block for native6");
        Set(Hero, "_block", 0);
    }

    static async Task NativeDamageOverTimeCases()
    {
        ValueProp loss = ValueProp.Unblockable | ValueProp.Unpowered;
        async Task<Hit> Tick(PowerModel power, Func<Task> hook, Creature target, Creature? dealer, ValueProp props, decimal raw)
        {
            Hits.Clear(); await hook();
            Check(Hits.Count == 1, $"real native {power.GetType().Name} issues one damage command");
            Hit hit = Hits.Single();
            Cases.Add(new { source = power.GetType().Name, applier = power.Applier == Hero ? "hero" : power.Applier == Enemy ? "enemy" : "none", hit.Raw, hit.Final, props = hit.Props.ToString(), hit.Enabled });
            Check(hit.Raw == raw && hit.Props == props && ReferenceEquals(hit.Target, target) && ReferenceEquals(hit.Dealer, dealer) && hit.Card == null,
                $"{power.GetType().Name} keeps raw{raw}, its native flags, target and dealer");
            return hit;
        }
        async Task<decimal> DeclaredSkill(Creature target, Creature? dealer, decimal raw, ValueProp props, Creature? source)
        {
            Hits.Clear();
            using (Begin(target, dealer, null, raw, props, source)) await Command(dealer, raw, props, null, new[] { target });
            return Hits.Single().Final;
        }

        // Hero-applied Poison on the enemy: each tick is the hero's skill, and the preview matches.
        Reset(); var poison = Power<PoisonPower>(Enemy, 6); poison.Applier = Hero;
        int preview = poison.CalculateTotalDamageNextTurn();
        Hit tick = await Tick(poison, () => (Task)Native(poison, "AfterSideTurnStart", CombatSide.Enemy, new[] { Enemy }, Combat)!, Enemy, null, loss, 6m);
        Equal(tick.Final, await DeclaredSkill(Enemy, null, 6m, loss, Hero), "hero-applied Poison6 resolves exactly as the hero's declared skill");
        Check(tick.Final > 6m && tick.Enabled, "hero-applied Poison6 is no longer literal 6 damage");
        Equal(preview, tick.Final, "Poison next-turn preview equals its non-critical tick");
        Check(poison.Amount == 5, "native Poison still decays by one stack");Clean("hero-applied Poison");

        // Enemy-applied Poison on the hero (as SkullFeather applies it): the enemy's incoming curve.
        Reset(); var enemyPoison = Power<PoisonPower>(Hero, 20); enemyPoison.Applier = Enemy;
        preview = enemyPoison.CalculateTotalDamageNextTurn();
        tick = await Tick(enemyPoison, () => (Task)Native(enemyPoison, "AfterSideTurnStart", CombatSide.Player, new[] { Hero }, Combat)!, Hero, null, loss, 20m);
        Equal(tick.Final, await DeclaredSkill(Hero, null, 20m, loss, Enemy), "enemy-applied Poison20 resolves exactly as that enemy's declared skill");
        Check(tick.Final > 20m && tick.Final != 2000m, "enemy-applied Poison uses the incoming curve rather than literal20 or environmental2000");
        Equal(preview, tick.Final, "enemy-applied Poison preview equals its tick");Clean("enemy-applied Poison");

        // Stacks with no other applier are a fixed HP loss, a hundred per stack like an HP-cost card.
        Reset(); var loose = Power<PoisonPower>(Hero, 6);
        preview = loose.CalculateTotalDamageNextTurn();
        tick = await Tick(loose, () => (Task)Native(loose, "AfterSideTurnStart", CombatSide.Player, new[] { Hero }, Combat)!, Hero, null, loss, 600m);
        Check(!tick.Enabled && tick.Final == 600m && preview == 600, "applierless Poison6 is a fixed 600 HP loss, previewed as 600");Clean("applierless Poison");

        // Slithering Strangler's Constrict hits the hero as its own owner.
        Reset(); var constrict = Power<ConstrictPower>(Hero, 3); constrict.Applier = Enemy;
        tick = await Tick(constrict, () => (Task)Native(constrict, "AfterSideTurnEnd", new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero })!, Hero, Hero, ValueProp.Unpowered, 3m);
        Equal(tick.Final, await DeclaredSkill(Hero, Hero, 3m, ValueProp.Unpowered, Enemy), "enemy-applied Constrict3 resolves as the strangler's declared skill");
        Check(constrict.Amount == 3, "native Constrict stacks unchanged");Clean("enemy-applied Constrict");

        // The hero applies Knowledge Demon's Disintegration to itself: a fixed HP loss. As the
        // hero's own skill, six stacks would exceed a 3500 HP hero every turn.
        Reset(); var disintegration = Power<DisintegrationPower>(Hero, 6); disintegration.Applier = Hero;
        tick = await Tick(disintegration, () => (Task)Native(disintegration, "AfterSideTurnEndLate", new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero })!, Hero, Hero, ValueProp.Unpowered, 600m);
        Check(!tick.Enabled && tick.Final == 600m && disintegration.Amount == 6, "self-applied Disintegration6 is a fixed 600 HP loss each turn");Clean("Disintegration");

        // Powdered Demise and Tinker Time's Choking rider: the hero's skill on the enemy.
        Reset(); var demise = Power<DemisePower>(Enemy, 9); demise.Applier = Hero;
        tick = await Tick(demise, () => (Task)Native(demise, "AfterSideTurnEnd", new ThrowingPlayerChoiceContext(), CombatSide.Enemy, new[] { Enemy })!, Enemy, null, loss, 9m);
        Equal(tick.Final, await DeclaredSkill(Enemy, null, 9m, loss, Hero), "hero-applied Demise9 resolves a150percent hero skill");Clean("Demise");
        Reset(); var strangle = Power<StranglePower>(Enemy, 6); strangle.Applier = Hero; var played = Play(Owned<StrikeIronclad>());
        tick = await Tick(strangle, async () =>
            {
                await (Task)Native(strangle, "BeforeCardPlayed", played)!;
                await (Task)Native(strangle, "AfterCardPlayed", new ThrowingPlayerChoiceContext(), played)!;
            }, Enemy, null, loss, 6m);
        Equal(tick.Final, await DeclaredSkill(Enemy, null, 6m, loss, Hero), "hero-applied Strangle6 per card resolves a100percent hero skill");Clean("Strangle");

        // Disabled HBR keeps native stacks as damage.
        Reset(false); var nativePoison = Power<PoisonPower>(Hero, 20); nativePoison.Applier = Enemy;
        Equal(nativePoison.CalculateTotalDamageNextTurn(), 20m, "disabled HBR Poison preview keeps native20");
        tick = await Tick(nativePoison, () => (Task)Native(nativePoison, "AfterSideTurnStart", CombatSide.Player, new[] { Hero }, Combat)!, Hero, null, loss, 20m);
        Equal(tick.Final, 20m, "disabled HBR enemy Poison keeps native20 damage");Clean("disabled HBR Poison");
    }

    static void NativeEnemyDamageTextCases()
    {
        Localization(); IsolateCenterVisuals();
        string Relic<T>() where T : RelicModel, new() => Owned<T>().DynamicDescription.GetFormattedText();
        string Smart(PowerModel power)
        {
            LocString smart = power.SmartDescription; smart.Add("OwnerName", "测试角色"); smart.Add("OnPlayer", power.Owner == Hero);
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, smart, null);
            return smart.GetFormattedText();
        }
        Reset();
        string hourglass = Relic<MercuryHourglass>(), kusarigama = Relic<Kusarigama>();
        Check(hourglass.Contains("造成[blue]3000[/blue]点伤害") && !hourglass.Contains('%'), "Mercury Hourglass text shows numeric skill damage and retains the point unit");
        Check(kusarigama.Contains("造成[blue]6000[/blue]点伤害"), "Kusarigama text retains the point unit around its numeric skill basis");
        Check(Relic<StoneCalendar>().Contains("52000") && Relic<LetterOpener>().Contains("5000") && Relic<FestivePopper>().Contains("9000") &&
              Relic<ScreamingFlagon>().Contains("20000") && Relic<ForgottenSoul>().Contains("1000") && Relic<LostWisp>().Contains("8000"),
            "every converted relic states its numeric damage basis");
        string parrying = Relic<ParryingShield>();
        Check(parrying.Contains("[blue]1000[/blue]") && parrying.Contains("6000") && !parrying.Contains('%'), "Parrying Shield text shows 1000 Block and numeric skill damage");
        Check(Relic<MrStruggles>().Contains("造成" + Math.Max(1, Player.PlayerCombatState!.TurnNumber) * 1000 + "点伤害") &&
            Relic<MrStruggles>().Contains("随当前回合数提高"), "Mr. Struggles states numeric current-turn damage with its growth rule");
        var poison = Power<PoisonPower>(Hero, 20); poison.Applier = Enemy;
        string poisonText = Smart(poison);
        Check(poisonText.Contains("失去[blue]2714[/blue]点生命") && !poisonText.Contains('%'), "Poison tooltip resolves its enemy applier's incoming skill to numeric damage on its known holder");
        var constrict = Power<ConstrictPower>(Hero, 3); constrict.Applier = Enemy;
        Check(Smart(constrict).Contains("受到[blue]407[/blue]点伤害"), "Constrict tooltip resolves its enemy applier against its known holder");
        Check(Owned<Disintegration>().Description.GetFormattedText().Contains("受到600点伤害"), "Disintegration card shows its six stacks as a fixed 600 HP");
        var chosen = Power<DisintegrationPower>(Hero, 7); chosen.Applier = Hero;
        Check(Smart(chosen).Contains("受到[blue]700[/blue]点伤害"), "a self-applied Disintegration power shows its fixed 700");
        Check(Owned<PowderedDemise>().DynamicDescription.GetFormattedText().Contains("失去[blue]9000[/blue]点生命"), "Powdered Demise shows numeric future damage basis without inventing a target");
        var tinker = (TinkerTime)RuntimeHelpers.GetUninitializedObject(typeof(TinkerTime));
        Set(tinker, "<IsMutable>k__BackingField", true); Set(tinker, "<Owner>k__BackingField", Player); Set(tinker, "<Id>k__BackingField", ModelDb.GetId(typeof(TinkerTime)));
        Check(OptionText(tinker, "TINKER_TIME.pages.CHOOSE_RIDER.options.CHOKING").Contains("失去[blue]6000[/blue]点生命"),
            "Tinker Time's Choking option states its numeric six-unit future damage basis");
        H("Mode", false, 1);
        Check(Relic<MercuryHourglass>().Contains("[blue]3[/blue]点伤害") && Smart(constrict).Contains("[blue]3[/blue]点伤害") &&
              Relic<MrStruggles>().Contains("等量于当前回合数"), "disabled HBR keeps every native damage text");
        H("Mode", true, 1);
    }
}
