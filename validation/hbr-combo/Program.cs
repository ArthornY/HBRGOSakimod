using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

static class ComboChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(MyLegendNextAttackPower).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<(decimal Damage, Creature[] Targets, bool Absolute)> DamageCalls = [];
    static Action<int>? AfterRecordedDamage;
    static Type Fixture => typeof(Checks);
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static CombatState Combat => State<CombatState>("Combat");
    static Type Small => PowerType("HbrComboSmallPower");
    static Type Medium => PowerType("HbrComboMediumPower");
    static Type Large => PowerType("HbrComboLargePower");
    static Type PowerType(string name) => Mod.GetType("HBRGOSakiMod.Core.Models.Powers." + name, true)!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static object? Call(string name, params object?[] args)
    {
        MethodInfo method = Helper("HbrComboHelper").GetMethod(name, F)!;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length > args.Length)
            args = args.Concat(parameters.Skip(args.Length).Select(parameter => parameter.DefaultValue)).ToArray();
        return Invoke(method, null, args);
    }
    static object? On(object receiver, string name, params object?[] args) => Invoke(receiver.GetType().GetMethod(name, F)!, receiver, args);
    static void Reset() { FixtureCall("Reset"); DamageCalls.Clear(); AfterRecordedDamage = null; }
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static List<PowerModel> Powers(Creature target) => (List<PowerModel>)FixtureCall("Powers", target)!;
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static void Equal(decimal actual, decimal expected, string message) => Check(actual == expected, $"{message}: {actual} = {expected}");
    static PowerModel Apply(Type type, int amount = 3, Creature? owner = null)
    {
        var power = (PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).ToMutable();
        Creature target = owner ?? Hero;
        PowerCmd.Apply(null!, power, target, amount, Hero, null, true).GetAwaiter().GetResult();
        return Powers(target).Contains(power) ? power : target.GetPower(power.Id)!;
    }
    static CardModel Card() => (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(HelloWorldHbrgoSaki)), null)!;
    static CardModel OwnedCard(Type type) => (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(type), null)!;
    static T RawPower<T>(Creature target, int amount = 1) where T : PowerModel =>
        (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, target, amount)!;
    static void Play(CardModel card, Creature? target = null, int energy = 3)
    {
        var play = new CardPlay { Card = card, Target = target, ResultPile = PileType.Discard,
#if HBR_BETA_NATIVE
            Player = Player,
#endif
            Resources = new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1 };
        ((Task)Invoke(card.GetType().GetMethod("OnPlay", F)!, card, null, play)!).GetAwaiter().GetResult();
    }
    static AttackCommand Attack(Creature? actor = null, ValueProp props = ValueProp.Move)
    {
        actor ??= Hero;
        var attack = new AttackCommand(1m);
        Set(attack, "<Attacker>k__BackingField", actor); Set(attack, "<DamageProps>k__BackingField", props);
        return attack.TargetingAllOpponents(Combat);
    }
    static decimal Preview(AttackCommand command, int hits) => Hook.ModifyAttackHitCount(Combat, command, hits);
    static void Begin(AttackCommand command) => Hook.BeforeAttack(Combat, command).GetAwaiter().GetResult();
    static void End(AttackCommand command) => Hook.AfterAttack(Combat, null!, command).GetAwaiter().GetResult();
    static void Hit(AttackCommand command, params Creature[] targets) => command.AddResultsInternal(
        targets.Select(target => new DamageResult(target, ValueProp.Move) { BlockedDamage = 1 }));
    static AttackContext Context(CardModel card) => AttackContext.CreateAsync(Combat, null!,
#if HBR_BETA_NATIVE
        new CardPlay { Card = card, Player = Player, Target = Enemy, ResultPile = PileType.Discard,
            Resources = new ResourceInfo { EnergySpent = 1, EnergyValue = 1, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1 }
#else
        card
#endif
        ).GetAwaiter().GetResult();
    static Task<IEnumerable<DamageResult>> NativeDamage(PlayerChoiceContext context, Creature[] targets, decimal damage,
        Creature dealer, CardModel card, ValueProp props = ValueProp.Move)
    {
        Type wrapper=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
        MethodInfo method=wrapper.GetMethods(F).Single(candidate=>candidate.Name=="Damage"&&candidate.GetParameters() is var p&&p.Length==7&&p[1].ParameterType==typeof(IEnumerable<Creature>));
        return (Task<IEnumerable<DamageResult>>)Invoke(method,null,context,targets,damage,props,dealer,card,null)!;
    }
    static bool StubDamage(object[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        Type api=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        Creature[] copy=((IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets",F)!,null,new object?[]{__args})!).ToArray();
        decimal amount=(decimal)__args[2];ValueProp props=(ValueProp)__args[3];
        Creature dealer=(Creature)__args[4];CardModel cardSource=(CardModel)__args[5];
        bool absolute = copy.Length > 0 && (bool)Invoke(Helper("HbrAbsoluteBaseDamageContext").GetMethod("Matches", F)!, null, copy[0], dealer, cardSource, amount)!;
        DamageCalls.Add((amount, copy, absolute));
        AfterRecordedDamage?.Invoke(DamageCalls.Count);
        __result = Task.FromResult<IEnumerable<DamageResult>>(copy.Select(target => new DamageResult(target, props) { BlockedDamage = 1 }).ToArray());
        return false;
    }
    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.combo.checks");
        foreach (Type type in new[] { Small, Medium, Large, typeof(MyLegendNextAttackPower), typeof(MyLegendNextAttackDoublePower), typeof(PhantomNextAttackDoublePower) })
        {
            ModelDb.Inject(type);
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        foreach (Type type in Mod.GetTypes().Where(type => type.IsDefined(typeof(HarmonyPatch), false) && type.Name.StartsWith("LegendAttackContext", StringComparison.Ordinal)))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs actual " + type.Name);
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrComboVirtualHitPatch", true)!).Patch().Count > 0,
            "installs actual native virtual combo prefix");
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighHitDamageAttackScopePatch", true)!).Patch().Count > 0,
            "installs actual native Execute scope for distinguishing play from preview");
        MethodInfo stub = typeof(ComboChecks).GetMethod(nameof(StubDamage), F)!;
        MethodInfo canonical=(MethodInfo)Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!.GetField("Command",F)!.GetValue(null)!;
        harmony.Patch(canonical, prefix: new HarmonyMethod(stub) { priority = Priority.Last });
        Reset();
    }
    static void FuturePreviewCases()
    {
        Reset(); Apply(Large, 1); Apply(Medium, 3); Apply(Small, 5);
        foreach ((int attacks, int expected) in new[] { (0, 12), (1, 6), (2, 6), (3, 3), (4, 3), (5, 2) })
            Equal((int)Call("PreviewHitCountAfterAttacks", Hero, 2, attacks)!, expected,
                "subsequent action preview respects remaining 1/3/5 combo charges after " + attacks + " attacks");
        Check(Powers(Hero).Sum(power => power.Amount) == 9, "future multi-action previews never spend charges");
        Reset();
    }
    static void FailureCases()
    {
        Reset(); var combo = Apply(Small); var command = Attack(); Begin(command);
        Type visual = Helper("HighHitDamageVisualHelper");
        object scope = Invoke(visual.GetMethod("BeginAttack", F)!, null, command)!;
        var failure = new InvalidOperationException("fixture attack interruption");
        Task<AttackCommand> wrapped = (Task<AttackCommand>)Invoke(visual.GetMethod("FinishAttack", F)!, null, Task.FromException<AttackCommand>(failure), scope)!;
        Invoke(visual.GetMethod("RestoreAttackCaller", F)!, null, scope);
        try { wrapped.GetAwaiter().GetResult(); throw new Exception("Faulted attack did not propagate original failure"); }
        catch (InvalidOperationException actual) { Check(ReferenceEquals(actual, failure), "existing native attack wrapper preserves original async failure"); }
        Check(combo.Amount == 3, "failed native attack wrapper releases reservation without consuming layer");
        var next = Attack(); Begin(next); Equal(Preview(next, 2), 3, "next native attack can claim combo after interrupted Execute");
        Hit(next, Enemy); End(next); Check(combo.Amount == 2, "next native attack consumes normally after interrupted Execute");
        command = Attack(); Begin(command); scope = Invoke(visual.GetMethod("BeginAttack", F)!, null, command)!;
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighHitDamageAttackScopePatch", true)!;
        var returned = (Exception)Invoke(patch.GetMethod("Finalizer", F)!, null, failure, scope, command)!;
        Check(ReferenceEquals(returned, failure) && combo.Amount == 2, "native attack synchronous finalizer releases reservation and keeps original exception");
        next = Attack(); Begin(next); Hit(next, Enemy); End(next);
        Check(combo.Amount == 1, "next native attack consumes normally after synchronous finalizer cancellation");
        Reset();
    }
    static void BasicCases()
    {
        foreach ((Type type, int expected) in new[] { (Small, 4), (Medium, 6), (Large, 6) })
        {
            Reset(); PowerModel power = Apply(type); var preview = Attack();
            for (int repeat = 0; repeat < 3; repeat++) Equal(Preview(preview, 3), expected, type.Name + " repeat preview transforms exactly once");
            Check(power.Amount == 3, type.Name + " preview never spends layers");
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                AttackCommand command = Attack(); Begin(command);
                Equal(Preview(command, 3), expected, type.Name + " one layer transforms one attack, independent of stored stacks");
                Hit(command, Enemy); Hit(command, Enemy); End(command);
                Check(power.Amount == remaining, type.Name + " consumes exactly one layer for a completed attack");
                if (remaining == 0) Check(!Powers(Hero).Contains(power), type.Name + " native removal occurs when final layer is spent");
            }
            Equal(Preview(Attack(), 3), 3, type.Name + " subsequent attack returns to original hits");
        }
    }
    static void GrantCases()
    {
        Type combo = PowerType("HbrComboPower");
        MethodInfo grant = combo.GetMethods(F).Single(method => method.Name == "Grant" && method.IsGenericMethodDefinition);
        foreach ((Type type, int charges) in new[] { (Large, 1), (Medium, 3), (Small, 5) })
        {
            Reset(); MethodInfo method = grant.MakeGenericMethod(type);
            ((Task)Invoke(method, null, null, Hero, Hero, null, 1)!).GetAwaiter().GetResult();
            PowerModel power = Powers(Hero).Single(power => power.GetType() == type);
            Check(power.Amount == charges, type.Name + " actual Grant applies default charges per acquisition");
            ((Task)Invoke(method, null, null, Hero, Hero, null, 1)!).GetAwaiter().GetResult();
            PowerModel[] instances = Powers(Hero).Where(power => power.GetType() == type).ToArray();
            Check(power.Amount == charges && instances.Length == 2 && instances[1].Amount == charges,
                type.Name + " repeated Grant creates a second independent native instance");
            ((Task)Invoke(method, null, null, Hero, Hero, null, 2)!).GetAwaiter().GetResult();
            PowerModel[] replaced = Powers(Hero).Where(power => power.GetType() == type).ToArray();
            PowerModel third = replaced.Single(candidate => !instances.Contains(candidate));
            Check(!Powers(Hero).Contains(power) && Powers(Hero).Contains(instances[1]) &&
                third.Amount == charges * 2 && instances[1].Amount == charges && replaced.Length == 2,
                type.Name + " a full equal-strength pair replaces the earliest shortest instance and resets to the new acquisition duration");
            var attack = Attack(); Begin(attack); Hit(attack, Enemy); End(attack);
            Check(third.Amount == charges * 2 - 1 && instances[1].Amount == charges - 1,
                type.Name + " each acquired instance spends one per complete attack");
            PowerCmd.ModifyAmount(null!, third, 1, Hero, null, true).GetAwaiter().GetResult();
            Check(third.Amount == charges * 2, type.Name + " ordinary stack mutation adds one charge without creating or replacing an instance");
        }
        Reset();
    }
    static void CombinedCases()
    {
        foreach (Type[] order in new[] { new[] { Small, Medium, Large }, new[] { Large, Medium, Small }, new[] { Medium, Large, Small } })
        {
            Reset(); PowerModel[] powers = order.Select(type => Apply(type)).ToArray();
            AttackCommand command = Attack(); Begin(command);
            Equal(Preview(command, 2), 12, "all variants use (base + 3 + 1) * 2 independent of grant order: " + string.Join(',', order.Select(t => t.Name)));
            Hit(command, Enemy, Hero); Hit(command, Enemy, Hero); End(command);
            Check(powers.All(power => power.Amount == 2), "multi-hit and multi-target attack spends one of each active variant");
            End(command); Check(powers.All(power => power.Amount == 2), "repeated AfterAttack cannot spend another layer");
        }
        Reset(); var legacy = Apply(typeof(MyLegendNextAttackPower), 2); var large = Apply(Large, 3);
        var oldCommand = Attack(); Begin(oldCommand); Equal(Preview(oldCommand, 2), 8, "new large doubles legacy next-hit additions after additive effects");
        Hit(oldCommand, Enemy); End(oldCommand);
        Check(legacy.Amount == 0 && large.Amount == 2, "legacy retains whole-stack consumption while combo spends only one");
        Reset(); var legacyDouble = Apply(typeof(MyLegendNextAttackDoublePower), 1); var medium = Apply(Medium, 3);
        var otherCommand = Attack(); Begin(otherCommand); Equal(Preview(otherCommand, 2), 10, "legacy doubling sorts after new medium additions");
        Hit(otherCommand, Enemy); End(otherCommand);
        Check(legacyDouble.Amount == 0 && medium.Amount == 2, "old doubling and new medium preserve separate consumption contracts");
    }
    static void InstancePairCases()
    {
        Reset(); var first = Apply(Medium, 7); var second = Apply(Medium, 3);
        var third = Apply(Medium, 5);
        Check(first.Amount == 7 && third.Amount == 5 && !Powers(Hero).Contains(second) && Powers(Hero).Count(power => power.GetType() == Medium) == 2,
            "third medium acquisition replaces the least-remaining equal-strength instance with five uses instead of adding five");
        var fourth = Apply(Medium, 2);
        Check(first.Amount == 7 && fourth.Amount == 2 && !Powers(Hero).Contains(third),
            "the next acquisition re-evaluates the shortest instance and resets it even when the new duration is shorter");
        Check(first.InstanceType == MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced &&
            !ReferenceEquals(first, fourth), "each combo group has independent native instanced identity");
        Reset(); first = Apply(Medium, 3); second = Apply(Medium, 3); third = Apply(Medium, 1);
        Check(!Powers(Hero).Contains(first) && Powers(Hero).Contains(second) && third.Amount == 1,
            "equal strength and equal remaining uses replace the earliest acquired combo deterministically");
        Equal(Preview(Attack(), 1), 7, "replacement changes only remaining uses and preserves both fixed medium hit bonuses");
        Reset();
        PowerModel[] six = [Apply(Small, 5), Apply(Small, 4), Apply(Medium, 3), Apply(Medium, 2), Apply(Large, 2), Apply(Large, 3)];
        Equal(Preview(Attack(), 1), 36, "two instances of each kind use (1+1+1+3+3)*2*2");
        var attack = Attack(); Begin(attack); Hit(attack, Enemy, Hero); Hit(attack, Enemy, Hero); End(attack);
        Check(six.Select(power => power.Amount).SequenceEqual(new[] { 4, 3, 2, 1, 1, 2 }),
            "multi-hit AoE spends exactly one use from every participating instance");
        Play(OwnedCard(typeof(DoublePowersHbrgoSaki)));
        Check(six.Select(power => power.Amount).SequenceEqual(new[] { 8, 6, 4, 2, 2, 4 }) &&
            Powers(Hero).OfType<HbrComboPower>().Count() == 6,
            "actual Double doubles each instance's remaining uses without merging pairs or doubling hit bonuses");
        Equal(Preview(Attack(), 1), 36, "Double changes durations while paired hit bonuses stay fixed");
        Reset();
    }
    static AttackCommand CardAttack(CardModel card)
    {
        var command = new AttackCommand(1m);
        MethodInfo fromCard = typeof(AttackCommand).GetMethods(F).Single(method => method.Name == "FromCard");
        Invoke(fromCard, command, fromCard.GetParameters().Length == 1 ? [card] : [card, null]);
        return command.Targeting(Enemy);
    }
    static void VirtualFormCases()
    {
        Reset(); var dual = RawPower<ReptureDualSwordsPower>(Hero); CardModel card = Card();
        Equal(Preview(CardAttack(card), 1), 4, "dual form adds virtual medium without any real combo instance");
        Equal(Preview(CardAttack(card), 0), 0, "dual form does not create a hit from an empty attack");
        Equal((int)Call("PreviewHitCountAfterAttacks", Hero, 1, 100)!, 4,
            "virtual medium persists through arbitrary prior attacks and does not reserve a slot");
        var first = Apply(Medium, 3); var second = Apply(Medium, 2); Apply(Large, 2); Apply(Large, 3);
        Equal(Preview(CardAttack(card), 1), 40, "virtual medium stacks with two real mediums before both large doublings");
        Check(Powers(Hero).OfType<HbrComboPower>().Count() == 4 && dual.Amount == 1,
            "virtual medium creates no counted power and consumes no form amount");
        var attack = CardAttack(card); Begin(attack); Hit(attack, Enemy); End(attack);
        Check(first.Amount == 2 && second.Amount == 1 && dual.Amount == 1,
            "real instances consume once while virtual form remains unlimited");
        Reset(); RawPower<ReptureDualSwordsPower>(Hero); card = Card();
        object scope = Call("BeginCardAttack", card) ?? throw new Exception("virtual-only form did not create direct scope");
        Check(true, "virtual-only form creates card-owned scope for direct hit loops");
        try
        {
            Equal((int)On(scope, "HitCount", 1)!, 4, "direct card scope snapshots virtual medium once");
            Equal(Preview(CardAttack(card), 4), 4, "native children of expanded direct card loop do not add virtual medium twice");
            var segments = (IReadOnlyList<decimal>)Call("TransformSegments", Hero, new decimal[] { 5, 9 })!;
            Check(segments.SequenceEqual(new decimal[] { 5, 9, 9, 9, 9 }), "direct segments append virtual medium hits before real doubling");
        }
        finally { ((IDisposable)scope).Dispose(); }
        Equal(Preview(CardAttack(card), 1), 4, "closing direct scope restores native virtual medium calculation");
        var context = Context(card);
        context.AddHit(NativeDamage(null!, [Enemy], 10m, Hero, card).GetAwaiter().GetResult());
        context.AddHit(NativeDamage(null!, [Enemy], 99m, Hero, card, ValueProp.Move | ValueProp.Unpowered).GetAwaiter().GetResult());
        context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Select(call => call.Damage).SequenceEqual(new decimal[] { 10, 99, 10, 10, 10 }),
            "virtual-only AttackContext replays three powered hits while leaving unpowered segment unchanged");
        Reset();
    }
    static IDisposable ChaseScope(CardModel card, bool automatic = false) =>
        (IDisposable)Invoke(Helper("DualSwordsChaseHelper").GetMethod("BeginPlay", F)!, null, card, automatic)!;
    static void ChaseComboCases()
    {
        Reset(); var dual = RawPower<ReptureDualSwordsPower>(Hero); CardModel card = OwnedCard(typeof(StrikeHbrgoSaki));
        Set(dual, "<NextAttackHasChase>k__BackingField", true);
        using (ChaseScope(card))
        {
            Equal(Preview(CardAttack(card), 1), 5, "physical chase adds one transient hit alongside virtual medium");
            Apply(Medium, 3); Apply(Medium, 3); Apply(Large, 2); Apply(Large, 2);
            Equal(Preview(CardAttack(card), 1), 44, "chase and virtual medium add before both real medium instances and both large doublings");
            using (ChaseScope(card, automatic: true))
                Equal(Preview(CardAttack(card), 1), 40, "nested automatic scope masks chase while preserving form and real combo bonuses");
            Equal(Preview(CardAttack(card), 1), 44, "exiting automatic scope restores enclosing manual chase");
            Check(Powers(Hero).OfType<HbrComboSmallPower>().Count() == 0,
                "physical chase never creates a five-use small instance or consumes its quota");
        }
        Reset();
    }
    static void ProjectedModeCases()
    {
        Reset(); RawPower<ReptureBowPower>(Hero); CardModel dualCard = OwnedCard(typeof(BladeDanceHbrgoSaki));
        Equal(Preview(CardAttack(dualCard), 4), 7, "bow-to-dual native preview includes virtual medium from the impending mode switch");
        using ((IDisposable)Invoke(Helper("ModeTransitionHelper").GetMethod("SuppressModeTransition", F)!, null, dualCard)!)
            Equal(Preview(CardAttack(dualCard), 4), 4, "suppressed mode transition leaves bow preview without virtual medium");
        Equal(Preview(CardAttack(dualCard), 4), 7, "preview projection resumes after transition suppression closes");
        Reset(); RawPower<ReptureDualSwordsPower>(Hero); CardModel bowCard = OwnedCard(typeof(StrongShotHbrgoSaki));
        Equal(Preview(CardAttack(bowCard), 3), 3, "dual-to-bow preview removes the departing form's virtual medium");
        using ((IDisposable)Invoke(Helper("ModeTransitionHelper").GetMethod("SuppressModeTransition", F)!, null, bowCard)!)
            Equal(Preview(CardAttack(bowCard), 3), 6, "suppressed bow card preserves the actual dual form's virtual medium");
        using (ChaseScope(bowCard, automatic: true))
            Equal(Preview(CardAttack(bowCard), 3), 6, "a resolving card command uses its live form instead of projecting a second switch");
        FixtureCall("Mode", false, 1);
        CardAttack(bowCard).WithHitCount(3).Execute(null!).GetAwaiter().GetResult();
        Check(DamageCalls.Count == 6, "real native Execute with a reactive bow card source still uses current dual form rather than preview projection");
        Reset();
    }
    static void ReservationCases()
    {
        foreach (Type type in new[] { Small, Medium, Large })
        {
            Reset(); var power = Apply(type); var zero = Attack(); Begin(zero);
            Equal(Preview(zero, 0), 0, type.Name + " zero-segment attack remains zero");
            End(zero); Check(power.Amount == 3, type.Name + " zero actual hits preserve layer");
            var empty = Attack(); Begin(empty); empty.AddResultsInternal([]); End(empty);
            Check(power.Amount == 3, type.Name + " empty target result group preserves layer");
            var next = Attack(); Begin(next); Hit(next, Enemy); End(next);
            Check(power.Amount == 2, type.Name + " empty attack releases reservation for next real attack");
        }
        Reset(); var combo = Apply(Small); var outer = Attack(); Begin(outer); var nested = Attack(); Begin(nested);
        Equal(Preview(nested, 2), 2, "nested attack cannot borrow outer reservation"); Hit(nested, Enemy); End(nested);
        Check(combo.Amount == 3, "nested real hit does not consume outer combo");
        Equal(Preview(outer, 2), 3, "outer reservation remains valid after nested attack"); Hit(outer, Enemy); End(outer);
        Check(combo.Amount == 2, "outer completion spends its one reserved layer");
        Reset(); combo = Apply(Small, 1); outer = Attack(); Begin(outer); Apply(Small, 2); Hit(outer, Enemy); End(outer);
        Check(combo.Amount == 0 && Powers(Hero).Single(power => power.GetType() == Small).Amount == 2,
            "second instance acquired during reserved attack is not consumed by the earlier attack");
        Reset(); combo = Apply(Small); outer = Attack(); Begin(outer);
        var clone = (PowerModel)combo.ClonePreservingMutability();
        var reserved = typeof(MyLegendNextAttackBasePower).GetProperty("ReservedAttack", F)!;
        Check(reserved.GetValue(clone) == null && ReferenceEquals(reserved.GetValue(combo), outer), "clone clears transient reservation without altering original");
        Hit(outer, Enemy); End(outer); Check(combo.Amount == 2 && clone.Amount == 3, "clone amount remains independent during original consumption");
    }
    static void HolderCases()
    {
        Reset(); var hero = Apply(Medium); var enemy = Apply(Small, 4, Enemy);
        var enemyCommand = Attack(Enemy); Begin(enemyCommand); Equal(Preview(enemyCommand, 2), 3, "enemy receives only its own small combo");
        Hit(enemyCommand, Hero); End(enemyCommand); Check(enemy.Amount == 3 && hero.Amount == 3, "enemy consumption preserves hero stacks");
        var unpowered = Attack(Hero, ValueProp.Move | ValueProp.Unpowered); Begin(unpowered);
        Equal(Preview(unpowered, 2), 2, "unpowered damage is not a combo attack"); Hit(unpowered, Enemy); End(unpowered);
        Check(hero.Amount == 3, "unpowered hit cannot consume combo");
        var nonAttack = Attack(Hero, ValueProp.Unpowered); Begin(nonAttack); Equal(Preview(nonAttack, 2), 2, "non-Move damage is excluded");
        Hit(nonAttack, Enemy); End(nonAttack); Check(hero.Amount == 3, "non-Move damage preserves combo");
        Reset();
    }
    static void DirectCases()
    {
        Reset(); var actor = (Creature)FixtureCall("Actor", Blank<SmallHopper>(), null, Player)!;
        var small = Apply(Small, 3, actor); var medium = Apply(Medium, 3, actor); var large = Apply(Large, 3, actor);
        Equal((int)Call("PreviewHitCount", actor, 2)!, 12, "direct preview shares additive then double formula");
        Check(new[] { small, medium, large }.All(power => power.Amount == 3), "direct previews do not reserve or consume");
        decimal[] source = [4m, 9m];
        var transformed = (IReadOnlyList<decimal>)Call("TransformSegments", actor, source)!;
        Check(transformed.SequenceEqual(new decimal[] { 4, 9, 9, 9, 9, 9, 4, 9, 9, 9, 9, 9 }), "direct segment transform duplicates original sequence and repeats final hit for additions");
        Check(source.SequenceEqual(new decimal[] { 4, 9 }), "direct preview does not mutate caller's source segment list");
        object scope = Call("BeginAttack", actor)!;
        try
        {
            Equal((int)On(scope, "HitCount", 2)!, 12, "direct reserved snapshot matches preview count");
            On(scope, "MarkHit"); On(scope, "MarkHit");
            ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Check(new[] { small, medium, large }.All(power => power.Amount == 2), "direct action with multiple hits spends exactly one of each");
            ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Check(new[] { small, medium, large }.All(power => power.Amount == 2), "direct completion is idempotent");
        }
        finally { ((IDisposable)scope).Dispose(); }
        scope = Call("BeginAttack", actor)!;
        try
        {
            Equal((int)On(scope, "HitCount", 0)!, 0, "direct zero base hits remain zero");
            ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Check(new[] { small, medium, large }.All(power => power.Amount == 2), "direct empty action releases reservation without consumption");
        }
        finally { ((IDisposable)scope).Dispose(); }
        scope = Call("BeginAttack", actor)!;
        Equal((int)On(scope, "HitCount", 2)!, 12, "direct cancellation initially reserves the available combo");
        ((IDisposable)scope).Dispose();
        Check(new[] { small, medium, large }.All(power => power.Amount == 2), "cancelled direct action preserves remaining charges");
        scope = Call("BeginAttack", actor)!;
        try
        {
            Equal((int)On(scope, "HitCount", 2)!, 12, "cancelled direct action releases reservations for the following action");
            object nested = Call("BeginAttack", actor)!;
            try
            {
                Equal((int)On(nested, "HitCount", 2)!, 2, "nested direct action cannot borrow the outer action's charges");
                On(nested, "MarkHit"); ((Task)On(nested, "Complete", (object?)null)!).GetAwaiter().GetResult();
            }
            finally { ((IDisposable)nested).Dispose(); }
            Check(new[] { small, medium, large }.All(power => power.Amount == 2), "nested direct completion cannot consume outer reservations");
            On(scope, "MarkHit"); ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Check(new[] { small, medium, large }.All(power => power.Amount == 1), "outer direct action can consume after nested action exits");
        }
        finally { ((IDisposable)scope).Dispose(); }
        Reset();
    }
    static void ContextCases()
    {
        Reset(); var small = Apply(Small); var medium = Apply(Medium); var large = Apply(Large);
        CardModel card = Card(); var context = Context(card);
        NativeDamage(null!, [Enemy], 4m, Hero, card).GetAwaiter().GetResult(); context.AddHit([new DamageResult(Enemy, ValueProp.Move)]);
        NativeDamage(null!, [Enemy], 9m, Hero, card).GetAwaiter().GetResult(); context.AddHit([new DamageResult(Enemy, ValueProp.Move)]);
        context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Select(call => call.Damage).SequenceEqual(new decimal[] { 4, 9, 9, 9, 9, 9, 4, 9, 9, 9, 9, 9 }), "actual AttackContext replay uses shared order and preserves per-segment raw damage: " + string.Join(',', DamageCalls.Select(call => call.Damage)));
        Check(new[] { small, medium, large }.All(power => power.Amount == 2), "actual AttackContext disposal consumes one layer of each variant");
        context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Count == 12 && new[] { small, medium, large }.All(power => power.Amount == 2), "repeated AttackContext disposal does not replay or consume twice");
        context = Context(card); context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Count == 12 && new[] { small, medium, large }.All(power => power.Amount == 2), "actual empty AttackContext preserves layers");
        Reset();
    }
    static void MixedContextCases()
    {
        Reset(); var small = Apply(Small); CardModel card = Card(); var context = Context(card);
        context.AddHit(NativeDamage(null!, [Enemy], 10m, Hero, card).GetAwaiter().GetResult());
        context.AddHit(NativeDamage(null!, [Enemy], 99m, Hero, card, ValueProp.Move | ValueProp.Unpowered).GetAwaiter().GetResult());
        context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Select(call => call.Damage).SequenceEqual(new decimal[] { 10, 99, 10 }),
            "mixed AttackContext combo duplicates only powered attack segments");
        Check(small.Amount == 2, "mixed AttackContext spends exactly one layer for its powered hit");
        DamageCalls.Clear(); context = Context(card);
        context.AddHit(NativeDamage(null!, [Enemy], 99m, Hero, card, ValueProp.Move | ValueProp.Unpowered).GetAwaiter().GetResult());
        context.DisposeAsync().GetAwaiter().GetResult();
        Check(DamageCalls.Select(call => call.Damage).SequenceEqual(new decimal[] { 99 }), "pure unpowered AttackContext does not gain extra segments");
        Check(small.Amount == 2, "pure unpowered AttackContext does not consume a combo layer");
        Reset();
    }
    static void PhantomCases()
    {
        Reset(); FixtureCall("Mode", false, 1);
        var actor = (Creature)FixtureCall("Actor", Blank<SmallHopper>(), null, Player)!;
        var small = Apply(Small, 5, actor); Apply(typeof(PhantomNextAttackDoublePower), 1, actor);
        AttackIntent[] intents = [new SingleAttackIntent(4), new SingleAttackIntent(9)];
        MethodInfo build = Helper("PhantomHelper").GetMethod("BuildAttackSegments", F)!;
        var segments = (IReadOnlyList<decimal>)Invoke(build, null, actor, intents, 0, true)!;
        Check(segments.SequenceEqual(new decimal[] { 4, 9, 9, 4, 9, 9 }), "actual phantom segment builder adds new small before legacy phantom doubling");
        Check(small.Amount == 5, "actual phantom intent segment preview preserves new combo charges");
        object scope = Call("BeginAttack", actor)!;
        try
        {
            segments = (IReadOnlyList<decimal>)Invoke(build, null, actor, intents, 0, true)!;
            var perform = (Task<IReadOnlyList<Creature>>)Invoke(Helper("PhantomHelper").GetMethod("PerformAttackIntent", F)!, null, actor, new Creature[] { Enemy }, segments, 0, Enemy)!;
            Check(perform.GetAwaiter().GetResult().SequenceEqual(new[] { Enemy }), "actual phantom damage loop reports its real target");
            Check(DamageCalls.Select(call => call.Damage).SequenceEqual(new decimal[] { 4, 9, 9, 4, 9, 9 }), "actual phantom damage loop executes previewed per-segment sequence");
            ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Check(small.Amount == 4, "actual phantom damage loop marks one complete attack for combo consumption");
        }
        finally { ((IDisposable)scope).Dispose(); }
        Reset();
    }
    static void ActualCardCases()
    {
        foreach ((Type type, int expectedHits) in new[] {
            (typeof(StrongShotHbrgoSaki), 4), (typeof(BladeDanceHbrgoSaki), 5),
            (typeof(DragonArrowHbrgoSaki), 4), (typeof(ConsecutiveSlashHbrgoSaki), 3) })
        {
            Reset(); FixtureCall("Mode", false, 1); var small = Apply(Small, 5);
            Play(OwnedCard(type), Enemy);
            Check(DamageCalls.Count == expectedHits, type.Name + " actual OnPlay and native Execute share one complete attack's combo segment count");
            Check(small.Amount == 4, type.Name + " actual OnPlay and native Execute consume only one combo charge for the whole card");
            Check(DamageCalls.All(call => call.Targets.SequenceEqual(new[] { Enemy })), type.Name + " real native Execute retains original target routing");
        }
        Reset(); FixtureCall("Mode", false, 1); var dynamicSmall = Apply(Small, 5);
        var resistance = RawPower<SixSenseResistancePower>(Enemy);
        typeof(SixSenseResistancePower).GetProperty("Resistances", F)!.SetValue(resistance, new int[] { 1, 1, 1, 1, 1, 1 });
        AfterRecordedDamage = hit => { if (hit == 1) resistance.Resistances[0] = 0; };
        Play(OwnedCard(typeof(ConsecutiveSlashHbrgoSaki)), Enemy);
        // The per-lost-resistance bonus left with the six-sense resistances (2026-11-09):
        // an older resistance power that changes between hits no longer changes the damage.
        Check(DamageCalls.Count == 3 && DamageCalls.All(call => call.Damage == DamageCalls[0].Damage),
            "ConsecutiveSlash deals the same damage on every native hit, whatever an old resistance power holds");
        Check(dynamicSmall.Amount == 4, "dynamic ConsecutiveSlash retains one-charge whole-card consumption");
        Reset(); FixtureCall("Mode", false, 1);
        var second = (Creature)FixtureCall("Actor", Blank<SmallHopper>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { Enemy, second });
        try
        {
            RawPower<FragilePower>(second, 2);
            var small = Apply(Small, 5);
            Play(OwnedCard(typeof(PiercingShotHbrgoSaki)));
            Check(DamageCalls.Count == 4 && DamageCalls.Count(call => call.Targets.SequenceEqual(new[] { Enemy })) == 2 && DamageCalls.Count(call => call.Targets.SequenceEqual(new[] { second })) == 2,
                "PiercingShot actual OnPlay gives each target its one extra segment without changing per-target commands");
            Check(small.Amount == 4, "PiercingShot actual two-target OnPlay consumes one combo charge for the whole card");
            Check(DamageCalls.Where(call => call.Targets.Contains(second)).All(call => call.Damage > DamageCalls.First(item => item.Targets.Contains(Enemy)).Damage),
                "PiercingShot preserves distinct per-target damage from the target's fragile status");
        }
        finally { Set(Combat, "_enemies", new List<Creature> { Enemy }); Reset(); }
    }
    static void FactoryReplayCases()
    {
        foreach ((Type type, decimal[] expected, bool[] absoluteExpected) in new[] {
            (Small, new decimal[] { 10, 20, 20 }, new[] { false, true, true }),
            (Large, new decimal[] { 10, 20, 10, 20 }, new[] { false, true, false, true }) })
        {
            Reset(); FixtureCall("Mode", false, 1); var power = Apply(type, 3); CardModel card = Card();
            object scope = Call("BeginCardAttack", card)!;
            async Task<AttackCommand> Execute(decimal damage, bool absolute)
            {
                using IDisposable? absoluteScope = absolute
                    ? (IDisposable)Invoke(Helper("HbrAbsoluteBaseDamageContext").GetMethod("Begin", F)!, null, Hero, card, Enemy, damage)!
                    : null;
                var command = new AttackCommand(damage);
                MethodInfo fromCard = typeof(AttackCommand).GetMethods(F).Single(method => method.Name == "FromCard");
                Invoke(fromCard, command, fromCard.GetParameters().Length == 1 ? [card] : [card, null]);
                return await command.Targeting(Enemy).Execute(null!);
            }
            try
            {
                Func<Task<AttackCommand>>[] attacks = [() => Execute(10m, false), () => Execute(20m, true)];
                foreach (Func<Task<AttackCommand>> attack in attacks) On(scope, "Record", attack().GetAwaiter().GetResult());
                ((Task)On(scope, "ReplayExtra", attacks, new Func<bool>(() => true))!).GetAwaiter().GetResult();
                Check(DamageCalls.Select(call => call.Damage).SequenceEqual(expected), type.Name + " factory replay preserves mixed 10/20 base damage order");
                Check(DamageCalls.Select(call => call.Absolute).SequenceEqual(absoluteExpected), type.Name + " repeated resource-damage factory re-enters its absolute damage context");
                Check(power.Amount == 3, type.Name + " nested factory commands do not consume the whole-card reservation");
                ((Task)On(scope, "Complete", (object?)null)!).GetAwaiter().GetResult();
                Check(power.Amount == 2, type.Name + " factory sequence consumes one layer after all original and extra attacks");
            }
            finally { ((IDisposable)scope).Dispose(); }
        }
        Reset();
    }
    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try { Install(); BasicCases(); GrantCases(); CombinedCases(); InstancePairCases(); ReservationCases(); HolderCases(); DirectCases(); FuturePreviewCases(); FailureCases(); ContextCases(); MixedContextCases(); PhantomCases(); ActualCardCases(); FactoryReplayCases(); VirtualFormCases(); ChaseComboCases(); ProjectedModeCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual native power application and amount mutation, native attack hooks and Execute, production AttackContext replay, direct combo scopes, phantom segment builder/damage loop, five actual card OnPlay routes and mixed attack factory replay with absolute resource-damage context. Shared fixture controls listener enumeration and logging; CreatureCmd.Damage is a deterministic segment/target recorder. No Godot scene, complete card-play action scheduling, complete cannon action, full save or multiplayer session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " combo checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
