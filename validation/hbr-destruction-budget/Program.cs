using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class DestructionBudgetChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const decimal DefaultSkillCoefficient = 3m;
    static readonly Assembly Mod = typeof(DestructionRatePower).Assembly;
    static readonly List<string> Passed = [];
    static Type Fixture => typeof(Checks);
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static CombatState Combat => State<CombatState>("Combat");
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string name, params object?[] args) => Invoke(Fixture.GetMethod(name, F)!, null, args);
    static object? Call(string helper, string name, params object?[] args)
    {
        MethodInfo method = Helper(helper).GetMethods(F).Single(method => method.Name == name && method.GetParameters().Length == args.Length);
        return Invoke(method, null, args);
    }
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static T RawPower<T>(Creature target, int amount = 1) where T : PowerModel =>
        (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, target, amount)!;
    static CardModel Card(Type type, int hits)
    {
        var card = (CardModel)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(type), null)!;
        if (card is DragonArrowHbrgoSaki)
            Call("DualSwordsCopyHelper", "RecordXEnergySpent", card, hits);
        else if (card is MagicSwordLuckySevenHbrgoSaki lucky)
            lucky.SetMagicSwordModeHitCount(hits);
        else card.DynamicVars.Repeat.BaseValue = hits;
        return card;
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static void Equal(decimal actual, decimal expected, string name) => Check(Math.Abs(actual - expected) < .000000000000000000000001m, $"{name}: {actual} = {expected}");
    static void Reset()
    {
        FixtureCall("Reset"); Set(Combat, "_encounter", null);
    }
    static void Room(string room)
    {
        string? name = room switch { "elite" => "BygoneEffigyElite", "boss" => "CeremonialBeastBoss", _ => null };
        object? encounter = name == null ? null : RuntimeHelpers.GetUninitializedObject(typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Models.Encounters." + name, true)!);
        if (encounter != null) GC.SuppressFinalize(encounter);
        Set(Combat, "_encounter", encounter);
    }
    static Creature Victim(string room)
    {
        Room(room);
        return room == "flat" ? (Creature)FixtureCall("Actor", Blank<FlatHand>(), null, null)! : Enemy;
    }
    static AttackCommand Command(CardModel card)
    {
        var command = new AttackCommand(1m);
        MethodInfo fromCard = typeof(AttackCommand).GetMethods(F).Single(method => method.Name == "FromCard");
        Invoke(fromCard, command, fromCard.GetParameters().Length == 1 ? [card] : [card, null]);
        return command.Targeting(Enemy);
    }
    static int Combo(string combo, int hits)
    {
        switch (combo)
        {
            case "small": PowerCmd.Apply<HbrComboSmallPower>(null!, Hero, 5, Hero, null, true).GetAwaiter().GetResult(); return hits + 1;
            case "large": PowerCmd.Apply<HbrComboLargePower>(null!, Hero, 1, Hero, null, true).GetAwaiter().GetResult(); return hits * 2;
            case "all":
                PowerCmd.Apply<HbrComboSmallPower>(null!, Hero, 5, Hero, null, true).GetAwaiter().GetResult();
                PowerCmd.Apply<HbrComboMediumPower>(null!, Hero, 3, Hero, null, true).GetAwaiter().GetResult();
                PowerCmd.Apply<HbrComboLargePower>(null!, Hero, 1, Hero, null, true).GetAwaiter().GetResult();
                return (hits + 4) * 2;
            default: return hits;
        }
    }
    sealed class Skill : IDisposable
    {
        readonly object _cardScope, _attackScope;
        internal readonly AttackCommand Command;
        internal readonly int Hits;
        internal Skill(CardModel card, int baseHits, int expectedHits)
        {
            _cardScope = Call("HighHitDamageVisualHelper", "BeginCardBatch", card)!;
            Command = DestructionBudgetChecks.Command(card);
            _attackScope = Call("HighHitDamageVisualHelper", "BeginAttack", Command)!;
            Hook.BeforeAttack(Combat, Command).GetAwaiter().GetResult();
            Hits = (int)(decimal)Call("HighHitDamageVisualHelper", "ResolveHitCount", Combat, Command, baseHits)!;
            Equal(Hits, expectedHits, card.GetType().Name + " actual native combo resolves final hit count");
            object destruction = _attackScope.GetType().GetField("DestructionSkill", F)!.GetValue(_attackScope)!;
            Equal((decimal)destruction.GetType().GetField("Coefficient", F)!.GetValue(destruction)!, DefaultSkillCoefficient,
                card.GetType().Name + " one multi-hit Execute owns the full skill coefficient");
            Equal((decimal)destruction.GetType().GetField("TotalHits", F)!.GetValue(destruction)!, expectedHits,
                card.GetType().Name + " skill budget divides only by the final resolved native hits");
        }
        public void Dispose()
        {
            try { Call("HighHitDamageVisualHelper", "CloseAttack", _attackScope); }
            finally { Call("HighHitDamageVisualHelper", "CloseCard", _cardScope); }
        }
    }
    static decimal HitGain(Creature target, CardModel card, bool claim = true)
    {
        using var hit = (IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, card, ValueProp.Move)!;
        Call("HbrDestructionGainHelper", "ObserveHit", target);
        return claim ? (decimal)Call("HbrDestructionGainHelper", "GetGain", target, Hero, card, ValueProp.Move)! : 0m;
    }
    static void BudgetCases()
    {
        foreach ((Type type, int baseHits) in new[] { (typeof(DragonArrowHbrgoSaki), 3), (typeof(BladeDanceHbrgoSaki), 4), (typeof(MagicSwordLuckySevenHbrgoSaki), 7) })
        foreach (string combo in new[] { "none", "small", "large", "all" })
        foreach ((string room, int coefficient) in new[] { ("normal", 2), ("elite", 5), ("boss", 5), ("flat", 6) })
        {
            Reset(); Creature target = Victim(room); CardModel card = Card(type, baseHits); int hits = Combo(combo, baseHits);
            using var skill = new Skill(card, baseHits, hits);
            decimal gain = 0;
            for (int index = 0; index < skill.Hits; index++) gain += HitGain(target, card);
            Equal(gain, coefficient * DefaultSkillCoefficient, $"{type.Name} {combo} {room}: full HP skill totals enemy coefficient times default skill coefficient regardless of hit count");
            Equal(HitGain(target, card), 0, $"{type.Name} {combo} {room}: extra claim beyond final hit cannot exceed one skill budget");
        }
        Reset();
    }
    static decimal Fraction(DestructionRatePower rate) => decimal.Parse(string.IsNullOrEmpty(rate.HbrFractionalGainData) ? "0" : rate.HbrFractionalGainData, CultureInfo.InvariantCulture);
    static void ActualReceiverCases()
    {
        foreach (Type type in new[] { typeof(DragonArrowHbrgoSaki), typeof(BladeDanceHbrgoSaki), typeof(MagicSwordLuckySevenHbrgoSaki) })
        foreach ((string room, int coefficient) in new[] { ("normal", 2), ("elite", 5), ("boss", 5), ("flat", 6) })
        {
            Reset(); Creature target = Victim(room); CardModel card = Card(type, 4);
            var rate = RawPower<DestructionRatePower>(target, DestructionRatePower.BaselineAmount);
            using var skill = new Skill(card, 4, 4);
            for (int hitIndex = 0; hitIndex < 4; hitIndex++)
            {
                if (hitIndex == 1) RawPower<BreakPower>(target);
                using var hit = (IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, card, ValueProp.Move)!;
                var result = new DamageResult(target, ValueProp.Move)
                { BlockedDamage = hitIndex == 1 ? 1000 : 0, UnblockedDamage = hitIndex == 1 ? 0 : 1000 };
                rate.AfterDamageReceived(null!, target, result, ValueProp.Move, Hero, card).GetAwaiter().GetResult();
                decimal value = rate.Amount - DestructionRatePower.BaselineAmount + Fraction(rate);
                decimal expected = coefficient * DefaultSkillCoefficient * Math.Max(0, hitIndex - 1) / 4m;
                Equal(value, expected, $"{type.Name} {room}: DP hit then fully blocked hit consume shares without growing destruction; hit{hitIndex + 1}");
                rate.AfterDamageReceived(null!, target, result, ValueProp.Move, Hero, card).GetAwaiter().GetResult();
                Equal(rate.Amount - DestructionRatePower.BaselineAmount + Fraction(rate), expected,
                    $"{type.Name} {room}: repeated receive callback cannot multiply coefficient or reclaim share; hit{hitIndex + 1}");
            }
            Equal(rate.Amount - DestructionRatePower.BaselineAmount + Fraction(rate), coefficient * DefaultSkillCoefficient / 2m,
                $"{type.Name} {room}: only the last two HP hits earn half of the complete skill budget");
        }
        Reset();
    }
    static void DuplicateClaimCases()
    {
        Reset(); CardModel card = Card(typeof(BladeDanceHbrgoSaki), 4);
        using var skill = new Skill(card, 4, 4);
        using var hit = (IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, card, ValueProp.Move)!;
        Call("HbrDestructionGainHelper", "ObserveHit", Enemy);
        Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, Hero, card, ValueProp.Move)!, 1.5m, "one hit claims its original quarter share from skill coefficient3");
        Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, Hero, card, ValueProp.Move)!, 0m, "repeated receive paths cannot claim the same hit twice");
    }
    static void FallbackAndExplicitCases()
    {
        foreach ((string room, int coefficient) in new[] { ("normal", 2), ("elite", 5), ("boss", 5), ("flat", 6) })
        {
            Reset(); Creature target = Victim(room); CardModel card = Card(typeof(BladeDanceHbrgoSaki), 4);
            Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", target, Hero, card, ValueProp.Move)!, coefficient * DefaultSkillCoefficient,
                room + ": no skill or hit scope uses the default skill coefficient3");
            using var hit = (IDisposable)Call("HbrDestructionGainHelper", "BeginHit", Hero, card, ValueProp.Move)!;
            Call("HbrDestructionGainHelper", "ObserveHit", target);
            Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", target, Hero, card, ValueProp.Move)!, coefficient * DefaultSkillCoefficient,
                room + ": hit scope without skill scope claims coefficient3 once");
            Equal((decimal)Call("HbrDestructionGainHelper", "GetGain", target, Hero, card, ValueProp.Move)!, 0m,
                room + ": fallback hit scope cannot claim coefficient3 twice");
        }
        Reset(); CardModel explicitCard = Card(typeof(BladeDanceHbrgoSaki), 4);
        using var explicitSkill = (IDisposable)Call("HbrDestructionGainHelper", "BeginSkill", Hero, explicitCard, 4m, .5m)!;
        Equal((decimal)explicitSkill.GetType().GetField("Coefficient", F)!.GetValue(explicitSkill)!, .5m,
            "explicit BeginSkill coefficient0point5 remains exactly0point5");
        decimal gain = 0;
        for (int index = 0; index < 4; index++) gain += HitGain(Enemy, explicitCard);
        Equal(gain, 1m, "four normal-enemy HP shares with explicit coefficient0point5 total1 without another default factor3");
    }
    static void Install()
    {
        FixtureCall("Setup");
        foreach (Type type in new[] { typeof(HbrComboSmallPower), typeof(HbrComboMediumPower), typeof(HbrComboLargePower) })
        {
            ModelDb.Inject(type); Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        Reset();
    }
    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try { Install(); BudgetCases(); ActualReceiverCases(); DuplicateClaimCases(); FallbackAndExplicitCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual HighHitDamageVisualHelper card and attack scopes, native BeforeAttack/ModifyAttackHitCount via ResolveHitCount/SetResolvedHitCount, actual per-hit HbrDestructionGainHelper and DestructionRatePower.AfterDamageReceived. Shared fixture substitutes listener enumeration/logging and builds the combat world without Godot. No complete damage execution, scene, complete card OnPlay or multiplayer session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " destruction-budget checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
