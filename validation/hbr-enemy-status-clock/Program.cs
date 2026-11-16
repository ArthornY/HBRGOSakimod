using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;

static class EnemyStatusStandalone { public static void Run(string path) => EnemyStatusChecks.Run(path); }

static class EnemyStatusChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(HbrAttackUpPower).Assembly;
    static readonly List<string> Passed = [];
    static Type Fixture => typeof(Checks);
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static CombatState Combat => State<CombatState>("Combat");
    static Player Player => State<Player>("Player");
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string name, params object?[] args) => Invoke(Fixture.GetMethod(name, F)!, null, args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string name, params object?[] args) => Invoke(Helper(helper).GetMethods(F).Single(method =>
        method.Name == name && !method.IsGenericMethodDefinition && method.GetParameters().Length == args.Length &&
        method.GetParameters().Select((parameter, index) => args[index] == null || parameter.ParameterType.IsInstanceOfType(args[index])).All(match => match)), null, args);
    static object? On(object owner, string name, params object?[] args) => Invoke(owner.GetType().GetMethod(name, F)!, owner, args);
    static object Profile(string limit, string exit, int remaining) => Activator.CreateInstance(Helper("HbrEffectProfile"),
        Enum.Parse(Helper("HbrEffectLimit"), limit), Enum.Parse(Helper("HbrEffectExit"), exit), remaining)!;
    static List<PowerModel> Powers(Creature owner) => (List<PowerModel>)FixtureCall("Powers", owner)!;
    static decimal Potency(PowerModel power) => (decimal)power.GetType().GetProperty("EffectPotency", F)!.GetValue(power)!;
    static int Remaining(PowerModel power) => (int)power.GetType().GetProperty("EffectRemaining", F)!.GetValue(power)!;
    static void Set(object owner, string name, object? value) => FixtureCall("Set", owner, name, value);
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Passed.Add(name); }
    static void Equal(decimal actual, decimal expected, string name) => Check(actual == expected, $"{name}: {actual} = {expected}");
    static void Reset() => FixtureCall("Reset");
    static T Raw<T>(Creature owner, int amount) where T : PowerModel =>
        (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static T[] Existing<T>(Creature? owner = null) where T : PowerModel => Powers(owner ?? Hero).OfType<T>().ToArray();
    static void Apply<T>(decimal basis, Creature? owner = null, int? remaining = null, object? profile = null) where T : HbrEffectPower
    {
        MethodInfo apply = Helper("HbrEffectApplication").GetMethods(F).Single(method => method.Name == "Apply" && method.IsGenericMethodDefinition);
        ((Task)Invoke(apply.MakeGenericMethod(typeof(T)), null, null, owner ?? Hero, basis, Hero, null, remaining, profile, true, null)!).GetAwaiter().GetResult();
    }
    static void Tick(PowerModel power, CombatSide side, params Creature[] participants) =>
        power.AfterSideTurnEnd(null!, side, participants).GetAwaiter().GetResult();
    static AttackCommand Attack()
    {
        var attack = new AttackCommand(1m);
        Set(attack, "<Attacker>k__BackingField", Hero); Set(attack, "<DamageProps>k__BackingField", ValueProp.Move);
        return attack;
    }
    static void Hit(AttackCommand attack) => attack.AddResultsInternal([new DamageResult(Enemy, ValueProp.Move) { BlockedDamage = 1 }]);
    static void Begin(AttackCommand attack) => Hook.BeforeAttack(Combat, attack).GetAwaiter().GetResult();
    static void End(AttackCommand attack) => Hook.AfterAttack(Combat, null!, attack).GetAwaiter().GetResult();
    static readonly List<decimal> EnergyLosses = [];
    static readonly List<decimal> HpLosses = [];
    sealed class StatusDpCapPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play) => target == Owner ? 100m : decimal.MaxValue;
#else
        public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? source) => target == Owner ? 100m : decimal.MaxValue;
