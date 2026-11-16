using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

static partial class EffectChecks
{
    static readonly List<GameAction> MagicSwordCancelledProbes = [];

    sealed class MagicSwordQueueProbe(ulong owner, GameActionType type) : GameAction
    {
        public override ulong OwnerId => owner;
        public override GameActionType ActionType => type;
        protected override Task ExecuteAction() => Task.CompletedTask;
        public override INetAction ToNetAction() => throw new NotSupportedException("Managed queue probe only");
    }

    // Managed native queue/scope checks; no game process or turn transition is
    // simulated here. The main fixture can call this alongside FateDiceCases.
    static void MagicSwordTurnEndCases()
    {
        RuntimeHelpers.RunClassConstructor(Helper("MagicSwordTurnEndHelper").TypeHandle);
        Check(true, "magic sword owner-queue reflection initializes against the actual native runtime");
        MagicSwordNativeWrapperCases();
        MagicSwordQueueCases();
        Reset();
        As31O(true);
        try
        {
            var card = Owned<StrikeIronclad>();
            object outer = Call("MagicSwordTurnEndHelper", "BeginCardPlay", card)!;
            object inner = Call("MagicSwordTurnEndHelper", "BeginCardPlay", card)!;
            object Chain(object scope) => scope.GetType().GetField("Chain", F)!.GetValue(scope)!;
            int Depth(object chain) => (int)chain.GetType().GetField("ActivePlays", F)!.GetValue(chain)!;
            object chain = Chain(outer);
            Check(ReferenceEquals(chain, Chain(inner)) && Depth(chain) == 2,
                "magic sword exit scopes include nested automatic plays in the original owner's chain");
            Call("MagicSwordTurnEndHelper", "EndCardPlay", inner, true);
            Check(Depth(chain) == 1, "finishing a nested lucky-seven play leaves the original card's scope open");
            Call("MagicSwordTurnEndHelper", "EndCardPlay", inner, false);
            Check(Depth(chain) == 1 && !(bool)chain.GetType().GetField("Failed", F)!.GetValue(chain)!,
                "a scope closes once even if both wrapper and finalizer report completion");
            Call("MagicSwordTurnEndHelper", "EndCardPlay", outer, false);
            Check(Depth(chain) == 0 && (bool)chain.GetType().GetField("Failed", F)!.GetValue(chain)!,
                "failed original-card cleanup marks the chain failed instead of forcing turn end");
            object next = Call("MagicSwordTurnEndHelper", "BeginCardPlay", card)!;
            Check(!ReferenceEquals(chain, Chain(next)), "completed and failed chains do not leak into the next card");
            Call("MagicSwordTurnEndHelper", "EndCardPlay", next, true);
            As31O(false);
            Check(Call("MagicSwordTurnEndHelper", "BeginCardPlay", card) == null,
                "other characters add no magic-sword turn-end scope");
        }
        finally
        {
            As31O(false);
            Reset();
        }
    }

    static void MagicSwordNativeWrapperCases()
    {
        MethodInfo wrapper = typeof(CardModel).GetMethod(nameof(CardModel.OnPlayWrapper), F)!;
        Type state = wrapper.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        var body = PatchProcessor.GetOriginalInstructions(state.GetMethod("MoveNext", F)!).ToArray();
        int before = Array.FindIndex(body, instruction => instruction.operand is MethodInfo method &&
            method.DeclaringType?.FullName == "MegaCrit.Sts2.Core.Hooks.Hook" && method.Name == "BeforeCardPlayed");
        int play = Array.FindIndex(body, instruction => instruction.operand is MethodInfo method &&
            method.DeclaringType == typeof(CardModel) && method.Name == "OnPlay");
        int cleanup = Array.FindIndex(body, Math.Max(0, play), instruction => instruction.operand is MethodInfo method &&
            method.DeclaringType?.FullName == "MegaCrit.Sts2.Core.Commands.CardPileCmd" && method.Name == "RemoveFromCombat");
        Check(before >= 0 && play > before && cleanup > play,
            "real native OnPlayWrapper surrounds BeforeCardPlayed, OnPlay, and final pile cleanup in this order");
    }

