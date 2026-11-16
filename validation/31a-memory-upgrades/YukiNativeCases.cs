using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;

static partial class EffectChecks
{
    static async Task YukiNativeCases()
    {
        foreach (Type type in new[] { typeof(ThirtyOneAYukiDebuffPower), typeof(ArtifactPower), typeof(HbrAttackDownPower), typeof(VulnerablePower), typeof(PotionOfBinding), typeof(PoisonPower), typeof(TwistedFunnel) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        var harmony = new Harmony(ProductionId + ".yuki-native");
        foreach (string name in new[] { "ThirtyOneAYukiNativeApplyPatch", "ThirtyOneAYukiNativeModifyPatch", "ThirtyOneAYukiEnumerableDebuffPatch", "ThirtyOneAYukiTwistedFunnelPatch" })
            harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch();
        try
        {
            GardenReset(); var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
            using ((IDisposable)Call("HbrDebuffApplicationScope", "BeginBatch", Hero)!)
            {
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Enemy, 10m, Hero, null, true);
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 10m, Hero, null, true);
                Equal(Enemy.GetPowerAmount<WeakPower>(), 13m, "Yuki bridges a real first native debuff application");
                Equal(Hero.GetPowerAmount<WeakPower>(), 13m, "Yuki shares the same thirty-percent bonus with the second AoE target");
                Equal(charge.Amount, 1m, "native multi-target application consumes only one Yuki charge");
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Enemy, 10m, Hero, null, true);
                Equal(Enemy.GetPowerAmount<WeakPower>(), 26m, "Yuki also bridges native stacking through ModifyAmount");
                Equal(charge.Amount, 0m, "a repeated application to one target consumes the next independent charge");
            }

            GardenReset(); charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 1);
            Power<ArtifactPower>(Enemy, 1);
            using ((IDisposable)Call("HbrDebuffApplicationScope", "BeginBatch", Hero)!)
            {
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Enemy, 10m, Hero, null, true);
                Equal(charge.Amount, 1m, "a native Artifact-blocked target does not prematurely spend the reserved Yuki bonus");
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 10m, Hero, null, true);
                Equal(Hero.GetPowerAmount<WeakPower>(), 13m, "an unblocked target in the same application retains Yuki's reserved bonus");
                Equal(charge.Amount, 0m, "the first accepted AoE target settles one Yuki reservation");
            }
            GardenReset(); charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 1);
            Power<ArtifactPower>(Enemy, 1);
            using ((IDisposable)Call("HbrDebuffApplicationScope", "BeginBatch", Hero)!)
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Enemy, 10m, Hero, null, true);
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 10m, Hero, null, true);
            Equal(Hero.GetPowerAmount<WeakPower>(), 13m, "closing a fully blocked application releases its reservation for the next debuff");
            Equal(charge.Amount, 0m, "the next accepted standalone native debuff spends the released charge");

            GardenReset(); charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 1);
            var card = GardenCard<HelloWorldHbrgoSaki>();
            object play = Call("CardPlayContextHelper", "Begin", card, false)!;
            try
            {
                foreach (Creature target in new[] { Enemy, Hero })
                    await PowerCmd.Apply<FragilePower>(new ThrowingPlayerChoiceContext(), target, 1m, Hero, card, true);
                foreach (Creature target in new[] { Enemy, Hero })
                {
                    decimal actual = (decimal)typeof(FragilePower).GetProperty("HbrEffectPercent", F)!.GetValue(target.Powers.OfType<FragilePower>().Single())!;
                    decimal expected = (decimal)Call("HbrEffectFormula", "Debuff", 39m, Hero, target,
                        Helper("HbrEffectFormula").GetField("Fragile", F)!.GetValue(null))!;
                    Equal(actual, expected, "Yuki boosts authored Fragile basis before attributes, once for each target");
                }
                Equal(charge.Amount, 0m, "card-owned authored AoE consumes one bonus through its shared card token");
            }
            finally { Call("CardPlayContextHelper", "Close", play); }
            await YukiEnumerableCase<WeakPower>(10m, 13m, null);
            await YukiEnumerableCase<FragilePower>(1m, 39m, "Fragile");
            await YukiEnumerableCase<HbrAttackDownPower>(1m, 19.5m, "IntelligenceLuck");
            await YukiEnumerableAsyncCase(cancel: false);
            await YukiEnumerableAsyncCase(cancel: true);
            await YukiPreApplyCancellationCase();
            await YukiNativePotionCallerCase();
            await YukiNativeFunnelCase();
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
    static async Task YukiEnumerableCase<T>(decimal amount, decimal expectedBasis, string? weights) where T : PowerModel
    {
        GardenReset(); var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
        foreach (int call in new[] { 1, 2 })
        {
            // This is the production typed callsite adapter. It opens its own
            // batch and delegates to the untouched native generic command.
            Task<IReadOnlyList<T>> task = YukiApplyEnumerable<T>(new[] { Enemy, Hero }, amount);
            IReadOnlyList<T> returned = await task;
            Check(returned.Count == 2 && returned.All(power => power.GetType() == typeof(T)) &&
                ReferenceEquals(returned[0].Owner, Enemy) && ReferenceEquals(returned[1].Owner, Hero),
                "native enumerable preserves its closed generic Task/result model and order: " + typeof(T).Name + "/" + call);
            Equal(charge.Amount, 2 - call,
                "native enumerable call without a card consumes one Yuki charge across both targets: " + typeof(T).Name + "/" + call);
            foreach (T power in returned)
            {
                if (weights == null)
                    Equal(power.Amount, expectedBasis * call,
                        "native enumerable repeated Weak stacking uses one thirty-percent bonus per command for " + power.Owner.Side);
                else
                {
                    string field = power is FragilePower ? "HbrEffectPercent" : "EffectPercent";
                    decimal actual = (decimal)power.GetType().GetProperty(field, F)!.GetValue(power)!;
                    decimal expected = (decimal)Call("HbrEffectFormula", "Debuff", expectedBasis, Hero, power.Owner,
                        Helper("HbrEffectFormula").GetField(weights, F)!.GetValue(null))!;
                    Equal(actual, expected, "native enumerable applies basis bonus then one attribute curve: " + typeof(T).Name + "/" + call + "/" + power.Owner.Side);
                }
            }
        }
    }

    static TaskCompletionSource<bool>? YukiWaitGate;
    static int YukiWaitReads;
    static bool YukiSuspendedWait(ref Task __result)
    {
        YukiWaitReads++;
        __result = YukiWaitReads == 1 ? YukiWaitGate!.Task : Task.CompletedTask;
        return false;
    }
    static async Task YukiEnumerableAsyncCase(bool cancel)
    {
        GardenReset(); var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
        var h = new Harmony(ProductionId + ".yuki-async-wait");
        YukiWaitGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        YukiWaitReads = 0;
        var wait = typeof(Cmd).GetMethods(F).Single(method => method.Name == "CustomScaledWait");
        h.Patch(wait, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YukiSuspendedWait), F)!) { priority = Priority.First });
        object? previous = AsyncValue("HbrDebuffApplicationScope", "Current");
        try
        {
            Task<IReadOnlyList<WeakPower>> pending = YukiApplyEnumerable<WeakPower>(new[] { Enemy, Hero }, 10m);
            Check(!pending.IsCompleted && YukiWaitReads == 1,
                "native enumerable yields at a real application animation wait before its second target: " + cancel);
            Check(ReferenceEquals(AsyncValue("HbrDebuffApplicationScope", "Current"), previous),
                "native enumerable Postfix restores caller AsyncLocal while its awaited body is suspended: " + cancel);
            if (!cancel)
            {
                YukiWaitGate.SetResult(true);
                IReadOnlyList<WeakPower> result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Check(result.Count == 2 && result.All(power => power is WeakPower),
                    "suspended native enumerable resumes with its original typed Weak result");
                Equal(Enemy.GetPowerAmount<WeakPower>(), 13m, "suspended native enumerable first target keeps its one bonus");
                Equal(Hero.GetPowerAmount<WeakPower>(), 13m, "suspended native enumerable second target inherits the same batch bonus");
                Equal(charge.Amount, 1m, "suspended native enumerable spends only one Yuki use after both targets");
            }
            else
            {
                YukiWaitGate.SetCanceled();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(10)); throw new Exception("Canceled native application unexpectedly completed"); }
                catch (OperationCanceledException) { Check(true, "native enumerable preserves cancellation rather than rewriting typed result"); }
                Equal(Enemy.GetPowerAmount<WeakPower>(), 13m, "canceled animation retains the first native target's already-applied Weak");
                Equal(charge.Amount, 1m, "canceled animation settles the one Yuki use whose boosted mutation already happened");
                // The first native mutation preceded the canceled animation. Clear
                // only its residue, then prove a later complete command can reserve.
                Powers(Enemy).Clear();
                IReadOnlyList<WeakPower> retry = await YukiApplyEnumerable<WeakPower>(new[] { Enemy, Hero }, 10m);
                Check(retry.Count == 2, "a new native enumerable command runs after cancellation");
                Equal(Enemy.GetPowerAmount<WeakPower>(), 13m, "cancellation leaves the next first-target bonus available");
                Equal(Hero.GetPowerAmount<WeakPower>(), 13m, "cancellation leaves the next second-target shared bonus available");
                Equal(charge.Amount, 0m, "retry consumes the remaining Yuki use once without reservation leakage");
            }
            Check(ReferenceEquals(AsyncValue("HbrDebuffApplicationScope", "Current"), previous),
                "native enumerable completion/cancellation leaves caller batch scope unchanged: " + cancel);
        }
        finally
        {
            YukiWaitGate.TrySetCanceled(); YukiWaitGate = null;
            h.UnpatchAll(h.Id);
        }
    }
    static Task<IReadOnlyList<T>> YukiApplyEnumerable<T>(IEnumerable<Creature> targets, decimal amount) where T : PowerModel
    {
        var method = Mod.GetTypes().SelectMany(type => type.GetMethods(F))
            .Single(method => method.Name == "ApplyEnumerable" && method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 6).MakeGenericMethod(typeof(T));
        return (Task<IReadOnlyList<T>>)Invoke(method, null, new ThrowingPlayerChoiceContext(), targets, amount, Hero, null, true)!;
    }
    static async Task YukiNativePotionCallerCase()
    {
        GardenReset(); var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
        var second = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, second });
        var potion = (PotionOfBinding)ModelDb.GetById<AbstractModel>(ModelDb.GetId(typeof(PotionOfBinding))).MutableClone();
        Set(potion, "_owner", Player);
        potion.DynamicVars["WeakPower"].UpgradeValueBy(9m);
        potion.DynamicVars["VulnerablePower"].UpgradeValueBy(9m);
        await (Task)Native(potion, "OnUse", new ThrowingPlayerChoiceContext(), null)!;
        foreach (Creature target in new[] { Enemy, second })
        {
            Equal(target.GetPowerAmount<WeakPower>(), 13m,
                "real native Potion of Binding caller routes one Weak application through the typed batch adapter");
            Equal(target.GetPowerAmount<VulnerablePower>(), 13m,
                "real native Potion of Binding caller routes its distinct Vulnerable application through the typed batch adapter");
        }
        Equal(charge.Amount, 0m,
            "real native Potion of Binding spends two Yuki charges for its two kinds, independently of target count");
    }

    static bool YukiCancelBeforeApplication(PowerModel power, ref Task __result)
    {
        if (power is not WeakPower) return true;
        __result = Task.FromCanceled(new CancellationToken(canceled: true));
        return false;
    }
    static async Task YukiPreApplyCancellationCase()
    {
        GardenReset(); var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
        var h = new Harmony(ProductionId + ".yuki-preapply-cancel");
        var before = typeof(MegaCrit.Sts2.Core.Hooks.Hook).GetMethods(F)
            .Single(method => method.Name == "BeforePowerAmountChanged");
        h.Patch(before, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(YukiCancelBeforeApplication), F)!) { priority = Priority.First });
        try
        {
            try { await YukiApplyEnumerable<WeakPower>(new[] { Enemy, Hero }, 10m); throw new Exception("pre-application cancellation unexpectedly completed"); }
            catch (OperationCanceledException) { Check(true, "native BeforePowerAmountChanged cancellation preserves its original cancellation"); }
            Check(Enemy.GetPower<WeakPower>() == null && Hero.GetPower<WeakPower>() == null,
                "cancellation before native application mutates neither target");
            Equal(charge.Amount, 2m, "cancellation before any accepted mutation preserves all Yuki uses");
        }
        finally { h.UnpatchAll(h.Id); }
        var retry = await YukiApplyEnumerable<WeakPower>(new[] { Enemy, Hero }, 10m);
        Check(retry.Count == 2 && retry.All(power => power.Amount == 13),
            "pre-application cancellation releases its reservation for the next native AoE");
        Equal(charge.Amount, 1m, "successful retry after pre-application cancellation consumes exactly one shared Yuki use");
    }

    static async Task YukiNativeFunnelCase()
    {
        GardenReset(); Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 1);
        var charge = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
        var second = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, second });
        var funnel = (TwistedFunnel)ModelDb.GetById<AbstractModel>(ModelDb.GetId(typeof(TwistedFunnel))).MutableClone();
        Set(funnel, "_owner", Player); funnel.DynamicVars["PoisonPower"].UpgradeValueBy(6m);
        await (Task)Native(funnel, "BeforeSideTurnStart", new ThrowingPlayerChoiceContext(), CombatSide.Player,
            new[] { Hero }, Combat)!;
        Equal(Enemy.GetPowerAmount<PoisonPower>(), 13m, "real native Twisted Funnel boosts first target's Poison");
        Equal(second.GetPowerAmount<PoisonPower>(), 13m, "real native Twisted Funnel shares the same Poison bonus with its second target");
        Equal(charge.Amount, 1m, "real native Twisted Funnel's source-free manual loop consumes one Yuki use");
    }

}