#endif
    }
    static bool NoFlash() => false;
    static bool NetIdLeaf(ref ulong? __result) { __result = Player.NetId; return false; }
    static bool WaitLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool EnergyLeaf(decimal amount, Player player, ref Task __result)
    {
        EnergyLosses.Add(amount);
        Set(player.PlayerCombatState!, "_energy", Math.Max(0, player.PlayerCombatState!.Energy - (int)amount));
        __result = Task.CompletedTask; return false;
    }
    static bool HpLeaf(object?[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        Type api = Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!;
        Creature[] targets = ((IEnumerable<Creature>)Invoke(api.GetMethod("ReadTargets", F)!, null, new object?[] { __args })!).ToArray();
        decimal amount = (decimal)__args[2]!;
        Check((ValueProp)__args[3]! == (ValueProp.Unpowered | ValueProp.Unblockable), "periodic HP overflow retains unpowered/unblockable flags");
        HpLosses.Add(amount);
        foreach (Creature target in targets)
        {
            if (target.GetPower<LargeHealthPower>() is { } large)
                On(large, "Store", Math.Max(0m, (decimal)On(large, "get_CurrentHp")! - amount), (decimal)On(large, "get_MaxHp")!);
            else Set(target, "_currentHp", Math.Max(0, target.CurrentHp - (int)amount));
        }
        __result = Task.FromResult<IEnumerable<DamageResult>>(Array.Empty<DamageResult>()); return false;
    }
    static void Setup()
    {
        FixtureCall("Setup");
        var h = new Harmony("hbr.enemy-status.clock");
        Type local = typeof(Hook).Assembly.GetType("MegaCrit.Sts2.Core.Context.LocalContext", true)!;
        h.Patch(local.GetProperty("NetId", F)!.GetMethod!, prefix: new HarmonyMethod(typeof(EnemyStatusChecks).GetMethod(nameof(NetIdLeaf), F)!));
        foreach (Type type in new[] { typeof(HbrEnemyChargePower), typeof(HbrImprisonPower), typeof(HbrBindPower), typeof(HbrStunPower),
            typeof(HbrContinuousDpDamagePower), typeof(HbrVirusPower), typeof(HbrIllusionPower), typeof(HbrConfusionPower),
            typeof(HbrContinuousDamagePower), typeof(HbrEnergyDrainPower), typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower),
            typeof(HbrAttackUpPower), typeof(HighDimensionalShellPower), typeof(BreakPower), typeof(DestructionRatePower), typeof(LargeHealthPower) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        foreach (string name in new[] { "HbrEffectApplicationPatch", "HbrEffectInstanceStackingPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0, "installs production " + name);
        foreach (Type type in new[] { typeof(PowerModel), typeof(RelicModel) })
            foreach (MethodInfo method in type.GetMethods(F).Where(m => m.Name == "Flash"))
                h.Patch(method, prefix: new HarmonyMethod(typeof(EnemyStatusChecks).GetMethod(nameof(NoFlash), F)!));
        h.Patch(typeof(Cmd).GetMethod("CustomScaledWait")!, prefix: new HarmonyMethod(typeof(EnemyStatusChecks).GetMethod(nameof(WaitLeaf), F)!));
        h.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.LoseEnergy), [typeof(decimal), typeof(Player)])!,
            prefix: new HarmonyMethod(typeof(EnemyStatusChecks).GetMethod(nameof(EnergyLeaf), F)!));
        Type damageApi = Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility", true)!;
        h.Patch((MethodInfo)damageApi.GetField("Command", F)!.GetValue(null)!,
            prefix: new HarmonyMethod(typeof(EnemyStatusChecks).GetMethod(nameof(HpLeaf), F)!));
    }
    static async Task Dispatch(string name, CombatSide side)
    {
        if (name == "AfterSideTurnEnd" && !typeof(Hook).GetMethods(F).Any(method => method.Name == name)) name = "AfterTurnEnd";
        Creature[] participants = side == CombatSide.Player ? [Hero] : [Enemy];
        MethodInfo hook = typeof(Hook).GetMethods(F).SingleOrDefault(m => m.Name == name) ??
            throw new Exception("Missing native side hook " + name + "; " + string.Join("; ", typeof(Hook).GetMethods(F)
                .Where(m => m.Name.Contains("Turn") || m.Name.Contains("Energy")).Select(m => m.ToString())));
        object?[] args = hook.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(CombatSide) ? (object)side :
            parameter.ParameterType == typeof(Player) ? Player :
            parameter.ParameterType == typeof(PlayerChoiceContext) ? new ThrowingPlayerChoiceContext() :
            parameter.ParameterType.IsAssignableFrom(typeof(CombatState)) ? Combat :
            parameter.ParameterType.IsAssignableFrom(typeof(Creature[])) ? participants :
            throw new Exception("Unmapped native hook parameter " + name + ": " + parameter)).ToArray();
        await (Task)Invoke(hook, null, args)!;
    }
    static void Clear()
    {
        Reset(); EnergyLosses.Clear(); HpLosses.Clear();
        Set(Hero, "_maxHp", 5000); Set(Hero, "_currentHp", 5000); Set(Hero, "_block", 9999);
        Set(Enemy, "_maxHp", 5000); Set(Enemy, "_currentHp", 5000);
        Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual") });
    }
    static T Attach<T>(Creature owner, int amount) where T : PowerModel => Raw<T>(owner, amount);
    static Creature Monster<T>(bool phantom = false) where T : MonsterModel =>
        (Creature)FixtureCall("Actor", FixtureCallGenericBlank<T>(), null, phantom ? Player : null)!;
    static T FixtureCallGenericBlank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static async Task NativeStatus<T>(Creature? owner = null, decimal amount = 1m) where T : PowerModel =>
        await PowerCmd.Apply<T>(new ThrowingPlayerChoiceContext(), owner ?? Hero, amount, Enemy, null, true);
    static async Task AttackCardEvent()
    {
        var card = FixtureCallGenericBlank<StrikeIronclad>(); Set(card, "_owner", Player); Set(card, "<IsMutable>k__BackingField", true);
        Set(card, "<Type>k__BackingField", CardType.Attack);
        var play = FixtureCallGenericBlank<CardPlay>(); Set(play, "<Card>k__BackingField", card);
        MethodInfo hook = typeof(Hook).GetMethods(F).Single(m => m.Name == "AfterCardPlayed");
        object?[] args = hook.GetParameters().Select(parameter => parameter.ParameterType == typeof(CardPlay) ? (object)play :
            parameter.ParameterType == typeof(PlayerChoiceContext) ? new ThrowingPlayerChoiceContext() :
            parameter.ParameterType.IsAssignableFrom(typeof(CombatState)) ? Combat :
            throw new Exception("Unmapped card hook parameter " + parameter)).ToArray();
        await (Task)Invoke(hook, null, args)!;
    }
    static async Task TableDurations()
    {
        foreach ((Type type, int length) in new[] { (typeof(DiamondEyeball), 2), (typeof(CorollaKnife), 2), (typeof(ArachneLine), 2),
            (typeof(SlimySerpent), 2), (typeof(BarrelWalker), 4), (typeof(ShrimpCrab), 4) })
        {
            Clear(); var monster = (MonsterModel)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(type), null)!;
            Creature caster = (Creature)FixtureCall("Actor", monster, null, null)!;
            decimal outgoingBefore = (decimal)FixtureCall("Preview", Enemy, Hero, 6m, ValueProp.Move, null)!;
            decimal incomingBefore = (decimal)FixtureCall("Preview", Hero, caster, 6m, ValueProp.Move, null)!;
            for (int index = 0; index < 3; index++)
                await (Task)Call("HbrPercentDebuffApplication", "ApplyAttackDown", new ThrowingPlayerChoiceContext(), Hero, 1m, caster, null, true)!;
            await (Task)Call("HbrPercentDebuffApplication", "ApplyDefenseDown", new ThrowingPlayerChoiceContext(), Hero, 1m, caster, null, true)!;
            HbrAttackDownPower[] copies = Existing<HbrAttackDownPower>();
            Check(copies.Length == 3 && copies.All(p => p.Amount == length), type.Name + " native application retains three independent original-duration effects");
            await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
            Check(copies.All(p => p.Amount == length - 1), type.Name + " application enemy end counts one");
            await Dispatch("BeforeSideTurnStart", CombatSide.Player);
            Check(Existing<HbrAttackDownPower>().Length == 3, type.Name + " attack-down survives the following player start");
            Check(copies[0].ModifyDamageMultiplicative(Enemy, 100m, ValueProp.Move, Hero, null) < 1m,
                type.Name + " surviving real power still reduces outgoing powered damage");
            Check((decimal)FixtureCall("Preview", Enemy, Hero, 6m, ValueProp.Move, null)! < outgoingBefore,
                type.Name + " native damage modifier dispatch still reduces actual player damage on the next turn");
            await Dispatch("AfterSideTurnEnd", CombatSide.Player);
            Check(Existing<HbrDefenseDownPower>().Single().ModifyDamageMultiplicative(Hero, 100m, ValueProp.Move, caster, null) > 1m,
                type.Name + " defense-down still increases received damage in the next enemy turn");
            Check((decimal)FixtureCall("Preview", Hero, caster, 6m, ValueProp.Move, null)! > incomingBefore,
                type.Name + " native damage modifier dispatch increases the next enemy hit");
            for (int turn = 1; turn < length; turn++) await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
            Check(Existing<HbrAttackDownPower>().Length == 0, type.Name + " expires at the exact enemy-end count");
            Clear(); Creature phantom = (Creature)FixtureCall("Actor", monster, null, Player)!;
            object? profile = Call("HbrEnemyDebuffDurationHelper", "ForTableMonster", ModelDb.Power<HbrAttackDownPower>(), phantom);
            Check(profile == null, type.Name + " phantom has no enemy-table duration override");
            for (int index = 0; index < 3; index++)
                await (Task)Call("HbrPercentDebuffApplication", "ApplyAttackDown", new ThrowingPlayerChoiceContext(), Enemy, 1m, phantom, null, true)!;
            Check(Existing<HbrAttackDownPower>(Enemy).Length == 2 && Existing<HbrAttackDownPower>(Enemy).All(p => p.Amount == 1),
                type.Name + " actual phantom applications retain the two-slot one-enemy-end profile");
        }
    }
    static async Task SwitchAndClocks()
    {
        Clear(); await NativeStatus<HbrImprisonPower>(); await NativeStatus<HbrImprisonPower>();
        Check(Existing<HbrImprisonPower>().Length == 2, "native generic Imprison produces separate framework instances");
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(Existing<HbrImprisonPower>().All(p => p.Amount == 1), "seal remains across the player's next turn");
        await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        Check(Existing<HbrImprisonPower>().Length == 2, "player end does not prematurely clear original enemy-end seal");
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(Existing<HbrImprisonPower>().Length == 0, "seal copies expire on second enemy end");
        Clear(); await NativeStatus<HbrBindPower>(); await NativeStatus<HbrBindPower>(); await NativeStatus<HbrStunPower>();
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Set(Player.PlayerCombatState!, "_energy", 3);
        await Dispatch("AfterEnergyReset", CombatSide.Player);
        Check(EnergyLosses.SequenceEqual(new[] { 2m }), "two binds plus stun lose two energy in the real reset hook");
        Check(Player.PlayerCombatState!.Energy == 1, "reset hook applies one actual two-energy spend");
        await Dispatch("AfterEnergyReset", CombatSide.Player);
        Check(EnergyLosses.Count == 1, "pending lock copies cannot spend again after a second reset");
        await Dispatch("BeforeSideTurnStart", CombatSide.Enemy);
        Check(Existing<HbrStunPower>().Length == 0 && Existing<HbrBindPower>().Length == 2, "stun has distinct enemy-start exit while bind retains enemy-end lifetime");
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(Existing<HbrBindPower>().Length == 0, "bind ends on its second enemy end");
        Clear(); await NativeStatus<HbrConfusionPower>(); await NativeStatus<HbrConfusionPower>();
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(Existing<HbrConfusionPower>().All(p => p.Amount == 3), "application enemy end does not shorten player confusion");
        for (int index = 0; index < 2; index++)
        { await Dispatch("AfterSideTurnEnd", CombatSide.Player); Check(Existing<HbrConfusionPower>().Length == 2, "confusion covers player turn " + (index + 1)); }
        await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        Check(Existing<HbrConfusionPower>().Length == 0, "confusion expires after its third player end");
        foreach (int energy in new[] { 0, 1 })
        {
            Clear(); await NativeStatus<HbrBindPower>(); await NativeStatus<HbrStunPower>();
            Set(Player.PlayerCombatState!, "_energy", energy);
            await Dispatch("AfterEnergyReset", CombatSide.Player);
            Check(Player.PlayerCombatState!.Energy == 0 && EnergyLosses.SequenceEqual(new[] { 2m }),
                "combined action lock clamps insufficient energy " + energy + " at zero, once");
        }
        foreach (bool stun in new[] { false, true })
        {
            Clear(); FixtureCall("Mode", false, 1);
            try
            {
                if (stun) await NativeStatus<HbrStunPower>(); else await NativeStatus<HbrBindPower>();
                Set(Player.PlayerCombatState!, "_energy", 3);
                await Dispatch("AfterEnergyReset", CombatSide.Player);
                Check(Player.PlayerCombatState!.Energy == 2 && EnergyLosses.SequenceEqual(new[] { 1m }),
                    "legacy " + (stun ? "stun" : "bind") + " keeps its actual one-energy spend and immediate removal");
                Check(!Hero.Powers.Any(power => power is HbrActionLockPower), "legacy action lock retains its immediate consume timing");
            }
            finally { FixtureCall("Mode", true, 1); }
        }
    }
    static DeflectorProtection Dp(int amount = 10000)
    {
        var dp = FixtureCallGenericBlank<DeflectorProtection>(); Set(dp, "<IsMutable>k__BackingField", true); Set(dp, "_owner", Player);
        Set(dp, "_dp", amount); Set(dp, "_hasInitializedDp", true);
        Set(Player, "_relics", new List<RelicModel> { State<RelicModel>("Manual"), dp }); return dp;
    }
    static async Task NumericAndReserve()
    {
        Clear(); DeflectorProtection dp = Dp(); await NativeStatus<HbrVirusPower>(); await NativeStatus<HbrVirusPower>();
        Check(dp.MaxDp == 4000 && dp.Dp == 4000, "two independent viruses lower actual maximum DP by sixty percent and clamp current DP");
        await NativeStatus<HbrVirusPower>(); await NativeStatus<HbrVirusPower>();
        Check(dp.MaxDp == 0 && dp.Dp == 0, "four viruses floor DP maximum at zero");
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(dp.MaxDp == 0 && Existing<HbrVirusPower>().All(p => p.Amount == 2), "virus retains its full player duration at enemy end");
        await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        Check(dp.MaxDp == 0 && Existing<HbrVirusPower>().All(p => p.Amount == 1), "virus remains active after the first player end");
        await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        Check(dp.MaxDp == 10000 && dp.Dp == 0, "expiry restores maximum without returning lost DP");
        Clear(); dp = Dp(); await NativeStatus<HbrContinuousDpDamagePower>(); await NativeStatus<HbrContinuousDpDamagePower>();
        await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        for (int turn = 1; turn <= 4; turn++)
        {
            await Dispatch("BeforeSideTurnStart", CombatSide.Player);
            Check(dp.Dp == 10000 - turn * 1000, "two periodic DP instances contribute ten percent at owner start " + turn);
            await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        }
        Check(Existing<HbrContinuousDpDamagePower>().Length == 0, "DP damage fires four times before expiration, not three");
        Clear(); await NativeStatus<HbrIllusionPower>(amount: 2); await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
        Check(Existing<HbrIllusionPower>().Single().Amount == 2, "illusion retains full duration at enemy end");
        await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        await NativeStatus<HbrIllusionPower>(amount: 3);
        Check((int)OnStatic(typeof(HbrIllusionPower), "TotalLayers", Hero)! == 5, "Illusion independent layer counts add");
        var attack = Attack(); Set(attack, "<Attacker>k__BackingField", Hero);
        await Hook.AfterAttack(Combat, null!, attack); // Player's copies consume via attack-card callback only.
        var first = Existing<HbrIllusionPower>().OrderBy(p => p.Amount).First();
        Equal(Potency(first), 2, "player's native attack callback does not double-spend card-controlled illusion");
        await AttackCardEvent();
        Equal(Potency(first), 1, "one real card hook consumes only the soonest-expiring illusion copy once");
        Check((int)OnStatic(typeof(HbrIllusionPower), "TotalLayers", Hero)! == 4, "other illusion copies retain their independent layers");
        await AttackCardEvent();
        Check(Existing<HbrIllusionPower>().Length == 1 && (int)OnStatic(typeof(HbrIllusionPower), "TotalLayers", Hero)! == 3,
            "exhausting earliest copy leaves later illusion intact");
        Clear(); dp = Dp(300); Apply<HbrContinuousDamagePower>(15m, remaining: 1);
        await Dispatch("BeforeSideTurnStart", CombatSide.Player);
        Check(dp.Dp == 0 && Hero.CurrentHp == 4550 && Hero.Block == 9999m, "reserve continuous damage spends DP300 then exact HP450, ignoring block9999");
        Check(HpLosses.SequenceEqual(new[] { 450m }), "HP overflow bypasses attribute/skill scaling with an exact percentage budget");
        Check(Existing<HbrContinuousDamagePower>().Length == 0, "one-count reserve periodic damage expires after its actual tick");
        Clear();
        var shell = (HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), shell, Enemy, 300m, Enemy, null, true);
        Apply<HbrContinuousDamagePower>(15m, Enemy, remaining: 1);
        await Dispatch("BeforeSideTurnStart", CombatSide.Enemy);
        Check(Enemy.GetPower<HighDimensionalShellPower>() == null && Enemy.HasPower<BreakPower>(),
            "reserve percentage damage empties enemy shell and invokes actual native Break application");
        Check(Enemy.CurrentHp == 4550 && HpLosses.SequenceEqual(new[] { 450m }), "enemy periodic damage also spends DP300 then exact HP450");
        Clear();
        shell = (HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), shell, Enemy, 300m, Enemy, null, true);
        Attach<StatusDpCapPower>(Enemy, 1); Apply<HbrContinuousDamagePower>(15m, Enemy, remaining: 1);
        await Dispatch("BeforeSideTurnStart", CombatSide.Enemy);
        Check(shell.CurrentDp == 200m && Enemy.CurrentHp == 5000 && HpLosses.Count == 0,
            "real DP cap retains DP and prevents periodic HP overflow from bypassing the surviving shield");
        Clear(); dp = Dp(50); Attach<StatusDpCapPower>(Hero, 1); Apply<HbrContinuousDamagePower>(15m, remaining: 1);
        await Dispatch("BeforeSideTurnStart", CombatSide.Player);
        Check(dp.Dp == 0 && Hero.CurrentHp == 4950 && HpLosses.SequenceEqual(new[] { 50m }),
            "one cap100 budget is shared by player DP50 and HP50 rather than independently allowing HP100");
        Clear(); shell = (HighDimensionalShellPower)ModelDb.Power<HighDimensionalShellPower>().ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), shell, Enemy, 50m, Enemy, null, true);
        Attach<StatusDpCapPower>(Enemy, 1); Apply<HbrContinuousDamagePower>(15m, Enemy, remaining: 1);
        await Dispatch("BeforeSideTurnStart", CombatSide.Enemy);
        Check(shell.CurrentDp == 0m && Enemy.HasPower<BreakPower>() && Enemy.CurrentHp == 4950 && HpLosses.SequenceEqual(new[] { 50m }),
            "one cap100 budget is shared by enemy DP50 and HP50 while retaining actual Break");
        Clear(); var health = Attach<LargeHealthPower>(Enemy, 1); On(health, "Store", 1000000000000m, 1000000000000m);
        Apply<HbrContinuousDamagePower>(15m, Enemy, remaining: 1); await Dispatch("BeforeSideTurnStart", CombatSide.Enemy);
        Check(HpLosses.SequenceEqual(new[] { 150000000000m }) && (decimal)On(health, "get_CurrentHp")! == 850000000000m,
            "periodic HP budget uses exact trillion-point health ledger rather than native proxy HP");
        Clear(); Apply<HbrEnergyDrainPower>(2m, remaining: 2); Apply<HbrEnergyDrainPower>(1m, remaining: 1);
        Set(Player.PlayerCombatState!, "_energy", 10); await Dispatch("BeforeSideTurnStart", CombatSide.Player);
        Check(EnergyLosses.Count == 0, "energy drain waits for native energy reset, not before it");
        await Dispatch("AfterEnergyReset", CombatSide.Player);
        Check(Player.PlayerCombatState!.Energy == 7 && Existing<HbrEnergyDrainPower>().Length == 1, "independent energy amounts add and one-count copy expires");
        await Dispatch("AfterEnergyReset", CombatSide.Player);
        Check(Player.PlayerCombatState!.Energy == 5 && Existing<HbrEnergyDrainPower>().Length == 0, "remaining energy instance lasts its exact number of resets");
        Apply<HbrEnergyDrainPower>(2m, Enemy);
        Check(Existing<HbrEnergyDrainPower>(Enemy).Length == 0, "generic common application rejects enemy energy drain");
        Creature summon = Monster<SmallHopper>(phantom: true); Apply<HbrEnergyDrainPower>(2m, summon);
        Check(Existing<HbrEnergyDrainPower>(summon).Length == 0, "generic common application rejects summon energy drain");
    }
    static async Task RecipientStatusClocks()
    {
        foreach ((Type type, int duration) in new[] { (typeof(HbrVirusPower), 2),
            (typeof(HbrIllusionPower), 2), (typeof(HbrConfusionPower), 3) })
        {
            Clear();
            if (type == typeof(HbrVirusPower))
                await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(), Enemy, 300m, Enemy, null, true);
            MethodInfo apply = typeof(PowerCmd).GetMethods(F).Single(method => method.Name == "Apply" &&
                method.IsGenericMethodDefinition && method.GetParameters().Length == 6 && method.GetParameters()[1].ParameterType == typeof(Creature));
            await (Task)Invoke(apply.MakeGenericMethod(type), null, new ThrowingPlayerChoiceContext(), Enemy, 1m, Hero, null, true)!;
            PowerModel effect = Enemy.Powers.Single(power => power.GetType() == type);
            Equal(effect.Amount, duration, type.Name + " enemy holder keeps its original duration");
            await Dispatch("AfterSideTurnEnd", CombatSide.Player);
            Equal(effect.Amount, duration, type.Name + " enemy holder ignores player turn end");
            for (int turn = 1; turn <= duration; turn++)
            {
                await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
                Check(turn < duration ? Enemy.Powers.Contains(effect) && effect.Amount == duration - turn : !Enemy.Powers.Contains(effect),
                    type.Name + " enemy holder retains exact enemy-end clock " + turn);
            }
        }
        Clear(); await NativeStatus<HbrIllusionPower>(amount: 5);
        for (int turn = 1; turn <= 2; turn++)
        {
            await Dispatch("AfterSideTurnEnd", CombatSide.Enemy);
            Check(Existing<HbrIllusionPower>().Single().Amount == 3 - turn,
                "unused player illusion is not shortened by enemy end " + turn);
            await Dispatch("AfterSideTurnEnd", CombatSide.Player);
        }
        Check(Existing<HbrIllusionPower>().Length == 0, "unused player illusion expires at exactly its second player end");
    }
    static object? OnStatic(Type type, string name, params object?[] args) => Invoke(type.GetMethod(name, F)!, null, args);
    static void InjectSavedCache(Type type)
    {
        Type? cache=typeof(SavedProperties).Assembly.GetTypes().SingleOrDefault(t=>t.Name=="SavedPropertiesTypeCache");
        if(cache?.GetMethod("InjectTypeIntoCache",F) is { } inject)Invoke(inject,null,type);
        else
        {
            // Beta folded the native saved-property cache into its model-id
            // serializer. Its debug API builds real metadata; the standalone
            // host does not initialize unrelated epoch/network-id registries.
            cache=typeof(SavedProperties).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;
            Invoke(cache.GetMethod("CacheSavedPropertiesForTypeDebug",F)!,null,type);
            cache.GetField("_initialized",F)!.SetValue(null,true);
        }
    }
    static void SaveFields()
    {
        Clear(); Apply<HbrContinuousDamagePower>(19m, remaining: 3);
        HbrContinuousDamagePower original = Existing<HbrContinuousDamagePower>().Single();
        InjectSavedCache(typeof(HbrContinuousDamagePower));
        var saved = SavedProperties.From(original);
        Check(saved != null, "native saved-property cache recognizes inherited enemy-status fields");
        var options = new JsonSerializerOptions { IncludeFields = true };
        string payload = JsonSerializer.Serialize(saved, options);
        SavedProperties reloaded = JsonSerializer.Deserialize<SavedProperties>(payload, options)!;
        var restored = Raw<HbrContinuousDamagePower>(Hero, original.Amount);
        reloaded.Fill(restored);
        Equal(Potency(restored), 19m, "native saved-property JSON roundtrip restores exact periodic potency");
        Equal(Remaining(restored), 3, "remaining native amount survives the restored copy");
        Check(On(restored, "get_CurrentEffectProfile")!.Equals(On(original, "get_CurrentEffectProfile")), "saved exit and unlimited policy survive JSON roundtrip");
        Check(typeof(HbrActionLockPower).GetProperty("EnergySpent")!.IsDefined(typeof(SavedPropertyAttribute)), "one-time action-lock consumption is a real saved property");
        Clear(); Apply<HbrEnergyDrainPower>(2m, remaining: 3);
        var energy = Existing<HbrEnergyDrainPower>().Single();
        InjectSavedCache(typeof(HbrEnergyDrainPower));
        payload = JsonSerializer.Serialize(SavedProperties.From(energy), options);
        var energyRestored = Raw<HbrEnergyDrainPower>(Hero, energy.Amount);
        JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(energyRestored);
        Check(Potency(energyRestored) == 2m && Remaining(energyRestored) == 3 &&
            On(energyRestored, "get_CurrentEffectProfile")!.Equals(On(energy, "get_CurrentEffectProfile")),
            "native SavedProperties JSON/Fill roundtrip restores energy intensity, clock and remaining amount");
        Clear(); Apply<HbrBindPower>(1m); Dispatch("AfterEnergyReset", CombatSide.Player).GetAwaiter().GetResult();
        var spent = Existing<HbrBindPower>().Single(); InjectSavedCache(typeof(HbrBindPower));
        payload = JsonSerializer.Serialize(SavedProperties.From(spent), options);
        var spentRestored = Raw<HbrBindPower>(Hero, spent.Amount);
        JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(spentRestored);
        EnergyLosses.Clear(); spentRestored.AfterEnergyReset(Player).GetAwaiter().GetResult();
        Check(spentRestored.EnergySpent && EnergyLosses.Count == 0, "native saved action-lock flag prevents a second spend after restoration");
        Clear(); Apply<HbrAttackUpPower>(30m);
        HbrAttackUpPower up = Existing<HbrAttackUpPower>().Single(); InjectSavedCache(typeof(HbrAttackUpPower));
        payload = JsonSerializer.Serialize(SavedProperties.From(up), options);
        var upRestored = Raw<HbrAttackUpPower>(Hero, up.Amount); JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(upRestored);
        Check(Potency(upRestored) == 30m && Remaining(upRestored) == 1,
            "native Fill can restore inherited effect fields and timed-buff legacy fields on concrete AttackUp");
        Clear(); Creature applier = Monster<DiamondEyeball>();
        ((Task)Call("HbrPercentDebuffApplication", "ApplyAttackDown", new ThrowingPlayerChoiceContext(), Hero, 1m, applier, null, true)!).GetAwaiter().GetResult();
        HbrAttackDownPower down = Existing<HbrAttackDownPower>().Single(); InjectSavedCache(typeof(HbrAttackDownPower));
        payload = JsonSerializer.Serialize(SavedProperties.From(down), options);
        var downRestored = Raw<HbrAttackDownPower>(Hero, down.Amount); JsonSerializer.Deserialize<SavedProperties>(payload, options)!.Fill(downRestored);
        Check(downRestored.HbrEffectData == down.HbrEffectData && downRestored.HbrUnlimitedInstances &&
            downRestored.HbrExitOverride == down.HbrExitOverride && downRestored.HbrDefaultRemainingOverride == 2,
            "native Fill restores inherited percentage-debuff potency, unlimited policy and table-monster duration");
    }
    public static void Run(string path)
    {
        bool ok = false; string? error = null;
        try { Setup(); TableDurations().GetAwaiter().GetResult(); SwitchAndClocks().GetAwaiter().GetResult(); NumericAndReserve().GetAwaiter().GetResult(); RecipientStatusClocks().GetAwaiter().GetResult(); SaveFields(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            boundary = "Real native PowerCmd applications and Hook side-turn/energy dispatch; actual common profile rules, DP mutation and saved-property JSON. Terminal HP and energy command leaves isolated to avoid engine UI, with exact values and flags checked. No Godot or in-game rendering." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " enemy status clock checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