    static void MagicSwordQueueCases()
    {
        var owner = Blank<Player>();
        var peer = Blank<Player>();
        Set(owner, "<NetId>k__BackingField", 101UL);
        Set(peer, "<NetId>k__BackingField", 202UL);
        var queues = new ActionQueueSet(new[] { owner, peer });
        MethodInfo get = typeof(ActionQueueSet).GetMethod("GetQueue", F)!;
        object ownQueue = get.Invoke(queues, new object[] { owner.NetId })!;
        object peerQueue = get.Invoke(queues, new object[] { peer.NetId })!;
        var own = (List<GameAction>)ownQueue.GetType().GetField("actions", F)!.GetValue(ownQueue)!;
        var other = (List<GameAction>)peerQueue.GetType().GetField("actions", F)!.GetValue(peerQueue)!;
        FieldInfo cancelling = ownQueue.GetType().GetField("isCancellingPlayerDrivenCombatActions", F)!;
        MagicSwordQueueProbe Probe(ulong id, GameActionType type, GameActionState state)
        {
            var action = new MagicSwordQueueProbe(id, type);
            Set(action, "<State>k__BackingField", state);
            return action;
        }
        var waitingCard = Probe(owner.NetId, GameActionType.CombatPlayPhaseOnly, GameActionState.WaitingForExecution);
        var waitingCombat = Probe(owner.NetId, GameActionType.Combat, GameActionState.WaitingForExecution);
        var executing = Probe(owner.NetId, GameActionType.CombatPlayPhaseOnly, GameActionState.Executing);
        var choosing = Probe(owner.NetId, GameActionType.CombatPlayPhaseOnly, GameActionState.GatheringPlayerChoice);
        var any = Probe(owner.NetId, GameActionType.Any, GameActionState.WaitingForExecution);
        var noncombat = Probe(owner.NetId, GameActionType.NonCombat, GameActionState.WaitingForExecution);
        var hook = new GenericHookGameAction(1, owner.NetId, GameActionType.CombatPlayPhaseOnly);
        Set(hook, "<State>k__BackingField", GameActionState.WaitingForExecution);
        var peerCard = Probe(peer.NetId, GameActionType.CombatPlayPhaseOnly, GameActionState.WaitingForExecution);
        own.AddRange(new GameAction[] { waitingCard, waitingCombat, executing, choosing, any, noncombat, hook });
        other.Add(peerCard);

        // Beta Cancel finishes by constructing a Godot Callable and deferring
        // completion on the engine loop. This host has no engine: replace that
        // leaf explicitly, while running the real production queue selection.
        // An unexpected hook/peer/executing action fails in the prefix instead
        // of ever reaching engine-facing native code.
        MethodInfo cancel = typeof(GameAction).GetMethod(nameof(GameAction.Cancel), F)!;
        MethodInfo selection = Helper("MagicSwordTurnEndHelper").GetMethod("CancelPendingPlayerActions", F)!;
        var instructions = PatchProcessor.GetOriginalInstructions(selection).ToArray();
        int cancelIndex = Array.FindIndex(instructions, instruction => Equals(instruction.operand, cancel));
        int eligibilityIndex = Array.FindIndex(instructions, instruction => instruction.operand is MethodInfo method &&
            method.DeclaringType == typeof(ActionQueueSet) && method.Name == nameof(ActionQueueSet.IsGameActionPlayerDriven));
        int stateIndex = Array.FindIndex(instructions, instruction => instruction.operand is MethodInfo method &&
            method.DeclaringType == typeof(GameAction) && method.Name == "get_State");
        Check(cancelIndex > eligibilityIndex && eligibilityIndex >= 0 && stateIndex > eligibilityIndex && stateIndex < cancelIndex &&
              instructions.Count(instruction => Equals(instruction.operand, cancel)) == 1,
            "production queue-selection IL calls the native Cancel leaf once after player-driven/type/state eligibility checks");
        const string isolationId = "hbr.magic-sword.turn-end.cancel-probe";
        var isolation = new Harmony(isolationId);
        MagicSwordCancelledProbes.Clear();
        isolation.Patch(cancel, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MagicSwordCancelLeaf), F)!));
        try { Call("MagicSwordTurnEndHelper", "CancelPendingPlayerActions", ownQueue); }
        finally { isolation.Unpatch(cancel, HarmonyPatchType.All, isolationId); }
        Check(waitingCard.State == GameActionState.Canceled && waitingCombat.State == GameActionState.Canceled,
            "magic sword selects the two waiting manual combat actions for the isolated Cancel leaf");
        Check(MagicSwordCancelledProbes.SequenceEqual(new GameAction[] { waitingCard, waitingCombat }),
            "only eligible owner actions enter Cancel; no engine-native cancellation path is invoked by this host");
        Check(own.SequenceEqual(new GameAction[] { executing, choosing, any, noncombat, hook }),
            "magic sword retains native executing/choice cleanup, hooks, and noncombat actions");
        Check((bool)cancelling.GetValue(ownQueue)! && !(bool)cancelling.GetValue(peerQueue)! &&
              other.SequenceEqual(new[] { peerCard }) && peerCard.State == GameActionState.WaitingForExecution,
            "magic sword cancellation marks only its owner's queue and preserves the teammate's queued card");
    }

    static bool MagicSwordCancelLeaf(GameAction __instance)
    {
        Check(__instance is MagicSwordQueueProbe && __instance.OwnerId == 101UL &&
              __instance.State == GameActionState.WaitingForExecution &&
              __instance.ActionType is GameActionType.Combat or GameActionType.CombatPlayPhaseOnly,
            "isolated Cancel receives only a waiting manual combat action owned by the exiting player");
        MagicSwordCancelledProbes.Add(__instance);
        Set(__instance, "<State>k__BackingField", GameActionState.Canceled);
        return false;
    }
}
