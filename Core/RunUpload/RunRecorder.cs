using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.RunUpload;

// Turns what the game is doing into RunRecordBuilder calls. It only reads
// game state: nothing here changes a model, a save or a synchronized value.
// Every entry point is guarded so a recorder failure is logged and dropped.
internal static class RunRecorder
{
    private static readonly List<IDisposable> Subscriptions = new();
    private static readonly Dictionary<Creature, (int Hp, int Dp)> DamageBefore = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<Creature, string> CurrentMoves = new(ReferenceEqualityComparer.Instance);
    private static readonly Dictionary<Player, int> OpenPlays = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<string> ReportedFailures = new(StringComparer.Ordinal);

    private static RunState _runState;
    private static Player _me;
    private static RunRecordBuilder _record;
    private static CombatRecordBuilder _combat;
    private static JsonObject _lastCombat;
    private static bool _combatWon;
    private static FloorLive _live;
    private static bool _resumePending;
    private static readonly Dictionary<ModelId, string> Rarities = new();

    // Which run is recorded, and whether the console changed it.
    private static RunRecordingGate Gate => RunUploadService.Gate;

    internal static bool IsRecording => _record != null;

    internal static void Register()
    {
        if (Subscriptions.Count > 0) return;
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(HandleRunStarted, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(HandleRunLoaded, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(HandleRunEnded, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<ActEnteredEvent>(HandleActEntered, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<RoomEnteredEvent>(HandleRoomEntered, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(HandleCombatStarting, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CombatVictoryEvent>(HandleCombatVictory, replayCurrentState: false));
        Subscriptions.Add(RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(HandleCombatEnded, replayCurrentState: false));
    }

    // ---- switch -------------------------------------------------------------------------

    // Drops the record held in memory.
    internal static void Discard()
    {
        _record = null;
        _combat = null;
        _live = null;
        _resumePending = false;
        ClearCombatState();
    }

    // Uploading was turned on or off, or local data was cleared: the run in
    // progress is not recorded from here on, nor uploaded. Recording starts
    // again with the next new run.
    internal static void SkipCurrentRun(string reason)
    {
        if (Gate.StopCurrentRun(reason))
            RunUploadService.Log($"The run in progress is not recorded ({reason}); recording starts with the next new run.");
        Discard();
    }

    // ---- lifecycle --------------------------------------------------------------------

    // A run is recorded from its start or not at all (RunRecordingGate).
    private static void HandleRunStarted(RunStartedEvent evt) => Guard("run-start", () =>
    {
        _runState = evt.RunState;
        _me = LocalPlayer(_runState);
        Discard();
        if (!Gate.RunStarted(RunKey(_runState), RunUploadService.SessionIntegrity) || _runState == null) return;
        _record = new RunRecordBuilder(Header(_runState, evt.OccurredAtUtc));
        if (_runState.Map != null) EnsureAct(_runState);
        Persist();
    });

    // A continued run goes on with its own saved record; without one it is
    // not recorded (it began before uploading was turned on).
    private static void HandleRunLoaded(RunLoadedEvent evt) => Guard("run-load", () =>
    {
        _runState = evt.RunState;
        _me = LocalPlayer(_runState);
        Discard();
        (RunRecordBuilder resumed, JsonObject live) = Gate.RunLoaded(RunKey(_runState), RunUploadService.SessionIntegrity);
        if (resumed == null || _runState == null)
        {
            if (Gate.SkipReason == RunRecordingGate.ReasonNoRecord)
                RunUploadService.Log("This run has no record from its start, so it is not uploaded; recording starts with the next new run.");
            return;
        }
        _record = resumed;
        _live = FloorLive.FromJson(live);
        _resumePending = true;
        Persist();
    });

    private static void HandleActEntered(ActEnteredEvent evt) => Guard("act", () =>
    {
        if (_record == null || evt.RunState == null) return;
        FinalizeFloor(evt.RunState);
        EnsureAct(evt.RunState);
        Persist();
    });

    private static void HandleRoomEntered(RoomEnteredEvent evt) => Guard("room", () =>
    {
        IRunState state = evt.RunState;
        AbstractRoom room = evt.Room;
        // No record: this run is not recorded (see HandleRunLoaded).
        if (_record == null || state == null || room == null || room.RoomType is RoomType.Map or RoomType.Unassigned) return;

        int actIndex = state.CurrentActIndex;
        if (_record.CurrentAct?["index"]?.GetValue<int>() is int recordedAct && recordedAct != actIndex)
            FinalizeFloor(state);
        EnsureAct(state);

        MapCoord? coord = state.CurrentMapCoord;
        JsonObject floor = _record.CurrentFloor;
        bool samePoint = floor != null && SameCoord(floor["at"], coord);
        if (!samePoint)
        {
            if (floor != null) FinalizeFloor(state);
            _record.BeginFloor(coord is { } c ? (c.col, c.row) : null, PointTypeName(state.CurrentMapPoint?.PointType));
            _live = FloorLive.Capture(_me);
            AddRoom(room);
        }
        else if (!(_resumePending && LastRoomMatches(room)))
        {
            AddRoom(room);
        }
        _live ??= FloorLive.Capture(_me);
        _resumePending = false;
        Persist();
    });

    private static void HandleCombatStarting(CombatStartingEvent evt) => Guard("combat-start", () =>
    {
        ICombatState combat = evt.CombatState;
        if (_record == null || combat == null) return;
        ClearCombatState();
        JsonObject room = _record.CurrentRoom;
        if (room == null || !IsCombatRoom(room))
            room = _record.AddRoom(RoomTypeName(combat.Encounter?.RoomType ?? RoomType.Monster), Id(combat.Encounter?.Id), null);

        // A battle cut short by closing the game becomes an earlier attempt.
        _combat = new CombatRecordBuilder(RunRecordBuilder.BeginCombatAttempt(room));
        _combatWon = false;
        foreach (Player player in combat.Players ?? Array.Empty<Player>())
        {
            if (player?.Creature == null) continue;
            if (ReferenceEquals(player, _me)) _combat.Player(player.Creature, Id(player.Character?.Id));
            else _combat.Ally(player.Creature, Seat(player), Id(player.Character?.Id));
        }
        foreach (Creature creature in combat.Allies ?? Array.Empty<Creature>())
            if (creature != null && !creature.IsPlayer)
                _combat.Summon(creature, CreatureId(creature), _combat.KeyOf(creature.PetOwner?.Creature));
        var monsters = new List<string>();
        foreach (Creature creature in combat.Enemies ?? Array.Empty<Creature>())
        {
            if (creature == null) continue;
            _combat.Enemy(creature, CreatureId(creature), EnemyParameter(creature), null);
            monsters.Add(CreatureId(creature));
        }
        if (room["model"] == null && Id(combat.Encounter?.Id) is { } encounter) room["model"] = encounter;
        if (monsters.Count > 0) room["monsters"] = RunRecordBuilder.Strings(monsters);
        _combat.SetHbr(HbrSnapshot(_me, includeInventory: false));
        room["combat"] = _combat.Combat;
        Persist();
    });

    private static void HandleCombatVictory(CombatVictoryEvent evt) => Guard("combat-victory", () =>
    {
        if (_combat != null) _combatWon = true;
        else if (_lastCombat?["result"] is JsonObject result) result["outcome"] = "win";
    });

    private static void HandleCombatEnded(CombatEndedEvent evt) => Guard("combat-end", () =>
    {
        if (_combat == null) return;
        ICombatState combat = evt.CombatState;
        string outcome = _combatWon || combat?.Enemies?.All(enemy => enemy == null || !enemy.IsAlive) == true
            ? "win"
            : _me?.Creature?.IsDead == true ? "loss" : "escape";
        FinishCombat(combat, outcome);
        Persist();
    });

    private static void HandleRunEnded(RunEndedEvent evt) => Guard("run-end", () =>
    {
        Gate.RunEnded();
        RunHistory history = RunManager.Instance?.History;
        bool live = _record != null;
        string runKey = Gate.RunKey;
        if (!live)
        {
            // A saved run abandoned from the menu ends without being loaded:
            // only its saved record is known, not its final state.
            if (history == null) return;
            runKey = $"{history.Seed}|{history.Ascension}";
            _record = RunRecordBuilder.Resume(Gate.SavedRecordOfEndedRun(runKey));
            if (_record == null) return;
            _record.Note("这一局在没有载入的情况下结束，结束时的状态缺失");
        }

        if (_combat != null) FinishCombat(null, evt.IsAbandoned ? "abandoned" : _me?.Creature?.IsDead == true ? "loss" : "interrupted");
        if (live && _runState != null && _me != null) FinalizeFloor(_runState);

        string outcome = evt.IsAbandoned ? "abandoned" : evt.IsVictory ? "win" : "loss";
        JsonObject killedBy = null;
        if (outcome == "loss" && history != null)
        {
            if (RealId(history.KilledByEncounter) is { } encounter) killedBy = new JsonObject { ["encounter"] = encounter };
            else if (RealId(history.KilledByEvent) is { } eventId) killedBy = new JsonObject { ["event"] = eventId };
        }
        DateTimeOffset? started = evt.Run?.StartTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(evt.Run.StartTime) : null;
        long playSeconds = evt.Run?.RunTime ?? 0;
        JsonObject final = live && _me != null ? FinalSnapshot() : new JsonObject { ["floor"] = _record.FloorCount };
        _record.Finish(outcome, killedBy, started, evt.OccurredAtUtc, playSeconds, final, ResolveNames(_record.CollectIds()));
        // The filter of schema-v1.md 3.2 reads these; a dropped run leaves nothing behind.
        _record.ApplyIntegrity(RunUploadService.SessionIntegrity, Gate.ConsoleUsedIn(runKey));

        RunUploadService.SubmitFinishedRun(_record.Run, history?.BuildId);
        RunUploadService.DeleteInProgress();
        Discard();
    });

    // A developer console command finished. One that may change the run
    // (anything but the game's harmless commands, RunUploadConsole) excludes
    // the run in progress: its record becomes an exclusion marker with no run
    // data, and the rest of the run is not recorded.
    internal static void OnConsoleCommand(string commandName, bool succeeded)
    {
        if (!Gate.ConsoleCommand(commandName, succeeded)) return;
        RunUploadService.Log($"Console command '{RunRecordBuilder.Text(commandName)}' may change the run; this run is not uploaded.");
        Discard();
    }

    // ---- combat hooks (called from RunRecorderPatches) ---------------------------------
    // These run on every hook call, so they return before allocating anything
    // unless a battle is being recorded (static lambdas capture nothing).

    internal static void OnSideTurnStarting(ICombatState combat, CombatSide side) => GuardCombat("turn-start", side, static side =>
    {
        if (side == CombatSide.Player)
        {
            CurrentMoves.Clear();
            _combat.CloseRound();
            _combat.TurnStart("player");
        }
        else if (side == CombatSide.Enemy)
        {
            _combat.TurnStart("enemy");
        }
    });

    internal static void OnHandDraw(ICombatState combat, Player player) => GuardCombat("round", (combat, player), static s =>
    {
        if (s.combat != null && ReferenceEquals(s.player, _me))
            _combat.EnsureRound(s.combat.RoundNumber, () => StartSnapshot(s.combat));
    });

    internal static void OnSideTurnStarted(ICombatState combat, CombatSide side) => GuardCombat("round", (combat, side), static s =>
    {
        if (s.combat != null && s.side == CombatSide.Player && _combat.CurrentRound != s.combat.RoundNumber)
            _combat.EnsureRound(s.combat.RoundNumber, () => StartSnapshot(s.combat));
    });

    internal static void OnSideTurnEnded(ICombatState combat, CombatSide side) => GuardCombat("turn-end", (combat, side), static s =>
    {
        if (s.side == CombatSide.Player) _combat.TurnEnd("player");
        else if (s.side == CombatSide.Enemy)
        {
            _combat.TurnEnd("enemy");
            _combat.EndRound(EndSnapshot(s.combat));
            CurrentMoves.Clear();
        }
    });

    internal static void OnCardDrawn(CardModel card) => GuardCombat("draw", card, static card =>
    {
        if (card != null && ReferenceEquals(card.Owner, _me))
            _combat.Draw("p", CardJson(card));
    });

    internal static void OnCardPlaying(CardPlay play) => GuardCombat("play", play, static play =>
    {
        CardModel card = play?.Card;
        Player owner = card?.Owner;
        string by = _combat.KeyOf(owner?.Creature);
        if (by == null) return;
        JsonObject cardJson = CardJson(card);
        ResourceInfo resources = play.Resources;
        int repeat = play.PlayCount > 1 ? Math.Max(0, play.PlayIndex) : 0;
        _combat.Play(by, cardJson, _combat.KeyOf(play.Target), resources.EnergySpent, resources.StarsSpent,
            play.IsAutoPlay, repeat);
        OpenPlays[owner] = cardJson["uid"].GetValue<int>();
    });

    internal static void OnCardPlayed(CardPlay play) => GuardCombat("play-end", play, static play =>
    {
        CardModel card = play?.Card;
        if (card == null) return;
        int uid = _combat.Uid(card);
        _combat.EndPlay(uid);
        if (card.Owner != null && OpenPlays.TryGetValue(card.Owner, out int open) && open == uid) OpenPlays.Remove(card.Owner);
    });

    internal static void OnRoll(SoulOfAnchor history) => GuardCombat("roll", history, static history =>
    {
        Player owner = history?.Owner;
        if (!ReferenceEquals(owner, _me)) return;
        SoulRollHistoryEntry entry = history.RollHistory.Count > 0 ? history.RollHistory[^1] : null;
        if (entry?.WinningRoll == null) return;
        SoulRollBreakdown winning = entry.WinningRoll;
        var bonuses = new JsonArray();
        foreach (SoulRollBonusDetail bonus in winning.Bonuses)
            bonuses.Add(CombatRecordBuilder.RollBonus(bonus.Amount, bonus.Source, bonus.IsPercentage));
        var discarded = new JsonArray();
        foreach (SoulRollBreakdown roll in entry.DiscardedRollDetails)
            discarded.Add(CombatRecordBuilder.DiscardedRoll(roll.BaseRoll, roll.FinalRoll, TierName(roll, owner)));
        int? cardUid = OpenPlays.TryGetValue(owner, out int uid) ? uid : null;
        _combat.Roll("p", cardUid, entry.Source, winning.BaseRoll, winning.FinalRoll, winning.DiceSource, bonuses,
            TierName(winning, owner), winning.IsForcedCritical, RollKeywordRules.GetCriticalMinimum(owner),
            RollKeywordRules.GetAbsoluteCriticalMinimum(owner), discarded);
    });

    internal static void OnPotionUsing(PotionModel potion, Creature target) => GuardCombat("potion", (potion, target), static s =>
    {
        string by = _combat.KeyOf(s.potion?.Owner?.Creature);
        if (by != null) _combat.Potion(by, Id(s.potion.Id), _combat.KeyOf(s.target));
    });

    internal static void OnPotionProcured(PotionModel potion)
    {
        if (_record == null || _live == null) return;
        try
        {
            if (potion != null && ReferenceEquals(potion.Owner, _me)) _live.PotionsGained.Add(Id(potion.Id));
        }
        catch (Exception exception)
        {
            Fail("potion-gain", exception);
        }
    }

    internal static void OnDamageIncoming(Creature target) => GuardCombat("damage-before", target, static target =>
    {
        if (target != null && _combat.KeyOf(target) != null) DamageBefore[target] = (target.CurrentHp, DpOf(target));
    });

    internal static void OnDamageReceived(Creature target, DamageResult result, Creature dealer, CardModel cardSource) =>
        GuardCombat("damage", (target, result, dealer, cardSource), static s =>
        {
            Creature target = s.target;
            DamageResult result = s.result;
            string to = _combat.KeyOf(target);
            if (to == null || result == null) return;
            (int hpBefore, int dpBefore) = DamageBefore.TryGetValue(target, out var before)
                ? before
                : (target.CurrentHp + Math.Max(0, result.UnblockedDamage - result.OverkillDamage), DpOf(target));
            DamageBefore.Remove(target);
            int hpLoss = Math.Max(0, hpBefore - target.CurrentHp);
            int dpLoss = Math.Max(0, dpBefore - DpOf(target));
            int blocked = Math.Max(0, result.BlockedDamage);
            int overkill = Math.Max(0, result.OverkillDamage);
            JsonObject source = s.cardSource != null
                ? CombatRecordBuilder.Source("cardUid", _combat.Uid(s.cardSource))
                : s.dealer != null && CurrentMoves.TryGetValue(s.dealer, out string move) && move != null
                    ? CombatRecordBuilder.Source("move", move)
                    : null;
            _combat.Damage(_combat.KeyOf(s.dealer), to, source, blocked + dpLoss + hpLoss + overkill, blocked, hpLoss,
                dpLoss, overkill, result.WasTargetKilled);
        });

    internal static void OnBlockGained(Creature creature, decimal amount, CardModel cardSource) =>
        GuardCombat("block", (creature, amount, cardSource), static s =>
        {
            string to = _combat.KeyOf(s.creature);
            if (to == null) return;
            _combat.Block(to, s.cardSource != null ? CombatRecordBuilder.Source("cardUid", _combat.Uid(s.cardSource)) : null,
                ToInt(s.amount));
        });

    internal static void OnPowerChanged(PowerModel power, decimal amount, Creature applier, CardModel cardSource) =>
        GuardCombat("power", (power, amount, applier, cardSource), static s =>
        {
            string to = _combat.KeyOf(s.power?.Owner);
            if (to == null || s.amount == 0m) return;
            _combat.Power(to, _combat.KeyOf(s.applier),
                s.cardSource != null ? CombatRecordBuilder.Source("cardUid", _combat.Uid(s.cardSource)) : null,
                Id(s.power.Id), ToInt(s.amount), s.power.Amount);
        });

    internal static void OnMove(MonsterModel monster) => GuardCombat("move", monster, static monster =>
    {
        Creature creature = monster?.Creature;
        string by = _combat.KeyOf(creature);
        string move = monster?.NextMove?.Id;
        if (by == null || string.IsNullOrEmpty(move)) return;
        CurrentMoves[creature] = move;
        _combat.Move(by, move);
    });

    internal static void OnDeath(Creature creature) => GuardCombat("death", creature, static creature =>
    {
        string who = _combat.KeyOf(creature);
        if (who != null) _combat.Death(who);
    });

    internal static void OnCreatureAdded(ICombatState combat, Creature creature) =>
        GuardCombat("spawn", (combat, creature), static s =>
        {
            Creature creature = s.creature;
            if (creature == null || creature.IsPlayer || _combat.KeyOf(creature) != null) return;
            string id = CreatureId(creature);
            string key = creature.Side == CombatSide.Enemy
                ? _combat.Enemy(creature, id, EnemyParameter(creature), s.combat?.RoundNumber)
                : _combat.Summon(creature, id, _combat.KeyOf(creature.PetOwner?.Creature));
            _combat.Spawn(key, id);
        });

    internal static void OnCardExhausted(CardModel card) => GuardCombat("exhaust", card, static card =>
    {
        if (card != null && ReferenceEquals(card.Owner, _me)) _combat.CardEvent("exhaust", "p", CardJson(card));
    });

    internal static void OnCardGenerated(CardModel card, Player creator) => GuardCombat("generate", (card, creator), static s =>
    {
        if (s.card == null || !ReferenceEquals(s.card.Owner ?? s.creator, _me)) return;
        _combat.Generate("p", CardJson(s.card), PileName(s.card.Pile?.Type));
    });

    internal static void OnShuffle(Player shuffler) => GuardCombat("shuffle", shuffler, static shuffler =>
    {
        if (ReferenceEquals(shuffler, _me))
            _combat.Shuffle("p", shuffler.PlayerCombatState?.DrawPile?.Cards?.Count ?? 0);
    });

    // ---- floor-level hooks ----------------------------------------------------------------

    internal static void OnItemPurchased(Player player, MerchantEntry entry, int goldSpent) => Guard("purchase", () =>
    {
        if (_record == null || _live == null || !ReferenceEquals(player, _me) || entry == null) return;
        var bought = new JsonObject();
        switch (entry)
        {
            case MerchantCardEntry card:
                bought["kind"] = "card";
                if (card.CreationResult?.Card is { } model) bought["card"] = RunRecordBuilder.CardRef(Id(model.Id), model.CurrentUpgradeLevel);
                break;
            case MerchantRelicEntry relic:
                bought["kind"] = "relic";
                bought["id"] = Id(relic.Model?.Id);
                break;
            case MerchantPotionEntry potion:
                bought["kind"] = "potion";
                bought["id"] = Id(potion.Model?.Id);
                break;
            case MerchantCardRemovalEntry:
                bought["kind"] = "removal";
                break;
            default:
                // Not one of the documented kinds (schema-v1.md 6.6); left out.
                return;
        }
        bought["price"] = goldSpent;
        _live.Bought.Add(bought);
    });

    internal static void OnSixSenseAllocated(ElectronicSoldierManual manual, IReadOnlyList<int> increases) => Guard("senses", () =>
    {
        if (_record == null || _live == null || manual == null || !ReferenceEquals(manual.Owner, _me) || increases == null) return;
        var allocation = new JsonObject();
        for (int i = 0; i < increases.Count && i < ElectronicSoldierManual.AllSixSenseAttributes.Count; i++)
            if (increases[i] != 0) allocation[SenseKey(ElectronicSoldierManual.AllSixSenseAttributes[i])] = increases[i];
        if (allocation.Count > 0) _live.SenseAllocations.Add(allocation);
    });

    internal static void OnEquipmentCommand(ElectronicSoldierManual manual, HbrEquipmentCommand command,
        HbrEquipmentState before) => Guard("equipment", () =>
    {
        if (_record == null || _live == null || manual == null || command == null || before == null ||
            !ReferenceEquals(manual.Owner, _me)) return;
        HbrEquipmentState after = manual.Equipment;
        if (after == null || after.Revision == before.Revision) return;
        int tier = manual.EquipmentTier;
        switch (command.Op)
        {
            case HbrEquipmentOp.Buy:
            {
                HbrShopOffer offer = HbrEquipmentShop.Find(command.Offer, tier);
                string action = offer?.Kind == HbrShopOfferKind.Craft ? "alchemy" : "buy";
                foreach (HbrEquipmentItem item in after.Items.Where(item => before.FindItem(item.Id) == null))
                    _live.Equipment.Add(EquipmentAction(action, item, offer?.Price));
                break;
            }
            case HbrEquipmentOp.Sell:
            {
                HbrEquipmentItem item = before.FindItem(command.ItemId);
                if (item != null) _live.Equipment.Add(EquipmentAction("sell", item, HbrEquipmentShop.SellPrice(item)));
                break;
            }
            case HbrEquipmentOp.Claim:
                foreach (HbrEquipmentItem item in after.Items.Where(item => before.FindItem(item.Id) == null))
                    _live.Equipment.Add(EquipmentAction("drop", item, null));
                break;
            case HbrEquipmentOp.Equip:
            case HbrEquipmentOp.Unequip:
            {
                HbrEquipmentSlot slot = HbrEquipmentSlot.FromPosition(command.Slot);
                var action = new JsonObject
                {
                    ["action"] = command.Op == HbrEquipmentOp.Equip ? "equip" : "unequip",
                    ["itemId"] = command.Op == HbrEquipmentOp.Equip ? command.ItemId : before.Equipped.ElementAtOrDefault(command.Slot),
                    ["slot"] = KindName(slot.Kind),
                    ["index"] = slot.Index
                };
                _live.Equipment.Add(action);
                break;
            }
            case HbrEquipmentOp.OpenPlus:
            case HbrEquipmentOp.ChangePlus:
                if (after.FindItem(command.ItemId) is { } changed) _live.Equipment.Add(EquipmentAction("plus", changed, null));
                break;
        }
    });

    // ---- floors ------------------------------------------------------------------------

    private static void EnsureAct(IRunState state)
    {
        if (_record == null || state == null) return;
        JsonObject act = _record.CurrentAct;
        // The map is built only once per act.
        if (act?["index"]?.GetValue<int>() == state.CurrentActIndex && act["map"] != null) return;
        _record.EnsureAct(state.CurrentActIndex, Id(state.Act?.Id), MapJson(state.Map));
    }

    private static void AddRoom(AbstractRoom room)
    {
        bool hasModel = room.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss or RoomType.Event;
        _record.AddRoom(RoomTypeName(room.RoomType), hasModel ? Id(room.ModelId) : null, null);
    }

    private static bool LastRoomMatches(AbstractRoom room)
    {
        JsonObject last = _record.CurrentRoom;
        return last != null && last["type"]?.GetValue<string>() == RoomTypeName(room.RoomType) &&
               (last["model"]?.GetValue<string>() ?? Id(room.ModelId)) == Id(room.ModelId);
    }

    private static bool IsCombatRoom(JsonObject room) =>
        room["type"]?.GetValue<string>() is "monster" or "elite" or "boss";

    // Choices, stats and HBR changes of the floor being left, from the game's
    // own per-floor history plus what the recorder saw live.
    private static void FinalizeFloor(IRunState state)
    {
        JsonObject floor = _record?.CurrentFloor;
        if (floor == null || floor["stats"] != null || _me == null) return;
        PlayerMapPointHistoryEntry entry = null;
        MapPointHistoryEntry point = FindHistory(state, floor);
        if (point != null)
        {
            entry = point.GetEntry(_me.NetId);
            if (floor["rooms"] is JsonArray rooms && point.Rooms != null)
                for (int i = 0; i < rooms.Count && i < point.Rooms.Count; i++)
                    if (rooms[i] is JsonObject room && IsCombatRoom(room))
                        room["turns"] = point.Rooms[i].TurnsTaken;
        }
        _record.SetFloorSection("choices", Choices(entry, floor));
        _record.SetFloorSection("hbr", FloorHbr(floor));
        DeflectorProtection protection = _me.GetRelic<DeflectorProtection>();
        Creature creature = _me.Creature;
        JsonObject stats = entry != null
            ? RunRecordBuilder.Stats(entry.CurrentHp, entry.MaxHp, protection?.Dp, protection?.MaxDp, entry.CurrentGold,
                entry.GoldGained, entry.GoldSpent, entry.GoldLost, entry.GoldStolen, entry.DamageTaken, entry.HpHealed,
                entry.MaxHpGained, entry.MaxHpLost)
            : RunRecordBuilder.Stats(creature?.CurrentHp ?? 0, creature?.MaxHp ?? 0, protection?.Dp, protection?.MaxDp,
                _me.Gold, 0, 0, 0, 0, 0, 0, 0, 0);
        _record.SetFloorSection("stats", stats);
        _live = null;
    }

    private static MapPointHistoryEntry FindHistory(IRunState state, JsonObject floor)
    {
        IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> acts = state?.MapPointHistory;
        int act = _record.CurrentAct?["index"]?.GetValue<int>() ?? -1;
        if (acts == null || act < 0 || act >= acts.Count || acts[act] == null) return null;
        IReadOnlyList<MapPointHistoryEntry> points = acts[act];
        string type = floor["pointType"]?.GetValue<string>();
        int index = (floor["actFloor"]?.GetValue<int>() ?? 0) - 1;
        if (index >= 0 && index < points.Count && PointTypeName(points[index]?.MapPointType) == type) return points[index];
        for (int i = points.Count - 1; i >= 0; i--)
            if (PointTypeName(points[i]?.MapPointType) == type && i <= index) return points[i];
        return null;
    }

    private static JsonObject Choices(PlayerMapPointHistoryEntry entry, JsonObject floor)
    {
        var choices = new JsonObject();
        string eventId = (floor["rooms"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(room => room["type"]?.GetValue<string>() == "event")?["model"]?.GetValue<string>();
        if (entry != null)
        {
            if (entry.CardChoices is { Count: > 0 } cardChoices)
            {
                var offered = new JsonArray();
                var picked = new JsonArray();
                foreach (CardChoiceHistoryEntry choice in cardChoices)
                {
                    if (choice.Card == null) continue;
                    offered.Add(CardChoiceJson(choice.Card));
                    if (choice.wasPicked) picked.Add(CardChoiceJson(choice.Card));
                }
                choices["cardChoices"] = new JsonArray(new JsonObject
                {
                    ["source"] = CardChoiceSource(floor),
                    ["offered"] = offered,
                    ["picked"] = picked,
                    ["skipped"] = picked.Count == 0
                });
            }
            AddModelChoices(choices, "relicChoices", entry.RelicChoices);
            AddModelChoices(choices, "potionChoices", entry.PotionChoices);
            if (entry.AncientChoices is { Count: > 0 } ancient)
            {
                var offered = new JsonArray();
                JsonObject picked = null;
                foreach (AncientChoiceHistoryEntry choice in ancient)
                {
                    offered.Add(RunRecordBuilder.Option(choice.TextKey, Text(choice.Title)));
                    if (choice.WasChosen) picked ??= RunRecordBuilder.Option(choice.TextKey, Text(choice.Title));
                }
                choices["ancientChoices"] = new JsonArray(new JsonObject { ["offered"] = offered, ["picked"] = picked });
            }
            if (entry.EventChoices is { Count: > 0 } events)
            {
                var list = new JsonArray();
                foreach (EventOptionHistoryEntry option in events)
                {
                    string key = option.Title?.LocEntryKey;
                    var item = new JsonObject();
                    if (eventId != null) item["event"] = eventId;
                    if (PageOf(key) is { } page) item["page"] = page;
                    item["picked"] = RunRecordBuilder.Option(key, Text(option.Title), vars: Vars(option.Variables));
                    list.Add(item);
                }
                choices["eventChoices"] = list;
            }
            if (entry.RestSiteChoices is { Count: > 0 } rest)
            {
                var list = new JsonArray();
                foreach (string choice in rest)
                {
                    var item = new JsonObject { ["choice"] = choice };
                    if (choice == "SMITH" && entry.UpgradedCards is { Count: 1 } upgraded)
                        item["card"] = RunRecordBuilder.CardRef(Id(upgraded[0]), DeckUpgrade(upgraded[0]));
                    if (choice == "HEAL" && entry.HpHealed > 0) item["healed"] = entry.HpHealed;
                    list.Add(item);
                }
                choices["rest"] = list;
            }
        }

        if (_live is { Bought.Count: > 0 })
        {
            var bought = (JsonArray)_live.Bought.DeepClone();
            if (entry?.CardsRemoved is { Count: > 0 } removed)
            {
                int next = 0;
                foreach (JsonObject item in bought.OfType<JsonObject>())
                    if (item["kind"]?.GetValue<string>() == "removal" && next < removed.Count)
                        item["card"] = CardJson(removed[next++]);
            }
            choices["shop"] = new JsonObject { ["bought"] = bought };
        }

        if (entry != null)
        {
            AddCards(choices, "cardsGained", entry.CardsGained);
            AddCards(choices, "cardsRemoved", entry.CardsRemoved);
            AddUpgrades(choices, "cardsUpgraded", entry.UpgradedCards);
            AddUpgrades(choices, "cardsDowngraded", entry.DowngradedCards);
            if (entry.CardsTransformed is { Count: > 0 } transformed)
                choices["cardsTransformed"] = new JsonArray(transformed
                    .Select(t => (JsonNode)new JsonObject { ["from"] = CardJson(t.OriginalCard), ["to"] = CardJson(t.FinalCard) })
                    .ToArray());
            if (entry.CardsEnchanted is { Count: > 0 } enchanted)
                choices["cardsEnchanted"] = new JsonArray(enchanted
                    .Select(e => (JsonNode)new JsonObject { ["card"] = CardJson(e.Card), ["enchantment"] = Id(e.Enchantment) })
                    .ToArray());
        }

        if (_live != null && _me != null)
        {
            var gained = new JsonArray();
            List<string> before = _live.Relics.ToList();
            foreach (RelicModel relic in _me.Relics)
            {
                string id = Id(relic?.Id);
                if (id == null) continue;
                if (before.Remove(id)) continue;
                gained.Add(new JsonObject { ["id"] = id, ["source"] = RelicSource(floor) });
            }
            if (gained.Count > 0) choices["relicsGained"] = gained;
        }
        if (entry?.RelicsRemoved is { Count: > 0 } relicsRemoved) choices["relicsRemoved"] = Ids(relicsRemoved);
        if (_live is { PotionsGained.Count: > 0 }) choices["potionsGained"] = RunRecordBuilder.Strings(_live.PotionsGained);
        if (entry?.PotionUsed is { Count: > 0 } used) choices["potionsUsed"] = Ids(used);
        if (entry?.PotionDiscarded is { Count: > 0 } discarded) choices["potionsDiscarded"] = Ids(discarded);
        if (entry?.CompletedQuests is { Count: > 0 } quests) choices["questsCompleted"] = Ids(quests);
        return choices;
    }

    private static void AddModelChoices(JsonObject choices, string name, List<ModelChoiceHistoryEntry> list)
    {
        if (list is not { Count: > 0 }) return;
        var offered = new JsonArray();
        string picked = null;
        foreach (ModelChoiceHistoryEntry choice in list)
        {
            string id = Id(choice.choice);
            if (id == null) continue;
            offered.Add(id);
            if (choice.wasPicked) picked ??= id;
        }
        if (offered.Count > 0) choices[name] = new JsonArray(new JsonObject { ["offered"] = offered, ["picked"] = picked });
    }

    private static void AddCards(JsonObject choices, string name, List<SerializableCard> cards)
    {
        if (cards is { Count: > 0 })
            choices[name] = new JsonArray(cards.Where(card => card != null).Select(card => (JsonNode)CardJson(card)).ToArray());
    }

    private static void AddUpgrades(JsonObject choices, string name, List<ModelId> ids)
    {
        if (ids is { Count: > 0 })
            choices[name] = new JsonArray(ids.Select(id => (JsonNode)RunRecordBuilder.CardRef(Id(id), DeckUpgrade(id))).ToArray());
    }

    private static JsonObject FloorHbr(JsonObject floor)
    {
        var hbr = new JsonObject();
        ElectronicSoldierManual manual = _me?.GetRelic<ElectronicSoldierManual>();
        if (_live == null || manual == null) return hbr;
        int level = manual.HbrBattleLevel;
        if (level != _live.Level)
        {
            hbr["levelBefore"] = _live.Level;
            hbr["levelAfter"] = level;
        }
        var allocated = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (JsonObject allocation in _live.SenseAllocations.OfType<JsonObject>())
            foreach (KeyValuePair<string, JsonNode> pair in allocation)
                allocated[pair.Key] = allocated.GetValueOrDefault(pair.Key) + pair.Value.GetValue<int>();
        int gained = manual.SixSensePoints - _live.Unspent + allocated.Values.Sum();
        if (gained > 0) hbr["senseGained"] = gained;
        if (_live.SenseAllocations.Count > 0) hbr["senseAllocations"] = _live.SenseAllocations.DeepClone();
        var changes = new JsonArray();
        string source = (floor["rooms"] as JsonArray)?.OfType<JsonObject>().Select(room => room["model"]?.GetValue<string>())
            .LastOrDefault(model => model != null) ?? floor["pointType"]?.GetValue<string>();
        foreach (SixSenseAttribute attribute in ElectronicSoldierManual.AllSixSenseAttributes)
        {
            string key = SenseKey(attribute);
            int delta = manual.GetSenseAmount(attribute) - _live.Senses.GetValueOrDefault(key) - allocated.GetValueOrDefault(key);
            if (delta != 0) changes.Add(new JsonObject { ["attribute"] = key, ["delta"] = delta, ["source"] = source });
        }
        if (changes.Count > 0) hbr["senseChanges"] = changes;
        if (_live.Equipment.Count > 0) hbr["equipment"] = _live.Equipment.DeepClone();
        return hbr;
    }

    // ---- snapshots -----------------------------------------------------------------------

    private static JsonObject StartSnapshot(ICombatState combat)
    {
        var start = new JsonObject();
        foreach (Creature creature in combat.Creatures ?? Array.Empty<Creature>())
        {
            string key = _combat.KeyOf(creature);
            if (key == null) continue;
            start[key] = CreatureState(creature, key == "p");
        }
        return start;
    }

    private static JsonObject EndSnapshot(ICombatState combat)
    {
        var end = new JsonObject();
        foreach (Creature creature in combat?.Creatures ?? Array.Empty<Creature>())
        {
            string key = _combat.KeyOf(creature);
            if (key == null) continue;
            end[key] = RunRecordBuilder.EndState(creature.CurrentHp, creature.Block, DpOrNull(creature));
        }
        return end;
    }

    private static JsonObject CreatureState(Creature creature, bool local)
    {
        DeflectorProtection protection = creature.Player != null && ReferenceEquals(creature.Player.Creature, creature)
            ? creature.Player.GetRelic<DeflectorProtection>()
            : null;
        int? dp = DpOrNull(creature);
        int? maxDp = protection?.MaxDp;
        int? energy = null, stars = null, od = null;
        JsonObject piles = null;
        if (local && creature.Player?.PlayerCombatState is { } combatState)
        {
            energy = combatState.Energy;
            stars = combatState.Stars;
            od = creature.Player.GetRelic<Overclock>()?.Charge;
            piles = RunRecordBuilder.Piles(combatState.Hand?.Cards?.Count ?? 0, combatState.DrawPile?.Cards?.Count ?? 0,
                combatState.DiscardPile?.Cards?.Count ?? 0, combatState.ExhaustPile?.Cards?.Count ?? 0);
        }
        var powers = new JsonArray();
        foreach (PowerModel power in creature.Powers ?? Array.Empty<PowerModel>())
            if (power != null) powers.Add(RunRecordBuilder.Power(Id(power.Id), power.Amount));
        return RunRecordBuilder.CreatureState(creature.CurrentHp, creature.MaxHp, creature.Block, dp, maxDp, energy,
            stars, od, piles, powers, creature.Side == CombatSide.Enemy ? Intents(creature) : null);
    }

    private static JsonArray Intents(Creature creature)
    {
        var intents = new JsonArray();
        var move = creature.Monster?.NextMove;
        if (move?.Intents == null) return intents;
        Creature[] targets = _me?.Creature != null ? [_me.Creature] : Array.Empty<Creature>();
        foreach (AbstractIntent intent in move.Intents)
        {
            if (intent == null) continue;
            int? damage = null, hits = null, cards = null;
            try
            {
                if (intent is AttackIntent attack)
                {
                    damage = attack.GetSingleDamage(targets, creature);
                    hits = attack.Repeats;
                }
                if (intent is StatusIntent status) cards = status.CardCount;
            }
            catch (Exception)
            {
                // A preview that cannot be computed is left out.
            }
            intents.Add(RunRecordBuilder.Intent(Snake(intent.IntentType.ToString()), move.Id, damage, hits, cards));
        }
        return intents;
    }

    private static JsonObject HbrSnapshot(Player player, bool includeInventory)
    {
        ElectronicSoldierManual manual = player?.GetRelic<ElectronicSoldierManual>();
        if (manual == null) return null;
        HbrBattleStats stats = manual.GetHbrBattleStats();
        HbrEquipmentBonus bonus = manual.GetEquipmentBonus();
        HbrEquipmentState state = manual.Equipment;
        var equipped = new JsonArray();
        for (int position = 0; state?.Equipped != null && position < state.Equipped.Length; position++)
        {
            int itemId = state.Equipped[position];
            if (itemId <= 0 || state.FindItem(itemId) is not { } item) continue;
            HbrEquipmentSlot slot = HbrEquipmentSlot.FromPosition(position);
            equipped.Add(new JsonObject { ["slot"] = KindName(slot.Kind), ["index"] = slot.Index, ["item"] = ItemJson(item) });
        }
        var equipment = new JsonObject
        {
            ["equipped"] = equipped,
            ["bonus"] = new JsonObject
            {
                ["strength"] = bonus.Strength,
                ["dexterity"] = bonus.Dexterity,
                ["intelligence"] = bonus.Intelligence,
                ["vitality"] = bonus.Vitality,
                ["spirit"] = bonus.Spirit,
                ["luck"] = bonus.Luck,
                ["dp"] = bonus.Dp,
                ["critRatePercent"] = bonus.CriticalRate / 100m
            },
            ["effects"] = EffectsJson(manual.GetEquipmentEffects())
        };
        if (includeInventory && state?.Items != null)
            equipment["inventory"] = new JsonArray(state.Items.Select(item => (JsonNode)ItemJson(item)).ToArray());
        return RunRecordBuilder.Hbr(manual.HbrBattleLevel,
            RunRecordBuilder.SixStats(stats.Strength, stats.Dexterity, stats.Intelligence, stats.Vitality, stats.Spirit, stats.Luck),
            SensesJson(manual), manual.SixSensePoints, bonus.CriticalRate / 100m, equipment);
    }

    private static JsonObject SensesJson(ElectronicSoldierManual manual) => RunRecordBuilder.Senses(
        manual.GetSenseAmount(SixSenseAttribute.Madness),
        manual.GetSenseAmount(SixSenseAttribute.LeadershipCharm),
        manual.GetSenseAmount(SixSenseAttribute.Talent),
        manual.GetSenseAmount(SixSenseAttribute.Gentleness),
        manual.GetSenseAmount(SixSenseAttribute.MentalStrength),
        manual.GetSenseAmount(SixSenseAttribute.Simplicity));

    private static JsonObject EffectsJson(HbrEquipmentEffectTotals totals)
    {
        var effects = new JsonObject();
        foreach (HbrEquipmentEffect effect in Enum.GetValues<HbrEquipmentEffect>())
        {
            decimal value = totals[effect];
            if (value != 0m) effects[Camel(effect.ToString())] = value;
        }
        return effects;
    }

    private static JsonObject ItemJson(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item.Template);
        return RunRecordBuilder.EquipmentItem(item.Id, item.Template, template != null ? KindName(template.Kind) : null,
            template?.Rarity ?? 0, template?.Name, item.Rolls,
            (item.Plus ?? Array.Empty<HbrPlusSlot>()).Select(plus => (StatName(plus.Stat), plus.Level)));
    }

    private static JsonObject EquipmentAction(string action, HbrEquipmentItem item, int? price)
    {
        var json = new JsonObject { ["action"] = action, ["item"] = ItemJson(item) };
        if (price.HasValue) json["price"] = price.Value;
        return json;
    }

    private static JsonObject FinalSnapshot()
    {
        Creature creature = _me.Creature;
        DeflectorProtection protection = _me.GetRelic<DeflectorProtection>();
        var final = new JsonObject
        {
            ["floor"] = _record.FloorCount,
            ["hp"] = creature?.CurrentHp ?? 0,
            ["maxHp"] = creature?.MaxHp ?? 0
        };
        if (protection != null)
        {
            final["dp"] = protection.Dp;
            final["maxDp"] = protection.MaxDp;
        }
        final["gold"] = _me.Gold;
        var deck = new JsonArray();
        foreach (CardModel card in _me.Deck?.Cards ?? Array.Empty<CardModel>())
        {
            if (card == null) continue;
            JsonObject json = RunRecordBuilder.CardRef(Id(card.Id), card.CurrentUpgradeLevel, Id(card.Enchantment?.Id));
            if (card.FloorAddedToDeck is int added) json["floorAdded"] = added;
            deck.Add(json);
        }
        final["deck"] = deck;
        var relics = new JsonArray();
        foreach (RelicModel relic in _me.Relics ?? Array.Empty<RelicModel>())
            if (relic != null) relics.Add(new JsonObject { ["id"] = Id(relic.Id), ["floorAdded"] = relic.FloorAddedToDeck });
        final["relics"] = relics;
        final["potions"] = RunRecordBuilder.Strings((_me.Potions ?? Enumerable.Empty<PotionModel>()).Select(potion => Id(potion?.Id)));
        if (HbrSnapshot(_me, includeInventory: true) is { } hbr) final["hbr"] = hbr;
        return final;
    }

    private static void FinishCombat(ICombatState combat, string outcome)
    {
        if (_combat == null) return;
        if (_combat.RoundCount > 0 && !_combat.RoundEnded && combat != null) _combat.EndRound(EndSnapshot(combat));
        Creature creature = _me?.Creature;
        DeflectorProtection protection = _me?.GetRelic<DeflectorProtection>();
        var player = new JsonObject { ["hp"] = creature?.CurrentHp ?? 0, ["maxHp"] = creature?.MaxHp ?? 0 };
        if (protection != null)
        {
            player["dp"] = protection.Dp;
            player["maxDp"] = protection.MaxDp;
        }
        _combat.Finish(outcome, player);
        _lastCombat = _combat.Combat;
        _combat = null;
        ClearCombatState();
    }

    private static void ClearCombatState()
    {
        DamageBefore.Clear();
        CurrentMoves.Clear();
        OpenPlays.Clear();
    }

    // ---- header, map and names ------------------------------------------------------------

    // Always a whole run: recorder.partial false, fromFloor 1.
    private static RunRecordHeader Header(RunState state, DateTimeOffset startedAt)
    {
        IReadOnlyList<Player> players = state.Players ?? Array.Empty<Player>();
        JsonObject multiplayer = null;
        if (players.Count > 1)
        {
            var allies = new JsonArray();
            foreach (Player player in players)
                if (player != null && !ReferenceEquals(player, _me))
                    allies.Add(new JsonObject { ["seat"] = Seat(player), ["character"] = Id(player.Character?.Id) });
            multiplayer = new JsonObject
            {
                ["playerCount"] = players.Count,
                ["localSeat"] = _me != null ? Seat(_me) : 0,
                ["allies"] = allies
            };
        }
        return new RunRecordHeader
        {
            Character = Id(_me?.Character?.Id),
            Ascension = state.AscensionLevel,
            Seed = state.Rng?.StringSeed,
            GameMode = Snake(state.GameMode.ToString()),
            Modifiers = (state.Modifiers ?? Array.Empty<ModifierModel>()).Select(modifier => Id(modifier?.Id)).ToList(),
            StartedAt = startedAt,
            Locale = LocManager.Instance?.Language,
            Multiplayer = multiplayer,
            Rules =new JsonObject { ["rollTiers"] = RollTiers() },
            Integrity = RunUploadService.SessionIntegrity
        };
    }

    // The tier minimums are read from the rules themselves, so the record
    // states the thresholds the game actually used.
    private static JsonObject RollTiers()
    {
        var tiers = new JsonObject();
        for (int roll = RollKeywordRules.RollMinimum; roll <= RollKeywordRules.RollMaximum; roll++)
        {
            SoulRollTier tier = RollKeywordRules.GetRollTier(null, SoulRollBreakdown.FromFinalRoll(roll));
            if (tier is SoulRollTier.None or SoulRollTier.Critical) continue;
            string name = TierName(tier);
            if (!tiers.ContainsKey(name)) tiers[name] = roll;
        }
        tiers["max"] = RollKeywordRules.RollMaximum;
        return tiers;
    }

    private static JsonObject MapJson(ActMap map)
    {
        if (map == null) return null;
        var points = new List<MapPoint>();
        void Add(MapPoint point)
        {
            if (point != null && !points.Contains(point)) points.Add(point);
        }
        foreach (MapPoint point in map.GetAllMapPoints() ?? Enumerable.Empty<MapPoint>()) Add(point);
        Add(map.StartingMapPoint);
        Add(map.BossMapPoint);
        Add(map.SecondBossMapPoint);
        IEnumerable<RunMapNode> nodes = points
            .OrderBy(point => point.coord.row).ThenBy(point => point.coord.col)
            .Select(point => new RunMapNode(point.coord.col, point.coord.row, PointTypeName(point.PointType),
                (point.Children ?? new HashSet<MapPoint>())
                .OrderBy(child => child.coord.row).ThenBy(child => child.coord.col)
                .Select(child => (child.coord.col, child.coord.row)).ToList()));
        return RunRecordBuilder.Map(map.GetColumnCount(), map.GetRowCount(), Coord(map.StartingMapPoint),
            Coord(map.BossMapPoint), Coord(map.SecondBossMapPoint), nodes.ToList());
    }

    private static Dictionary<string, string> ResolveNames(IEnumerable<string> ids)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            try
            {
                int dot = id.IndexOf('.');
                if (dot <= 0) continue;
                AbstractModel model = ModelDb.GetByIdOrNull<AbstractModel>(new ModelId(id.Substring(0, dot), id.Substring(dot + 1)));
                object title = model?.GetType().GetProperty("Title", BindingFlags.Public | BindingFlags.Instance)?.GetValue(model);
                string text = title switch
                {
                    string value => value,
                    LocString loc => Text(loc),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(text)) names[id] = text;
            }
            catch (Exception)
            {
                // A name that cannot be resolved is left out; the site shows the id.
            }
        }
        return names;
    }

    // ---- persistence ------------------------------------------------------------------

    private static void Persist()
    {
        if (_record == null) return;
        RunUploadService.SaveInProgress(Gate.RunKey, _record.Run, _live?.ToJson());
    }

    // ---- small helpers -------------------------------------------------------------------

    private static Player LocalPlayer(RunState state)
    {
        IReadOnlyList<Player> players = state?.Players;
        if (players == null || players.Count == 0) return null;
        return LocalContext.GetMe(players) ?? (players.Count == 1 ? players[0] : null);
    }

    private static string RunKey(RunState state) => state == null ? null : $"{state.Rng?.StringSeed}|{state.AscensionLevel}";

    private static int Seat(Player player) => _runState?.GetPlayerSlotIndex(player) ?? 0;

    internal static string Id(ModelId id) => id == null ? null : id.ToString();

    private static string RealId(ModelId id)
    {
        string text = Id(id);
        return string.IsNullOrEmpty(text) || text.StartsWith("NONE.", StringComparison.Ordinal) ? null : text;
    }

    private static string CreatureId(Creature creature) => Id(creature?.Monster?.Id ?? creature?.ModelId);

    private static decimal? EnemyParameter(Creature creature)
    {
        try
        {
            decimal value = HbrEnemyProfileHelper.GetEnemyParameter(creature);
            return value > 0m ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int DpOf(Creature creature) => DpOrNull(creature) ?? 0;

    private static int? DpOrNull(Creature creature)
    {
        if (creature == null) return null;
        if (creature.Player != null && ReferenceEquals(creature.Player.Creature, creature) &&
            creature.Player.GetRelic<DeflectorProtection>() is { } protection)
            return protection.Dp;
        return creature.GetPower<HighDimensionalShellPower>() is { } shell ? ToInt(shell.CurrentDp) : null;
    }

    private static int DeckUpgrade(ModelId id)
    {
        int best = 0;
        foreach (CardModel card in _me?.Deck?.Cards ?? Array.Empty<CardModel>())
            if (card != null && Equals(card.Id, id)) best = Math.Max(best, card.CurrentUpgradeLevel);
        return Math.Max(1, best);
    }

    private static JsonObject CardJson(CardModel card) => _combat != null
        ? _combat.Card(card, Id(card.Id), card.CurrentUpgradeLevel)
        : RunRecordBuilder.CardRef(Id(card.Id), card.CurrentUpgradeLevel);

    private static JsonObject CardJson(SerializableCard card) =>
        RunRecordBuilder.CardRef(Id(card?.Id), card?.CurrentUpgradeLevel ?? 0);

    // Card choices also carry the rarity (schema-v1.md 6.1).
    private static JsonObject CardChoiceJson(SerializableCard card) =>
        RunRecordBuilder.CardRef(Id(card?.Id), card?.CurrentUpgradeLevel ?? 0, null, RarityOf(card?.Id));

    private static string RarityOf(ModelId id)
    {
        if (id == null) return null;
        if (Rarities.TryGetValue(id, out string cached)) return cached;
        string rarity = null;
        try
        {
            CardRarity value = ModelDb.GetByIdOrNull<CardModel>(id)?.Rarity ?? CardRarity.None;
            if (value != CardRarity.None) rarity = Snake(value.ToString());
        }
        catch (Exception)
        {
            // Left out when the card cannot be looked up.
        }
        Rarities[id] = rarity;
        return rarity;
    }

    private static JsonArray Ids(IEnumerable<ModelId> ids) => RunRecordBuilder.Strings(ids.Select(Id));

    private static JsonObject Vars(Dictionary<string, object> variables)
    {
        if (variables is not { Count: > 0 }) return null;
        var vars = new JsonObject();
        foreach (KeyValuePair<string, object> pair in variables)
        {
            switch (pair.Value)
            {
                case int value: vars[pair.Key] = value; break;
                case long value: vars[pair.Key] = value; break;
                case decimal value: vars[pair.Key] = value; break;
                case double value: vars[pair.Key] = value; break;
                case float value: vars[pair.Key] = value; break;
                case bool value: vars[pair.Key] = value; break;
                case string value: vars[pair.Key] = RunRecordBuilder.Text(value); break;
            }
        }
        return vars;
    }

    private static string PageOf(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        int start = key.IndexOf(".pages.", StringComparison.Ordinal);
        if (start < 0) return null;
        start += ".pages.".Length;
        int end = key.IndexOf('.', start);
        return end > start ? key.Substring(start, end - start) : null;
    }

    private static string CardChoiceSource(JsonObject floor)
    {
        string type = (floor["rooms"] as JsonArray)?.OfType<JsonObject>().LastOrDefault()?["type"]?.GetValue<string>();
        return type switch
        {
            "monster" or "elite" or "boss" => "reward",
            "event" => "event",
            "shop" => "shop",
            _ => "other"
        };
    }

    private static string RelicSource(JsonObject floor) =>
        floor["pointType"]?.GetValue<string>() == "ancient"
            ? "ancient"
            : (floor["rooms"] as JsonArray)?.OfType<JsonObject>().LastOrDefault()?["type"]?.GetValue<string>() switch
            {
                "monster" => "combat",
                "elite" => "elite",
                "boss" => "boss",
                "treasure" => "treasure",
                "shop" => "shop",
                "event" => "event",
                _ => "other"
            };

    private static bool SameCoord(JsonNode at, MapCoord? coord) =>
        at is JsonArray { Count: 2 } array && coord is { } c &&
        array[0]?.GetValue<int>() == c.col && array[1]?.GetValue<int>() == c.row;

    private static (int Col, int Row)? Coord(MapPoint point) => point == null ? null : (point.coord.col, point.coord.row);

    private static string PointTypeName(MapPointType? type) => type is { } value ? Snake(value.ToString()) : "unknown";

    private static string RoomTypeName(RoomType type) => Snake(type.ToString());

    private static string PileName(PileType? type) =>
        type is PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust ? Snake(type.Value.ToString()) : null;

    private static string KindName(HbrEquipmentKind kind) => Snake(kind.ToString());

    private static string StatName(HbrEquipmentStat stat) => stat == HbrEquipmentStat.CriticalRate ? "critRatePercent" : Camel(stat.ToString());

    private static string SenseKey(SixSenseAttribute attribute) => Camel(attribute.ToString());

    private static string TierName(SoulRollTier tier) => Snake(tier.ToString());

    private static string TierName(SoulRollBreakdown roll, Player owner) =>
        TierName(roll.ResolvedTier ?? RollKeywordRules.GetRollTier(owner, roll));

    private static string Text(LocString loc)
    {
        if (loc == null) return null;
        try
        {
            return RunRecordBuilder.Text(loc.GetFormattedText());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int ToInt(decimal value) => (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);

    private static string Snake(string name) => RunRecordNames.Snake(name);

    private static string Camel(string name) => RunRecordNames.Camel(name);

    private static void Guard(string where, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Fail(where, exception);
        }
    }

    private static void GuardCombat<T>(string where, T state, Action<T> action)
    {
        if (_combat == null) return;
        try
        {
            action(state);
        }
        catch (Exception exception)
        {
            Fail(where, exception);
        }
    }

    private static void Fail(string where, Exception exception)
    {
        if (ReportedFailures.Add(where)) RunUploadService.Log($"Run recorder failed at {where}: {exception}");
        _record?.Note($"记录器在 {where} 出错，这部分记录可能不完整");
    }

    // What a floor needs from its start, and what the recorder saw during it.
    private sealed class FloorLive
    {
        internal int Level;
        internal int Unspent;
        internal Dictionary<string, int> Senses = new(StringComparer.Ordinal);
        internal List<string> Relics = new();
        internal JsonArray SenseAllocations = new();
        internal JsonArray Equipment = new();
        internal JsonArray Bought = new();
        internal List<string> PotionsGained = new();

        internal static FloorLive Capture(Player player)
        {
            var live = new FloorLive();
            if (player == null) return live;
            live.Relics = (player.Relics ?? Array.Empty<RelicModel>()).Select(relic => Id(relic?.Id)).Where(id => id != null).ToList();
            if (player.GetRelic<ElectronicSoldierManual>() is { } manual)
            {
                live.Level = manual.HbrBattleLevel;
                live.Unspent = manual.SixSensePoints;
                foreach (SixSenseAttribute attribute in ElectronicSoldierManual.AllSixSenseAttributes)
                    live.Senses[SenseKey(attribute)] = manual.GetSenseAmount(attribute);
            }
            return live;
        }

        internal JsonObject ToJson()
        {
            var senses = new JsonObject();
            foreach (KeyValuePair<string, int> pair in Senses) senses[pair.Key] = pair.Value;
            return new JsonObject
            {
                ["level"] = Level,
                ["unspent"] = Unspent,
                ["senses"] = senses,
                ["relics"] = RunRecordBuilder.Strings(Relics),
                ["senseAllocations"] = SenseAllocations.DeepClone(),
                ["equipment"] = Equipment.DeepClone(),
                ["bought"] = Bought.DeepClone(),
                ["potionsGained"] = RunRecordBuilder.Strings(PotionsGained)
            };
        }

        internal static FloorLive FromJson(JsonObject json)
        {
            if (json == null) return null;
            var live = new FloorLive
            {
                Level = json["level"]?.GetValue<int>() ?? 0,
                Unspent = json["unspent"]?.GetValue<int>() ?? 0
            };
            if (json["senses"] is JsonObject senses)
                foreach (KeyValuePair<string, JsonNode> pair in senses) live.Senses[pair.Key] = pair.Value?.GetValue<int>() ?? 0;
            if (json["relics"] is JsonArray relics) live.Relics = relics.Select(node => node?.GetValue<string>()).Where(id => id != null).ToList();
            if (json["senseAllocations"] is JsonArray allocations) live.SenseAllocations = (JsonArray)allocations.DeepClone();
            if (json["equipment"] is JsonArray equipment) live.Equipment = (JsonArray)equipment.DeepClone();
            if (json["bought"] is JsonArray bought) live.Bought = (JsonArray)bought.DeepClone();
            if (json["potionsGained"] is JsonArray potions)
                live.PotionsGained = potions.Select(node => node?.GetValue<string>()).Where(id => id != null).ToList();
            return live;
        }
    }
}
