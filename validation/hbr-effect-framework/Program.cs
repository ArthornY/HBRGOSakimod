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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class FrameworkChecks
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
    static object? Call(string helper, string name, params object?[] args) => Invoke(Helper(helper).GetMethod(name, F)!, null, args);
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
    static void Setup()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.effect.framework.checks");
        foreach (Type type in new[] { typeof(HbrAttackUpPower), typeof(HbrDefenseUpPower), typeof(HbrSpiritDownPower), typeof(HbrWisdomDownPower),
            typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower) })
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrEffectApplicationPatch", true)!).Patch().Count > 0,
            "installs production generic native application adapter");
    }
    static void FormulaCases()
    {
        Equal((decimal)Call("HbrOriginalNumericFormula", "EvaluateLinearBuffPotency", 30m, 100m, 100m)!, 30m, "reference INT preserves authored buff base");
        Equal((decimal)Call("HbrOriginalNumericFormula", "EvaluateLinearBuffPotency", 30m, 200m, 100m)!, 30.6m, "buff scales above reference without upper threshold");
        Equal((decimal)Call("HbrOriginalNumericFormula", "EvaluateLinearBuffPotency", 30m, -5100m, 100m)!, -1.2m, "buff formula has no lower clamp");
        Equal((decimal)Call("HbrOriginalNumericFormula", "EvaluateLinearDebuffPotency", 15m, 100m, 150m)!, 14.25m, "debuff negative attribute difference lowers potency");
        Equal((decimal)Call("HbrOriginalNumericFormula", "EvaluateLinearDebuffPotency", 15m, 100m, 1300m)!, -3m, "debuff formula preserves signed negative result");
        Equal((decimal)Call("HbrFragilePowerHelper", "EvaluatePercent", 1m, 100m, 100m, 150m)!, 28.5m, "Fragile uses original INT1 LUCK2 weighted linear formula");
        Equal((decimal)Call("HbrPercentDebuffHelper", "EvaluatePercent", 1m, 100m, 100m, 150m)!, 14.25m, "attack and defense down share INT2 LUCK1 linear formula");
    }
    static void SlotCases()
    {
        Reset(); Apply<HbrAttackUpPower>(30m); Apply<HbrAttackUpPower>(30m);
        Check(Existing<HbrAttackUpPower>().Length == 2 && Existing<HbrAttackUpPower>().All(power => power.Amount == 1), "equal-potency acquisitions remain two independent one-use instances");
        Apply<HbrAttackUpPower>(20m);
        Check(Existing<HbrAttackUpPower>().Length == 2 && Existing<HbrAttackUpPower>().All(power => Potency(power) == 30m), "weaker third instance is ignored");
        Apply<HbrAttackUpPower>(40m);
        Check(Existing<HbrAttackUpPower>().Select(Potency).SequenceEqual(new[] { 30m, 40m }), "stronger third instance replaces weakest without merging durations");
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3); Apply<HbrAttackUpPower>(30m, remaining: 1);
        var longer = Existing<HbrAttackUpPower>()[0]; Apply<HbrAttackUpPower>(30m, remaining: 7);
        Check(Existing<HbrAttackUpPower>().Contains(longer) && Existing<HbrAttackUpPower>().Select(power => power.Amount).SequenceEqual(new[] { 3, 7 }), "equal strength replaces least remaining and resets to new duration");
        Reset(); object only = Profile("Only", "PlayerTurnEnd", 3);
        Apply<HbrAttackUpPower>(30m, profile: only); Apply<HbrAttackUpPower>(30m, remaining: 4, profile: only); Apply<HbrAttackUpPower>(30m, remaining: 5, profile: only);
        Check(Existing<HbrAttackUpPower>().Length == 2 && Existing<HbrAttackUpPower>().Select(power => power.Amount).SequenceEqual(new[] { 4, 5 }), "Only explicitly falls back to Default two-instance replacement");
        Reset(); for (int i = 0; i < 4; i++) Apply<HbrSpiritDownPower>(50m, Enemy);
        Check(Existing<HbrSpiritDownPower>(Enemy).Length == 4, "original None profile admits more than two independent Spirit Down instances");
        Equal(Existing<HbrSpiritDownPower>(Enemy).Select(Potency).Sum(), 190m, "Spirit Down each computes its own INT-based snapshot");
        Equal((decimal)Invoke(typeof(HbrSpiritDownPower).GetMethod("GetSpiritFactor", F)!, null, Enemy)!, -.9m,
            "unlimited Spirit Down instances aggregate without potency floor");
        Reset(); FixtureCall("Act", 20); Apply<HbrSpiritDownPower>(50m, Enemy);
        Equal(Potency(Existing<HbrSpiritDownPower>(Enemy).Single()), -12.5m,
            "native effect storage preserves negative potency after an unbounded attribute difference");
        Reset(); PowerCmd.Apply<HbrSpiritDownPower>(null!, Enemy, 0m, Hero, null, true).GetAwaiter().GetResult();
        Check(Existing<HbrSpiritDownPower>(Enemy).Length == 0, "zero native duration cannot become a fresh full-potency stat debuff");
        Reset(); PowerCmd.Apply<HbrDefenseUpPower>(null!, Hero, 30m, Hero, null, true).GetAwaiter().GetResult();
        var native = Existing<HbrDefenseUpPower>().Single();
        Equal(Potency(native), 30m, "native PowerCmd application routes authored percentage through common resolver");
        Equal(native.Amount, 1m, "native power Amount stores original modal one-turn duration independently of authored potency");
    }
    static void LifecycleCases()
    {
        Reset(); Apply<HbrAttackUpPower>(30m, profile: Profile("Default", "PlayerTurnEnd", 3)); var buff = Existing<HbrAttackUpPower>().Single();
        Tick(buff, CombatSide.Enemy, Enemy); Equal(buff.Amount, 3m, "PlayerTurnEnd ignores enemy side");
        Tick(buff, CombatSide.Player, Enemy); Equal(buff.Amount, 3m, "another personal-turn participant cannot age holder's buff");
        Tick(buff, CombatSide.Player, Hero); Equal(buff.Amount, 2m, "holder's player side end ages PlayerTurnEnd once");
        Reset(); Apply<HbrDefenseUpPower>(30m, remaining: 2); var defense = Existing<HbrDefenseUpPower>().Single();
        Tick(defense, CombatSide.Player, Hero); Equal(defense.Amount, 2m, "EnemyTurnEnd does not mean holder side end");
        Tick(defense, CombatSide.Enemy, Enemy); Equal(defense.Amount, 1m, "EnemyTurnEnd ages allied defensive buff at enemy side end");
        Reset(); Apply<HbrAttackUpPower>(30m, profile: Profile("None", "Eternal", 1)); var eternal = Existing<HbrAttackUpPower>().Single();
        Tick(eternal, CombatSide.Player, Hero); Tick(eternal, CombatSide.Enemy, Enemy);
        var attack = Attack(); Begin(attack); Hit(attack); End(attack);
        Equal(eternal.Amount, 1m, "Eternal neither ages at turn ends nor consumes attacks");
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3); var counted = Existing<HbrAttackUpPower>().Single();
        var empty = Attack(); Begin(empty); End(empty); Equal(counted.Amount, 3m, "Count with no actual hit is retained");
        attack = Attack(); Begin(attack); Hit(attack); Hit(attack); End(attack);
        Equal(counted.Amount, 2m, "Count spends once per complete multi-hit attack");
        End(attack); Equal(counted.Amount, 2m, "Count completion is idempotent");
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3); counted = Existing<HbrAttackUpPower>().Single();
        CardModel card = (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(HelloWorldHbrgoSaki)), null)!;
        object direct = Call("HbrComboHelper", "BeginCardAttack", card)!;
        try
        {
            for (int i = 0; i < 2; i++) { attack = Attack(); Begin(attack); Hit(attack); End(attack); }
            Equal(counted.Amount, 3m, "direct loop's native children cannot consume reserved Count early");
            On(direct, "MarkHit"); ((Task)On(direct, "Complete", (object?)null)!).GetAwaiter().GetResult();
            Equal(counted.Amount, 2m, "direct card scope consumes Count once after its entire loop");
        }
        finally { ((IDisposable)direct).Dispose(); }
    }
    static void SaveAndDoubleCases()
    {
        Reset(); var old = Raw<HbrAttackUpPower>(Hero, 30);
        Equal(Potency(old), 30m, "old saved Amount reads as potency before migration");
        Equal(Remaining(old), 2m, "old saved RemainingTurns reads as duration without preview mutation");
        Check(old.Amount == 30 && string.IsNullOrEmpty(old.HbrPotencyData), "reading old saved shape does not mutate or materialize state");
        Tick(old, CombatSide.Enemy, Enemy);
        Equal(Potency(old), 30m, "old buff snapshot migrates before lifecycle countdown");
        Equal(old.Amount, 1m, "old 30-percent buff does not accidentally last thirty turns");
        Reset(); var wisdom = Raw<HbrWisdomDownPower>(Hero, 3);
        Equal(Potency(wisdom), 80m, "old duration-only Wisdom save restores original fixed80percent potency");
        Tick(wisdom, CombatSide.Enemy, Enemy); Equal(wisdom.Amount, 2m, "old Wisdom keeps saved three-turn duration");
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3, profile: Profile("Only", "PlayerTurnEnd", 3)); var original = Existing<HbrAttackUpPower>().Single();
        var clone = (HbrAttackUpPower)original.ClonePreservingMutability();
        Check(clone.HbrPotencyData == original.HbrPotencyData && clone.HbrExitOverride == original.HbrExitOverride && clone.HbrLimitOverride == original.HbrLimitOverride,
            "native mutable clone preserves primitive saved potency and profile overrides");
        var props = typeof(HbrEffectPower).GetProperties(F).Where(property => property.DeclaringType == typeof(HbrEffectPower) &&
            property.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "SavedPropertyAttribute")).ToArray();
        var restored = Raw<HbrAttackUpPower>(Hero, original.Amount);
        foreach (var property in props) property.SetValue(restored, property.GetValue(original));
        Equal(Potency(restored), 30m, "saved primitive property roundtrip restores frozen potency");
        Tick(restored, CombatSide.Enemy, Enemy); Equal(restored.Amount, 3m, "saved PlayerTurnEnd override survives restoration");
        ((Task)On(original, "DoubleExisting", null, 30m, 3, Hero, null)!).GetAwaiter().GetResult();
        Equal(Potency(original), 60m, "Double transforms snapshotted potency without reapplying attributes");
        Equal(original.Amount, 6m, "Double independently doubles remaining duration");
    }
    static void EffectCommandCountCases()
    {
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3); var power = Existing<HbrAttackUpPower>().Single();
        object BeginCommand()
        {
            IEnumerable<Creature> targets = new[] { Enemy };
            return Call("HbrEffectSkillDamageContext", "BeginCommand", targets, 1m, ValueProp.Unpowered, Hero, null)!;
        }
        object BeginIntent() => Call("HbrEffectSkillDamageContext", "Begin", Enemy, Hero, null, 1m, ValueProp.Unpowered, Hero)!;
        using ((IDisposable)BeginIntent())
        {
            object command = BeginCommand();
            ((Task<IEnumerable<DamageResult>>)Call("HbrEffectSkillDamageContext", "Finish",
                Task.FromResult<IEnumerable<DamageResult>>(new[] { new DamageResult(Enemy, ValueProp.Unpowered) }), command)!).GetAwaiter().GetResult();
        }
        Equal(power.Amount, 2m, "formula-based non-AttackCommand damage consumes one Count after successful complete command");
        using ((IDisposable)BeginIntent())
        {
            object command = BeginCommand();
            ((Task<IEnumerable<DamageResult>>)Call("HbrEffectSkillDamageContext", "Finish",
                Task.FromResult<IEnumerable<DamageResult>>(Array.Empty<DamageResult>()), command)!).GetAwaiter().GetResult();
        }
        Equal(power.Amount, 2m, "empty effect command preserves reserved Count");
        using ((IDisposable)BeginIntent())
        {
            object command = BeginCommand(); var failure = new InvalidOperationException("fixture effect failure");
            try
            {
                ((Task<IEnumerable<DamageResult>>)Call("HbrEffectSkillDamageContext", "Finish",
                    Task.FromException<IEnumerable<DamageResult>>(failure), command)!).GetAwaiter().GetResult();
                throw new Exception("effect failure did not propagate");
            }
            catch (InvalidOperationException actual) { Check(ReferenceEquals(actual, failure), "failed effect retains original exception"); }
        }
        Equal(power.Amount, 2m, "failed effect command releases Count without spending");
        using ((IDisposable)BeginIntent())
        {
            object command = BeginCommand();
            ((Task<IEnumerable<DamageResult>>)Call("HbrEffectSkillDamageContext", "Finish",
                Task.FromResult<IEnumerable<DamageResult>>(new[] { new DamageResult(Enemy, ValueProp.Unpowered), new DamageResult(Hero, ValueProp.Unpowered) }), command)!).GetAwaiter().GetResult();
        }
        Equal(power.Amount, 1m, "next multi-target effect can claim released Count and spends only once");
    }
    static CardModel OwnedCard(Type type) => (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(type), null)!;
    static void SourceProfileCases()
    {
        Reset(); CardModel ice = OwnedCard(typeof(MimicIceFlowerThousandArrowsHbrgoSaki));
        PowerCmd.Apply<HbrDefenseDownPower>(null!, Enemy, 3m, Hero, ice, true).GetAwaiter().GetResult();
        HbrDefenseDownPower eternal = Existing<HbrDefenseDownPower>(Enemy).Single();
        Check(eternal.HbrExitOverride == 4 && eternal.Amount == 1, "verified Ice Flower DefenseDown snapshots original Eternal exit");
        Tick(eternal, CombatSide.Enemy, Enemy); Tick(eternal, CombatSide.Player, Hero);
        Equal(eternal.Amount, 1m, "Ice Flower Eternal DefenseDown survives both side ends");
        PowerCmd.Apply<FragilePower>(null!, Enemy, 3m, Hero, ice, true).GetAwaiter().GetResult();
        var fragile = Existing<FragilePower>(Enemy).Single();
        Equal(fragile.Amount, 1m, "mod-authored Ice Flower Fragile uses generic original one-turn profile instead of unmatched DefenseDown part");
        Tick(fragile, CombatSide.Enemy, Enemy);
        Check(!Powers(Enemy).Contains(fragile), "generic Fragile expires after its original one enemy-side end");
        ((Task)Call("HbrPercentDebuffApplication", "Copy", null, eternal, Hero, null, true)!).GetAwaiter().GetResult();
        var copies = Existing<HbrDefenseDownPower>(Enemy);
        Check(copies.Length == 2 && copies.All(power => power.HbrExitOverride == 4), "copied debuff preserves frozen Eternal profile instead of using generic duration");
        var cloned = (HbrDefenseDownPower)eternal.ClonePreservingMutability();
        Check(cloned.HbrExitOverride == eternal.HbrExitOverride && cloned.HbrLimitOverride == eternal.HbrLimitOverride &&
            cloned.HbrDefaultRemainingOverride == eternal.HbrDefaultRemainingOverride,
            "native clone preserves percent-debuff saved exit, limit and initial-duration provenance");
        object profile = Call("HbrEffectProfileResolver", "Resolve", ModelDb.Power<HbrDefenseDownPower>(), ice)!;
        Check((string)Call("HbrEffectProfileResolver", "Duration", profile, null)! == "本场战斗",
            "source-specific duration presentation names Eternal correctly");
        profile = Call("HbrEffectProfileResolver", "Resolve", ModelDb.Power<FragilePower>(), ice)!;
        Check((string)Call("HbrEffectProfileResolver", "Duration", profile, null)! == "1回合",
            "same card's separate Fragile duration presentation stays one turn");
        Reset();
    }
    static void EnemyAdapterCases()
    {
        Reset(); Apply<HbrWisdomDownPower>(80m, Enemy);
        var wisdom = Existing<HbrWisdomDownPower>(Enemy).Single();
        Equal(wisdom.ModifyBlockMultiplicative(Enemy, 100m, ValueProp.Move, null!, null!), .2m,
            "HBR enemy Wisdom Down80 means actual block factor20percent rather than old fixed75percent");
        object intelligence = Helper("HbrEffectFormula").GetField("Intelligence", F)!.GetValue(null)!;
        Equal((decimal)Call("HbrEffectFormula", "Buff", 30m, Enemy, intelligence)!, 29.58m,
            "enemy INT-only buff view respects Wisdom Down without changing its underlying E");
        object mixed = Helper("HbrEffectFormula").GetField("IntelligenceLuck", F)!.GetValue(null)!;
        Equal((decimal)Call("HbrEffectFormula", "Debuff", 15m, Enemy, Hero, mixed)!, 13.8m,
            "enemy mixed INT2 LUCK1 view reduces only INT share and leaves luck share at E");
        Equal((decimal)Call("HbrEnemyProfileHelper", "GetEnemyParameter", Enemy)!, 150m,
            "Wisdom Down does not lower global enemy E or physical attack reference");
        Apply<HbrWisdomDownPower>(80m, Enemy); var pair = Existing<HbrWisdomDownPower>(Enemy);
        Equal(pair[0].ModifyBlockMultiplicative(Enemy, 100m, ValueProp.Move, null!, null!), -.6m,
            "HBR enemy Wisdom sums independent instances without an effect floor");
        Equal(pair[1].ModifyBlockMultiplicative(Enemy, 100m, ValueProp.Move, null!, null!), 1m,
            "second Wisdom instance does not apply the aggregate block factor twice");
        Reset(); Apply<HbrSpiritDownPower>(50m, Enemy); var spirit = Existing<HbrSpiritDownPower>(Enemy).Single();
        Equal(spirit.ModifyDamageMultiplicative(Enemy, 100m, ValueProp.Move, Hero, null!), 1.2375m,
            "HBR enemy Spirit bridge uses actual resolved47.5percent potency rather than fixed25percent vulnerability");
        Apply<HbrSpiritDownPower>(50m, Enemy); var spirits = Existing<HbrSpiritDownPower>(Enemy);
        Equal(spirits[0].ModifyDamageMultiplicative(Enemy, 100m, ValueProp.Move, Hero, null!), 1.475m,
            "Spirit bridge adds all independent potency snapshots once");
        Equal(spirits[1].ModifyDamageMultiplicative(Enemy, 100m, ValueProp.Move, Hero, null!), 1m,
            "second Spirit instance does not apply the summed equivalent multiplier twice");
        FixtureCall("Mode", false, 1);
        wisdom = Raw<HbrWisdomDownPower>(Enemy, 3);
        Equal(wisdom.ModifyBlockMultiplicative(Enemy, 100m, ValueProp.Move, null!, null!), .75m,
            "legacy non-HBR Wisdom fallback retains old25percent block reduction");
        Equal(spirit.ModifyDamageMultiplicative(Enemy, 100m, ValueProp.Move, Hero, null!), 1.25m,
            "legacy non-HBR Spirit fallback retains old25percent received-damage increase");
        Reset();
    }
    static bool Noncritical(ref bool __result) { __result = false; return false; }
    static void DamageBridgeRegressionCases()
    {
        Reset(); Apply<HbrAttackUpPower>(30m, remaining: 3); var buff = Existing<HbrAttackUpPower>().Single();
        var harmony = new Harmony("hbr.effect.framework.declared-applier");
        MethodInfo critical = Helper("HbrEffectSkillDamageContext").GetMethod("IsCritical", F)!;
        harmony.Patch(critical, prefix: new HarmonyMethod(typeof(FrameworkChecks).GetMethod(nameof(Noncritical), F)!));
        const ValueProp dot = ValueProp.Unblockable | ValueProp.Unpowered;
        try
        {
            using var intent = (IDisposable)Call("HbrEffectSkillDamageContext", "Begin", Enemy, null, null, 2m, dot, Hero)!;
            IEnumerable<Creature> targets = new[] { Enemy };
            object command = Call("HbrEffectSkillDamageContext", "BeginCommand", targets, 2m, dot, null, null)!;
            decimal actual = (decimal)FixtureCall("Actual", Enemy, null, 2m, dot)!;
            decimal basis = (decimal)Call("HbrSkillPower", "EvaluateDamageFromUnits", 2m, 100m, 150m, false)!;
            Equal(actual, decimal.Floor(basis * 1.3m), "dealerless declared-applier skill receives applier's AttackUp in the real arithmetic pipeline");
            Equal(buff.Amount, 3m, "declared-applier modifier stays active until whole command completes");
            Check(intent.GetType().GetField("Dealer", F)!.GetValue(intent) == null,
                "arithmetic source bridge does not rewrite native dealer attribution");
            ((Task<IEnumerable<DamageResult>>)Call("HbrEffectSkillDamageContext", "Finish",
                Task.FromResult<IEnumerable<DamageResult>>(new[] { new DamageResult(Enemy, dot) }), command)!).GetAwaiter().GetResult();
            Equal(buff.Amount, 2m, "dealerless skill consumes exactly the AttackUp use that actually contributed");
        }
        finally { harmony.Unpatch(critical, HarmonyPatchType.All, harmony.Id); }
        Reset();
        using ((IDisposable)Call("HbrEffectSkillDamageContext", "BeginWorld", Hero, Enemy, null, 1000m, ValueProp.Unpowered, Enemy)!)
        {
            decimal actual = (decimal)FixtureCall("Actual", Hero, Enemy, 1000m, ValueProp.Unpowered)!;
            decimal expected = (decimal)Call("HbrSkillPower", "EvaluateIncomingDamageFromUnits", 10m, 150m, 100m, 100m)!;
            Equal(actual, decimal.Floor(expected), "enemy world reflection divides by incoming100 then re-expands once through incoming curve");
        }
        using ((IDisposable)Call("HbrEffectSkillDamageContext", "BeginWorld", Enemy, Hero, null, 1000m, ValueProp.Unpowered, Hero)!)
        {
            decimal actual = (decimal)FixtureCall("Actual", Enemy, Hero, 1000m, ValueProp.Unpowered)!;
            decimal expected = (decimal)Call("HbrSkillPower", "EvaluateDamageFromUnits", 1m, 100m, 150m, false)!;
            Equal(actual, decimal.Floor(expected), "player world reflection divides by outgoing1000 then re-expands once through outgoing curve");
        }
        using ((IDisposable)Call("HbrEffectSkillDamageContext", "BeginWorld", Hero, null, null, 1000m, ValueProp.Unpowered, null)!)
            Equal((decimal)FixtureCall("Actual", Hero, null, 1000m, ValueProp.Unpowered)!, 1000m,
                "environmental world amount remains in combat units without invented attributes");
        Reset();
    }
    public static void Run(string report)
    {
        bool ok = false; string? error = null;
        try { Setup(); FormulaCases(); SlotCases(); LifecycleCases(); SaveAndDoubleCases(); EffectCommandCountCases(); SourceProfileCases(); EnemyAdapterCases(); DamageBridgeRegressionCases(); BossDebuffCases(); NararaDispelCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            boundary = "Actual native power apply/receiving hooks, shared instance policy, fixed-side lifecycle, native attack hooks, direct-loop reservation, primitive saved-property restoration and native cloning. No Godot scene, full save-file load, complete multiplayer session or rendered UI." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " framework checks");
        if (!ok) throw new Exception("Inspect " + report);
    }
}
