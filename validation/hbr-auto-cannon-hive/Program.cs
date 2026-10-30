using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static class HiveChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(HotaruContestCannon).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<(Player Owner, PileType Pile)> Generated = [];
    static Creature Cannon = null!, Phantom = null!;
    static PersonalHivePower Hive = null!;
    static Type Fixture => typeof(Checks);
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static CombatState Combat => State<CombatState>("Combat");
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string method, params object?[] args) => Invoke(Helper(helper).GetMethod(method, F)!, null, args);
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static IDisposable Automatic(Player? owner = null, string phase = "Start") =>
        (IDisposable)Call("HotaruAutomaticCannonContext", "Begin", owner ?? Player, Enum.Parse(Helper("HotaruCannonPhase"), phase))!;
    static bool Suppressed(Creature target, Creature dealer, CardModel? source, ValueProp props = ValueProp.Move) =>
        (bool)Call("HotaruAutomaticCannonContext", "SuppressesPersonalHive", target, dealer, source, props)!;
    static T Power<T>(Creature target, int amount) where T : PowerModel =>
        (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, target, amount)!;
    static CardModel Card() => (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(HelloWorldHbrgoSaki)), null)!;

    static bool StubGenerated(CardModel card, PileType newPileType, ref Task<CardPileAddResult> __result)
    {
        Check(card is Dazed, "native PersonalHive creates actual Dazed model");
        Generated.Add((card.Owner, newPileType));
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }
    static bool Skip() => false;
    static bool Wait(ref Task __result) { __result = Task.CompletedTask; return false; }

    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.automatic-cannon-hive.checks");
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.PhantomCardRecipientPatch", true)!;
        Check(harmony.CreateClassProcessor(patch).Patch().Count > 0, "installs actual PersonalHive production callback prefix");
        foreach (MethodInfo method in typeof(CardPileCmd).GetMethods(F).Where(m => m.Name == "AddGeneratedCardToCombat"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(HiveChecks).GetMethod(nameof(StubGenerated), F)!));
        foreach (MethodInfo method in typeof(CardCmd).GetMethods(F).Where(m => m.Name == "PreviewCardPileAdd"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(HiveChecks).GetMethod(nameof(Skip), F)!));
        foreach (MethodInfo method in typeof(Cmd).GetMethods(F).Where(m => m.Name == "Wait"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(HiveChecks).GetMethod(nameof(Wait), F)!));
        ModelDb.Inject(typeof(Dazed)); Set(ModelDb.Card<Dazed>(), "<Id>k__BackingField", ModelDb.GetId(typeof(Dazed)));
        Set(Combat, "_allCards", new List<CardModel>());
        Cannon = (Creature)FixtureCall("Actor", Blank<HotaruContestCannon>(), null, Player)!;
        Phantom = (Creature)FixtureCall("Actor", Blank<SmallHopper>(), null, Player)!;
        Set(Combat, "_allies", new List<Creature> { Hero, Cannon, Phantom });
        Hive = Power<PersonalHivePower>(Enemy, 2);
    }

    // Seed the same immutable issuing-intent metadata as DamageCore, then use
    // the actual production Begin/Close boundary and matching implementation.
    // No numeric damage, character stats or rendered battle is simulated here.
    static async Task WithCannonHit(Func<Task> callback, CardModel? source = null,
        Creature? cannon = null, Creature? target = null, Creature? dealer = null, bool reaction = false)
    {
        cannon ??= Cannon; target ??= Enemy; dealer ??= cannon.PetOwner!.Creature;
        Type stats = Helper("HotaruCannonStatsHelper");
        Type intentType = stats.GetNestedType("Intent", F)!;
        object intent = Activator.CreateInstance(intentType, true)!;
        Set(intent, "Cannon", cannon); Set(intent, "Target", target); Set(intent, "Dealer", dealer);
        Set(intent, "Card", source); Set(intent, "Amount", 10m); Set(intent, "Props", ValueProp.Move); Set(intent, "IsReaction", reaction);
        object pending = stats.GetField("Pending", F)!.GetValue(null)!;
        PropertyInfo value = pending.GetType().GetProperty("Value")!;
        object? old = value.GetValue(pending); value.SetValue(pending, intent);
        object? scope = null;
        try
        {
            object?[] args = [new Creature[] { target }, 10m, ValueProp.Move, dealer, source, null];
            scope = Invoke(stats.GetMethod("Begin", F)!, null, args);
            Check(scope != null && (bool)scope.GetType().GetField("Capture", F)!.GetValue(scope)!, "production damage boundary claims seeded cannon intent");
            await callback();
        }
        finally
        {
            if (scope != null) Call("HotaruCannonStatsHelper", "Close", scope);
            Set(intent, "Active", false); value.SetValue(pending, old);
        }
    }
    static Task Receive(Creature? dealer = null, CardModel? source = null, ValueProp props = ValueProp.Move, Creature? target = null) =>
        Hive.AfterDamageReceived(null!, target ?? Enemy, new DamageResult(target ?? Enemy, props) { BlockedDamage = 1 }, props, dealer ?? Hero, source);

    static async Task Cases()
    {
        var card = Card();
        foreach (CardModel? source in new CardModel?[] { null, card })
        {
            Generated.Clear();
            await WithCannonHit(async () =>
            {
                Check(!Suppressed(Enemy, Hero, source), "manual cannon has no automatic immunity with " + (source == null ? "null source" : "card source"));
                await Receive(source: source);
            }, source);
            Check(Generated.Count == 2 && Generated.All(row => row.Owner == Player && row.Pile == PileType.Draw), "manual cannon preserves native two Dazed draw-pile requests");
        }
        foreach (string phase in new[] { "Start", "End" })
        {
            Generated.Clear();
            using (Automatic(phase: phase))
            {
                await WithCannonHit(async () =>
                {
                    Check(Suppressed(Enemy, Hero, null), phase + " automatic phase recognizes current cannon");
                    await Task.Yield();
                    Check(Suppressed(Enemy, Hero, null), phase + " automatic cannon origin flows across await");
                    await Receive(); await Receive();
                    Check(!Suppressed(Enemy, Hero, card), "different source cannot borrow automatic cannon command");
                    Check(!Suppressed(Hero, Hero, null), "different target cannot borrow automatic cannon command");
                });
                // A distinct nested/charged/reward action gets a fresh actual
                // command boundary but inherits the natural phase origin.
                await WithCannonHit(async () => { await Task.Yield(); await Receive(source: card); }, card, reaction: true);
                Check(Generated.Count == 0, phase + " automatic primary, extra and reaction cannon hits generate no Dazed");
                await Receive();
                Check(Generated.Count == 2, phase + " ordinary player hit inside phase still triggers native hive");
                await WithCannonHit(async () =>
                {
                    Check(!Suppressed(Enemy, Phantom, null), "phantom cannot borrow owner's automatic cannon exemption");
                    await Receive(Phantom);
                }, cannon: Phantom, dealer: Phantom);
                Check(Generated.Count == 2, "phantom retains existing PersonalHive recipient handling");
            }
            Generated.Clear();
            await WithCannonHit(async () => { await Receive(); });
            Check(Generated.Count == 2, phase + " scope disposal restores normal cannon hive triggering");
        }
        await ScopeIsolation(card);
    }

    static async Task ScopeIsolation(CardModel card)
    {
        Generated.Clear();
        var foreign = Blank<Player>(); Set(foreign, "_relics", new List<RelicModel>());
        Set(foreign, "<Deck>k__BackingField", new CardPile(PileType.Deck));
        var foreignState = Blank<PlayerCombatState>();
        foreach (var row in new[] { ("Hand", PileType.Hand), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust), ("PlayPile", PileType.Play) })
            Set(foreignState, "<" + row.Item1 + ">k__BackingField", new CardPile(row.Item2));
        Set(foreign, "<PlayerCombatState>k__BackingField", foreignState);
        var foreignHero = (Creature)FixtureCall("Actor", null, foreign, null)!; Set(foreign, "<Creature>k__BackingField", foreignHero);
        var foreignCannon = (Creature)FixtureCall("Actor", Blank<HotaruContestCannon>(), null, foreign)!;
        using (Automatic())
        {
            await WithCannonHit(async () =>
            {
                Check(!Suppressed(Enemy, foreignHero, null), "other player's cannon does not inherit this owner's natural phase exemption");
                await Receive(foreignHero);
            }, cannon: foreignCannon, dealer: foreignHero);
            Check(Generated.Count == 2 && Generated.All(row => row.Owner == foreign), "foreign player cannon retains native Dazed recipient");
            using (Automatic(foreign, "End"))
                await WithCannonHit(async () => { Check(!Suppressed(Enemy, Hero, null), "nested different-owner phase does not mask hit ownership"); await Receive(); });
            await WithCannonHit(async () => { Check(Suppressed(Enemy, Hero, null), "disposing nested phase restores outer owner scope"); await Receive(); });
        }
        Check(Generated.Count == 4, "nested owner scopes suppress exactly their own matching cannon hits");

        Generated.Clear();
        try
        {
            using var scope = Automatic();
            await WithCannonHit(async () => { await Task.Yield(); throw new InvalidOperationException("fixture abort"); });
        }
        catch (InvalidOperationException error) when (error.Message == "fixture abort") { }
        await WithCannonHit(async () => { Check(!Suppressed(Enemy, Hero, null), "exception restores automatic origin and hit scope"); await Receive(); });
        Check(Generated.Count == 2, "failed automatic action does not leak immunity into next manual hit");

        Generated.Clear();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task automatic = Task.Run(async () =>
        {
            using var scope = Automatic();
            await WithCannonHit(async () => { ready.SetResult(); await resume.Task; Check(Suppressed(Enemy, Hero, null), "parallel automatic branch retains its own phase and hit"); await Receive(); });
        });
        await ready.Task;
        await WithCannonHit(async () => { Check(!Suppressed(Enemy, Hero, null), "separate manual async branch does not inherit suspended automatic scope"); await Receive(); });
        resume.SetResult(); await automatic;
        Check(Generated.Count == 2, "parallel manual and automatic branches generate only manual Dazed");
    }

    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try { Install(); Cases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Real PersonalHive.AfterDamageReceived and production prefix; native Dazed creation, real auto-origin and cannon Begin/Close contexts seeded with issuing metadata. Card-pile addition, previews, waits and engine/logging leaves controlled. Async awaits, owner nesting, exception cleanup, manual concurrent branch. No game process, rendered UI, full Millsage turn execution or multiplayer session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " automatic-cannon hive checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
