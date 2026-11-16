using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class DpBreakStatusChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Assembly Mod = typeof(HighDimensionalShellPower).Assembly;
    static readonly List<string> Passed = [];
    static object? Invoke(MethodInfo m, object? owner, params object?[] args)
    { try { return m.Invoke(owner, args); } catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; } }
    static object? Fixture(string name, params object?[] args) => Invoke(typeof(EnemyStatusChecks).GetMethod(name, F)!, null, args);
    static T State<T>(string name) => (T)Invoke(typeof(EnemyStatusChecks).GetMethod("State", F)!.MakeGenericMethod(typeof(T)), null, name)!;
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static CombatState Combat => State<CombatState>("Combat");
    static void Set(object owner, string name, object? value) => Fixture("Set", owner, name, value);
    static object? Call(string helper, string name, params object?[] args) => Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers." + helper, true)!.GetMethod(name, F)!, null, args);
    static object? On(object owner, string name, params object?[] args) => Invoke(owner.GetType().GetMethod(name, F)!, owner, args);
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Passed.Add(name); }
    static void Reset(CombatSide side = CombatSide.Player) { Fixture("Clear"); Set(Combat, "<CurrentSide>k__BackingField", side); }
    static HbrAttackDownPower[] Down(Creature target) => target.Powers.OfType<HbrAttackDownPower>().ToArray();
    static decimal Percent(HbrAttackDownPower power) => (decimal)On(power, "get_EffectPercent")!;
    static async Task Penalty(Creature owner) => await (Task)Call("HbrDpBreakStatusHelper", "Apply", new ThrowingPlayerChoiceContext(), owner)!;
    static T Raw<T>(Creature owner, int amount) where T : PowerModel => (T)Invoke(typeof(EnemyStatusChecks).GetMethod("Raw", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static Creature Monster<T>(bool phantom = false) where T : MonsterModel => (Creature)Invoke(typeof(EnemyStatusChecks).GetMethod("Monster", F)!.MakeGenericMethod(typeof(T)), null, phantom)!;
    static async Task Hit(HighDimensionalShellPower shell, decimal amount)
    {
        shell.TryAbsorb(amount);
        await shell.AfterDamageReceived(new ThrowingPlayerChoiceContext(), shell.Owner,
            new DamageResult(shell.Owner, ValueProp.Move), ValueProp.Move, Hero, null!);
    }
    static async Task<HighDimensionalShellPower> Shell(Creature owner, Type? type = null)
    {
        type ??= typeof(HighDimensionalShellPower);
        if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        var shell = (HighDimensionalShellPower)ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), shell, owner, 100m, owner, null, true);
        return shell;
    }
    static async Task OrdinaryAndDirect()
    {
        Reset(); var shell = await Shell(Enemy); Raw<HbrEnemyChargePower>(Enemy, 1); Raw<HbrEnemyChargePower>(Enemy, 1);
        await Hit(shell, 99m);
        Check(Down(Enemy).Length == 0 && Enemy.HasPower<HbrEnemyChargePower>(), "partial DP damage leaves charge and adds no penalty");
        var intent = Enemy.Monster!.NextMove;
        await Hit(shell, 1m);
        Check(!Enemy.HasPower<HbrEnemyChargePower>(), "actual ordinary DP break clears charge");
        Check(Down(Enemy).Length == 1 && Percent(Down(Enemy)[0]) == 30m && Down(Enemy)[0].Amount == 1, "ordinary DP break gives one fixed30 one-turn instance");
        Check(ReferenceEquals(Enemy.Monster.NextMove, intent), "DP break does not add a stun or replace queued intent");
        await Hit(shell, 999m);
        Check(Down(Enemy).Length == 1, "repeated hit callback on depleted shell does not apply a second penalty");
        Reset(); shell = await Shell(Enemy); Raw<HbrEnemyChargePower>(Enemy, 1);
        await (Task)On(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0)!;
        Check(Down(Enemy).Length == 1 && !Enemy.HasPower<HbrEnemyChargePower>(), "explicit BreakImmediately shares charge clear and penalty");
        await (Task)On(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0)!;
        Check(Down(Enemy).Length == 1, "explicit break against empty removed shell is inert");
        Reset(); shell = await Shell(Enemy);
        await (Task)On(shell, "DamageDpOnly", new ThrowingPlayerChoiceContext(), 100m, Hero, null, null)!;
        Check(Down(Enemy).Length == 1, "DP-only damage shares real exhaustion penalty");
        Reset(); var phantom = Monster<NeedleBird>(true); Raw<PhantomPower>(phantom, 1);
        shell = await Shell(phantom, typeof(PhantomHighDimensionalShellPower));
        Raw<HbrEnemyChargePower>(phantom, 1); await Hit(shell, 100m);
        Check(Down(phantom).Length == 1 && !phantom.HasPower<HbrEnemyChargePower>(), "phantom DP exhaustion clears charge even though default Break is suppressed");
    }
    static async Task PlayersAndCosts()
    {
        Reset(CombatSide.Enemy); var dp = (DeflectorProtection)Fixture("Dp", 100)!; Raw<HbrEnemyChargePower>(Hero, 1);
        dp.AbsorbDp(99m);
        await dp.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Hero, new DamageResult(Hero, ValueProp.Move), ValueProp.Move, Enemy, null!);
        Check(Down(Hero).Length == 0, "player partial DP damage does not apply penalty");
        dp.AbsorbDp(1m);
        await dp.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Hero, new DamageResult(Hero, ValueProp.Move), ValueProp.Move, Enemy, null!);
        Check(Down(Hero).Length == 1 && Percent(Down(Hero)[0]) == 30m && !Hero.HasPower<HbrEnemyChargePower>(), "player actual damage exhaustion gives fixed30 and clears charge");
        await dp.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Hero, new DamageResult(Hero, ValueProp.Move), ValueProp.Move, Enemy, null!);
        Check(Down(Hero).Length == 1, "player callback is consumed once");
        Reset(); dp = (DeflectorProtection)Fixture("Dp", 100)!; Raw<HbrEnemyChargePower>(Hero, 1);
        On(dp, "LoseDpDirect", 100m);
        await dp.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Hero, new DamageResult(Hero, ValueProp.Move), ValueProp.Move, Enemy, null!);
        Check(dp.Dp == 0 && Down(Hero).Length == 0 && Hero.HasPower<HbrEnemyChargePower>(), "resource cost reaching0 leaves charge and does not masquerade as break");
        Reset(); dp = (DeflectorProtection)Fixture("Dp", 100)!; Raw<HbrEnemyChargePower>(Hero, 1);
        dp.LoseMaxDp(int.MaxValue);
        await dp.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Hero, new DamageResult(Hero, ValueProp.Move), ValueProp.Move, Enemy, null!);
        Check(dp.MaxDp == 0 && dp.Dp == 0 && Down(Hero).Length == 0 && Hero.HasPower<HbrEnemyChargePower>(),
            "player maximum clamp reaching0 leaves charge and creates no break penalty");
        Reset(); var maxShell = await Shell(Enemy); Raw<HbrEnemyChargePower>(Enemy, 1);
        FieldInfo config = Mod.GetType("HBRGOSakiMod.Core.Config.HBRGOSakiConfig", true)!.GetField("_enemyHighDimensionalShellPercent", F)!;
        object previousConfig = config.GetValue(null)!;
        try
        {
            config.SetValue(null, 0); On(maxShell, "ClampToMaximum");
            await maxShell.AfterDamageReceived(new ThrowingPlayerChoiceContext(), Enemy, new DamageResult(Enemy, ValueProp.Move), ValueProp.Move, Hero, null!);
            Check(maxShell.CurrentDp == 0m && Down(Enemy).Length == 0 && Enemy.HasPower<HbrEnemyChargePower>() && Enemy.Powers.Contains(maxShell),
                "shell maximum clamp reaching0 leaves charge and creates no break penalty");
        }
        finally { config.SetValue(null, previousConfig); }
        Reset(); maxShell = await Shell(Enemy); Raw<HbrEnemyChargePower>(Enemy, 1);
        await PowerCmd.Remove(maxShell);
        Check(Down(Enemy).Length == 0 && Enemy.HasPower<HbrEnemyChargePower>(), "ordinary shell removal is not a DP break");
        Reset(); dp = (DeflectorProtection)Fixture("Dp", 100)!;
        await (Task)Call("HbrEnemyEffects", "BreakDp", new ThrowingPlayerChoiceContext(), Hero, Enemy)!;
        Check(dp.Dp == 0 && Down(Hero).Length == 1, "explicit enemy DP-breaking skill applies player penalty");
        Reset(); dp = (DeflectorProtection)Fixture("Dp", 100)!;
        await (Task)Call("HbrEnemyEffects", "LoseDpShare", new ThrowingPlayerChoiceContext(), Hero, .001m, Enemy)!;
        Check(dp.Dp > 0 && Down(Hero).Length == 0, "periodic partial DP drain does not trigger break penalty");
        await (Task)Call("HbrEnemyEffects", "LoseDpShare", new ThrowingPlayerChoiceContext(), Hero, 1m, Enemy)!;
        Check(dp.Dp == 0 && Down(Hero).Length == 1, "periodic DP drain reaching0 triggers one penalty");
        Reset(); dp = (DeflectorProtection)Fixture("Dp", 100)!;
        Creature needle = Monster<NeedleBird>(); var needleShell = await Shell(needle);
        Raw<HbrEnemyChargePower>(needle, 1); await Hit(needleShell, 100m);
        await (Task)On(needle.Monster!, "RuinBulletMove", (object)new Creature[] { Hero })!;
        Check(dp.Dp == 100 && !needle.HasPower<HbrEnemyChargePower>(), "stale advertised Ruin Bullet cannot empty player DP after charge is broken");
    }
    static async Task ClocksAndHooks()
    {
        Reset(); await Penalty(Enemy); var effect = Down(Enemy).Single();
        Check(effect.ModifyDamageMultiplicative(Hero, 100m, ValueProp.Move, Enemy, null) == .7m,
            "fixed30 produces exact0point7 outgoing powered attack factor");
        await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(Enemy.Powers.Contains(effect), "enemy penalty survives player end");
        await effect.AfterSideTurnEnd(null!, CombatSide.Enemy, []);
        Check(Enemy.Powers.Contains(effect), "unrelated participant end does not expire holder penalty");
        await effect.BeforeSideTurnStart(null!, CombatSide.Enemy, [Enemy], Combat);
        await effect.AfterSideTurnEnd(null!, CombatSide.Enemy, [Enemy]);
        Check(!Enemy.Powers.Contains(effect), "enemy penalty ends after holder next own turn");
        Reset(); await Penalty(Hero); effect = Down(Hero).Single();
        Check((bool)On(effect, "get_HbrWaitForOwnerTurnStart")!, "break on holder side waits for next holder turn start");
        await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(Hero.Powers.Contains(effect), "same-turn rebound break does not expire immediately");
        await effect.BeforeSideTurnStart(null!, CombatSide.Player, [], Combat);
        Check((bool)On(effect, "get_HbrWaitForOwnerTurnStart")!, "teammate-only extra turn does not arm owner penalty expiry");
        await effect.BeforeSideTurnStart(null!, CombatSide.Player, [Hero], Combat);
        await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(!Hero.Powers.Contains(effect), "player penalty ends at next participating own turn end");
        Reset(); var phantom = Monster<NeedleBird>(true); await Penalty(phantom); effect = Down(phantom).Single();
        await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(phantom.Powers.Contains(effect), "same-side phantom penalty survives current summoner turn end");
        await effect.BeforeSideTurnStart(null!, CombatSide.Player, [Hero], Combat);
        await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(!phantom.Powers.Contains(effect), "phantom penalty uses its summoner participating own turn end");
        Reset(); Raw<ArtifactPower>(Enemy, 1); Raw<HbrEnemyChargePower>(Enemy, 1); await Penalty(Enemy);
        Check(Down(Enemy).Length == 0 && !Enemy.HasPower<ArtifactPower>() && !Enemy.HasPower<HbrEnemyChargePower>(), "fixed penalty retains actual Artifact block and consumption while charge still clears");
        Reset(); await Penalty(Enemy); await Penalty(Enemy); await Penalty(Enemy);
        Check(Down(Enemy).Length == 2 && Down(Enemy).All(p => Percent(p) == 30m), "repeat real breaks use ordinary independent two-slot policy");
        Reset(); await Penalty(Hero); effect = Down(Hero).Single();
        Fixture("InjectSavedCache", typeof(HbrAttackDownPower));
        var opts = new JsonSerializerOptions { IncludeFields = true };
        var payload = JsonSerializer.Serialize(SavedProperties.From(effect), opts);
        var copy = Raw<HbrAttackDownPower>(Hero, 1); JsonSerializer.Deserialize<SavedProperties>(payload, opts)!.Fill(copy);
        Check((bool)On(copy, "get_HbrWaitForOwnerTurnStart")! && copy.HbrExitOverride == effect.HbrExitOverride && Percent(copy) == 30m,
            "native SavedProperties JSON restores fixed potency, appended owner clock and wait gate");
    }
    static async Task NararaLayers()
    {
        foreach (bool direct in new[] { false, true })
        {
            Reset(); var dog = Monster<NararaShibaBossMonster>();
            typeof(NararaShibaBossMonster).GetProperty("ExactDpLayerMaximum", F)!.SetValue(dog.Monster, 100m);
            typeof(NararaShibaBossMonster).GetProperty("DpLayersRemaining", F)!.SetValue(dog.Monster, 3);
            var shell = await Shell(dog, typeof(NararaLayeredDpPower)); Raw<HbrEnemyChargePower>(dog, 1);
            if (direct) await (Task)On(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0)!;
            else await Hit(shell, 100m);
            Check(shell.CurrentDp == 100m && Down(dog).Length == 1 && !dog.HasPower<HbrEnemyChargePower>(),
                "Narara intermediate layer refill still applies one penalty via " + (direct ? "explicit" : "native damage"));
            if (direct) await (Task)On(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0)!;
            else await Hit(shell, 1000m);
            Check(shell.CurrentDp == 1m && Down(dog).Length == 1,
                "Narara protected last1DP creates no false layer break via " + (direct ? "explicit" : "native damage"));
            Reset(); dog = Monster<NararaShibaBossMonster>();
            typeof(NararaShibaBossMonster).GetProperty("ExactDpLayerMaximum", F)!.SetValue(dog.Monster, 100m);
            typeof(NararaShibaBossMonster).GetProperty("DpLayersRemaining", F)!.SetValue(dog.Monster, 1);
            typeof(NararaShibaBossMonster).GetProperty("LastDpTurnEndTriggered", F)!.SetValue(dog.Monster, true);
            shell = await Shell(dog, typeof(NararaLayeredDpPower)); Raw<HbrEnemyChargePower>(dog, 1);
            if (direct) await (Task)On(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0)!;
            else await Hit(shell, 100m);
            Check(shell.CurrentDp == 0m && Down(dog).Length == 1 && !dog.HasPower<HbrEnemyChargePower>() && !dog.Powers.Contains(shell),
                "Narara final layer applies exactly one penalty via " + (direct ? "explicit" : "native damage"));
        }
    }
    public static void Run(string path)
    {
        bool ok = false; string? error = null;
        try
        {
            Fixture("Setup");
            foreach (Type type in new[] { typeof(PhantomPower), typeof(PhantomHighDimensionalShellPower), typeof(NararaLayeredDpPower) })
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            OrdinaryAndDirect().GetAwaiter().GetResult(); PlayersAndCosts().GetAwaiter().GetResult();
            ClocksAndHooks().GetAwaiter().GetResult(); NararaLayers().GetAwaiter().GetResult(); ok = true;
        }
        catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(ex); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            nativeSourceSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            telemetryIsolation = Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary = "Real native PowerCmd/Artifact/SavedProperties and production DP mutation/callbacks. Shared fixture isolates native animation waits/UI only; no Godot or in-game full turn loop." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " DP break checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
