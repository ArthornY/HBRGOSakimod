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
        return target.GetPower(power.Id)!;
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
        MethodInfo method = typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method => method.GetParameters() is var args && args.Length is 6 or 7 && args[1].ParameterType == typeof(IEnumerable<Creature>) && args[2].ParameterType == typeof(decimal))
            .OrderBy(method => method.GetParameters().Length).First();
        object?[] arguments = method.GetParameters().Length == 6
            ? [context, targets, damage, props, dealer, card]
            : [context, targets, damage, props, dealer, card, null];
        return (Task<IEnumerable<DamageResult>>)Invoke(method, null, arguments)!;
    }
    static bool StubDamage(IEnumerable<Creature> targets, decimal amount, ValueProp props, Creature dealer, CardModel cardSource,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        Creature[] copy = targets.ToArray();
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
        MethodInfo stub = typeof(ComboChecks).GetMethod(nameof(StubDamage), F)!;
        foreach (MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(method => method.Name == nameof(CreatureCmd.Damage) && method.GetParameters() is var args && args.Length is 6 or 7 && args[1].ParameterType == typeof(IEnumerable<Creature>) && args[2].ParameterType == typeof(decimal)))
            harmony.Patch(method, prefix: new HarmonyMethod(stub) { priority = Priority.Last });
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
            Check(power.Amount == charges * 2 && Powers(Hero).Count(power => power.GetType() == type) == 1,
                type.Name + " repeated Grant accumulates remaining charges in one native power");
            ((Task)Invoke(method, null, null, Hero, Hero, null, 2)!).GetAwaiter().GetResult();
            Check(power.Amount == charges * 4, type.Name + " multi-application Grant multiplies acquisition count by default charges");
            var attack = Attack(); Begin(attack); Hit(attack, Enemy); End(attack);
            Check(power.Amount == charges * 4 - 1, type.Name + " acquired duration still spends one per completed attack");
            PowerCmd.ModifyAmount(null!, power, 1, Hero, null, true).GetAwaiter().GetResult();
            Check(power.Amount == charges * 4, type.Name + " ordinary stack mutation adds one charge without multiplying duration");
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
        Check(combo.Amount == 2, "layers granted during reserved attack are retained after one-layer consumption");
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
        Check(DamageCalls.Count == 3 && DamageCalls[1].Damage > DamageCalls[0].Damage,
            "ConsecutiveSlash recalculates damage after resistance changes between real native hits");
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
        try { Install(); BasicCases(); GrantCases(); CombinedCases(); ReservationCases(); HolderCases(); DirectCases(); FuturePreviewCases(); FailureCases(); ContextCases(); MixedContextCases(); PhantomCases(); ActualCardCases(); FactoryReplayCases(); ok = true; }
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
