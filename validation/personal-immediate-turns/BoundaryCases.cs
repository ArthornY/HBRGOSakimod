using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

static partial class EffectChecks
{
    sealed class PersonalBoundaryProbe : PowerModel
    {
        internal string Tag = "";
        internal bool Positive;
        internal bool FailEnd;
        public override PowerType Type => Positive ? PowerType.Buff : PowerType.Debuff;
        public override PowerStackType StackType => PowerStackType.Counter;
        Task Record(string phase)
        {
            PersonalNativeEvents.Add(Tag + ":" + phase);
            if (FailEnd && phase == "end") throw new InvalidOperationException("controlled-personal-end-failure");
            return Task.CompletedTask;
        }
        public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combat) => Record("before");
        public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combat) => Record("start");
        public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) => Record("player-start");
        public override Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) => Record("before-end");
        public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) => Record("end");
        public override Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) => Record("late-end");
    }
    static PersonalBoundaryProbe PersonalProbe(Creature owner, string tag, bool positive = false, bool fail = false)
    {
        var power = Blank<PersonalBoundaryProbe>(); Set(power, "<IsMutable>k__BackingField", true);
        Set(power, "_owner", owner); Set(power, "_amount", 3);
        power.Tag = tag; power.Positive = positive; power.FailEnd = fail;
        Powers(owner).Add(power); return power;
    }
    static async Task<(long Id, int Ordinal)> PersonalBeginOwner()
    {
        PersonalCall("Request", Player, 1); var identity = PersonalIdentity(PersonalTicketObject());
        await PersonalNetExecute(true, Player, identity.Id, identity.Ordinal); return identity;
    }

    static async Task PersonalPendingVotesCase()
    {
        Player peer = PersonalWorld();
        PersonalCall("Request", Player, 1); var ticket = PersonalIdentity(PersonalTicketObject());
        CombatManager.Instance.SetReadyToEndTurn(Player, true);
        CombatManager.Instance.SetReadyToEndTurn(peer, true);
#if HBR_BETA_NATIVE
        Check(!PersonalEndSignalCompleted(), "pending source-tail request intercepts the genuine beta all-ready one-shot signal before personal begin");
#else
        var deferred = (Func<Task>)typeof(CombatManager).GetField("_deferredEndTurnTransition", F)!.GetValue(CombatManager.Instance)!;
        Set(CombatManager.Instance, "_deferredEndTurnTransition", null);
        await deferred();
        Check(!PersonalEndSignalCompleted(), "pending source-tail request intercepts stable's real deferred all-ready callback before personal begin");
#endif
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && CombatManager.Instance.IsPlayerReadyToEndTurn(peer),
            "pending personal admission never erases the two native ordinary ready votes");
        await PersonalNetExecute(true, Player, ticket.Id, ticket.Ordinal);
        await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
        Check(PersonalEndSignalCompleted(), "restoring pending-at-source-tail session reissues ordinary end after clearing personal state");
    }

    static async Task PersonalForcedReadyWithoutPeerCase()
    {
        Player peer = PersonalWorld(); var ownerVotes = new List<bool>();
        Action<Player, bool> observe = (player, canBackOut) => { if (ReferenceEquals(player, Player)) ownerVotes.Add(canBackOut); };
        CombatManager.Instance.PlayerEndedTurn += observe;
        try
        {
            CombatManager.Instance.SetReadyToEndTurn(Player, false);
            var ticket = await PersonalBeginOwner();
            Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(Player),
                "personal begin can suspend a forced native owner vote even while the teammate has not voted ready");
            PlayerCmd.EndTurn(Player, true);
            await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
            Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && !CombatManager.Instance.IsPlayerReadyToEndTurn(peer) && !PersonalEndSignalCompleted(),
                "forced owner vote restores without ending the still-playing teammate's ordinary turn");
            Check(ownerVotes.Count >= 2 && ownerVotes.All(canBackOut => !canBackOut),
                "real native PlayerEndedTurn events preserve canBackOut:false when the forced ordinary vote is restored on both stable and beta");
        }
        finally { CombatManager.Instance.PlayerEndedTurn -= observe; }
        PersonalReadyGuardLifetimeCases();
    }

    static void PersonalReadyGuardLifetimeCases()
    {
        Type type = Helper("PersonalTurnReadyGuard");
        object Guard() => Activator.CreateInstance(type, F, null, new object[] { CombatManager.Instance, Player }, null)!;
        void GuardCall(object guard, string method) => Invoke(type.GetMethod(method, F)!, guard);
        bool Forced(object guard) => (bool)type.GetProperty("WasForced", F)!.GetValue(guard)!;
        bool HasNativeConstraint()
        {
#if HBR_BETA_NATIVE
            object turn = typeof(CombatManager).GetField("_turnState", F)!.GetValue(CombatManager.Instance)!;
            return ((HashSet<Player>)turn.GetType().GetProperty("PlayersThatCannotUnEndTurn", F)!.GetValue(turn)!).Contains(Player);
#else
            // Stable exposes the accepted vote's canBackOut rule through the
            // production per-combat tracker rather than a native constraint set.
            return Forced(Guard());
#endif
        }

        PersonalWorld(); CombatManager.Instance.SetReadyToEndTurn(Player, false);
        object cleared = Guard();
        Check(Forced(cleared), "ready lifetime fixture captures the native forced owner vote");
        GuardCall(cleared, "UndoReady");
        Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(Player),
            "real native Undo clears the borrowed owner ready vote before lifetime restoration");
        GuardCall(cleared, "RestoreConstraint");
        Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && !HasNativeConstraint() && !Forced(Guard()),
            "restoring a guard after native Ready was cleared never revives its old forbidden constraint or forced-vote rule");

        PersonalWorld(); CombatManager.Instance.SetReadyToEndTurn(Player, false);
        object nextTurn = Guard(); GuardCall(nextTurn, "UndoReady");
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", Player.PlayerCombatState!.TurnNumber + 1);
        CombatManager.Instance.SetReadyToEndTurn(Player, true);
        GuardCall(nextTurn, "RestoreConstraint");
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && !HasNativeConstraint() && !Forced(Guard()),
            "an old guard never overwrites a new turn's ready rule even when beta reuses the same CombatTurnState object");

        PersonalWorld(); CombatManager.Instance.SetReadyToEndTurn(Player, false);
        object sameTurn = Guard(); GuardCall(sameTurn, "UndoReady");
        CombatManager.Instance.SetReadyToEndTurn(Player, true);
        GuardCall(sameTurn, "RestoreConstraint");
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player) && HasNativeConstraint() && Forced(Guard()),
            "same-turn restoration with an owner still Ready preserves its original forced-vote constraint and rule");
    }

    static async Task PersonalNativeFailureCleanup()
    {
        PersonalWorld();
        var original = PersonalOwned<StrikeIronclad>(); Player.PlayerCombatState!.Hand.AddInternal(original);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards", F)!.GetValue(Combat)!).Add(original);
        PersonalCall("Request", Player, 1); var ticket = PersonalIdentity(PersonalTicketObject());
        PersonalThrowDraw = true;
        try
        {
            await PersonalNetExecute(true, Player, ticket.Id, ticket.Ordinal);
            Check(false, "controlled entry failure must propagate");
        }
        catch (InvalidOperationException error) when (error.Message == "controlled-personal-draw-failure")
        { Check(true, "entry failure propagates its original exception through recorded net Execute"); }
        finally { PersonalThrowDraw = false; }
        Check(original.Pile == Player.PlayerCombatState!.Hand && Player.PlayerCombatState!.Energy == 7 &&
            !(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)!,
            "entry finally restores the original owner hand/energy and releases the failed active ticket");

        PersonalWorld(); var failedEnd = PersonalProbe(Hero, "owner", fail: true);
        ticket = await PersonalBeginOwner();
        try
        {
            await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
            Check(false, "controlled exit failure must propagate");
        }
        catch (InvalidOperationException error) when (error.Message == "controlled-personal-end-failure")
        { Check(true, "end-boundary failure preserves its original exception"); }
        Check(!(bool)PersonalCall("IsActiveFor", Player)! && !(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)! && Player.PlayerCombatState!.Energy == 7,
            "failed end boundary still restores owner resources and clears active/pending gates");
        Powers(Hero).Remove(failedEnd);

        Player peer = PersonalWorld(); ticket = await PersonalBeginOwner();
        Set(Hero, "_currentHp", 0);
        PersonalCall("OnPlayerDeath", Player);
        Check(!(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)! && !(bool)PersonalCall("IsActiveFor", Player)!,
            "personal owner death clears the active schedule ticket instead of leaving ordinary end blocked");
        Check(peer.PlayerCombatState!.Energy == 5, "owner death cleanup leaves teammate resources unchanged");
        Check(Player.PlayerCombatState!.Phase == PlayerTurnPhase.Play,
            "personal owner death restores the original ordinary phase while clearing its active session");
        Set(Hero, "_currentHp", 5000);

        PersonalWorld(); await PersonalBeginOwner(); PersonalCall("Request", Player, 2); PersonalCall("Clear");
        Check(!(bool)PersonalCoordinator.GetProperty("IsBusy", F)!.GetValue(null)! &&
            PersonalCoordinator.GetField("_current", F)!.GetValue(null) == null,
            "combat reset clears active, queued and admission personal-turn state");
    }

    static async Task PersonalDurationCases()
    {
        Player peer = PersonalWorld();
        Creature pet = (Creature)H("Actor", null, null, Player)!;
        Creature peerPet = (Creature)H("Actor", null, null, peer)!;
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, pet, peerPet });
        var ownDebuff = PersonalProbe(Hero, "owner"); PersonalProbe(pet, "pet");
        PersonalProbe(peer.Creature, "peer"); PersonalProbe(peerPet, "peer-pet");
        var ownBuff = PersonalProbe(Hero, "owner-buff", positive: true);
        var petBuff = PersonalProbe(pet, "pet-buff", positive: true);
        var peerBuff = PersonalProbe(peer.Creature, "peer-buff", positive: true);
        HbrCalamityPower Calamity(Creature holder)
        {
            var power = Power<HbrCalamityPower>(holder, 5);
            Set(power, "<RemainingTurns>k__BackingField", 2); return power;
        }
        var ownCalamity = Calamity(Hero); var petCalamity = Calamity(pet); var peerCalamity = Calamity(peer.Creature);
        var ticket = await PersonalBeginOwner();
        string[] start = ["owner:before", "pet:before", "owner:start", "pet:start", "owner:player-start", "pet:player-start"];
        Check(PersonalNativeEvents.Where(e => !e.StartsWith("draw:")).SequenceEqual(start),
            "real personal begin dispatches negative before/start/player-start exactly once to owner and owned pet, preserving phase order");
        Check(ownCalamity.RemainingTurns == 2 && petCalamity.RemainingTurns == 2 && peerCalamity.RemainingTurns == 2,
            "Calamity's original EnemyTurnEnd clock does not tick at a personal player turn start");
        MethodInfo duration = Helper("ExtraTurnDurationHelper").GetMethod("ShouldSuppressDurationTick", F, null, new[] { typeof(PowerModel) }, null)!;
        bool Protected(PowerModel power) => (bool)Invoke(duration, null, power)!;
        Check(Protected(ownBuff) && Protected(petBuff) && !Protected(peerBuff) && !Protected(ownDebuff),
            "personal duration predicate protects only positive owner/pet durations, leaving debuffs and teammate durations normal");
        var durationHarmony = new Harmony(ProductionId + ".personal-duration");
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.ShadowDanceExtraTurnDurationPatch", true)!;
        durationHarmony.CreateClassProcessor(patch).Patch();
        MethodBase target = AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.TickDownDuration));
        try
        {
            await PowerCmd.TickDownDuration(ownBuff);
            Check(ownBuff.Amount == 3, "real TickDownDuration patch preserves positive duration during personal turn");
            await PowerCmd.Decrement(ownBuff);
            Check(ownBuff.Amount == 2, "ordinary trigger stack consumption still decrements a protected positive power");
            await PowerCmd.TickDownDuration(peerBuff);
            Check(peerBuff.Amount == 2, "real teammate duration tick remains active while another player has personal duration protection");
        }
        finally { durationHarmony.Unpatch(target, HarmonyPatchType.All, durationHarmony.Id); }
        await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
        string[] end = ["owner:before-end", "pet:before-end", "owner:end", "pet:end", "owner:late-end", "pet:late-end"];
        Check(PersonalNativeEvents.Where(e => !e.StartsWith("draw:")).SequenceEqual(start.Concat(end)),
            "real personal finish dispatches owner/pet negative end/late-end without triggering peer or positive-power side callbacks");
        Check(!Protected(ownBuff) && !Protected(petBuff), "ordinary duration countdown resumes immediately after personal restoration");
        await PowerCmd.TickDownDuration(ownBuff);
        Check(ownBuff.Amount == 1, "real ordinary duration tick decrements the owner's positive power after personal restoration");
        foreach (var calamity in new[] { ownCalamity, petCalamity, peerCalamity })
            await calamity.AfterSideTurnEnd(null!, CombatSide.Enemy, new[] { Enemy });
        Check(ownCalamity.RemainingTurns == 1 && petCalamity.RemainingTurns == 1 && peerCalamity.RemainingTurns == 1,
            "Calamity follows the shared enemy-side clock for holder, pet and teammate after personal restoration");
    }

    static async Task PersonalOverclockChargeCases()
    {
        Player peer = PersonalWorld(); var ticket = await PersonalBeginOwner();
        var relic = Owned<Overclock>(); Set(Player, "_relics", new List<RelicModel> { relic });
        Creature pet = (Creature)H("Actor", null, null, Player)!;
        Creature peerPet = (Creature)H("Actor", null, null, peer)!;
        var ownerCard = PersonalOwned<StrikeIronclad>();
        var peerCard = PersonalOwned<StrikeIronclad>(); Set(peerCard, "<IsMutable>k__BackingField", true); Set(peerCard, "_owner", peer);
        async Task Card(CardModel card, decimal gain, string label)
        {
            decimal before = relic.PreciseCharge; CardPlay play = Play(card);
            await relic.BeforeCardPlayed(play); await relic.AfterCardPlayed(null!, play);
            Equal(relic.PreciseCharge - before, gain, label);
        }
        async Task Damage(Creature dealer, CardModel? card, decimal gain, string label)
        {
            var result = Blank<DamageResult>(); Set(result, "<UnblockedDamage>k__BackingField", 1000);
            decimal before = relic.PreciseCharge;
            await relic.AfterDamageGiven(null!, dealer, result, ValueProp.Move, Enemy, card!);
            Equal(relic.PreciseCharge - before, gain, label);
        }
        await Card(ownerCard, 2m, "personal extra-turn attack still accumulates the normal two OD points");
        await Card(peerCard, 2m, "ordinary teammate attack accumulates OD while another owner takes a personal turn");
        await Damage(Hero, ownerCard, 2.5m, "personal owner hit keeps the fixed2.5 OD accumulation");
        await Damage(pet, null, 2.5m, "personal owner pet/cannon-attributed hit accumulates OD normally");
        await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
        Type coordinator = Helper("OverclockImmediateTurnCoordinator");
        Type sessionType = coordinator.GetNestedType("Session", F)!;
        object session = Activator.CreateInstance(sessionType, F, null, new object[] { relic, Combat, new[] { Player } }, null)!;
        coordinator.GetField("_current", F)!.SetValue(null, session);
        try
        {
            await Card(ownerCard, 0m, "true OD owner attack no longer accumulates passive OD charge");
            await Damage(Hero, ownerCard, 0m, "true OD owner damage does not charge the gauge");
            await Damage(pet, null, 0m, "true OD suppression follows a pet/cannon's actual owner");
            await Card(peerCard, 2m, "a non-OD teammate's normal attack still charges while OD belongs to a different actor");
            await Damage(peer.Creature, peerCard, 2.5m, "a non-OD teammate's normal hit still charges during somebody else's OD");
            await Damage(peerPet, null, 2.5m, "a non-OD teammate's pet/cannon hit is not suppressed by relic-holder OD");
            MethodInfo suppress = typeof(Overclock).GetMethod("IsPassiveChargeSuppressedFor", F)!;
            Check((bool)Invoke(suppress, null, null, ownerCard)! && !(bool)Invoke(suppress, null, null, peerCard)!,
                "card-source fallback attributes cannon/effect damage without a dealer to the correct OD participant");
        }
        finally { coordinator.GetField("_current", F)!.SetValue(null, null); }
        await Card(ownerCard, 2m, "ordinary/native-extra owner actions resume normal passive OD accumulation outside true OD");
    }

    static void PersonalDescriptionCases()
    {
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool", true)!;
        if (!ModelDb.Contains(pool)) ModelDb.Inject(pool);
        foreach (Type type in new[] { typeof(BoostUpExtraTurnPower), typeof(ShadowDanceExtraTurnPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            var power = (PowerModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", Hero);
            Set(power, "<Id>k__BackingField", ModelDb.GetId(type)); Set(power, "_amount", 2);
            LocString description = power.Description;
            string formatted = description.GetFormattedText();
            Check(!formatted.Contains("hbrgoSakiEnergyPrefix") && formatted.Contains("energy") && formatted.Contains("额外回合") && formatted.Contains("积累OD"),
                type.Name + " real Description binds its energy-icons variable and formats the current personal-turn rules");
            Check(type.GetMethods(F | BindingFlags.DeclaredOnly).All(method => method.Name != "ShouldTakeExtraTurn"),
                type.Name + " does not request a second native extra turn after its personal request");
        }
    }
}
