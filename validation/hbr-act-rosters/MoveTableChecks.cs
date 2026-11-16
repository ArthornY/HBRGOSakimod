using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

// The thirty-five move-table monsters (docs/hbr-monster-mechanics-proposal.md,
// first batch 2026-11-07, second and third batches 2026-11-08; the seven original
// act-one monsters, fourth batch 2026-11-08): their dp/hp tables against
// the archived hbr.quest rows, the table walk (weights, once, condition,
// variants), DP Break and the returning shell, every move body for an HBR
// character, a legacy character and an original character, the new statuses'
// settlement in those three cases, the phantom side, the all-unit and
// lowest-DP target lists, and the block pass-through record.
static partial class ActRosterChecks
{
    const string TableState = "HBR_MOVE_TABLE";
    const string Archive = "D:/stsmod/outputs/monster-mechanics-hbrquest-20261107/enemies";

    static readonly Type[] TablePowerTypes =
    [
        typeof(HbrEnemyChargePower), typeof(HbrAttackUpPower), typeof(HbrDefenseUpPower),
        typeof(HbrSpiritDownPower), typeof(HbrImprisonPower), typeof(HbrBindPower), typeof(HbrStunPower),
        typeof(HbrContinuousDpDamagePower), typeof(HbrCalamityPower), typeof(HighDimensionalShellPower),
        typeof(BreakPower), typeof(DestructionRatePower), typeof(HbrIllusionPower), typeof(HbrConfusionPower),
        typeof(HbrVirusPower), typeof(HbrWisdomDownPower), typeof(HbrContinuousDamagePower),
        typeof(HbrProvokePower),typeof(ShrimpRunawayPower),typeof(PillarOverdriveDetectionPower),
        typeof(BarrelDemiseCountdownPower),typeof(DiamondDpCheckPower),typeof(FaintnessStormCountdownPower),
    ];

    static IEnumerable<Type> TableSavedTypes => TablePowerTypes.Where(t => t != typeof(DestructionRatePower))
        .Concat(TableVariants.Select(v => v.Type).Distinct());

    static bool IsTableMonster(Type type) => typeof(HbrgoMoveTableMonster).IsAssignableFrom(type);

    // Monster, its table variant, and the original enemy label that variant copies.
    static readonly (Type Type, int Variant, string Label)[] TableVariants =
    [
        (typeof(NeedleBird), 0, "NeedleBirdSmallVirtual_MC02"),
        (typeof(DiamondEyeball), 0, "DiamondEyeballDefaultVirtual_MC03_01"),
        (typeof(BigotryGate), 0, "BigotryGateSmall_01"),
        (typeof(BigotryGate), 1, "BigotryGateSmall_02"),
        (typeof(BigotryGate), 2, "BigotryGateDefault_01"),
        (typeof(ShrimpCrab), 0, "ShrimpCrabDefault_01"),
        (typeof(ShrimpCrab), 1, "ShrimpCrabDefault_02"),
        (typeof(PillarGuardian), 0, "PillarGuardianDefault"),
        // second batch
        (typeof(CrowHorse), 0, "CrowHorseDefaultVirtual"),
        (typeof(IvoryScooper), 0, "IvoryScooperDefaultVirtual"),
        (typeof(AbyssKnockerKiller), 0, "AbyssKnockerKillerVirtual_MC03"),
        (typeof(TargetWireFrameKiller), 0, "TargetWireFrameKiller_Day2E01"),
        (typeof(PileGolemEdge), 0, "PileGolemEdgeSmall"),
        (typeof(BarrelWalker), 0, "BarrelWalkerDefaultFire_MC04_02"),
        (typeof(SlimySerpent), 0, "SlimySerpentDefault"),
        (typeof(GiraffeHalo), 0, "GiraffeHaloDefault_01"),
        (typeof(ArachneLine), 0, "ArachneLineDefault"),
        // third batch
        (typeof(Marionette), 0, "MarionetteDefaultVirtual"),
        (typeof(Marionette), 1, "MarionetteSmallVirtual_first"),
        (typeof(MarionetteKiller), 0, "MarionetteKiller2"),
        (typeof(MarionetteKiller), 1, "MarionetteKillerCurse"),
        (typeof(CatHorn), 0, "CatHornDefaultVirtual"),
        (typeof(CatHorn), 1, "CatHornSmallVirtual_first"),
        (typeof(CatHornKiller), 0, "CatHornKillerVirtual"),
        (typeof(WireSlasher), 0, "WireSlasherDefaultDark"),
        (typeof(OctopusTail), 0, "OctopusTailDefaultVirtual"),
        (typeof(OctopusTailKiller), 0, "OctopusTailKillerVirtual"),
        (typeof(TargetWireFrame), 0, "TargetWireFrameSmallVirtual"),
        (typeof(CrestHopperKiller), 0, "CrestHopperKiller2"),
        (typeof(DarkKnockerKiller), 0, "DarkKnockerKillerVirtual"),
        (typeof(BalbFlea), 0, "BalbFleaDefault"),
        (typeof(TriClaw), 0, "TriClawSmall"),
        (typeof(TriClaw), 1, "TriClawDefault"),
        (typeof(CorollaKnife), 0, "CorollaKnifeSmall_01"),
        (typeof(CorollaKnife), 1, "CorollaKnifeSmall_02"),
        (typeof(MolArms), 0, "MolArmsSmall"),
        // fourth batch: the mod's original act-one monsters
        (typeof(SmallHopper), 0, "CrestHopperSmall"),
        (typeof(CrestHopper), 0, "CrestHopperDefaultVirtual"),
        (typeof(HealHopper), 0, "CrestHopperHealVirtual"),
        (typeof(HealHopper), 1, "CrestHopperHealVirtual_MC03"),
        (typeof(GoldHopper), 0, "CrestHopperDefaultGold01"),
        (typeof(Knocker), 0, "DarkKnockerSmallVirtual"),
        (typeof(ClayKnocker), 0, "DarkKnockerDefaultVirtual"),
        (typeof(AbyssKnocker), 0, "AbyssKnockerDefaultVirtual_MC02"),
    ];

    static readonly Type[] SecondBatch =
    [
        typeof(CrowHorse), typeof(IvoryScooper), typeof(AbyssKnockerKiller), typeof(TargetWireFrameKiller),
        typeof(PileGolemEdge), typeof(BarrelWalker), typeof(SlimySerpent), typeof(GiraffeHalo), typeof(ArachneLine),
    ];

    enum HeroKind { Hbr, Legacy, Original }

    // Pass-through simulated by the recorded attack leaf: the hit passed block
    // and this much reached DP or HP (once per attack).
    static bool SimulatePassThrough;
    const decimal SimulatedPassedDamage = 30m;
    static readonly List<(decimal Amount, Player Player)> EnergyLosses = [];
    // Single-target damage commands (a confused card or attack hitting its holder).
    static readonly List<(Creature Target, decimal Amount, ValueProp Props, Creature? Dealer)> SingleDamage = [];
    // Creatures that escaped (CreatureCmd.Escape is recorded instead of run).
    static readonly List<Creature> Escapes = [];
    static DeflectorProtection? HeroDp;
    static Overclock? HeroOd;

    static Player HeroPlayer => State<Player>("Player");
    static object HeroManual => Fixture.GetField("Manual", F)!.GetValue(null)!;
    static Type Effects => ModType("HBRGOSakiMod.Core.Helpers.HbrEnemyEffects");
    static Type Tracker => ModType("HBRGOSakiMod.Core.Helpers.HbrEnemyHitTracker");

    static object? Prop(object owner, string name)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetProperty(name, F | BindingFlags.DeclaredOnly) is { } property) return property.GetValue(owner);
        throw new MissingMemberException(owner.GetType().FullName, name);
    }

    static void SetProp(object owner, string name, object? value)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetProperty(name, F | BindingFlags.DeclaredOnly) is { } property) { property.SetValue(owner, value); return; }
        throw new MissingMemberException(owner.GetType().FullName, name);
    }

    static object? Call(object owner, string name, params object?[] args)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetMethods(F | BindingFlags.DeclaredOnly).FirstOrDefault(m => m.Name == name && !m.IsGenericMethod &&
                    m.GetParameters().Length == args.Length) is { } method)
                return Invoke(method, owner, args);
        throw new MissingMethodException(owner.GetType().FullName, name);
    }

    static List<object> Items(object? sequence) => ((System.Collections.IEnumerable)sequence!).Cast<object>().ToList();
    static void Await(object? task) => ((Task)task!).GetAwaiter().GetResult();

    static void Effect(string method, params object?[] args) =>
        Await(Invoke(Effects.GetMethods(F).Single(m => m.Name == method && !m.IsGenericMethod), null, args));
    static void Effect<T>(string method, params object?[] args) =>
        Await(Invoke(Effects.GetMethods(F).Single(m => m.Name == method && m.IsGenericMethod).MakeGenericMethod(typeof(T)), null, args));

    // ---- recorded leaves ------------------------------------------------------

    static bool EnergyLeaf(decimal amount, Player player, ref Task __result)
    { EnergyLosses.Add((amount, player)); __result = Task.CompletedTask; return false; }

    static void RecordPassThrough(Creature creature, decimal amount) =>
        Invoke(Tracker.GetMethod("RecordPassThrough", F)!, null, creature, amount, null);

    static bool SingleDamageLeaf(object?[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        SingleDamage.Add(((Creature)__args[1]!, (decimal)__args[2]!, (ValueProp)__args[3]!, (Creature?)__args[4]));
        __result = Task.FromResult<IEnumerable<DamageResult>>(Array.Empty<DamageResult>());
        return false;
    }

    static bool EscapeLeaf(object?[] __args, ref Task __result)
    { Escapes.Add((Creature)__args[0]!); __result = Task.CompletedTask; return false; }

    static void InstallTableLeaves(Harmony harmony)
    {
        harmony.Patch(typeof(PlayerCmd).GetMethod(nameof(PlayerCmd.LoseEnergy), [typeof(decimal), typeof(Player)])!,
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(EnergyLeaf), F)!));
        foreach (MethodInfo escape in typeof(CreatureCmd).GetMethods().Where(m => m.Name == nameof(CreatureCmd.Escape)))
            harmony.Patch(escape, prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(EscapeLeaf), F)!));
    }

    // ---- heroes ---------------------------------------------------------------

    // HBR and legacy heroes are mod characters (Electronic Soldier Manual and
    // Deflector Protection); the original character has neither relic.
    // overdrive >= 0 also gives a mod character an Overclock gauge with that
    // much charge (100 per level).
    static void UseHero(HeroKind kind, int dp = -1, int overdrive = -1)
    {
        Player player = HeroPlayer;
        List<RelicModel> relics = [];
        HeroDp = null;
        HeroOd = null;
        if (kind != HeroKind.Original)
        {
            relics.Add((RelicModel)HeroManual);
            HeroDp = Blank<DeflectorProtection>();
            Set(HeroDp, "<IsMutable>k__BackingField", true);
            Set(HeroDp, "_owner", player);
            relics.Add(HeroDp);
            if (overdrive >= 0)
            {
                HeroOd = Blank<Overclock>();
                Set(HeroOd, "<IsMutable>k__BackingField", true);
                Set(HeroOd, "_owner", player);
                Set(HeroOd, "_charge", overdrive);
                relics.Add(HeroOd);
            }
        }
        Set(player, "_relics", relics);
        Set(player, "<Character>k__BackingField", kind == HeroKind.Original ? Blank<Ironclad>() : Blank<HbrgoSaki>());
        FixtureCall("Mode", kind == HeroKind.Hbr, 1);
        if (HeroDp != null)
        {
            HeroDp.EnsureDpInitialized();
            if (dp >= 0) Set(HeroDp, "_dp", Math.Min(dp, HeroDp.MaxDp));
        }
    }

    static void RestoreHero()
    {
        Set(HeroPlayer, "_relics", new List<RelicModel> { (RelicModel)HeroManual });
        Set(HeroPlayer, "<Character>k__BackingField", Blank<HbrgoSaki>());
        HeroDp = null;
        HeroOd = null;
    }

    static IEnumerable<HeroKind> HeroKinds => [HeroKind.Hbr, HeroKind.Legacy, HeroKind.Original];

    static void ResetTable(HeroKind kind, int ascension, int dp = -1, int overdrive = -1)
    {
        Reset(kind == HeroKind.Hbr, ascension);
        // The native fixture reuses its CombatState. Room-specific break
        // cases must not leave Boss/Elite duration rules on later actors.
        Set(Combat, "_encounter", null);
        UseHero(kind, dp, overdrive);
        EnergyLosses.Clear();
        Escapes.Clear();
        SimulatePassThrough = false;
        KillHeroAfterAttack = false;
    }

    static MonsterModel TableActor(Type type, int variant, out Creature actor, int hp = 1_000_000)
    {
        actor = Actor(type, hp);
        MonsterModel monster = actor.Monster!;
        SetProp(monster, nameof(HbrgoMoveTableMonster.MoveTableVariant), variant);
        return monster;
    }

    static Creature PhantomActor(Type type)
    {
        var phantom = (Creature)FixtureCall("Actor", Blank(type), null, HeroPlayer)!;
        Set(phantom, "_maxHp", 1_000_000); Set(phantom, "_currentHp", 1_000_000);
        return phantom;
    }

    static MoveState MoveOf(MonsterModel monster, string id) =>
        (MoveState)Machine(monster).States[id];

    static void SetUp(MonsterModel monster, string? next = null)
    {
        if (monster.MoveStateMachine == null) monster.SetUpForCombat();
        if (next != null)
            monster.SetMoveImmediate((MoveState)monster.MoveStateMachine!.States[next], forceTransition: true);
    }

    // ---- snapshot ---------------------------------------------------------------

    sealed record SnapshotRow(int Order, int Weight, bool Once, string Condition, bool Preemptive, string Skill);
    sealed record SnapshotTable(string Label, SnapshotRow[] Rows);
    // Chances: each chance-based status part as (type, min, max); max 0 is a fixed chance.
    sealed record SnapshotSkill(string Label, string Target, int Hits, string Defense, string[] Types,
        (string Type, decimal Min, decimal Max)[] Chances);
    sealed record SnapshotEnemy(string Label, string Sha, decimal Dp, Dictionary<string, decimal> Heals,
        SnapshotSkill[] Skills, SnapshotTable DpTable, SnapshotTable HpTable);

    static Dictionary<string, SnapshotEnemy> LoadSnapshot()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,
            "validation/hbr-act-rosters/hbrquest-move-tables.json")));
        SnapshotTable Table(JsonElement table)
        {
            var rows = table.GetProperty("rows").EnumerateArray().Select(row =>
            {
                string[] skills = row.GetProperty("skills").EnumerateArray().Select(s => s.GetString()!).ToArray();
                Check(skills.Length == 1, $"snapshot row {row.GetProperty("order").GetInt32()} names one skill");
                return new SnapshotRow(row.GetProperty("order").GetInt32(), row.GetProperty("weight").GetInt32(),
                    row.GetProperty("once").GetBoolean(), row.GetProperty("condition").GetString()!,
                    row.GetProperty("preemptive").GetBoolean(), skills[0]);
            }).ToArray();
            return new SnapshotTable(table.GetProperty("label").GetString()!, rows);
        }
        var result = new Dictionary<string, SnapshotEnemy>();
        foreach (JsonElement enemy in document.RootElement.GetProperty("enemies").EnumerateArray())
        {
            var heals = enemy.GetProperty("healDp").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDecimal());
            var skills = enemy.GetProperty("skills").EnumerateArray().Select(s => new SnapshotSkill(
                s.GetProperty("label").GetString()!, s.GetProperty("target").GetString()!, s.GetProperty("hits").GetInt32(),
                s.GetProperty("defense").GetString()!, s.GetProperty("types").EnumerateArray().Select(t => t.GetString()!).ToArray(),
                s.GetProperty("chances").EnumerateArray().Select(c => (c[0].GetString()!, c[1].GetDecimal(), c[2].GetDecimal())).ToArray())).ToArray();
            result[enemy.GetProperty("label").GetString()!] = new SnapshotEnemy(enemy.GetProperty("label").GetString()!,
                enemy.GetProperty("responseSha256").GetString()!, enemy.GetProperty("dp").GetDecimal(), heals, skills,
                Table(enemy.GetProperty("dpTable")), Table(enemy.GetProperty("hpTable")));
        }
        return result;
    }

    // ---- data ---------------------------------------------------------------------

    static object Tables(MonsterModel monster, int variant) =>
        Invoke(monster.GetType().GetMethod("GetMoveTables", F)!, monster, variant)!;

    static List<object> TableMoves(MonsterModel monster) => Items(Prop(monster, "TableMoves"));

    static void CompareTable(object table, SnapshotTable expected, string label)
    {
        Check((string)Prop(table, "Label")! == expected.Label, $"{label} is table {expected.Label}");
        List<object> steps = Items(Prop(table, "Steps"));
        // OctopusTail's hp tables are numbered from 2 in the original; step N is
        // the Nth original order.
        int[] orders = expected.Rows.Select(r => r.Order).Distinct().OrderBy(o => o).ToArray();
        Check(orders.Length == 0 || orders.SequenceEqual(Enumerable.Range(orders[0], orders.Length)),
            $"{label} original orders {(orders.Length == 0 ? "are empty" : $"run {orders[0]}..{orders[^1]}")} without gaps");
        Equal(steps.Count, orders.Length, label + " step count");
        for (int index = 0; index < steps.Count; index++)
        {
            List<object> options = Items(Prop(steps[index], "Options"));
            SnapshotRow[] rows = expected.Rows.Where(r => r.Order == orders[index]).ToArray();
            bool same = options.Count == rows.Length && options.Zip(rows).All(pair =>
                (string)Prop(pair.First, "Skill")! == pair.Second.Skill &&
                (int)Prop(pair.First, "Weight")! == pair.Second.Weight &&
                (bool)Prop(pair.First, "Once")! == pair.Second.Once &&
                (string)Prop(pair.First, "Condition")! == pair.Second.Condition && !pair.Second.Preemptive);
            Check(same, $"{label} step {index + 1} = {string.Join(" / ", rows.Select(r =>
                $"{r.Skill}:{r.Weight}{(r.Once ? " once" : "")}{(r.Condition != "" ? " if " + r.Condition : "")}"))}");
        }
    }

    static readonly Dictionary<string, string> StatusTypes = new()
    {
        ["Charge"] = "Charge", ["DpBreak"] = "DpBreakRandom", ["Bind"] = "BindRandom", ["Stun"] = "StunRandom",
        ["Calamity"] = "Misfortune", ["SpiritDown"] = "SpiritDownRate", ["AttackUp"] = "AttackUp",
        ["DefenseUp"] = "DefenseUp", ["Imprison"] = "ImprisonRandom", ["ContinuousDp"] = "ContinuousDpDamageRate",
        ["Illusion"] = "IllusionRandom", ["Confusion"] = "ConfusionRandom", ["OdDown"] = "OverDrivePointDown",
        ["Absorb"] = "HealDpByDamage", ["Virus"] = "VirusRandom", ["WisdomDown"] = "WisdomDownRate",
        ["Escape"] = "Escape",
    };

    // Archived original chance metadata. Enemy applications now ignore it
    // and ignore block; phantom replay retains its prior passed-target rule.
    static readonly string[] ChanceStatuses = ["Bind", "Stun", "Illusion", "Confusion", "Imprison", "Virus"];

    static void TableDataCases(Dictionary<string, SnapshotEnemy> snapshot)
    {
        Check(snapshot.Count == TableVariants.Length && TableVariants.All(v => snapshot.ContainsKey(v.Label)),
            $"the snapshot holds the {TableVariants.Length} original variants");
        Check(TableSpecs.Select(m => m.Type).OrderBy(t => t.Name)
            .SequenceEqual(TableVariants.Select(v => v.Type).Distinct().OrderBy(t => t.Name)) &&
            TableSpecs.All(m => IsTableMonster(m.Type)),
            "all 28 new monsters and the 7 original act-one monsters use move tables");
        var usedByType = new Dictionary<Type, HashSet<string>>();
        foreach ((Type type, int variant, string enemyLabel) in TableVariants)
        {
            SnapshotEnemy original = snapshot[enemyLabel];
            MonsterModel monster = Mutable(type);
            string label = $"{type.Name} variant {variant}";
            object tables = Tables(monster, variant);
            Check((string)Prop(tables, "EnemyLabel")! == enemyLabel, $"{label} copies {enemyLabel}");
            CompareTable(Prop(tables, "Dp")!, original.DpTable, label + " dp");
            CompareTable(Prop(tables, "Hp")!, original.HpTable, label + " hp");
            string archived = Path.Combine(Archive, enemyLabel + ".json");
            if (File.Exists(archived))
                Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archived))) == original.Sha,
                    $"{enemyLabel} snapshot matches the archived response hash");

            List<object> moves = TableMoves(monster);
            HashSet<string> used = original.DpTable.Rows.Concat(original.HpTable.Rows).Select(r => r.Skill).ToHashSet();
            Check(used.IsSubsetOf(moves.Select(m => (string)Prop(m, "Skill")!)),
                $"{label} has a move for each skill its tables use ({used.Count})");
            if (!usedByType.TryGetValue(type, out HashSet<string>? all)) usedByType[type] = all = [];
            all.UnionWith(used);
            foreach (object move in moves)
            {
                string skill = (string)Prop(move, "Skill")!;
                if (original.Skills.SingleOrDefault(s => s.Label == skill) is not { } source) continue;
                Check(Prop(move, "Target")!.ToString() == source.Target,
                    $"{label} {skill} keeps the original target {source.Target}");
                Check((bool)Prop(move, "SpiritDefense")! == (source.Defense == "spirit"),
                    $"{label} {skill} is {(source.Defense == "spirit" ? "spirit" : "vitality")}-biased as the original");
            }
            // Native SetUp always attaches a Creature before generating intents.
            FixtureCall("Actor", monster, null, null);
            MonsterMoveStateMachine machine = Machine(monster);
            Check(machine.States.TryGetValue(TableState, out MonsterState? dispatcher) && !dispatcher.IsMove &&
                !dispatcher.ShouldAppearInLogs, $"{label} starts from the table state");
            Check(moves.All(m => machine.States.TryGetValue((string)Prop(m, "MoveId")!, out MonsterState? state) &&
                    state is MoveState move && ReferenceEquals(move.FollowUpState, dispatcher)) &&
                machine.States.Values.OfType<MoveState>().All(state => ReferenceEquals(state.FollowUpState, dispatcher)),
                $"{label} moves all return to the table state");
        }
        foreach ((Type type, HashSet<string> used) in usedByType)
            Check(TableMoves(Mutable(type)).Select(m => (string)Prop(m, "Skill")!).ToHashSet().SetEquals(used),
                $"{type.Name} has no move outside its variants' tables");

        // Each move spec against the first variant whose original has its skill.
        foreach (var spec in TableSpecs)
        {
            Type type = spec.Type;
            List<object> moves = TableMoves(Mutable(type));
            foreach (MoveSpec move in spec.Moves)
            {
                SnapshotEnemy original = TableVariants.Where(v => v.Type == type).Select(v => snapshot[v.Label])
                    .First(e => e.Skills.Any(s => s.Label == move.Skill));
                Check(moves.Any(m => (string)Prop(m, "MoveId")! == move.Id && (string)Prop(m, "Skill")! == move.Skill),
                    $"{type.Name}.{move.Id} is {move.Skill}");
                SnapshotSkill source = original.Skills.Single(s => s.Label == move.Skill);
                if (move.Damage0 > 0)
                    Check(move.Hits0 == source.Hits && move.Hits10 == source.Hits, $"{type.Name}.{move.Id} hits {source.Hits} times as the original");
                Check(move.Spirit == (source.Defense == "spirit"), $"{type.Name}.{move.Id} defense bias recorded");
                if (move.Status != "")
                    Check(source.Types.Contains(StatusTypes[move.Status]), $"{type.Name}.{move.Id} carries original {StatusTypes[move.Status]}");
                if (ChanceStatuses.Contains(move.Status))
                {
                    var chance = source.Chances.First(c => c.Type == StatusTypes[move.Status]);
                    bool sure = chance.Min >= 1m;
                    Check(move.Sure == sure, sure
                        ? $"{type.Name}.{move.Id}: the original {chance.Type} always lands (source metadata)"
                        : $"{type.Name}.{move.Id}: the original {chance.Type} has chance {chance.Min:0.##}-{chance.Max:0.##} (source metadata)");
                }
                foreach (string debuff in move.Debuff.Split('+', StringSplitOptions.RemoveEmptyEntries))
                    Check(source.Types.Contains(debuff), $"{type.Name}.{move.Id} carries original {debuff}");
                if (move.HealRatio > 0)
                    Equal(move.HealRatio, original.Heals[move.Skill] / original.Dp, $"{type.Name}.{move.Id} heals the original share of DP");
            }
        }
        // Variants with their own original maximum DP heal their own share.
        foreach ((Type type, string field, string label, string skill) in new[]
        {
            (typeof(BigotryGate), "DefaultHealDpRatio", "BigotryGateDefault_01", "BigotryGate_Heal"),
            (typeof(Marionette), "TutorialRecoveryDpRatio", "MarionetteSmallVirtual_first", "Marionette_Buff01"),
            (typeof(CatHorn), "FirstHealDpRatio", "CatHornSmallVirtual_first", "CatHorn_Heal01"),
            (typeof(TriClaw), "DefaultHealDpRatio", "TriClawDefault", "TriClaw_Heal"),
        })
            Equal((decimal)type.GetField(field, F)!.GetValue(null)!, snapshot[label].Heals[skill] / snapshot[label].Dp,
                $"{label} heals its own share of DP");
    }

    // ---- table walk ---------------------------------------------------------------

    static string Roll(MonsterMoveStateMachine machine, Creature owner, Rng rng)
    {
        MoveState move = machine.RollMove([Hero], owner, rng);
        machine.OnMovePerformed(move);
        return move.Id;
    }

    static string[] Walk(Type type, int variant, uint seed, int count, Action<MonsterModel>? setup = null)
    {
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel monster = TableActor(type, variant, out Creature actor);
        setup?.Invoke(monster);
        MonsterMoveStateMachine machine = Machine(monster);
        var rng = new Rng(seed);
        return Enumerable.Range(0, count).Select(_ => Roll(machine, actor, rng)).ToArray();
    }

    static string RollAt(MonsterModel monster, int step, Rng rng)
    {
        SetProp(monster, nameof(HbrgoMoveTableMonster.MoveTableStep), step);
        return (string)Call(monster, "RollTableMove", rng)!;
    }

    static void Frequency(Type type, int step, string first, decimal share, string label)
    {
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel monster = TableActor(type, 0, out _);
        var rng = new Rng(20261107);
        int hits = Enumerable.Range(0, 4000).Count(_ => RollAt(monster, step, rng) == first);
        decimal observed = hits / 4000m;
        Check(Math.Abs(observed - share) <= 0.03m, $"{label}: {first} rolled {observed:P1}, weight {share:P0}");
    }

    static void TableWalkCases()
    {
        foreach ((Type type, int variant, _) in TableVariants)
        {
            string[] once = Walk(type, variant, 7, 40), again = Walk(type, variant, 7, 40);
            Check(once.SequenceEqual(again), $"{type.Name} variant {variant} rolls the same 40 moves from the same monster AI seed");
            Cases.Add(new { table = type.Name, variant, seed = 7, rolls = once });
        }
        // Single-option steps consume no randomness; a choice draws once from the given RNG.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel needle = TableActor(typeof(NeedleBird), 0, out Creature needleActor);
        // Compared with a twin RNG, so no version-specific counter is read.
        Rng counted = new(3), twin = new(3);
        RollAt(needle, 0, counted);
        Check(counted.NextInt(1_000_000) == twin.NextInt(1_000_000), "a single-option step draws no number from the monster AI RNG");
        RollAt(needle, 3, counted);
        twin.NextInt(100);
        Check(counted.NextInt(1_000_000) == twin.NextInt(1_000_000), "a weighted step draws one number from the monster AI RNG");

        Frequency(typeof(NeedleBird), 3, "PECK_MOVE", 0.6m, "NeedleBird dp step 4 (60:40)");
        Frequency(typeof(NeedleBird), 4, "PECK_MOVE", 0.4m, "NeedleBird dp step 5 (40:60)");
        Frequency(typeof(DiamondEyeball), 3, "LASER_BIND_MOVE", 0.4m, "DiamondEyeball dp step 4 (40:60)");
        Frequency(typeof(PillarGuardian), 7, "PILLAR_STRIKE_MOVE", 0.8m, "PillarGuardian dp step 8 (80:20)");
        Frequency(typeof(BigotryGate), 1, "IMPACT_MOVE", 0.5m, "BigotryGate step 2 (50:50)");

        // Ruin Bullet waits for Charge; without it the step is skipped.
        string[] plain = Walk(typeof(NeedleBird), 0, 11, 3);
        Check(plain[0] == "PECK_MOVE" && plain[1] == "CHARGE_MOVE" &&
            plain[2] is "PECK_MOVE" or "MURDER_BLADE_MOVE",
            "NeedleBird without Charge skips Ruin Bullet to step 4: " + string.Join(",", plain));
        ResetTable(HeroKind.Hbr, 0);
        needle = TableActor(typeof(NeedleBird), 0, out needleActor);
        MonsterMoveStateMachine machine = Machine(needle);
        var rng = new Rng(11);
        Roll(machine, needleActor, rng); Roll(machine, needleActor, rng);
        Effect("ApplyCharge", needleActor);
        Check(Roll(machine, needleActor, rng) == "RUIN_BULLET_MOVE", "NeedleBird fires Ruin Bullet while charged");
        Equal(((HbrgoMoveTableMonster)needle).MoveTableStep, 3, "Ruin Bullet advances to step 4");

        // Seven once-only opening steps, then the 8-9-10 loop.
        string[] pillar = Walk(typeof(PillarGuardian), 0, 5, 40);
        bool loop = Enumerable.Range(10, 30).All(i => ((i - 10) % 3) switch
        {
            0 => pillar[i] is "PILLAR_STRIKE_MOVE" or "DESTRUCTION_SPEAR_MOVE",
            1 => pillar[i] == "ATTACK_UP_MOVE",
            _ => pillar[i] == "PARALYSIS_BURST_MOVE",
        });
        Check(pillar[3] == "PARALYSIS_BURST_MOVE" && pillar[5] == "ATTACK_UP_MOVE" &&
            pillar[6] == "PARALYSIS_BURST_MOVE" && loop,
            "PillarGuardian opens once with steps 1-7 and then loops steps 8-10: " + string.Join(",", pillar.Take(16)));
        Check(Walk(typeof(PillarGuardian), 0, 5, 7).Length == 7, "PillarGuardian opening walk");
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel opened = TableActor(typeof(PillarGuardian), 0, out Creature openedActor);
        MonsterMoveStateMachine openedMachine = Machine(opened);
        var openRng = new Rng(5);
        for (int i = 0; i < 10; i++) Roll(openedMachine, openedActor, openRng);
        Equal(((HbrgoMoveTableMonster)opened).DpOnceUsedMask, 0b111_1111, "PillarGuardian marks its seven once steps used");

        // The encounter variants start their own tables.
        Check(Walk(typeof(BigotryGate), 0, 1, 1)[0] == "IMPACT_MOVE", "BigotryGateSmall_01 opens with AttackSingle01");
        Check(Walk(typeof(BigotryGate), 1, 1, 2).SequenceEqual(["ATTACK_UP_MOVE", "STRONG_IMPULSE_MOVE"]),
            "BigotryGateSmall_02 opens with Attack Up, Strong Impulse");
        Check(Walk(typeof(BigotryGate), 2, 1, 1)[0] == "DISASTER_SHOT_MOVE", "BigotryGateDefault_01 opens with Disaster Shot");
        string[] gateOne = Walk(typeof(BigotryGate), 0, 9, 9), gateTwo = Walk(typeof(BigotryGate), 1, 9, 9);
        Check(Array.IndexOf(gateOne, "STRONG_IMPULSE_MOVE") == 3 && Array.IndexOf(gateTwo, "STRONG_IMPULSE_MOVE") == 1,
            "two gates release Strong Impulse on turns 4 and 2");
        Check(Walk(typeof(ShrimpCrab), 0, 1, 2).SequenceEqual(["PINCER_MOVE", "MIRAGE_PULSE_MOVE"]) &&
            Walk(typeof(ShrimpCrab), 1, 1, 1)[0] == "MIRAGE_PULSE_MOVE",
            "the two crabs open with Pincer and Mirage Pulse");

        // Second batch: openings, weights, once-only openings and loops.
        Check(Walk(typeof(CrowHorse), 0, 1, 1)[0] == "TRAMPLE_MOVE", "CrowHorse opens with Attack01");
        Check(Walk(typeof(IvoryScooper), 0, 1, 2).SequenceEqual(["SCOOP_MOVE", "ENERGY_DRAIN_MOVE"]),
            "IvoryScooper opens with Attack01, then Energy Drain");
        Check(Walk(typeof(AbyssKnockerKiller), 0, 1, 1)[0] == "RAGE_WAVE_MOVE", "AbyssKnockerKiller opens with Rage Wave");
        Check(Walk(typeof(TargetWireFrameKiller), 0, 1, 1)[0] == "SCATTER_MOVE",
            "TargetWireFrameKiller opens with its all-unit attack");
        string[] golem = Walk(typeof(PileGolemEdge), 0, 1, 3);
        Check(golem[0] is "CONFUSION_SPIKE_MOVE" or "AVALANCHE_MOVE" && golem[1] == "ATTACK_UP_MOVE" &&
            golem[2] == "CONFUSION_SPIKE_MOVE", "PileGolemEdge powers up, then fires Confusion Spike: " + string.Join(",", golem));
        string[] barrel = Walk(typeof(BarrelWalker), 0, 3, 12);
        Check(Enumerable.Range(0, 12).All(i => (i % 4) switch
        {
            0 => barrel[i] == "CANNON_MOVE",
            1 => barrel[i] is "CANNON_MOVE" or "RAGE_MAGNUM_MOVE",
            2 => barrel[i] == "ATTACK_UP_MOVE",
            _ => barrel[i] == "HEAVY_CANNON_MOVE",
        }), "BarrelWalker loops cannon, cannon or Rage Magnum, Attack Up, heavy cannon: " + string.Join(",", barrel));
        string[] slimy = Walk(typeof(SlimySerpent), 0, 5, 25);
        Check(slimy[1] == "ATTACK_UP_MOVE" && slimy[2] == "DESTRUCTION_RUSH_MOVE" && slimy[3] == "DISEASE_PLANT_MOVE" &&
            Enumerable.Range(7, 18).All(i => ((i - 7) % 3) switch
            {
                0 => slimy[i] == "ATTACK_UP_MOVE",
                1 => slimy[i] == "DESTRUCTION_RUSH_MOVE",
                _ => slimy[i] == "DISEASE_PLANT_MOVE",
            }), "SlimySerpent opens once with steps 1-7, then loops Attack Up, Destruction Rush, Disease Plant: " +
            string.Join(",", slimy.Take(13)));
        string[] giraffe = Walk(typeof(GiraffeHalo), 0, 5, 24);
        Check(giraffe[2] == "JUDGMENT_RAY_MOVE" && giraffe[4] == "ATTACK_UP_MOVE" && giraffe[5] == "JUDGMENT_RAY_MOVE" &&
            giraffe[7] == "HALO_SHOTS_MOVE" &&
            Enumerable.Range(8, 16).All(i => giraffe[i] == ((i - 8) % 2 == 0 ? "ATTACK_UP_MOVE" : "JUDGMENT_RAY_MOVE")),
            "GiraffeHalo opens once with steps 1-8, then alternates Attack Up and Judgment Ray: " +
            string.Join(",", giraffe.Take(12)));
        Check(Walk(typeof(ArachneLine), 0, 1, 1)[0] == "EIGHT_SLASH_MOVE", "ArachneLine opens with AttackSingle01");
        Frequency(typeof(CrowHorse), 1, "TRAMPLE_MOVE", 0.5m, "CrowHorse dp step 2 (50:50)");
        Frequency(typeof(TargetWireFrameKiller), 1, "COIL_SHOT_MOVE", 0.4m, "TargetWireFrameKiller dp step 2 (40:60)");
        Frequency(typeof(BarrelWalker), 1, "CANNON_MOVE", 0.4m, "BarrelWalker dp step 2 (40:60)");
        Frequency(typeof(ArachneLine), 1, "PIERCING_FLASH_MOVE", 0.6m, "ArachneLine dp step 2 (60:40)");
        // Third batch.
        string[] puppet = Walk(typeof(Marionette), 0, 2, 7);
        Check(puppet[0] == "ATTACK_MOVE" && puppet[1] == "STRONG_ATTACK_MOVE" && puppet[3] == "RECOVERY_MOVE",
            "Marionette opens with its two attacks, then recovers on turn 4: " + string.Join(",", puppet));
        string[] tutorial = Walk(typeof(Marionette), 1, 2, 8);
        Check(tutorial.SequenceEqual(["ATTACK_MOVE", "ATTACK_MOVE", "STRONG_ATTACK_MOVE", "RECOVERY_MOVE",
                "ATTACK_MOVE", "ATTACK_MOVE", "STRONG_ATTACK_MOVE", "RECOVERY_MOVE"]),
            "the tutorial puppet (MarionetteSmallVirtual_first) loops attack, attack, strong attack, recovery");
        string[] doll = Walk(typeof(MarionetteKiller), 0, 4, 12);
        Check(doll[1] == "RECOVERY_MOVE" && doll[5] == "STRONG_ATTACK_MOVE" && doll[11] == "STRONG_ATTACK_MOVE",
            "MarionetteKiller's step 6, listed twice in the original, is always the strong attack: " + string.Join(",", doll));
        string[] curse = Walk(typeof(MarionetteKiller), 1, 4, 21);
        Check(curse[0] == "CURSE_STAMP_MOVE" && Enumerable.Range(5, 16).All(i => ((i - 5) % 4) switch
            {
                0 or 2 => curse[i] == "ATTACK_UP_MOVE",
                1 => curse[i] is "CURSE_ATTACK_MOVE" or "CURSE_STAMP_MOVE",
                _ => curse[i] == "CURSE_STAMP_MOVE",
            }), "the cursed doll opens once with steps 1-5, then loops Attack Up and Curse Stamp: " + string.Join(",", curse.Take(13)));
        Check(Walk(typeof(CatHorn), 1, 1, 3) is ["HORN_RUSH_MOVE", "HORN_RUSH_MOVE" or "CREEPY_BALL_MOVE", "SWEEP_MOVE"],
            "the α cat horn (CatHornSmallVirtual_first) opens with Attack01, a single, then the sweep");
        Check(Walk(typeof(CatHorn), 0, 1, 2)[1] == "SWEEP_MOVE" && Walk(typeof(CatHornKiller), 0, 1, 1)[0] == "SWEEP_MOVE",
            "CatHorn sweeps on turn 2; CatHornKiller opens with it");
        Check(Walk(typeof(WireSlasher), 0, 1, 2).SequenceEqual(["ATTACK_MOVE", "ALL_ATTACK_MOVE"]),
            "WireSlasher opens with its attack, then the all-unit attack");
        Check(Walk(typeof(OctopusTail), 0, 1, 2)[1] == "ALL_ATTACK_MOVE" && Walk(typeof(OctopusTailKiller), 0, 1, 1)[0] == "ALL_ATTACK_MOVE",
            "OctopusTail's all-unit attack comes on turn 2; OctopusTailKiller opens with it");
        Check(Walk(typeof(TargetWireFrame), 0, 1, 2).SequenceEqual(["COIL_SHOT_MOVE", "SCATTER_MOVE"]),
            "TargetWireFrame opens with Attack01, then its all-unit attack");
        Check(Walk(typeof(CrestHopperKiller), 0, 1, 2).SequenceEqual(["ATTACK_MOVE", "COMBO_MOVE"]),
            "CrestHopperKiller opens with its attack, then the combo");
        Check(Walk(typeof(DarkKnockerKiller), 0, 1, 2).SequenceEqual(["ATTACK_MOVE", "ALL_ATTACK_MOVE"]),
            "DarkKnockerKiller opens with its attack, then the all-unit attack");
        Check(Walk(typeof(BalbFlea), 0, 1, 2).SequenceEqual(["RAM_MOVE", "FLASH_MOVE"]),
            "BalbFlea opens with Ram, then the all-unit flash");
        string[] small = Walk(typeof(TriClaw), 0, 6, 12);
        Check(Enumerable.Range(0, 12).All(i => i % 2 == 0 ? small[i] is "DART_MOVE" or "ATTACK_UP_MOVE"
                : small[i] is "DART_MOVE" or "CLAWS_MOVE"),
            "TriClawSmall loops two steps: dart or Attack Up, then dart or claws: " + string.Join(",", small));
        string[] tri = Walk(typeof(TriClaw), 1, 6, 19);
        Check(tri[2] == "ATTACK_UP_MOVE" && Enumerable.Range(4, 15).All(i => ((i - 4) % 5) switch
            {
                0 or 3 => tri[i] == "ATTACK_UP_MOVE",
                1 => tri[i] is "DART_MOVE" or "CLAWS_MOVE",
                _ => tri[i] == "CLAWS_MOVE",
            }), "TriClawDefault opens once with steps 1-4, then loops steps 5-9: " + string.Join(",", tri.Take(12)));
        string[] knife = Walk(typeof(CorollaKnife), 0, 3, 10), second = Walk(typeof(CorollaKnife), 1, 3, 10);
        Check(knife[3] == "ATTACK_UP_MOVE" && knife[4] == "RAMPAGE_SLICE_MOVE" && knife[8] == "ATTACK_UP_MOVE" &&
            second[0] == "ATTACK_UP_MOVE" && second[1] == "RAMPAGE_SLICE_MOVE" && second[5] == "ATTACK_UP_MOVE",
            "the two knives power up on turns 4 and 1: " + string.Join(",", knife.Take(5)) + " / " + string.Join(",", second.Take(5)));
        string[] mol = Walk(typeof(MolArms), 0, 8, 20);
        Check(mol[3] == "ATTACK_UP_MOVE" && Enumerable.Range(5, 15).All(i => ((i - 5) % 3) switch
            {
                0 => mol[i] is "DOUBLE_SMASH_MOVE" or "DECEIVE_SHOT_MOVE",
                1 => mol[i] == "ATTACK_UP_MOVE",
                _ => mol[i] == "ALL_ATTACK_MOVE",
            }), "MolArms opens once with steps 1-5, then loops a single, Attack Up, the all-unit attack: " + string.Join(",", mol.Take(11)));
        Frequency(typeof(TargetWireFrame), 2, "COIL_SHOT_MOVE", 0.6m, "TargetWireFrame dp step 3 (60:40)");
        Frequency(typeof(CatHornKiller), 1, "HORN_RUSH_MOVE", 0.4m, "CatHornKiller dp step 2 (40:60)");
        Frequency(typeof(CorollaKnife), 0, "SLASH_MOVE", 0.7m, "CorollaKnife dp step 1 (70:30)");
        // OctopusTail's hp step 2 (original order 3) has three rows, 100:50:50.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel octopus = TableActor(typeof(OctopusTail), 0, out _);
        SetProp(octopus, nameof(HbrgoMoveTableMonster.MoveTableIndex), 1);
        var octopusRng = new Rng(20261109);
        string[] octopusRolls = Enumerable.Range(0, 4000).Select(_ => RollAt(octopus, 1, octopusRng)).ToArray();
        decimal allShare = octopusRolls.Count(r => r == "ALL_ATTACK_MOVE") / 4000m;
        decimal whipShare = octopusRolls.Count(r => r == "TAIL_WHIP_MOVE") / 4000m;
        Check(Math.Abs(allShare - 0.5m) <= 0.03m && Math.Abs(whipShare - 0.25m) <= 0.03m,
            $"OctopusTail hp step 2 (100:50:50): all-unit {allShare:P1}, tail whip {whipShare:P1}");

        // ArachneLine's hp step 8 lists Attack Up and Heal at 100 each: even odds.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel arachne = TableActor(typeof(ArachneLine), 0, out _);
        SetProp(arachne, nameof(HbrgoMoveTableMonster.MoveTableIndex), 1);
        var arachneRng = new Rng(20261108);
        int ups = Enumerable.Range(0, 4000).Count(_ => RollAt(arachne, 7, arachneRng) == "ATTACK_UP_MOVE");
        Check(Math.Abs(ups / 4000m - 0.5m) <= 0.03m, $"ArachneLine hp step 8 (100:100): Attack Up rolled {ups / 4000m:P1}");
    }

    // ---- DP Break and the returning shell -------------------------------------------

    static void TableBreakCases()
    {
        // The table switch itself.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel needle = TableActor(typeof(NeedleBird), 0, out Creature needleActor);
        var table = (HbrgoMoveTableMonster)needle;
        MonsterMoveStateMachine machine = Machine(needle);
        var rng = new Rng(13);
        Roll(machine, needleActor, rng); Roll(machine, needleActor, rng);
        Check(Call(needle, "OnShellBroken") == null &&
            table.MoveTableIndex == 1 && table.MoveTableStep == 0, "NeedleBird takes the move after a DP break from its hp table");
        string first = Roll(machine, needleActor, rng);
        Check(first is "PECK_MOVE" or "MURDER_BLADE_MOVE" && Roll(machine, needleActor, rng) == "CHARGE_MOVE",
            "NeedleBird walks hp steps 1-2 in Break");
        Call(needle, "OnShellApplied");
        Check(table.MoveTableIndex == 0 && table.MoveTableStep == 0 && Roll(machine, needleActor, rng) == "PECK_MOVE",
            "the returning shell restarts NeedleBird's dp table at step 1");

        ResetTable(HeroKind.Hbr, 0);
        MonsterModel gate = TableActor(typeof(BigotryGate), 0, out _);
        var gateTable = (HbrgoMoveTableMonster)gate;
        SetProp(gate, nameof(HbrgoMoveTableMonster.MoveTableStep), 4);
        Check(Call(gate, "OnShellBroken") == null &&
            gateTable.MoveTableIndex == 0 && gateTable.MoveTableStep == 4,
            "BigotryGate (no hp table) keeps walking its dp table after a DP break");

        ResetTable(HeroKind.Hbr, 0);
        MonsterModel pillar = TableActor(typeof(PillarGuardian), 0, out Creature pillarActor);
        MonsterMoveStateMachine pillarMachine = Machine(pillar);
        var pillarRng = new Rng(21);
        for (int i = 0; i < 5; i++) Roll(pillarMachine, pillarActor, pillarRng);
        Call(pillar, "OnShellBroken");
        string hpFirst = Roll(pillarMachine, pillarActor, pillarRng);
        Check(hpFirst is "PILLAR_STRIKE_MOVE" or "DESTRUCTION_SPEAR_MOVE", "PillarGuardian Break uses hp step 1");
        Call(pillar, "OnShellApplied");
        string[] after = [Roll(pillarMachine, pillarActor, pillarRng), Roll(pillarMachine, pillarActor, pillarRng), Roll(pillarMachine, pillarActor, pillarRng)];
        Check(after[0] == "ATTACK_UP_MOVE" && after[1] == "PARALYSIS_BURST_MOVE" &&
            after[2] is "PILLAR_STRIKE_MOVE" or "DESTRUCTION_SPEAR_MOVE",
            "after Break the dp table skips the used once steps 1-5 and finishes 6-7 before the loop: " + string.Join(",", after));

        // Second batch: every one has an hp table. Break switches to it from the
        // next move; the returning shell restarts the dp table, skipping used once steps.
        foreach ((Type type, string[] hpOpeners, string[] dpOpeners) in new[]
        {
            (typeof(CrowHorse), new[] { "DESTRUCTION_BALL_MOVE" }, new[] { "TRAMPLE_MOVE" }),
            (typeof(IvoryScooper), new[] { "DESTROY_BASH_MOVE" }, new[] { "SCOOP_MOVE" }),
            (typeof(AbyssKnockerKiller), new[] { "RAGE_WAVE_MOVE" }, new[] { "RAGE_WAVE_MOVE" }),
            (typeof(TargetWireFrameKiller), new[] { "SCATTER_MOVE" }, new[] { "SCATTER_MOVE" }),
            (typeof(PileGolemEdge), new[] { "ATTACK_UP_MOVE" }, new[] { "CONFUSION_SPIKE_MOVE", "AVALANCHE_MOVE" }),
            (typeof(BarrelWalker), new[] { "CANNON_MOVE", "RAGE_MAGNUM_MOVE" }, new[] { "CANNON_MOVE" }),
            (typeof(SlimySerpent), new[] { "ATTACK_UP_MOVE" }, new[] { "DESTRUCTION_RUSH_MOVE" }),
            (typeof(GiraffeHalo), new[] { "HALO_SHOTS_MOVE", "JUDGMENT_RAY_MOVE" }, new[] { "JUDGMENT_RAY_MOVE" }),
            (typeof(ArachneLine), new[] { "PIERCING_FLASH_MOVE" }, new[] { "EIGHT_SLASH_MOVE" }),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            MonsterModel monster = TableActor(type, 0, out Creature actor);
            var walker = (HbrgoMoveTableMonster)monster;
            MonsterMoveStateMachine walk = Machine(monster);
            var walkRng = new Rng(17);
            Roll(walk, actor, walkRng);
            Roll(walk, actor, walkRng);
            Check(Call(monster, "OnShellBroken") == null && walker.MoveTableIndex == 1 &&
                walker.MoveTableStep == 0, $"{type.Name} takes the move after a DP break from its hp table");
            string hpMove = Roll(walk, actor, walkRng);
            Check(hpOpeners.Contains(hpMove), $"{type.Name} Break opens with hp step 1 ({hpMove})");
            Call(monster, "OnShellApplied");
            string dpMove = Roll(walk, actor, walkRng);
            Check(walker.MoveTableIndex == 0 && dpOpeners.Contains(dpMove),
                $"{type.Name} restarts its dp table when the shell returns, used once steps skipped ({dpMove})");
        }

        // Third batch: with an hp table a DP break leads into it from the next move;
        // without one (the tutorial puppet, the knives) the dp table carries on.
        foreach ((Type type, int variant, string hpOpener) in new[]
        {
            (typeof(MarionetteKiller), 1, "CURSE_STAMP_MOVE"),
            (typeof(MolArms), 0, "ATTACK_UP_MOVE"),
            (typeof(WireSlasher), 0, "ALL_ATTACK_MOVE"),
            (typeof(CrestHopperKiller), 0, "COMBO_MOVE"),
            (typeof(DarkKnockerKiller), 0, "ALL_ATTACK_MOVE"),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            MonsterModel monster = TableActor(type, variant, out Creature actor);
            var walker = (HbrgoMoveTableMonster)monster;
            MonsterMoveStateMachine walk = Machine(monster);
            var walkRng = new Rng(29);
            Roll(walk, actor, walkRng);
            Check(Call(monster, "OnShellBroken") == null && walker.MoveTableIndex == 1 &&
                Roll(walk, actor, walkRng) == hpOpener, $"{type.Name} variant {variant} Break opens with hp step 1 ({hpOpener})");
        }
        foreach ((Type type, int variant) in new[] { (typeof(Marionette), 1), (typeof(CorollaKnife), 0), (typeof(CorollaKnife), 1) })
        {
            ResetTable(HeroKind.Hbr, 0);
            MonsterModel monster = TableActor(type, variant, out _);
            var walker = (HbrgoMoveTableMonster)monster;
            SetProp(monster, nameof(HbrgoMoveTableMonster.MoveTableStep), 2);
            Check(Call(monster, "OnShellBroken") == null && walker.MoveTableIndex == 0 && walker.MoveTableStep == 2,
                $"{type.Name} variant {variant} (no hp table) keeps its dp table after a DP break");
        }

        // DP breaks no longer stun, including special bosses (2026-11-14).
        foreach ((Type type, string next, bool stuns, bool hpTable) in new[]
        {
            (typeof(NeedleBird), "RUIN_BULLET_MOVE", false, true),
            (typeof(BigotryGate), "STRONG_IMPULSE_MOVE", false, false),
            (typeof(Knocker), "ALL_ATTACK_MOVE", false, true),
            (typeof(GoldHopper), "ESCAPE_MOVE", false, false),
            (typeof(TezukaSaki), "THREE_GUN_FRENZY_MOVE", false, false),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            Creature actor = Actor(type);
            MonsterModel monster = actor.Monster!;
            SetUp(monster, next);
            if (type == typeof(NeedleBird)) Effect("ApplyCharge", actor);
            HighDimensionalShellPower shell = Shell(actor, 100m);
            Await(Invoke(typeof(HighDimensionalShellPower).GetMethod("BreakImmediately", F)!, shell,
                new ThrowingPlayerChoiceContext(), 0));
            string label = $"{type.Name} DP break";
            Check(monster.NextMove.Id == next, $"{label} no longer stuns: the current intent {next} still goes ahead");
            Check(!actor.HasPower<HighDimensionalShellPower>() && actor.GetPowerAmount<BreakPower>() == 2 &&
                    actor.HasPower<DestructionRatePower>(),
                $"{label} applies a normal 2-turn Break and Destruction Rate");
            if (monster is HbrgoMoveTableMonster broken)
            {
                Equal(broken.MoveTableIndex, hpTable ? 1 : 0, label + " table index");
                Check(actor.HasPower<HbrEnemyChargePower>() == (type == typeof(NeedleBird)),
                    label + (type == typeof(NeedleBird) ? " keeps its Charge, so Ruin Bullet still fires" : " gives no Charge"));
                Await(PowerCmd.Remove(actor.GetPower<BreakPower>()));
                Check(actor.HasPower<HighDimensionalShellPower>() && broken.MoveTableIndex == 0 &&
                    (!hpTable || broken.MoveTableStep == 0),
                    label + (hpTable ? ": Break expiry restores the shell and restarts the dp table"
                        : ": Break expiry restores the shell; the dp table keeps its place"));
            }
            Cases.Add(new { breakCase = type.Name, next, stuns, nextMove = monster.NextMove.Id, followUp = monster.NextMove.FollowUpStateId });
        }

        // Every ordinary shell uses the same no-stun wording.
        foreach ((Type type, bool special) in new[]
        {
            (typeof(TezukaSaki), true), (typeof(NararaShibaBossMonster), true), (typeof(RedCrimson), true),
            (typeof(RedCrimsonBossMonster), true), (typeof(FlatHand), true), (typeof(DeathSlug), true),
            (typeof(SkullFeather), true),
            (typeof(NeedleBird), false), (typeof(BigotryGate), false), (typeof(GoldHopper), false),
            (typeof(SmallHopper), false), (typeof(PillarGuardian), false),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            HighDimensionalShellPower shell = Shell(Actor(type), 100m);
            string description = shell.Description.LocEntryKey;
            string compact = (string)Prop(Prop(shell, "CompactDescription")!, "LocEntryKey")!;
            Check(description == "HIGH_DIMENSIONAL_SHELL_POWER.description" &&
                compact == "HIGH_DIMENSIONAL_SHELL_POWER.compactDescription",
                $"the ordinary shell on {type.Name} uses the common no-stun text");
            Cases.Add(new { shellStun = type.Name, special, description, compact });
        }
        Check(typeof(HighDimensionalShellPower).GetProperty("KeepsBreakStun", F) == null,
            "the obsolete special-owner DP-stun selector is removed");
        using (JsonDocument powers = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,
                   "HBRGOSakiMod/localization/zhs/powers.json"))))
        {
            string Text(string key) => powers.RootElement.GetProperty(key).GetString()!;
            string plain = Text("HIGH_DIMENSIONAL_SHELL_POWER.description");
            string compact = Text("HIGH_DIMENSIONAL_SHELL_POWER.compactDescription");
            Check(!plain.Contains("眩晕") && plain.Contains("{breakAmount}") && plain.Contains("[gold]Break[/gold]与[gold]破坏率[/gold]") &&
                  !powers.RootElement.TryGetProperty("HIGH_DIMENSIONAL_SHELL_POWER.stunDescription", out _) &&
                  !compact.Contains("击晕") && !powers.RootElement.TryGetProperty("HIGH_DIMENSIONAL_SHELL_POWER.compactStunDescription", out _) &&
                  !Text("PARTIAL_DERIVATIVE_SHIELD_POWER.description").Contains("当前意图改为眩晕") &&
                  !Text("HBR_ENEMY_CHARGE_POWER.description").Contains("DP破坏时眩晕") &&
                  Text("HBR_ENEMY_CHARGE_POWER.description").Contains("DP被击破") &&
                  plain.Contains("30%") && plain.Contains("1回合") &&
                  compact.Contains("30%") && compact.Contains("1回合"),
                "all DP shells drop stun wording, obsolete special-boss variants are removed, and Charge clears on DP break without implying a stun");
        }
        SpecialBossNoBreakStunCases();
    }

    static HighDimensionalShellPower SpecialShell(Type type, Creature owner, decimal dp)
    {
        var shell = (HighDimensionalShellPower)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(type), null, owner, 1)!;
        FieldInfo queue = typeof(HighDimensionalShellPower).GetField("_dpChanges", F)!;
        Set(shell, "_dpChanges", Activator.CreateInstance(queue.FieldType));
        Set(shell, "_pendingExtraDpDamage", new Stack<decimal>());
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData))!
            .SetValue(shell, dp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return shell;
    }

    static MoveState SetOrdinaryIntent(MonsterModel monster)
    {
        SetUp(monster);
        MoveState move = monster.MoveStateMachine!.States.Values.OfType<MoveState>()
            .First(state => state.Id != "STUNNED" && !state.Id.Contains("REVIVE", StringComparison.Ordinal));
        monster.SetMoveImmediate(move, forceTransition: true);
        return move;
    }

    static void SpecialBossNoBreakStunCases()
    {
        foreach (Type power in new[] { typeof(GhostStatePower), typeof(PermanentBreakPower), typeof(NararaLayeredDpPower),
                     typeof(PartialDerivativeShieldPower), typeof(NararaSleepingPower) })
            if (!ModelDb.Contains(power)) ModelDb.Inject(power);
        foreach ((Type room, int turns) in new[]
        { (typeof(SmallHoppersNormal), 2), (typeof(ShrimpCrabsElite), 3), (typeof(RedCrimsonBoss), 4) })
        foreach (Type type in new[] { typeof(RedCrimson), typeof(RedCrimsonBossMonster), typeof(FlatHand),
                     typeof(DeathSlug), typeof(SkullFeather), typeof(TezukaSaki) })
        {
            ResetTable(HeroKind.Hbr, 0);
            Set(Combat, "_encounter", Blank(room));
            Creature actor = Actor(type);
            MoveState intent = SetOrdinaryIntent(actor.Monster!);
            HighDimensionalShellPower shell = Shell(actor, 100m);
            Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
            Check(ReferenceEquals(actor.Monster!.NextMove, intent), $"{type.Name} ordinary DP break keeps its actual queued intent in {room.Name}");
            Check(actor.GetPowerAmount<BreakPower>() == turns && actor.HasPower<DestructionRatePower>() &&
                !actor.HasPower<HighDimensionalShellPower>(), $"{type.Name} retains {turns}-turn Break and destruction when its shell breaks");
            BreakPower broken = actor.GetPower<BreakPower>()!;
            for (int turn = 1; turn <= turns; turn++)
            {
                Await(broken.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [actor], Combat));
                Check(turn < turns ? actor.GetPowerAmount<BreakPower>() == turns - turn && !actor.HasPower<HighDimensionalShellPower>() :
                    !actor.HasPower<BreakPower>() && actor.HasPower<HighDimensionalShellPower>(),
                    $"{type.Name} {room.Name} native turn {turn}/{turns} restores the shell only on expiry");
            }
            Check(ReferenceEquals(actor.Monster.NextMove, intent),
                $"{type.Name} timed Break expiry keeps its queued intent");
        }

        ResetTable(HeroKind.Hbr, 0);
        Creature tezuka = Actor(typeof(TezukaSaki));
        MoveState tezukaIntent = SetOrdinaryIntent(tezuka.Monster!);
        HighDimensionalShellPower derivative = SpecialShell(typeof(PartialDerivativeShieldPower), tezuka, 100m);
        Await(Call(derivative, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
        Check(tezuka.HasPower<GhostStatePower>() && tezuka.HasPower<PermanentBreakPower>() &&
            tezuka.HasPower<DestructionRatePower>() && !tezuka.HasPower<PartialDerivativeShieldPower>(),
            "Tezuka partial shield still grants Ghost State, permanent Break and destruction");
        Check(ReferenceEquals(tezuka.Monster!.NextMove, tezukaIntent), "Tezuka partial shield depletion does not stun or replace the queued intent");

        foreach (int remaining in new[] { 3, 1 })
        {
            ResetTable(HeroKind.Hbr, 0);
            Creature dog = Actor(typeof(NararaShibaBossMonster));
            MoveState intent = SetOrdinaryIntent(dog.Monster!);
            SetProp(dog.Monster!, "ExactDpLayerMaximum", 100m);
            SetProp(dog.Monster!, "DpLayersRemaining", remaining);
            // This case isolates the stun decision; the real final-DP forced
            // turn end has its own Narara suite and remains unchanged.
            SetProp(dog.Monster!, "LastDpTurnEndTriggered", true);
            HighDimensionalShellPower shell = SpecialShell(typeof(NararaLayeredDpPower), dog, 100m);
            Await(Call(shell, "BreakImmediately", new ThrowingPlayerChoiceContext(), 0));
            Check((int)Prop(dog.Monster!, "DpLayersRemaining")! == remaining - 1 && ReferenceEquals(dog.Monster!.NextMove, intent),
                $"awake Narara layer {remaining} depletes without stun or an intent replacement");
            Check(remaining > 1 ? shell.CurrentDp == 100m && !dog.HasPower<BreakPower>() :
                dog.HasPower<BreakPower>() && dog.HasPower<DestructionRatePower>() && !dog.HasPower<NararaLayeredDpPower>(),
                $"Narara layer {remaining} retains its reserve/final-Break resource behavior");
        }
        ResetTable(HeroKind.Hbr, 0);
        Check(Combat.Encounter == null, "special Boss break cases clear their temporary encounter before later resource checks");
    }

    // ---- move bodies ------------------------------------------------------------

    static int HeroDpNow => HeroDp?.Dp ?? -1;

    static void ExpectDebuffs(HeroKind kind, MoveSpec move, int ascension, Creature actor, string label)
    {
        int raw = ascension == 0 || move.DebuffRaw10 == 0 ? move.DebuffRaw : move.DebuffRaw10;
        string[] kinds = move.Debuff.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach ((string name, Type hbr, Type native) in new[] { ("AttackDown", typeof(HbrAttackDownPower), typeof(WeakPower)),
            ("DefenseDown", typeof(HbrDefenseDownPower), typeof(VulnerablePower)) })
        {
            PowerModel[] hbrPowers = Hero.Powers.Where(p => p.GetType() == hbr).ToArray();
            PowerModel[] nativePowers = Hero.Powers.Where(p => p.GetType() == native).ToArray();
            if (!kinds.Contains(name))
            {
                Check(hbrPowers.Length == 0 && nativePowers.Length == 0, $"{label} applies no {name}");
                continue;
            }
            if (kind == HeroKind.Hbr)
            {
                Check(hbrPowers.Length == 1 && nativePowers.Length == 0, $"{label} applies HBR {name}");
                Equal(Modifier(hbrPowers[0]), raw, $"{label} HBR {name} authored modifier count");
                decimal basis = actor.Monster switch
                {
                    BarrelWalker => 25m,
                    ShrimpCrab => 20m,
                    _ => 30m
                };
                if (ascension >= 8) basis *= 1.25m;
                decimal casterE = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", actor)!;
                decimal targetE = (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", Hero)!;
                Equal(EffectPercent(hbrPowers[0]), basis * (1m + (casterE - targetE) * 0.0002m),
                    $"{label} HBR {name} original upper-power basis {basis}% enters the shared attribute formula");
            }
            else
            {
                Check(nativePowers.Length == 1 && hbrPowers.Length == 0, $"{label} applies native {native.Name} ({kind})");
                Equal(nativePowers[0].Amount, raw, $"{label} {native.Name} turns");
            }
        }
    }

    static decimal ExpectedEnemyBuff(decimal basis, Creature owner, HeroKind kind) => kind != HeroKind.Hbr ? basis :
        basis * (1m + ((decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyProfileHelper", "GetEnemyParameter", owner)! - 100m) * 0.0002m);

    static decimal EffectStrength(PowerModel power) => (decimal)Prop(power, "EffectPotency")!;
    static int IllusionLayers(Creature owner) => (int)owner.Powers.OfType<HbrIllusionPower>().Sum(EffectStrength);

    static void ExpectStatus(HeroKind kind, MoveSpec move, int ascension, bool passed, Creature actor,
        int dpBefore, string label)
    {
        int amount = ascension == 0 ? move.Status0 : move.Status10;
        bool Has<T>(Creature creature) where T : PowerModel => creature.Powers.Any(p => p is T);
        var expectedOnHero = new HashSet<Type>();
        // HBR enemy control ailments attach through full block; legacy and
        // phantom behavior retain their previous pass-through condition.
        bool lands = kind == HeroKind.Hbr || !IsTracked(move) || passed || move.Sure;
        switch (move.Status)
        {
            case "Charge":
                Check(Has<HbrEnemyChargePower>(actor), label + " charges itself");
                break;
            case "AttackUp":
            case "DefenseUp":
                PowerModel buff = actor.Powers.Single(p => p is HbrTimedPercentBuffPower);
                Check(buff.GetType() == (move.Status == "AttackUp" ? typeof(HbrAttackUpPower) : typeof(HbrDefenseUpPower)) &&
                    buff.Amount == 2 && EffectStrength(buff) == ExpectedEnemyBuff(amount, actor, kind),
                    $"{label} gains {move.Status} {amount}% until its next turn ends");
                break;
            case "SpiritDown":
                expectedOnHero.Add(typeof(HbrSpiritDownPower));
                Check(Hero.GetPowerAmount<HbrSpiritDownPower>() == amount, $"{label} applies Spirit Down for {amount} turns ({kind})");
                break;
            case "Bind":
            case "Stun":
                Type lockType = move.Status == "Bind" ? typeof(HbrBindPower) : typeof(HbrStunPower);
                if (lands) expectedOnHero.Add(lockType);
                Check(Hero.Powers.Any(p => p.GetType() == lockType) == lands,
                    $"{label} {(lands ? "applies" : "withholds")} {move.Status} when the hit {(passed ? "passed" : "was stopped by")} block ({kind})");
                break;
            case "ContinuousDp":
                bool hasDp = kind != HeroKind.Original;
                if (hasDp) expectedOnHero.Add(typeof(HbrContinuousDpDamagePower));
                Check(hasDp ? Hero.GetPowerAmount<HbrContinuousDpDamagePower>() == amount : !Has<HbrContinuousDpDamagePower>(Hero),
                    hasDp ? $"{label} drains DP for {amount} turns ({kind})" : $"{label} has no DP to drain on an original character");
                break;
            case "Imprison":
                if (lands) expectedOnHero.Add(typeof(HbrImprisonPower));
                Check(Has<HbrImprisonPower>(Hero) == lands, lands
                    ? $"{label} seals the hero ({kind})"
                    : $"{label} withholds Seal when block stopped the hit ({kind})");
                break;
            case "Virus":
                bool virus = lands && kind != HeroKind.Original;
                if (virus) expectedOnHero.Add(typeof(HbrVirusPower));
                Check(virus ? Hero.GetPowerAmount<HbrVirusPower>() == amount : !Has<HbrVirusPower>(Hero),
                    virus ? $"{label} gives Virus for {amount} turns ({kind})"
                        : kind == HeroKind.Original ? $"{label} has no DP for Virus on an original character"
                        : $"{label} withholds Virus when block stopped the hit ({kind})");
                if (virus)
                    Check(HeroDp!.MaxDp == (int)Math.Floor(FullMaxDp * 0.7m) && HeroDpNow == Math.Min(dpBefore, HeroDp.MaxDp),
                        $"{label} lowers the maximum DP to 70% ({HeroDp.MaxDp} of {FullMaxDp}) and keeps DP under it ({kind})");
                break;
            case "WisdomDown":
                expectedOnHero.Add(typeof(HbrWisdomDownPower));
                Check(Hero.GetPowerAmount<HbrWisdomDownPower>() == amount, $"{label} applies Wisdom Down for {amount} turns ({kind})");
                break;
            case "Calamity":
                if (kind == HeroKind.Hbr) expectedOnHero.Add(typeof(HbrCalamityPower));
                Check(kind == HeroKind.Hbr ? Hero.GetPowerAmount<HbrCalamityPower>() == amount : !Has<HbrCalamityPower>(Hero),
                    kind == HeroKind.Hbr ? $"{label} applies {amount} Calamity" : $"{label} applies no Calamity without HBR attributes ({kind})");
                break;
            case "DpBreak":
                Check(kind == HeroKind.Original ? HeroDp == null : dpBefore > 0 && HeroDpNow == 0,
                    kind == HeroKind.Original ? $"{label} has no DP to break on an original character" : $"{label} empties Deflector Protection ({kind})");
                break;
            case "Illusion":
            case "Confusion":
                Type control = move.Status == "Illusion" ? typeof(HbrIllusionPower) : typeof(HbrConfusionPower);
                if (lands) expectedOnHero.Add(control);
                PowerModel? held = Hero.Powers.SingleOrDefault(p => p.GetType() == control);
                Check(lands ? held != null && (move.Status == "Illusion" ? EffectStrength(held) == amount : held.Amount == amount) : held == null, lands
                    ? $"{label} applies {move.Status} {amount} when the hit {(passed ? "passed" : "was stopped by")} block{(move.Sure ? " (always lands in the original)" : "")} ({kind})"
                    : $"{label} withholds {move.Status} when block stopped the hit ({kind})");
                break;
            case "Escape":
                Check(Escapes.SequenceEqual([actor]), $"{label} escapes ({kind})");
                break;
            case "OdDown":
                Check(kind == HeroKind.Original ? HeroOd == null && Hero.Powers.Count == 0 : HeroOd?.Charge == OdBefore - amount,
                    kind == HeroKind.Original ? $"{label} has no Overclock gauge to lower on an original character"
                        : $"{label} lowers the Overclock gauge by {amount}% of a level ({kind})");
                break;
        }
        if (kind == HeroKind.Hbr && actor.Side == CombatSide.Enemy && actor.Monster is AbyssKnockerKiller && move.Id == "RAGE_WAVE_MOVE")
        {
            expectedOnHero.Add(typeof(HbrCalamityPower));
            var calamity = Hero.GetPower<HbrCalamityPower>();
            Check(calamity != null && calamity.Amount == (ascension == 0 ? 20 : 30) && calamity.RemainingTurns == 2,
                label + " applies authorized M17 twenty/thirty Calamity for two turns");
        }
        if (kind == HeroKind.Hbr && actor.Side == CombatSide.Enemy && actor.Monster is IvoryScooper && move.Id == "DESTROY_BASH_MOVE")
        {
            expectedOnHero.Add(typeof(HbrContinuousDamagePower));
            var periodic = Hero.GetPower<HbrContinuousDamagePower>();
            Check(periodic != null && periodic.Amount == 4 && EffectStrength(periodic) == (ascension == 0 ? 10 : 15),
                label + " applies authorized M16 ten/fifteen-percent poison burst for four turns");
        }
        foreach (Type status in new[] { typeof(HbrSpiritDownPower), typeof(HbrBindPower), typeof(HbrStunPower),
            typeof(HbrContinuousDpDamagePower), typeof(HbrImprisonPower), typeof(HbrCalamityPower),
            typeof(HbrIllusionPower), typeof(HbrConfusionPower), typeof(HbrVirusPower), typeof(HbrWisdomDownPower), typeof(HbrContinuousDamagePower) })
            if (!expectedOnHero.Contains(status))
                Check(Hero.Powers.All(p => p.GetType() != status), $"{label} leaves no {status.Name}");
        if (move.Status is not ("DpBreak" or "Virus") && HeroDp != null)
            Equal(HeroDpNow, dpBefore, label + " leaves Deflector Protection alone");
    }

    const int OdBefore = 50;

    // The attack scope a move's original target maps to.
    static string ScopeOf(MonsterModel monster, string moveId)
    {
        object tableMove = TableMoves(monster).First(m => (string)Prop(m, "MoveId")! == moveId);
        return Prop(tableMove, "Target")!.ToString() switch
        {
            "All" => "AllUnits",
            "MinimumDp" => "MinimumDp",
            _ => "Players",
        };
    }

    // Run both blocked and passed cases for original chance-based ailments
    // (enemy contact is now sufficient) and for the still pass-based Absorb.
    static bool IsTracked(MoveSpec move) =>
        move.Damage0 > 0 && (ChanceStatuses.Contains(move.Status) || move.Status == "Absorb");

    // The hero's maximum Deflector Protection DP without Virus.
    static int FullMaxDp;

    static void TableMoveBodyCases()
    {
        foreach (var spec in TableSpecs)
        foreach (HeroKind kind in HeroKinds)
        foreach (int ascension in new[] { 0, 10 })
        foreach (MoveSpec move in spec.Moves)
        foreach (bool passed in IsTracked(move) ? new[] { true, false } : new[] { true })
        {
            ResetTable(kind, ascension, dp: 500, overdrive: move.Status == "OdDown" ? OdBefore : -1);
            string tracked = IsTracked(move) ? passed ? " (passed block)" : " (blocked)" : "";
            string label = $"{kind} asc{ascension} {spec.Type.Name}.{move.Id}{tracked}";
            MonsterModel monster = TableActor(spec.Type, 0, out Creature actor);
            // 1000 DP, or a full shell when the maximum is lower (GoldHopper's 10).
            decimal initialDp = Math.Min(1000m, HighDimensionalShellPower.CalculateAmount(actor));
            HighDimensionalShellPower shell = Shell(actor, initialDp);
            decimal maximum = (decimal)Prop(shell, "MaxAmount")!;
            if (move.Id == "RUIN_BULLET_MOVE") Effect("ApplyCharge", actor);
            int dpBefore = HeroDpNow;
            FullMaxDp = HeroDp?.MaxDp ?? 0;
            SimulatePassThrough = passed;
            MoveOf(monster, move.Id).PerformMove(new[] { Hero }).GetAwaiter().GetResult();
            SimulatePassThrough = false;

            int damage = ascension == 0 ? move.Damage0 : move.Damage10;
            int hits = ascension == 0 ? move.Hits0 : move.Hits10;
            if (damage > 0)
            {
                Check(Attacks.Count == 1 && Attacks[0] == (spec.Type, damage, hits), $"{label} attacks {damage} x{hits}");
                string scope = ScopeOf(monster, move.Id);
                Check(AttackScopes.SequenceEqual([scope]), $"{label} attacks {scope} as the original target");
            }
            else
                Check(Attacks.Count == 0, label + " does not attack");
            Check(Blocks.Count == 0 && Extra.Count == 0 && SingleDamage.Count == 0 && actor.GetPowerAmount<StrengthPower>() == 0,
                label + " gains no block or Strength and adds no extra damage");
            ExpectDebuffs(kind, move, ascension, actor, label);
            ExpectStatus(kind, move, ascension, passed, actor, dpBefore, label);
            if (move.Id == "RUIN_BULLET_MOVE")
                Check(!actor.HasPower<HbrEnemyChargePower>(), label + " spends its Charge");
            // Absorb: the DP and HP the hits took (simulated), x4 with HBR numerics, x0.5 otherwise.
            decimal absorbed = move.Status == "Absorb" && passed
                ? decimal.Floor(SimulatedPassedDamage * (kind == HeroKind.Hbr ? 4m : 0.5m))
                : 0m;
            decimal expectedDp = move.HealRatio > 0
                ? Math.Min(maximum, initialDp + decimal.Ceiling(maximum * Math.Min(1m, move.HealRatio)))
                : Math.Min(maximum, initialDp + absorbed);
            Equal(shell.CurrentDp, expectedDp, move.HealRatio > 0
                ? $"{label} restores {move.HealRatio:P1} of its maximum DP"
                : absorbed > 0 ? $"{label} restores {absorbed} DP from {SimulatedPassedDamage} damage dealt ({kind})"
                : label + " keeps its DP");
            Cases.Add(new { tableMove = spec.Type.Name, move = move.Id, kind = kind.ToString(), ascension, passed,
                attacks = Attacks.Select(a => new[] { a.Damage, a.Hits }).ToArray(),
                heroPowers = Hero.Powers.Select(p => $"{p.GetType().Name}:{p.Amount}").ToArray(),
                actorPowers = actor.Powers.Select(p => $"{p.GetType().Name}:{p.Amount}").ToArray(),
                heroDp = HeroDpNow, dp = shell.CurrentDp });
        }
        // The BigotryGateDefault_01 copy heals its own share.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel victory = TableActor(typeof(BigotryGate), 2, out Creature victoryActor);
        HighDimensionalShellPower victoryShell = Shell(victoryActor, 1000m);
        decimal victoryMax = (decimal)Prop(victoryShell, "MaxAmount")!;
        MoveOf(victory, "HEAL_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
        Equal(victoryShell.CurrentDp, Math.Min(victoryMax, 1000m + decimal.Ceiling(victoryMax * 5000m / 7300m)),
            "BigotryGateDefault_01 heals 5000/7300 of its maximum DP");
        // Break blocks the heal: no shell, nothing restored.
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel broken = TableActor(typeof(PillarGuardian), 0, out Creature brokenActor);
        MoveOf(broken, "HEAL_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
        Check(!brokenActor.HasPower<HighDimensionalShellPower>(), "a heal during Break (no shell) restores nothing");

        // Third-batch variants with their own numbers.
        foreach ((Type type, int variant, string move, decimal ratio) in new[]
        {
            (typeof(Marionette), 1, "RECOVERY_MOVE", 600m / 2000m),
            (typeof(CatHorn), 1, "HEAL_MOVE", 700m / 3000m),
            (typeof(TriClaw), 1, "HEAL_MOVE", 5000m / 3200m),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            MonsterModel healer = TableActor(type, variant, out Creature healerActor);
            HighDimensionalShellPower own = Shell(healerActor, 1000m);
            decimal ownMax = (decimal)Prop(own, "MaxAmount")!;
            MoveOf(healer, move).PerformMove(new[] { Hero }).GetAwaiter().GetResult();
            Equal(own.CurrentDp, Math.Min(ownMax, 1000m + decimal.Ceiling(ownMax * Math.Min(1m, ratio))),
                $"{type.Name} variant {variant} {move} restores {ratio:P1} of its maximum DP");
        }
        foreach (int ascension in new[] { 0, 10 })
        {
            ResetTable(HeroKind.Hbr, ascension);
            MonsterModel claw = TableActor(typeof(TriClaw), 1, out _);
            MoveOf(claw, "CLAWS_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
            Check(Attacks.SequenceEqual([(typeof(TriClaw), 4, 5)]), $"asc{ascension} TriClawDefault's claws hit 4 x5 on both difficulties");
        }
        // A monster left on its own cycle keeps the native target list.
        ResetTable(HeroKind.Hbr, 0);
        Creature saki = Actor(typeof(TezukaSaki));
        SetUp(saki.Monster!);
        MoveOf(saki.Monster!, "THREE_GUN_FRENZY_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
        Check(Attacks.Count == 1 && AttackScopes.SequenceEqual(["Players"]),
            "a monster outside the move tables (TezukaSaki) still attacks the player creatures only");
    }

    // ---- status settlement ----------------------------------------------------------

    static decimal EnemyHit(Creature dealer, decimal raw = 10m) =>
        (decimal)FixtureCall("Preview", Hero, dealer, raw, ValueProp.Move, null)!;
    static decimal HeroHit(Creature target, decimal raw = 10m) =>
        (decimal)FixtureCall("Preview", target, Hero, raw, ValueProp.Move, null)!;

    static void Near(decimal actual, decimal expected, string label) =>
        Check(Math.Abs(actual - expected) <= 1m, $"{label}: {actual} ~ {expected}");

    static decimal EffectPercent(PowerModel power) =>
        (decimal)Static("HBRGOSakiMod.Core.Helpers.HbrPercentDebuffHelper", "GetEffectPercent", power)!;

    static decimal Spirit(Creature creature)
    {
        object?[] args = [creature, null];
        bool has = (bool)Invoke(ModType("HBRGOSakiMod.Core.Helpers.HbrBattleStatsHelper").GetMethod("TryGetStats", F)!, null, args)!;
        return has ? (decimal)Prop(args[1]!, "Spirit")! : -1m;
    }

    static void StatusSettlementCases()
    {
        foreach (HeroKind kind in HeroKinds)
        {
            // Attack Up: the final damage of the holder's attacks (and its intent).
            ResetTable(kind, 0);
            TableActor(typeof(PillarGuardian), 0, out Creature pillarActor);
            decimal plain = EnemyHit(pillarActor);
            Effect<HbrAttackUpPower>("ApplyTimedBuff", pillarActor, 100);
            decimal doubled = EnemyHit(pillarActor);
            Near(doubled, plain * (1m + ExpectedEnemyBuff(100m, pillarActor, kind) / 100m), $"{kind} Attack Up 100% doubles the attack ({plain} -> {doubled})");
            Effect<HbrAttackUpPower>("ApplyTimedBuff", pillarActor, 20);
            Check(pillarActor.Powers.OfType<HbrAttackUpPower>().Count() == 2 &&
                pillarActor.Powers.OfType<HbrAttackUpPower>().Sum(EffectStrength) == ExpectedEnemyBuff(120m, pillarActor, kind),
                $"{kind} two independent Attack Up snapshots add their scaled strengths");
            ResetTable(kind, 0);
            TableActor(typeof(BigotryGate), 0, out Creature gateActor);
            plain = EnemyHit(gateActor);
            Effect<HbrAttackUpPower>("ApplyTimedBuff", gateActor, 20);
            Near(EnemyHit(gateActor), plain * (1m + ExpectedEnemyBuff(20m, gateActor, kind) / 100m), $"{kind} Attack Up20 follows the retained enemy-parameter scaling");
            HbrAttackUpPower up = gateActor.GetPower<HbrAttackUpPower>()!;
            Await(up.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
            Check(up.Amount == 2, $"{kind} Attack Up ignores the other side's turn end");
            Await(up.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [gateActor]));
            Check(up.Amount == 1 && gateActor.HasPower<HbrAttackUpPower>(), $"{kind} Attack Up survives the turn it was cast");
            Await(up.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [gateActor]));
            Check(!gateActor.HasPower<HbrAttackUpPower>(), $"{kind} Attack Up ends with the holder's next turn");

            // Defense Up: HBR adds -75% to the defense-down sum; legacy keeps a separate factor.
            ResetTable(kind, 0);
            TableActor(typeof(ShrimpCrab), 0, out Creature crab);
            decimal open = HeroHit(crab, 100m);
            Effect<HbrDefenseUpPower>("ApplyTimedBuff", crab, 75);
            Near(HeroHit(crab, 100m), open * (1m - ExpectedEnemyBuff(75m, crab, kind) / 100m), $"{kind} Steel Shield uses retained enemy-parameter strength");
            Await(ApplyDefenseDown(crab));
            PowerModel down = crab.Powers.Single(p => p is HbrDefenseDownPower or VulnerablePower);
            decimal expected = down is HbrDefenseDownPower
                ? open * (1m + EffectPercent(down) / 100m - ExpectedEnemyBuff(75m, crab, kind) / 100m)
                : open * 1.5m * 0.25m;
            Near(HeroHit(crab, 100m), expected, down is HbrDefenseDownPower
                ? $"{kind} Steel Shield and defense down add up"
                : $"{kind} Steel Shield multiplies with Vulnerable");

            // Spirit Down: HBR halves spirit; without attributes the hit grows by a quarter.
            ResetTable(kind, 0);
            MonsterModel gate = TableActor(typeof(BigotryGate), 0, out Creature impulse);
            Set(gate, "<NextMove>k__BackingField", MoveOf(gate, "STRONG_IMPULSE_MOVE"));
            decimal before = EnemyHit(impulse, 19m);
            decimal spirit = Spirit(Hero);
            Effect<HbrSpiritDownPower>("ApplyStatRateDown", new[] { Hero }, impulse);
            decimal after = EnemyHit(impulse, 19m);
            if (kind == HeroKind.Hbr)
            {
                Equal(Spirit(Hero), spirit * 0.5m, "HBR Spirit Down halves the hero's spirit");
                Check(after > before, $"HBR spirit-biased Strong Impulse hits harder after Spirit Down ({before} -> {after})");
            }
            else
                Near(after, before * 1.25m, $"{kind} Spirit Down adds 25% to attack damage taken");

            // Seal: during the holder's turn, cards costing 2 or more (X included) cannot be played.
            ResetTable(kind, 0);
            TableActor(typeof(ShrimpCrab), 0, out Creature sealer);
            Effect("ApplyImprison", new[] { Hero }, sealer);
            var strike = OwnedCardOf<StrikeIronclad>();
            var bash = OwnedCardOf<Bash>();
            var whirlwind = OwnedCardOf<Whirlwind>();
            bool Playable(CardModel card, AutoPlayType type = AutoPlayType.None) =>
                Hook.ShouldPlay(Combat, card, out _, type);
            Check(Playable(strike) && !Playable(bash) && !Playable(whirlwind) && Playable(bash, AutoPlayType.Default),
                $"{kind} Seal blocks playing 2-cost and X-cost cards by hand, not 1-cost or automatic plays");
            HbrImprisonPower seal = Hero.GetPower<HbrImprisonPower>()!;
            if (kind == HeroKind.Hbr)
            {
                Await(seal.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [sealer]));
                Await(seal.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
                Check(Hero.HasPower<HbrImprisonPower>() && !Playable(bash), "HBR seal covers the player's following turn");
                Await(seal.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [sealer]));
            }
            else Await(seal.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
            Check(!Hero.HasPower<HbrImprisonPower>() && Playable(bash), $"{kind} Seal ends on its selected clock");

            // Bind / Stun: two energy less when the next turn starts, once.
            foreach (Type lockType in new[] { typeof(HbrBindPower), typeof(HbrStunPower) })
            {
                ResetTable(kind, 0);
                TableActor(typeof(DiamondEyeball), 0, out Creature binder);
                MethodInfo apply = Effects.GetMethod("ApplyActionLock", F)!.MakeGenericMethod(lockType);
                Await(Invoke(apply, null, new[] { Hero }, binder));
                Await(Invoke(apply, null, new[] { Hero }, binder));
                PowerModel[] locks = Hero.Powers.Where(p => p.GetType() == lockType).ToArray();
                Check(locks.Length == (kind == HeroKind.Hbr ? 2 : 1), $"{kind} {lockType.Name} retains selected instance policy");
                foreach (PowerModel lockPower in locks) Await(lockPower.AfterEnergyReset(HeroPlayer));
                decimal lockCost = kind == HeroKind.Hbr ? 2m : 1m;
                Check(EnergyLosses.Count == 1 && EnergyLosses[0] == (lockCost, HeroPlayer),
                    $"{kind} {lockType.Name} costs {lockCost} energy once despite repeated applications");
                if (kind == HeroKind.Hbr)
                {
                    foreach (PowerModel lockPower in locks)
                    {
                        if (lockPower is HbrStunPower) Await(lockPower.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [binder], Combat));
                        else for (int tick = 0; tick < 2; tick++) Await(lockPower.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [binder]));
                    }
                }
                Check(Hero.Powers.All(p => p.GetType() != lockType), $"{kind} action lock expires on its matching clock");
            }

            // Continuous DP damage: 5% of the maximum each turn, DP only, four turns.
            ResetTable(kind, 0);
            TableActor(typeof(PillarGuardian), 0, out Creature drainer);
            Effect("ApplyContinuousDpDamage", new[] { Hero }, drainer);
            if (kind == HeroKind.Original)
                Check(!Hero.HasPower<HbrContinuousDpDamagePower>(), "an original character has no DP to drain");
            else
            {
                int max = HeroDp!.MaxDp, share = Math.Max(1, max / 20);
                int hp = Hero.CurrentHp;
                HbrContinuousDpDamagePower drain = Hero.GetPower<HbrContinuousDpDamagePower>()!;
                for (int turn = 1; turn <= 4; turn++)
                    Await(drain.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero], Combat));
                Equal(HeroDp.Dp, max - 4 * share, $"{kind} four turns drain 4 x 5% of {max} DP");
                Check(!Hero.HasPower<HbrContinuousDpDamagePower>() && Hero.CurrentHp == hp,
                    $"{kind} the drain ends after four turns and never touches HP");
                Effect("ApplyContinuousDpDamage", new[] { Hero }, drainer);
                Set(HeroDp, "_dp", 0);
                Await(Hero.GetPower<HbrContinuousDpDamagePower>()!.BeforeSideTurnStart(
                    new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero], Combat));
                Check(HeroDp.Dp == 0 && Hero.CurrentHp == hp, $"{kind} no DP, nothing drained");
            }

            // Illusion: each layer makes the next attack card's damage miss (its
            // other effects stay); a layer is spent per attack card played.
            ResetTable(kind, 0);
            TableActor(typeof(TargetWireFrameKiller), 0, out Creature spinner);
            var strikeCard = OwnedCardOf<StrikeIronclad>();
            var skillCard = OwnedCardOf<DefendIronclad>();
            decimal cardHit = CardHit(spinner, strikeCard), plainHit = HeroHit(spinner);
            Check(cardHit > 0m, $"{kind} baseline: the attack card deals {cardHit}");
            Effect("ApplyIllusion", new[] { Hero }, 1, spinner);
            Check(CardHit(spinner, strikeCard) == 0m && HeroHit(spinner) == plainHit,
                $"{kind} Illusion makes the attack card miss and leaves other damage alone");
            HbrIllusionPower illusion = Hero.GetPower<HbrIllusionPower>()!;
            Await(illusion.AfterCardPlayed(new ThrowingPlayerChoiceContext(), Play(skillCard)));
            Check(IllusionLayers(Hero) == 1, $"{kind} a skill card spends no Illusion layer");
            Await(illusion.AfterCardPlayed(new ThrowingPlayerChoiceContext(), Play(strikeCard)));
            Check(!Hero.HasPower<HbrIllusionPower>() && CardHit(spinner, strikeCard) == cardHit,
                $"{kind} the missed attack card spends the layer; the next one hits");
            Effect("ApplyIllusion", new[] { Hero }, 1, spinner);
            Effect("ApplyIllusion", new[] { Hero }, 2, spinner);
            Effect("ApplyIllusion", new[] { Hero }, 1, spinner);
            illusion = Hero.GetPower<HbrIllusionPower>()!;
            if (kind == HeroKind.Hbr)
            {
                HbrIllusionPower[] copies = Hero.Powers.OfType<HbrIllusionPower>().ToArray();
                Check(copies.Length == 3 && IllusionLayers(Hero) == 4, "HBR Illusion keeps each application and adds its layers");
                foreach (HbrIllusionPower copy in copies) Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [spinner]));
                foreach (HbrIllusionPower copy in copies) Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
                Check(IllusionLayers(Hero) == 4 && copies.All(copy => copy.Amount == 1), "HBR Illusion counts player ends and survives the first player turn");
                foreach (HbrIllusionPower copy in copies) Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
            }
            else
            {
                Check(IllusionLayers(Hero) == 2 && illusion.RemainingTurns == 2, $"{kind} legacy Illusion retains maximum layers and refresh");
                Await(illusion.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [spinner]));
                Await(illusion.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
                Check(IllusionLayers(Hero) == 2 && illusion.RemainingTurns == 1, $"{kind} legacy Illusion counts holder turns");
                Await(illusion.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero]));
            }
            Check(!Hero.HasPower<HbrIllusionPower>(), $"{kind} Illusion expires on its selected clock");

            // Confusion: the first single-target attack card each turn picks among
            // the enemies and the holder. Hitting the holder, the card's printed
            // damage is dealt to them as the confusing enemy's attack.
            ResetTable(kind, 0);
            TableActor(typeof(PileGolemEdge), 0, out Creature golem);
            using (UseRunRng("hbr-confusion-" + kind))
            {
                Effect("ApplyConfusion", new[] { Hero }, golem);
                HbrConfusionPower confusion = Hero.GetPower<HbrConfusionPower>()!;
                Equal(confusion.Amount, 3, $"{kind} Confusion lasts 3 turns");
                var areaCard = OwnedCardOf<Whirlwind>();
                decimal golemHit = CardHit(golem, strikeCard);
                int selfHits = 0, wrong = 0;
                for (int turn = 0; turn < 400; turn++)
                {
                    Await(confusion.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, [Hero], Combat));
                    if (Call(confusion, "ChooseCardTarget", areaCard, golem) != golem || confusion.UsedThisTurn) wrong++;
                    Creature chosen = (Creature)Call(confusion, "ChooseCardTarget", strikeCard, golem)!;
                    bool self = Field(confusion, "_selfHitCard") == strikeCard;
                    if (chosen != golem || !confusion.UsedThisTurn) wrong++;
                    if (self)
                    {
                        selfHits++;
                        SingleDamage.Clear();
                        if (CardHit(golem, strikeCard) != 0m) wrong++;
                        Await(confusion.BeforeCardPlayed(Play(strikeCard)));
                        if (SingleDamage.Count != 1 || SingleDamage[0] != (Hero, 6m, ValueProp.Move, golem)) wrong++;
                        Await(confusion.AfterCardPlayed(new ThrowingPlayerChoiceContext(), Play(strikeCard)));
                        if (Field(confusion, "_selfHitCard") != null || CardHit(golem, strikeCard) != golemHit) wrong++;
                    }
                    else if (CardHit(golem, strikeCard) != golemHit) wrong++;
                    // A second single-target attack card that turn is not confused.
                    if (Call(confusion, "ChooseCardTarget", strikeCard, golem) != golem || Field(confusion, "_selfHitCard") != null) wrong++;
                }
                Check(wrong == 0, $"{kind} Confusion leaves area cards alone, confuses one card per turn, and a self-hit stops the card's damage and deals its printed 6 to the holder as the golem's attack");
                Check(selfHits is >= 170 and <= 230, $"{kind} Confusion hits the holder in {selfHits}/400 turns with one enemy (1 in 2)");
                CombatSide endSide = CombatSide.Player;
                Await(confusion.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), endSide, endSide == CombatSide.Player ? [Hero] : [golem]));
                Equal(confusion.Amount, 2, $"{kind} Confusion uses its selected clock");
                Effect("ApplyConfusion", new[] { Hero }, golem);
                var copies = Hero.Powers.OfType<HbrConfusionPower>().ToArray();
                Check(copies.Length == (kind == HeroKind.Hbr ? 2 : 1), $"{kind} Confusion preserves original instance policy");
                for (int turn = 0; turn < 3; turn++)
                    foreach (HbrConfusionPower copy in copies.Where(copy => copy.Owner != null))
                        Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), endSide, endSide == CombatSide.Player ? [Hero] : [golem]));
                Check(!Hero.HasPower<HbrConfusionPower>(), $"{kind} Confusion expires independently");
            }

            // OD down: a share of one Overclock level (100 charge per level).
            ResetTable(kind, 0, overdrive: OdBefore);
            TableActor(typeof(CrowHorse), 0, out Creature horse);
            if (kind == HeroKind.Original)
            {
                LowerOverdrive(new[] { Hero }, 5m, horse);
                Check(HeroOd == null && Hero.Powers.Count == 0, "an original character has no Overclock gauge to lower");
            }
            else
            {
                LowerOverdrive(new[] { Hero }, 5m, horse);
                Equal(HeroOd!.Charge, OdBefore - 5, $"{kind} OD down takes 5% of a level");
                LowerOverdrive(new[] { Hero }, 10m, horse);
                Equal(HeroOd.Charge, OdBefore - 15, $"{kind} hard OD down takes 10%");
                Set(HeroOd, "_charge", 3);
                LowerOverdrive(new[] { Hero }, 5m, horse);
                Equal(HeroOd.Charge, 0, $"{kind} OD down stops at an empty gauge");
                Set(HeroOd, "_charge", OdBefore);
                LowerOverdrive(new[] { PhantomActor(typeof(SmallHopper)) }, 5m, horse);
                Equal(HeroOd.Charge, OdBefore, $"{kind} a summon has no gauge: OD down on it leaves its owner's alone");
            }

            // Absorb: DP back from the damage dealt, x4 with HBR numerics and x0.5
            // otherwise; without a shell (Break) nothing is restored.
            ResetTable(kind, 0);
            TableActor(typeof(IvoryScooper), 0, out Creature absorber);
            HighDimensionalShellPower drainShell = Shell(absorber, 1000m);
            decimal drainMax = (decimal)Prop(drainShell, "MaxAmount")!;
            decimal rate = kind == HeroKind.Hbr ? 4m : 0.5m;
            decimal restored = AbsorbDp(absorber, 50m);
            Check(restored == decimal.Floor(50m * rate) && drainShell.CurrentDp == Math.Min(drainMax, 1000m + restored),
                $"{kind} Absorb restores {rate}x the 50 damage dealt ({restored})");
            TableActor(typeof(IvoryScooper), 0, out Creature brokenAbsorber);
            Equal(AbsorbDp(brokenAbsorber, 50m), 0m, $"{kind} Absorb restores nothing without a shell (Break)");

            // Virus: the maximum DP is 30% lower for two of the holder's turns; DP
            // above it is lost and does not come back when the maximum recovers.
            ResetTable(kind, 0);
            TableActor(typeof(OctopusTail), 0, out Creature octopusTail);
            if (kind == HeroKind.Original)
            {
                Effect("ApplyVirus", new[] { Hero }, octopusTail);
                Check(!Hero.HasPower<HbrVirusPower>(), "an original character has no DP for Virus to lower");
            }
            else
            {
                int fullMax = HeroDp!.MaxDp, lowered = (int)Math.Floor(fullMax * 0.7m);
                Set(HeroDp, "_dp", fullMax);
                Effect("ApplyVirus", new[] { Hero }, octopusTail);
                HbrVirusPower virus = Hero.GetPower<HbrVirusPower>()!;
                Check(HeroDp.MaxDp == lowered && HeroDp.Dp == lowered,
                    $"{kind} Virus lowers the maximum DP to {lowered} of {fullMax}; DP above it is lost");
                Effect("ApplyVirus", new[] { Hero }, octopusTail);
                var copies = Hero.Powers.OfType<HbrVirusPower>().ToArray();
                int actualLow = (int)decimal.Floor(fullMax * (kind == HeroKind.Hbr ? 0.4m : 0.7m));
                Check(copies.Length == (kind == HeroKind.Hbr ? 2 : 1) && HeroDp.MaxDp == actualLow,
                    $"{kind} Virus uses independent summed HBR copies or legacy refresh");
                CombatSide endSide = CombatSide.Player;
                for (int tick = 0; tick < 2; tick++)
                    foreach (HbrVirusPower copy in copies.Where(copy => copy.Owner != null))
                        Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), endSide, endSide == CombatSide.Player ? [Hero] : [octopusTail]));
                Check(!Hero.HasPower<HbrVirusPower>() && HeroDp.MaxDp == fullMax && HeroDp.Dp == actualLow,
                    $"{kind} Virus restores maximum without refunding lost DP");
            }

            // Wisdom Down: HBR intelligence -25% (block follows it); without
            // attributes, block from cards -25%. Three of the holder's turns.
            ResetTable(kind, 0);
            TableActor(typeof(MolArms), 0, out Creature molArms);
            decimal intelligence = Intelligence(Hero);
            Effect<HbrWisdomDownPower>("ApplyStatRateDown", new[] { Hero }, molArms);
            HbrWisdomDownPower wisdom = Hero.GetPower<HbrWisdomDownPower>()!;
            Equal(wisdom.Amount, 3, $"{kind} Wisdom Down lasts 3 turns");
            decimal cardBlock = wisdom.ModifyBlockMultiplicative(Hero, 10m, ValueProp.Move, OwnedCardOf<DefendIronclad>(), null);
            if (kind == HeroKind.Hbr)
            {
                Equal(Intelligence(Hero), intelligence * 0.2m, "HBR Wisdom Down retains the user-selected eighty percent loss");
                Equal(cardBlock, 1m, "HBR Wisdom Down does not cut card block a second time");
            }
            else
                Check(cardBlock == 0.75m &&
                    wisdom.ModifyBlockMultiplicative(Hero, 10m, ValueProp.Move | ValueProp.Unpowered, null, null) == 1m &&
                    wisdom.ModifyBlockMultiplicative(molArms, 10m, ValueProp.Move, null, null) == 1m,
                    $"{kind} Wisdom Down cuts the holder's card block by a quarter and nothing else");
            Effect<HbrWisdomDownPower>("ApplyStatRateDown", new[] { Hero }, molArms);
            var wisdomCopies = Hero.Powers.OfType<HbrWisdomDownPower>().ToArray();
            Check(wisdomCopies.Length == 2 && wisdomCopies.All(copy => copy.Amount == 3),
                $"{kind} existing original None wisdom policy keeps both independent durations");
            for (int turn = 0; turn < 3; turn++)
                foreach (HbrWisdomDownPower copy in wisdomCopies)
                    Await(copy.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [molArms]));
            Check(!Hero.HasPower<HbrWisdomDownPower>(), $"{kind} Wisdom Down ends after three enemy ends");
        }
        RestoreHero();
    }

    static decimal Intelligence(Creature creature)
    {
        object?[] args = [creature, null];
        bool has = (bool)Invoke(ModType("HBRGOSakiMod.Core.Helpers.HbrBattleStatsHelper").GetMethod("TryGetStats", F)!, null, args)!;
        return has ? (decimal)Prop(args[1]!, "Intelligence")! : -1m;
    }

    // Fills an enemy's shell to its maximum.
    static HighDimensionalShellPower FullShell(Creature actor)
    {
        HighDimensionalShellPower shell = Shell(actor, 1m);
        decimal maximum = (decimal)Prop(shell, "MaxAmount")!;
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData))!
            .SetValue(shell, maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return shell;
    }

    static decimal CardHit(Creature target, CardModel card, decimal raw = 10m) =>
        (decimal)FixtureCall("Preview", target, Hero, raw, ValueProp.Move, card)!;

    static CardPlay Play(CardModel card)
    {
        var play = Blank<CardPlay>();
        Set(play, "<Card>k__BackingField", card);
        Set(play, "<PlayIndex>k__BackingField", 0);
        Set(play, "<PlayCount>k__BackingField", 1);
        return play;
    }

    static object? Field(object owner, string name)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, F | BindingFlags.DeclaredOnly) is { } field) return field.GetValue(owner);
        throw new MissingFieldException(owner.GetType().FullName, name);
    }

    static void LowerOverdrive(IEnumerable<Creature> targets, decimal percent, Creature applier) =>
        Invoke(Effects.GetMethod("LowerOverdrive", F)!, null, targets, percent, applier);

    static decimal AbsorbDp(Creature attacker, decimal passed) =>
        (decimal)Invoke(Effects.GetMethod("AbsorbDp", F)!, null, attacker, passed)!;

    static AttackCommand AttackBy(Creature attacker) => new AttackCommand(10m).FromMonster(attacker.Monster!);

    // The run's synced RNG, for the statuses that roll (Confusion); the fixture has none.
    static IDisposable UseRunRng(string seed)
    {
        object run = State<object>("RunState");
        Set(run, "<Rng>k__BackingField", new RunRngSet(seed));
        return new Restore(() => Set(run, "<Rng>k__BackingField", null));
    }

    sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    static Task ApplyDefenseDown(Creature target) =>
        (Task)Invoke(ModType("HBRGOSakiMod.Core.Helpers.HbrPercentDebuffApplication").GetMethods(F)
            .Single(m => m.Name == "ApplyDefenseDown" && m.GetParameters()[1].ParameterType == typeof(Creature)),
            null, new ThrowingPlayerChoiceContext(), target, 1m, Hero, null, false)!;

    static T OwnedCardOf<T>() where T : CardModel, new()
    {
        var card = new T();
        Set(card, "<IsMutable>k__BackingField", true);
        Set(card, "_owner", HeroPlayer);
        return card;
    }

    // ---- phantom side ---------------------------------------------------------------

    static Creature Enemy(decimal dp = 0m)
    {
        Creature enemy = Actor(typeof(SmallHopper));
        enemy.Monster!.SetUpForCombat();
        enemy.Monster.SetMoveImmediate(enemy.Monster.MoveStateMachine!.States.Values.OfType<MoveState>().First(),
            forceTransition: true);
        if (dp > 0m) Shell(enemy, dp);
        return enemy;
    }

    static void PhantomStatusCases()
    {
        MethodInfo after = ModType("HBRGOSakiMod.Core.Models.Monsters.IHbrgoPhantomAttackResults").GetMethod("AfterPhantomAttack", F)!;
        void Replay(Creature phantom, string move, Creature[] attacked, Creature[] passed, decimal passedDamage = 0m) =>
            Await(Invoke(after, phantom.Monster, MoveOf(phantom.Monster!, move), attacked, passed, passedDamage));

        foreach (bool hbr in new[] { true, false })
        {
            HeroKind kind = hbr ? HeroKind.Hbr : HeroKind.Legacy;
            // Laser Bind / Paralysis Burst: a stunned turn, once per enemy per combat, past block only.
            foreach ((Type type, string move) in new[] { (typeof(DiamondEyeball), "LASER_BIND_MOVE"),
                (typeof(PillarGuardian), "PARALYSIS_BURST_MOVE") })
            {
                ResetTable(kind, 0);
                Creature phantom = PhantomActor(type);
                Creature blocked = Enemy(), hit = Enemy();
                string original = hit.Monster!.NextMove.Id;
                Replay(phantom, move, [blocked, hit], [hit]);
                Check(hit.Monster.NextMove.Id == "STUNNED" && hit.Monster.NextMove.FollowUpStateId == original &&
                    blocked.Monster!.NextMove.Id != "STUNNED",
                    $"HBR{hbr} phantom {type.Name}.{move} stuns the enemy it hit past block, then resumes its move");
                hit.Monster.SetMoveImmediate((MoveState)hit.Monster.MoveStateMachine!.States[original], forceTransition: true);
                Replay(phantom, move, [hit], [hit]);
                Check(hit.Monster.NextMove.Id == original, $"HBR{hbr} phantom {type.Name}.{move} stuns an enemy once per combat");
                Check(!hit.Powers.Any(p => p is HbrActionLockPower) && !Hero.Powers.Any(p => p is HbrActionLockPower),
                    $"HBR{hbr} phantom {type.Name}.{move} gives no energy status");
            }
            // Prism Ray / Disaster Shot / Strong Impulse / Pillar Strike on the attacked enemy.
            ResetTable(kind, 0);
            Creature eye = PhantomActor(typeof(DiamondEyeball));
            Creature target = Enemy(dp: 500m);
            // Same shell as the target: legacy DP still cuts damage by 25%.
            decimal untouched = HeroHit(Enemy(dp: 500m), 100m);
            Replay(eye, "PRISM_RAY_MOVE", [target], []);
            Check(hbr ? target.Powers.Any(p => p is HbrDefenseDownPower) : target.GetPowerAmount<VulnerablePower>() == 2,
                $"HBR{hbr} phantom Prism Ray lowers the enemy's defense even when blocked");
            Creature gate = PhantomActor(typeof(BigotryGate));
            Replay(gate, "DISASTER_SHOT_MOVE", [target], []);
            Check(hbr ? target.GetPowerAmount<HbrCalamityPower>() == 20 : !target.HasPower<HbrCalamityPower>(),
                hbr ? "HBR phantom Disaster Shot lowers the enemy's E by 20" : "legacy phantom Disaster Shot applies no Calamity");
            Replay(gate, "STRONG_IMPULSE_MOVE", [target], []);
            Check(target.GetPowerAmount<HbrSpiritDownPower>() == 2, $"HBR{hbr} phantom Strong Impulse gives the enemy Spirit Down");
            if (!hbr)
                Near(HeroHit(target, 100m), untouched * 1.25m * 1.5m,
                    "legacy the enemy with Spirit Down and Vulnerable takes x1.25 x1.5");
            else
                Check(HeroHit(target, 100m) > untouched, "HBR the enemy with Spirit Down, defense down and Calamity takes more");
            Creature pillar = PhantomActor(typeof(PillarGuardian));
            Creature shellless = Enemy();
            Replay(pillar, "PILLAR_STRIKE_MOVE", [target, shellless], []);
            Check(target.GetPowerAmount<HbrContinuousDpDamagePower>() == 4 && !shellless.HasPower<HbrContinuousDpDamagePower>(),
                $"HBR{hbr} phantom Pillar Strike drains a shelled enemy only");
            HighDimensionalShellPower targetShell = target.GetPower<HighDimensionalShellPower>()!;
            decimal shellDp = targetShell.CurrentDp, maxShell = (decimal)Prop(targetShell, "MaxAmount")!;
            Await(target.GetPower<HbrContinuousDpDamagePower>()!.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(),
                CombatSide.Enemy, [target], Combat));
            Equal(targetShell.CurrentDp, Math.Max(0m, shellDp - Math.Max(1m, decimal.Floor(maxShell * 0.05m))),
                $"HBR{hbr} the drain takes 5% of the enemy's maximum shell");

            // Non-attack moves performed by a phantom against enemies.
            ResetTable(kind, 0);
            Creature needle = PhantomActor(typeof(NeedleBird));
            Creature shelled = Enemy(dp: 600m);
            Effect("ApplyCharge", needle);
            Check(needle.HasPower<HbrEnemyChargePower>(), $"HBR{hbr} a phantom charges itself");
            MoveOf(needle.Monster!, "RUIN_BULLET_MOVE").PerformMove(new[] { shelled }).GetAwaiter().GetResult();
            Equal(shelled.GetPower<HighDimensionalShellPower>()!.CurrentDp, 300m, $"HBR{hbr} phantom Ruin Bullet halves the enemy's DP");
            Check(!needle.HasPower<HbrEnemyChargePower>() && shelled.Monster!.NextMove.Id != "STUNNED",
                $"HBR{hbr} phantom Ruin Bullet spends its Charge without breaking the shell");
            Creature first = Enemy(dp: 600m), second = Enemy(dp: 600m);
            Effect("ApplyCharge", needle);
            MoveOf(needle.Monster!, "RUIN_BULLET_MOVE").PerformMove(new[] { first, second }).GetAwaiter().GetResult();
            Check(new[] { first, second }.Count(e => e.GetPower<HighDimensionalShellPower>()!.CurrentDp == 300m) == 1,
                $"HBR{hbr} phantom Ruin Bullet picks one enemy");

            Creature crab = PhantomActor(typeof(ShrimpCrab));
            Creature busy = Enemy(), attacker = Enemy();
            busy.Monster!.SetMoveImmediate(new MoveState("BUFF_ONLY", _ => Task.CompletedTask, new BuffIntent())
                { FollowUpStateId = busy.Monster.NextMove.Id }, forceTransition: true);
            attacker.Monster!.SetMoveImmediate(new MoveState("PLAIN_ATTACK", _ => Task.CompletedTask, new SingleAttackIntent(5)),
                forceTransition: true);
            MoveOf(crab.Monster!, "MIRAGE_PULSE_MOVE").PerformMove(new[] { busy }).GetAwaiter().GetResult();
            MoveOf(crab.Monster!, "MIRAGE_PULSE_MOVE").PerformMove(new[] { attacker }).GetAwaiter().GetResult();
            Check(busy.Monster.NextMove.Id == "STUNNED" && attacker.Monster.NextMove.Id == "PLAIN_ATTACK",
                $"HBR{hbr} phantom Mirage Pulse skips an enemy's move unless it is a plain single attack");
            Check(busy.Powers.Any(p => p is HbrAttackDownPower or WeakPower) &&
                busy.Powers.Any(p => p is HbrDefenseDownPower or VulnerablePower) && !busy.HasPower<HbrImprisonPower>(),
                $"HBR{hbr} phantom Mirage Pulse also lowers the enemy's attack and defense");

            MoveOf(crab.Monster!, "STEEL_SHIELD_MOVE").PerformMove(new[] { busy }).GetAwaiter().GetResult();
            Creature gatePhantom = PhantomActor(typeof(BigotryGate));
            MoveOf(gatePhantom.Monster!, "ATTACK_UP_MOVE").PerformMove(new[] { busy }).GetAwaiter().GetResult();
            Check(EffectStrength(crab.GetPower<HbrDefenseUpPower>()!) == 75m && EffectStrength(gatePhantom.GetPower<HbrAttackUpPower>()!) == 20m &&
                !busy.HasPower<HbrDefenseUpPower>() && !busy.HasPower<HbrAttackUpPower>(),
                $"HBR{hbr} phantom Steel Shield and Attack Up buff the phantom itself");

            // Second batch. Illusion and Confusion: on enemies whose damage passed block.
            ResetTable(kind, 0, overdrive: OdBefore);
            Creature spinner = PhantomActor(typeof(TargetWireFrameKiller));
            Creature dazed = Enemy(), guarded = Enemy();
            decimal enemyRaw = EnemyHit(dazed);
            Replay(spinner, "ILLUSION_SPIN_MOVE", [guarded, dazed], [dazed]);
            Check(IllusionLayers(dazed) == 1 && !guarded.HasPower<HbrIllusionPower>(),
                $"HBR{hbr} phantom Illusion Spin dazes only the enemy it hit past block");
            Check(EnemyHit(dazed) == 0m && EnemyHit(guarded) == enemyRaw, $"HBR{hbr} the dazed enemy's next attack misses");
            Await(dazed.GetPower<HbrIllusionPower>()!.AfterAttack(new ThrowingPlayerChoiceContext(), AttackBy(dazed)));
            Check(!dazed.HasPower<HbrIllusionPower>() && EnemyHit(dazed) == enemyRaw,
                $"HBR{hbr} the enemy's missed attack spends its Illusion layer");
            Replay(PhantomActor(typeof(GiraffeHalo)), "JUDGMENT_RAY_MOVE", [guarded, dazed], [dazed]);
            Check(IllusionLayers(dazed) == 2 && IllusionLayers(guarded) == 2,
                $"HBR{hbr} phantom Judgment Ray gives 2 Illusion layers to every attacked enemy, blocked or not (it always lands in the original)");

            Creature golemPhantom = PhantomActor(typeof(PileGolemEdge));
            Creature confused = Enemy(), calm = Enemy();
            Replay(golemPhantom, "CONFUSION_SPIKE_MOVE", [calm, confused], [confused]);
            Check(confused.HasPower<HbrConfusionPower>() && !calm.HasPower<HbrConfusionPower>(),
                $"HBR{hbr} phantom Confusion Spike confuses only the enemy it hit past block");
            confused.Monster!.SetMoveImmediate(new MoveState("PLAIN_ATTACK", _ => Task.CompletedTask, new SingleAttackIntent(7)),
                forceTransition: true);
            decimal selfDamage = hbr ? 7m : (decimal)Invoke(ModType("HBRGOSakiMod.Core.Helpers.HbrResourceValueHelper")
                .GetMethod("Fixed", F, [typeof(Creature), typeof(decimal)])!, null, confused, 7m)!;
            using (UseRunRng("hbr-enemy-confusion-" + hbr))
            {
                int selfHits = 0, wrong = 0;
                for (int attack = 0; attack < 200; attack++)
                {
                    Effect("ApplyConfusion", new[] { confused }, golemPhantom);
                    HbrConfusionPower power = confused.GetPower<HbrConfusionPower>()!;
                    AttackCommand command = AttackBy(confused);
                    SingleDamage.Clear();
                    Await(power.BeforeAttack(command));
                    bool self = EnemyHit(confused) == 0m;
                    if (!self && EnemyHit(confused) != enemyRaw) wrong++;
                    Await(power.AfterAttack(new ThrowingPlayerChoiceContext(), command));
                    if (self) selfHits++;
                    if (self ? SingleDamage.Count != 1 || SingleDamage[0] != (confused, selfDamage, ValueProp.Unpowered, null)
                            : SingleDamage.Count != 0)
                        wrong++;
                    if (confused.HasPower<HbrConfusionPower>()) wrong++;
                }
                Check(wrong == 0, $"HBR{hbr} a confused enemy's next attack either lands normally or misses and deals its intent ({selfDamage}) to itself, then Confusion ends");
                Check(selfHits is >= 75 and <= 125, $"HBR{hbr} a confused enemy hits itself in {selfHits}/200 attacks (1 in 2)");
            }

            // OD down: the summoner gains the share once per replay; attack/defense down on every attacked enemy.
            Replay(PhantomActor(typeof(CrowHorse)), "DESTRUCTION_BALL_MOVE", [guarded, calm], []);
            Equal(HeroOd!.Charge, OdBefore + 5, $"HBR{hbr} phantom Destruction Ball gives the summoner 5% of an OD level once, however many it hit");
            Creature serpent = PhantomActor(typeof(SlimySerpent));
            Replay(serpent, "DESTRUCTION_RUSH_MOVE", [guarded, calm], []);
            Equal(HeroOd.Charge, OdBefore + 10, $"HBR{hbr} phantom Destruction Rush gives another 5%");
            Creature weakened = Enemy(), exposed = Enemy(), pierced = Enemy();
            Replay(serpent, "DISEASE_PLANT_MOVE", [weakened], []);
            Check(hbr ? weakened.Powers.Any(p => p is HbrAttackDownPower) : weakened.GetPowerAmount<WeakPower>() == 2,
                $"HBR{hbr} phantom Disease Plant lowers the enemy's attack even when blocked");
            Replay(PhantomActor(typeof(BarrelWalker)), "RAGE_MAGNUM_MOVE", [exposed], []);
            Check(hbr ? exposed.Powers.Any(p => p is HbrDefenseDownPower) : exposed.GetPowerAmount<VulnerablePower>() == 1,
                $"HBR{hbr} phantom Rage Magnum lowers every attacked enemy's defense");
            Replay(PhantomActor(typeof(ArachneLine)), "PIERCING_FLASH_MOVE", [pierced], []);
            Check(hbr ? pierced.Powers.Any(p => p is HbrDefenseDownPower) : pierced.GetPowerAmount<VulnerablePower>() == 2,
                $"HBR{hbr} phantom Piercing Flash lowers every attacked enemy's defense");

            // Absorb: the phantom's own shell, from the DP and HP the replay took.
            foreach ((Type type, string move) in new[] { (typeof(IvoryScooper), "ENERGY_DRAIN_MOVE"),
                (typeof(ArachneLine), "ABSORB_WHIP_MOVE") })
            {
                Creature drainer = PhantomActor(type);
                HighDimensionalShellPower own = Shell(drainer, 100m);
                decimal ownMax = (decimal)Prop(own, "MaxAmount")!;
                Replay(drainer, move, [dazed], [dazed], 20m);
                Equal(own.CurrentDp, Math.Min(ownMax, 100m + decimal.Floor(20m * (hbr ? 4m : 0.5m))),
                    $"HBR{hbr} phantom {type.Name}.{move} restores its own DP from the 20 damage dealt");
                Replay(drainer, move, [dazed], [], 0m);
                Equal(own.CurrentDp, Math.Min(ownMax, 100m + decimal.Floor(20m * (hbr ? 4m : 0.5m))),
                    $"HBR{hbr} phantom {type.Name}.{move} restores nothing when block stopped every hit");
            }

            // Third batch. Creepy Ball's seal and Illusion Spin's illusion past block only.
            ResetTable(kind, 0);
            Creature sealedEnemy = Enemy(), openEnemy = Enemy();
            foreach (Creature enemy in new[] { sealedEnemy, openEnemy })
                enemy.Monster!.SetMoveImmediate(new MoveState("BUFF_ONLY", _ => Task.CompletedTask, new BuffIntent())
                    { FollowUpStateId = enemy.Monster.NextMove.Id }, forceTransition: true);
            Replay(PhantomActor(typeof(CatHorn)), "CREEPY_BALL_MOVE", [openEnemy, sealedEnemy], [sealedEnemy]);
            Check(sealedEnemy.Monster!.NextMove.Id == "STUNNED" && openEnemy.Monster!.NextMove.Id == "BUFF_ONLY",
                $"HBR{hbr} phantom Creepy Ball skips the non-attack move of the enemy it hit past block only");
            Creature spun = Enemy(), stopped = Enemy();
            Replay(PhantomActor(typeof(TargetWireFrame)), "ILLUSION_SPIN_MOVE", [stopped, spun], [spun]);
            Check(IllusionLayers(spun) == 1 && !stopped.HasPower<HbrIllusionPower>(),
                $"HBR{hbr} phantom Illusion Spin (α) dazes only the enemy it hit past block");

            // Erosion Slice: Virus on a shelled enemy hit past block; its shell maximum drops 30%.
            Creature infected = Enemy(), resisted = Enemy(), broken = Enemy();
            HighDimensionalShellPower infectedShell = FullShell(infected);
            FullShell(resisted);
            decimal shellMax = (decimal)Prop(infectedShell, "MaxAmount")!, shellLow = decimal.Floor(shellMax * 0.7m);
            Replay(PhantomActor(typeof(OctopusTail)), "EROSION_SLICE_MOVE", [resisted, infected, broken], [infected, broken]);
            Check(infected.GetPowerAmount<HbrVirusPower>() == 2 && !resisted.HasPower<HbrVirusPower>() &&
                !broken.HasPower<HbrVirusPower>(),
                $"HBR{hbr} phantom Erosion Slice infects the shelled enemy it hit past block, not the blocked one or one in Break");
            Check((decimal)Prop(infectedShell, "MaxAmount")! == shellLow && infectedShell.CurrentDp == shellLow,
                $"HBR{hbr} Virus lowers the enemy's shell maximum to {shellLow} of {shellMax}; DP above it is lost");
            infectedShell.Restore(shellMax);
            Equal(infectedShell.CurrentDp, shellLow, $"HBR{hbr} an infected shell cannot be restored above the lowered maximum");
            HbrVirusPower enemyVirus = infected.GetPower<HbrVirusPower>()!;
            Await(enemyVirus.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [infected]));
            Await(enemyVirus.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, [infected]));
            Check(!infected.HasPower<HbrVirusPower>() && (decimal)Prop(infectedShell, "MaxAmount")! == shellMax &&
                infectedShell.CurrentDp == shellLow,
                $"HBR{hbr} when the enemy's Virus ends its shell maximum recovers but the lost DP does not");

            // Deceive Shot, Curse Stamp and Rotation Cut land on every attacked enemy.
            Creature dimmed = Enemy(), stamped = Enemy(), cut = Enemy();
            Replay(PhantomActor(typeof(MolArms)), "DECEIVE_SHOT_MOVE", [dimmed], []);
            HbrWisdomDownPower dim = dimmed.GetPower<HbrWisdomDownPower>()!;
            Check(dim.Amount == 3 && dim.ModifyBlockMultiplicative(dimmed, 10m, ValueProp.Move, null, null) == (hbr ? 0.2m : 0.75m),
                $"HBR{hbr} phantom Deceive Shot gives the attacked enemy Wisdom Down: block from its moves -25%, blocked or not");
            Replay(PhantomActor(typeof(MarionetteKiller)), "CURSE_STAMP_MOVE", [stamped], []);
            Check(hbr ? stamped.GetPowerAmount<HbrCalamityPower>() == 20 : !stamped.HasPower<HbrCalamityPower>(),
                hbr ? "HBR phantom Curse Stamp lowers the enemy's E by 20" : "legacy phantom Curse Stamp applies no Calamity");
            Replay(PhantomActor(typeof(CorollaKnife)), "ROTATION_CUT_MOVE", [cut], []);
            Check(hbr ? cut.Powers.Any(p => p is HbrDefenseDownPower) : cut.GetPowerAmount<VulnerablePower>() == 2,
                $"HBR{hbr} phantom Rotation Cut lowers the attacked enemy's defense even when blocked");
        }
        // PhantomHelper hands replayed attacks to move-table monsters with their hit and passed targets.
        Type phantomHelper = ModType("HBRGOSakiMod.Core.Helpers.PhantomHelper");
        Check(phantomHelper.GetNestedTypes(F).SelectMany(t => t.GetMethods(F)).Concat(phantomHelper.GetMethods(F))
                .Any(m => CallsMethod(m, after)), "PhantomHelper's intent replay calls IHbrgoPhantomAttackResults.AfterPhantomAttack");
        RestoreHero();
    }

    static bool CallsMethod(MethodBase method, MethodInfo target)
    {
        byte[]? il;
        try { il = method.GetMethodBody()?.GetILAsByteArray(); }
        catch (Exception) { return false; }
        if (il == null) return false;
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F) continue;
            try
            {
                if (method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)) == target) return true;
            }
            catch (Exception) { }
        }
        return false;
    }

    // ---- all-unit and lowest-DP targets -----------------------------------------------

    static void AllUnitTargetCases()
    {
        var harmony = new Harmony("hbr.act-rosters.targets");
        foreach (string patch in new[] { "HbrAttackTargetingPatch", "HotaruCannonTargetabilityPatch" })
            Check(harmony.CreateClassProcessor(ModType("HBRGOSakiMod.Core.Patches.Combat." + patch)).Patch().Count == 1,
                "installs production " + patch);
        MethodInfo possible = typeof(AttackCommand).GetMethod("GetPossibleTargets", F)!;
        Type scopes = ModType("HBRGOSakiMod.Core.Models.Monsters.HbrgoAttackTargets");
        MethodInfo mark = ModType("HBRGOSakiMod.Core.Helpers.HbrAttackTargeting").GetMethod("Set", F)!;
        List<Creature> Targets(Creature attacker, string scope)
        {
            AttackCommand attack = AttackBy(attacker);
            if (scope != "") Invoke(mark, null, attack, Enum.Parse(scopes, scope));
            return ((IReadOnlyList<Creature>)Invoke(possible, attack)!).ToList();
        }
        try
        {
            ResetTable(HeroKind.Hbr, 0, dp: 300);
            TableActor(typeof(CrowHorse), 0, out Creature horse);
            Creature phantom = PhantomActor(typeof(SmallHopper));
            var osty = (Creature)FixtureCall("Actor", Blank(typeof(MegaCrit.Sts2.Core.Models.Monsters.Osty)), null, HeroPlayer)!;
            var cannon = (Creature)FixtureCall("Actor", Blank(typeof(HotaruHappinessCannon)), null, HeroPlayer)!;
            Creature fallen = PhantomActor(typeof(SmallHopper));
            Set(fallen, "_currentHp", 0);
            Set(Combat, "_allies", new List<Creature> { Hero, phantom, osty, cannon, fallen });
            Check(Targets(horse, "").SequenceEqual([Hero]), "a native monster attack targets the player creatures only");
            Check(Targets(horse, "Players").SequenceEqual([Hero]), "the Players scope keeps the native list");
            Check(Targets(horse, "AllUnits").SequenceEqual([Hero, phantom, osty]),
                "an all-unit attack also hits the player side's living summons (a 30A phantom, Osty), never a Hotaru cannon");
            Check(!cannon.IsHittable && osty.IsHittable, "Hotaru cannons are not hittable; other summons are");

            // MinimumDp: the living player with the least Deflector Protection DP.
            Player second = Blank<Player>();
            Set(second, "_runState", State<object>("RunState"));
            var ally = (Creature)FixtureCall("Actor", null, second, null)!;
            Set(second, "<Creature>k__BackingField", ally);
            var allyDp = Blank<DeflectorProtection>();
            Set(allyDp, "<IsMutable>k__BackingField", true);
            Set(allyDp, "_owner", second);
            Set(second, "_relics", new List<RelicModel> { allyDp });
            Set(HeroDp!, "_dp", 300);
            Set(allyDp, "_dp", 100);
            Set(Combat, "_allies", new List<Creature> { Hero, phantom, ally });
            Check(Targets(horse, "MinimumDp").SequenceEqual([ally]), "MinimumDp hits the player with less DP (100 of 300)");
            Set(allyDp, "_dp", 400);
            Check(Targets(horse, "MinimumDp").SequenceEqual([Hero]), "MinimumDp follows the current DP (300 of 400)");
            Check(Targets(horse, "AllUnits").SequenceEqual([Hero, ally, phantom]), "an all-unit attack hits both players and the summon");
            Set(second, "_relics", new List<RelicModel>());
            Check(Targets(horse, "MinimumDp").SequenceEqual([ally]), "a player without Deflector Protection counts as 0 DP");
            Set(ally, "_currentHp", 0);
            Check(Targets(horse, "MinimumDp").SequenceEqual([Hero]), "MinimumDp skips a dead player");
        }
        finally
        {
            Set(Combat, "_allies", new List<Creature> { Hero });
            harmony.UnpatchAll(harmony.Id);
        }

        // The move-table monsters' all-unit and lowest-DP moves on both sides.
        MethodInfo moveTarget = typeof(HbrgoMoveTableMonster).GetMethod("MoveTargetOf", F)!;
        foreach ((Type type, string move, string target) in new[]
        {
            (typeof(NeedleBird), "MURDER_BLADE_MOVE", "All"), (typeof(DiamondEyeball), "DISASTER_RAY_MOVE", "All"),
            (typeof(BigotryGate), "STRONG_IMPULSE_MOVE", "All"), (typeof(PillarGuardian), "PARALYSIS_BURST_MOVE", "All"),
            (typeof(CrowHorse), "EXTINCTION_RAY_MOVE", "All"), (typeof(BarrelWalker), "HEAVY_CANNON_MOVE", "MinimumDp"),
            (typeof(CrowHorse), "TRAMPLE_MOVE", "Single"), (typeof(PileGolemEdge), "ATTACK_UP_MOVE", "Self"),
            (typeof(Knocker), "ALL_ATTACK_MOVE", "All"), (typeof(ClayKnocker), "ALL_ATTACK_MOVE", "All"),
            (typeof(AbyssKnocker), "RAGE_WAVE_MOVE", "All"), (typeof(HealHopper), "REPAIR_BETA_MOVE", "AllyAll"),
            (typeof(HealHopper), "REPAIR_OMEGA_MOVE", "AllyAll"), (typeof(GoldHopper), "ESCAPE_MOVE", "Self"),
        })
            Check(Invoke(moveTarget, null, Blank(type), move)?.ToString() == target, $"{type.Name}.{move} targets {target}");
        Check(Invoke(moveTarget, null, Blank(typeof(TezukaSaki)), "THREE_GUN_FRENZY_MOVE") == null,
            "a monster outside the move tables (TezukaSaki) has no table target");
        Type phantomHelper = ModType("HBRGOSakiMod.Core.Helpers.PhantomHelper");
        MethodInfo lowest = phantomHelper.GetMethod("LowestDpTarget", F)!;
        ResetTable(HeroKind.Hbr, 0);
        Creature thick = Enemy(dp: 500m), thin = Enemy(dp: 200m), broken = Enemy();
        Check(Invoke(lowest, null, new object?[] { new[] { thick, thin } }) == thin, "a phantom's lowest-DP move picks the enemy with the least shell DP");
        Check(Invoke(lowest, null, new object?[] { new[] { thick, thin, broken } }) == broken, "an enemy in Break (no shell) counts as 0 DP");
        MethodInfo[] replay = phantomHelper.GetNestedTypes(F).SelectMany(t => t.GetMethods(F))
            .Concat(phantomHelper.GetMethods(F)).ToArray();
        Check(replay.Any(m => CallsMethod(m, moveTarget)) && replay.Any(m => CallsMethod(m, lowest)),
            "PhantomHelper's intent replay reads each move's table target and picks the lowest-DP enemy");

        // The real replay damage loop (single-target damage commands recorded).
        MethodInfo performOn = phantomHelper.GetMethod("PerformAttackIntentOn", F)!;
        MethodInfo perform = phantomHelper.GetMethod("PerformAttackIntent", F)!;
        Check(perform.GetParameters().Length == 5, "the original five-argument PerformAttackIntent is kept for its callers");
        IReadOnlyList<Creature> RunReplay(MethodInfo method, Creature phantom, Creature[] targets, decimal[] segments,
            Creature? preferred, params object?[] extra) =>
            ((Task<IReadOnlyList<Creature>>)Invoke(method, null,
                new object?[] { phantom, targets, segments, 0, preferred }.Concat(extra).ToArray())!).GetAwaiter().GetResult();
        foreach (bool hbr in new[] { true, false })
        {
            ResetTable(hbr ? HeroKind.Hbr : HeroKind.Legacy, 0);
            Creature sweeper = PhantomActor(typeof(CrowHorse));
            Creature first = Enemy(), second = Enemy();
            SingleDamage.Clear();
            IReadOnlyList<Creature> swept = RunReplay(performOn, sweeper, [first, second], [5m, 7m], second, true);
            Check(swept.SequenceEqual([first, second]) && SingleDamage.Select(d => (d.Target, d.Amount, d.Dealer))
                    .SequenceEqual([(first, 5m, sweeper), (second, 5m, sweeper), (first, 7m, sweeper), (second, 7m, sweeper)]),
                $"HBR{hbr} a phantom's all-unit replay hits every enemy with each segment, even with a preferred target");
            SingleDamage.Clear();
            IReadOnlyList<Creature> single = RunReplay(performOn, sweeper, [first, second], [5m, 7m], second, false);
            Check(single.SequenceEqual([second]) && SingleDamage.Select(d => (d.Target, d.Amount))
                    .SequenceEqual([(second, 5m), (second, 7m)]),
                $"HBR{hbr} a single-target replay hits only its chosen enemy");
            SingleDamage.Clear();
            IReadOnlyList<Creature> original = RunReplay(perform, sweeper, [first, second], [5m], first);
            Check(original.SequenceEqual([first]) && SingleDamage.Select(d => d.Target).SequenceEqual([first]),
                $"HBR{hbr} the original replay entry point is unchanged (single target)");
        }
        RestoreHero();
    }

    // ---- block pass-through record -----------------------------------------------------

    static void PassThroughCases()
    {
        MethodInfo postfix = ModType("HBRGOSakiMod.Core.Patches.Combat.CreatureDamageBlockInternalPatch").GetMethod("Postfix", F)!;
        bool Passed(Creature receiver, decimal amount, decimal blocked, bool state, Creature probe)
        {
            object scope = Invoke(Tracker.GetMethod("Begin", F)!, null)!;
            try
            {
                object?[] args = [receiver, amount, ValueProp.Move, blocked, state];
                Invoke(postfix, null, args);
                return (bool)Invoke(scope.GetType().GetMethod("Passed", F)!, scope, probe)!;
            }
            finally { ((IDisposable)scope).Dispose(); }
        }
        foreach (HeroKind kind in HeroKinds)
        {
            ResetTable(kind, 0, dp: 300);
            Check(!Passed(Hero, 10m, 10m, true, Hero), $"{kind} a hit stopped by block is not recorded");
            Check(Passed(Hero, 10m, 4m, true, Hero), $"{kind} a hit past block is recorded, into DP or HP");
            Check(!Passed(Hero, 10m, 4m, false, Hero), $"{kind} a redirected or HP-only block call is not recorded here");
        }
        ResetTable(HeroKind.Hbr, 0);
        Creature pet = (Creature)FixtureCall("Actor", Blank(typeof(SmallHopper)), null, HeroPlayer)!;
        Check(Passed(pet, 10m, 2m, true, pet) && !Passed(pet, 10m, 2m, true, Hero),
            "a summon hit directly (an all-unit attack) records only itself");
        // A redirected hit (30A's phantom block): the phantom's block call runs
        // inside the summoner's, so both are credited with the damage past block.
        MethodInfo enter = Tracker.GetMethod("EnterBlock", F)!, exit = Tracker.GetMethod("ExitBlock", F)!;
        object redirect = Invoke(Tracker.GetMethod("Begin", F)!, null)!;
        try
        {
            Invoke(enter, null, Hero);
            Invoke(enter, null, pet);
            Invoke(postfix, null, new object?[] { pet, 10m, ValueProp.Move, 2m, true });
            Invoke(exit, null, Hero);
            decimal Amount(Creature probe) => (decimal)Invoke(redirect.GetType().GetMethod("Amount", F)!, redirect, probe)!;
            Check(Amount(Hero) == 8m && Amount(pet) == 8m,
                "a phantom taking its summoner's hit records the summoner too, with the 8 damage past block");
        }
        finally { ((IDisposable)redirect).Dispose(); }
        object counted = Invoke(Tracker.GetMethod("Begin", F)!, null)!;
        try
        {
            RecordPassThrough(Hero, 6m);
            RecordPassThrough(Hero, 4m);
            Check((decimal)Invoke(counted.GetType().GetMethod("Amount", F)!, counted, Hero)! == 10m,
                "the damage past block adds up over an attack's hits");
        }
        finally { ((IDisposable)counted).Dispose(); }
        object outer = Invoke(Tracker.GetMethod("Begin", F)!, null)!;
        ((IDisposable)outer).Dispose();
        RecordPassThrough(Hero, 5m);
        Check(!(bool)Invoke(outer.GetType().GetMethod("Passed", F)!, outer, Hero)!, "nothing is recorded outside a scope");
        RestoreHero();
    }

    // ---- encounters -------------------------------------------------------------------

    static void EncounterVariantCases()
    {
        static (Type, int[]) Of(Type type, params int[] variants) => (type, variants);
        MonsterModel[] Lineup(Type encounter)
        {
            Ascension = 0;
            var model = (EncounterModel)Blank(encounter);
            Set(model, "<IsMutable>k__BackingField", true);
            return ((IEnumerable<(MonsterModel, string)>)Invoke(encounter.GetMethod("GenerateMonsters", F)!, model)!)
                .Select(row => row.Item1).ToArray();
        }
        foreach ((Type encounter, (Type Type, int[] Variants)[] expected) in new (Type, (Type, int[])[])[]
        {
            (typeof(BigotryGateWeak), [Of(typeof(BigotryGate), 0)]),
            (typeof(BigotryGateSquadNormal), [Of(typeof(BigotryGate), 0), Of(typeof(TriClaw), 0)]),
            (typeof(BigotryGateAndCorollaKnivesNormal), [Of(typeof(BigotryGate), 0), Of(typeof(CorollaKnife), 0, 1)]),
            (typeof(BigotryGatesNormal), [Of(typeof(BigotryGate), 0, 1)]),
            (typeof(BigotryGateAndMarionetteKillersNormal), [Of(typeof(BigotryGate), 2), Of(typeof(MarionetteKiller), 1, 1)]),
            (typeof(ShrimpCrabsElite), [Of(typeof(ShrimpCrab), 0, 1)]),
            (typeof(MarionetteWeak), [Of(typeof(Marionette), 1)]),
            (typeof(MarionettesWithHealerNormal), [Of(typeof(Marionette), 0, 0), Of(typeof(HealHopper), 0)]),
            (typeof(MarionetteAndCatHornsNormal), [Of(typeof(Marionette), 0), Of(typeof(CatHorn), 0, 0)]),
            (typeof(CatHornsNormal), [Of(typeof(CatHorn), 1, 1)]),
            (typeof(MarionetteKillersWeak), [Of(typeof(MarionetteKiller), 0, 0)]),
            (typeof(KillerTrainingSquadNormal), [Of(typeof(MarionetteKiller), 0), Of(typeof(HealHopper), 1)]),
            (typeof(MolArmsAndTriClawNormal), [Of(typeof(MolArms), 0), Of(typeof(TriClaw), 1)]),
            (typeof(TriClawAndBalbFleasNormal), [Of(typeof(TriClaw), 0)]),
            (typeof(CorollaKnivesNormal), [Of(typeof(CorollaKnife), 0, 1)]),
            (typeof(CorollaKnifeSquadNormal), [Of(typeof(CorollaKnife), 0), Of(typeof(TriClaw), 0)]),
            (typeof(MolArmsPairNormal), [Of(typeof(MolArms), 0, 0)]),
            // fourth batch
            (typeof(OctopusTailSquadNormal), [Of(typeof(HealHopper), 0)]),
            (typeof(TargetWireFrameSquadNormal), [Of(typeof(HealHopper), 0)]),
            (typeof(KnockersWithHealerNormal), [Of(typeof(HealHopper), 0), Of(typeof(Knocker), 0, 0)]),
            (typeof(CrestHopperWithHealersNormal), [Of(typeof(HealHopper), 0, 0)]),
            (typeof(CatHornKillerWithHealersNormal), [Of(typeof(HealHopper), 1, 1)]),
            (typeof(DarkKnockerKillerSquadNormal), [Of(typeof(HealHopper), 1)]),
        })
        {
            MonsterModel[] lineup = Lineup(encounter);
            foreach ((Type type, int[] variants) in expected)
                Check(lineup.Where(m => m.GetType() == type).Select(m => ((HbrgoMoveTableMonster)m).MoveTableVariant)
                    .SequenceEqual(variants), $"{encounter.Name} uses {type.Name} variants [{string.Join(",", variants)}]");
        }

        // Two MolArms: the second starts at step 2, step 1's "once" spent, so the
        // loop's boosted all-unit attacks fall one turn apart.
        HbrgoMoveTableMonster[] arms = Lineup(typeof(MolArmsPairNormal)).Cast<HbrgoMoveTableMonster>().ToArray();
        Check(arms[0].MoveTableStep == 0 && arms[0].DpOnceUsedMask == 0 && arms[1].MoveTableStep == 1 &&
            arms[1].DpOnceUsedMask == 1, "MolArmsPairNormal starts the second MolArms at step 2 with step 1 spent");
        string[] lead = Walk(typeof(MolArms), 0, 8, 14);
        string[] next = Walk(typeof(MolArms), 0, 8, 14, m => Call(m, "StartDpTableAt", 1));
        Check(next[2] == "ATTACK_UP_MOVE" && lead[3] == "ATTACK_UP_MOVE" &&
            Enumerable.Range(6, 8).All(i => (lead[i] == "ALL_ATTACK_MOVE") == ((i - 7) % 3 == 0) &&
                (next[i] == "ALL_ATTACK_MOVE") == ((i - 6) % 3 == 0)),
            "the two MolArms power up and fire the all-unit attack one turn apart: " +
            string.Join(",", lead.Skip(5).Take(6)) + " / " + string.Join(",", next.Skip(5).Take(6)));

        // Same-type monsters in one fight start one dp step apart: the fourth
        // batch, then (user, 2026-11-08) the 17 earlier fights as well.
        static int[] Steps(MonsterModel[] lineup, Type type) =>
            lineup.Where(m => m.GetType() == type).Select(m => ((HbrgoMoveTableMonster)m).MoveTableStep).ToArray();
        foreach ((Type encounter, Type type, int[] steps) in new[]
        {
            (typeof(CatHornsNormal), typeof(CatHorn), new[] { 0, 1 }),
            (typeof(MarionettesWithHealerNormal), typeof(Marionette), new[] { 0, 1 }),
            (typeof(MarionetteAndCatHornsNormal), typeof(CatHorn), new[] { 0, 1 }),
            (typeof(OctopusTailsNormal), typeof(OctopusTail), new[] { 0, 1 }),
            (typeof(TargetWireFramesNormal), typeof(TargetWireFrame), new[] { 0, 1 }),
            (typeof(WireSlashersNormal), typeof(WireSlasher), new[] { 0, 1, 2 }),
            (typeof(MarionetteKillersWeak), typeof(MarionetteKiller), new[] { 0, 1 }),
            (typeof(CrestHopperKillerAndMarionettesNormal), typeof(MarionetteKiller), new[] { 0, 1 }),
            (typeof(CrestHopperKillersNormal), typeof(CrestHopperKiller), new[] { 0, 1, 2 }),
            (typeof(DarkKnockerKillersNormal), typeof(DarkKnockerKiller), new[] { 0, 1 }),
            (typeof(DarkKnockerKillerAndMarionettesNormal), typeof(MarionetteKiller), new[] { 0, 1 }),
            (typeof(CatHornKillersNormal), typeof(CatHornKiller), new[] { 0, 1 }),
            (typeof(OctopusTailKillersNormal), typeof(OctopusTailKiller), new[] { 0, 1 }),
            (typeof(MarionetteKillerAndCatHornsNormal), typeof(CatHornKiller), new[] { 0, 1 }),
            (typeof(TargetWireFrameKillerElite), typeof(CrestHopperKiller), new[] { 0, 1 }),
            (typeof(TriClawAndBalbFleasNormal), typeof(BalbFlea), new[] { 0, 1 }),
            (typeof(BigotryGateAndMarionetteKillersNormal), typeof(MarionetteKiller), new[] { 0, 1 }),
            (typeof(SmallHoppersNormal), typeof(SmallHopper), new[] { 0, 1, 2 }),
            (typeof(CrestHopperSwarmNormal), typeof(CrestHopper), new[] { 0, 1, 2 }),
            (typeof(CrestHopperWithHealersNormal), typeof(HealHopper), new[] { 0, 1 }),
            (typeof(KnockersWithHealerNormal), typeof(Knocker), new[] { 0, 1 }),
            (typeof(CatHornKillerWithHealersNormal), typeof(HealHopper), new[] { 0, 1 }),
            (typeof(OctopusTailSquadNormal), typeof(CrestHopper), new[] { 0 }),
            (typeof(KnockersElite), typeof(Knocker), new[] { 0 }),
        })
            Check(Steps(Lineup(encounter), type).SequenceEqual(steps),
                $"{encounter.Name} starts its {type.Name}s at dp steps [{string.Join(",", steps.Select(s => s + 1))}]");
        Check(Lineup(typeof(CrestHopperSwarmNormal)).Cast<CrestHopper>().All(h => h.IsSwarmMember),
            "the staggered CrestHopper trio keeps the swarm HP range");
        HbrgoMoveTableMonster[] dolls = Lineup(typeof(BigotryGateAndMarionetteKillersNormal))
            .OfType<MarionetteKiller>().Cast<HbrgoMoveTableMonster>().ToArray();
        Check(dolls.All(d => d.MoveTableVariant == 1) && dolls[0].DpOnceUsedMask == 0 &&
            dolls[1].DpOnceUsedMask == 1, "the second cursed doll's spent step 1 is a once step; the first opens with it");
        // The rule itself, over every registered encounter of acts one to three
        // (SmallHoppersWeak's rolled pair is covered below): monsters of one
        // type on one table never share a starting dp step.
        var sharedTables = new List<string>();
        bool apart = true;
        foreach (ActSpec act in Acts)
        foreach (EncounterModel registered in RegistryEncounters(act))
        {
            Type type = registered.GetType();
            if (type == typeof(SmallHoppersWeak)) continue;
            var model = (EncounterModel)Blank(type);
            Set(model, "<IsMutable>k__BackingField", true);
            Set(model, "_rng", new Rng(20261108));
            var groups = ((IEnumerable<(MonsterModel, string)>)Invoke(type.GetMethod("GenerateMonsters", F)!, model)!)
                .Select(row => row.Item1).OfType<HbrgoMoveTableMonster>()
                .GroupBy(m => (m.GetType(), m.MoveTableVariant)).Where(g => g.Count() > 1).ToArray();
            if (groups.Length == 0) continue;
            sharedTables.Add(type.Name);
            apart &= groups.All(g => g.Select(m => m.MoveTableStep).Distinct().Count() == g.Count());
        }
        Check(apart && sharedTables.Count == 23,
            $"in all {sharedTables.Count} registered fights with same-type monsters on one table, they start at different dp steps");
        // SmallHoppersWeak rolls its first monster; a second SmallHopper starts at step 2.
        var openers = new HashSet<Type>();
        bool staggered = true;
        for (uint seed = 1; seed <= 64; seed++)
        {
            var weak = (EncounterModel)Blank(typeof(SmallHoppersWeak));
            Set(weak, "<IsMutable>k__BackingField", true);
            Set(weak, "_rng", new Rng(seed));
            MonsterModel[] pair = ((IEnumerable<(MonsterModel, string)>)Invoke(typeof(SmallHoppersWeak).GetMethod("GenerateMonsters", F)!, weak)!)
                .Select(row => row.Item1).ToArray();
            openers.Add(pair[0].GetType());
            staggered &= pair.Length == 2 && pair[1] is SmallHopper && ((HbrgoMoveTableMonster)pair[0]).MoveTableStep == 0 &&
                ((HbrgoMoveTableMonster)pair[1]).MoveTableStep == (pair[0] is SmallHopper ? 1 : 0);
        }
        Check(openers.SetEquals([typeof(SmallHopper), typeof(HealHopper)]) && staggered,
            "SmallHoppersWeak: a second SmallHopper starts at dp step 2 after a SmallHopper, at step 1 after a HealHopper");
    }

    // ---- fourth batch: the mod's original act-one monsters --------------------------

    static void FourthBatchCases()
    {
        // Openings, loops and weights.
        string[] small = Walk(typeof(SmallHopper), 0, 4, 15);
        Check(Enumerable.Range(0, 15).All(i => (i % 5) switch
            {
                0 or 2 => small[i] == "ATTACK_MOVE",
                1 => small[i] is "ATTACK_MOVE" or "COMBO_MOVE",
                _ => small[i] == "COMBO_MOVE",
            }), "SmallHopper loops attack, attack or combo, attack, combo, combo: " + string.Join(",", small.Take(10)));
        string[] crest = Walk(typeof(CrestHopper), 0, 4, 18);
        Check(Enumerable.Range(0, 18).All(i => (i % 6) switch
            {
                0 or 3 => crest[i] == "ATTACK_MOVE",
                2 => crest[i] is "ATTACK_MOVE" or "COMBO_MOVE",
                _ => crest[i] == "COMBO_MOVE",
            }), "CrestHopper loops attack, combo, attack or combo, attack, combo, combo: " + string.Join(",", crest.Take(12)));
        string[] heal = Walk(typeof(HealHopper), 0, 4, 12), omega = Walk(typeof(HealHopper), 1, 4, 12);
        Check(Enumerable.Range(0, 12).All(i => (i % 3) switch
            {
                0 => heal[i] == "ATTACK_MOVE",
                1 => heal[i] is "ATTACK_MOVE" or "COMBO_MOVE",
                _ => heal[i] == "REPAIR_BETA_MOVE",
            }), "HealHopper (CrestHopperHealVirtual) loops attack, attack or combo, Repair β: " + string.Join(",", heal.Take(6)));
        Check(Enumerable.Range(0, 12).All(i => i % 2 == 0 ? omega[i] == "ATTACK_MOVE" : omega[i] is "ATTACK_MOVE" or "COMBO_MOVE"),
            "HealHopper (CrestHopperHealVirtual_MC03) only attacks while its shell holds: " + string.Join(",", omega.Take(6)));
        string[] knock = Walk(typeof(Knocker), 0, 4, 12);
        Check(Enumerable.Range(0, 12).All(i => (i % 6) switch
            {
                0 or 3 => knock[i] == "ATTACK_MOVE",
                1 => knock[i] is "ATTACK_MOVE" or "MULTI_ATTACK_MOVE",
                _ => knock[i] == "ALL_ATTACK_MOVE",
            }), "Knocker loops attack, attack or double, all-unit, attack, all-unit, all-unit: " + string.Join(",", knock.Take(6)));
        string[] clay = Walk(typeof(ClayKnocker), 0, 4, 14);
        Check(Enumerable.Range(0, 14).All(i => (i % 7) switch
            {
                0 => clay[i] == "ATTACK_MOVE",
                2 => clay[i] is "ATTACK_MOVE" or "MULTI_ATTACK_MOVE",
                _ => clay[i] == "ALL_ATTACK_MOVE",
            }), "ClayKnocker loops attack, all-unit, attack or double, then four all-unit attacks: " + string.Join(",", clay.Take(7)));
        string[] abyss = Walk(typeof(AbyssKnocker), 0, 4, 10);
        Check(Enumerable.Range(0, 10).All(i => (i % 5) switch
            {
                0 => abyss[i] == "ATTACK_MOVE",
                1 or 4 => abyss[i] == "RAGE_WAVE_MOVE",
                _ => abyss[i] is "ATTACK_MOVE" or "MULTI_ATTACK_MOVE",
            }), "AbyssKnocker loops attack, Rage Wave, two attacks or doubles, Rage Wave: " + string.Join(",", abyss.Take(5)));
        Frequency(typeof(SmallHopper), 1, "ATTACK_MOVE", 0.5m, "SmallHopper dp step 2 (50:50)");
        Frequency(typeof(CrestHopper), 2, "ATTACK_MOVE", 0.5m, "CrestHopper dp step 3 (50:50)");
        Frequency(typeof(HealHopper), 1, "ATTACK_MOVE", 0.5m, "HealHopper dp step 2 (50:50)");
        Frequency(typeof(Knocker), 1, "ATTACK_MOVE", 0.5m, "Knocker dp step 2 (50:50)");
        Frequency(typeof(ClayKnocker), 2, "ATTACK_MOVE", 0.5m, "ClayKnocker dp step 3 (50:50)");
        Frequency(typeof(AbyssKnocker), 3, "ATTACK_MOVE", 0.5m, "AbyssKnocker dp step 4 (50:50)");

        // Break: with an hp table the move after a DP break comes from it and the
        // returning shell restarts the dp table; GoldHopper has none and keeps
        // its dp table (no stun since 2026-11-09).
        foreach ((Type type, int variant, string[] hpOpeners) in new[]
        {
            (typeof(SmallHopper), 0, new[] { "ATTACK_MOVE" }),
            (typeof(CrestHopper), 0, new[] { "ATTACK_MOVE" }),
            (typeof(HealHopper), 0, new[] { "ATTACK_MOVE", "COMBO_MOVE" }),
            (typeof(HealHopper), 1, new[] { "REPAIR_OMEGA_MOVE" }),
            (typeof(Knocker), 0, new[] { "ATTACK_MOVE" }),
            (typeof(ClayKnocker), 0, new[] { "MULTI_ATTACK_MOVE" }),
            (typeof(AbyssKnocker), 0, new[] { "RAGE_WAVE_MOVE" }),
        })
        {
            ResetTable(HeroKind.Hbr, 0);
            MonsterModel monster = TableActor(type, variant, out Creature actor);
            var walker = (HbrgoMoveTableMonster)monster;
            MonsterMoveStateMachine walk = Machine(monster);
            var walkRng = new Rng(31);
            Roll(walk, actor, walkRng);
            Check(Call(monster, "OnShellBroken") == null && walker.MoveTableIndex == 1 &&
                hpOpeners.Contains(Roll(walk, actor, walkRng)),
                $"{type.Name} variant {variant} Break opens with hp step 1 ({string.Join(" or ", hpOpeners)})");
            Call(monster, "OnShellApplied");
            Check(walker.MoveTableIndex == 0 && Roll(walk, actor, walkRng) == "ATTACK_MOVE",
                $"{type.Name} variant {variant} restarts its dp table at step 1 when the shell returns");
        }
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel goldBreak = TableActor(typeof(GoldHopper), 0, out _);
        SetProp(goldBreak, nameof(HbrgoMoveTableMonster.MoveTableStep), 1);
        Check(Call(goldBreak, "OnShellBroken") == null &&
            ((HbrgoMoveTableMonster)goldBreak).MoveTableIndex == 0 && ((HbrgoMoveTableMonster)goldBreak).MoveTableStep == 1,
            "GoldHopper (no hp table) keeps its dp table after a DP break, so no stun puts off its escape");

        // GoldHopper as an enemy: the original's HP 1 / DP 10 and 99 Intangible.
        GoldHopperOriginalResourceCases();
        // GoldHopper escapes on the original's timing: 60% on each of its first
        // two turns, surely on the third. Escaping gives no bonus reward.
        Frequency(typeof(GoldHopper), 0, "ATTACK_MOVE", 0.4m, "GoldHopper dp step 1 (attack 40 : escape 60)");
        Frequency(typeof(GoldHopper), 1, "ATTACK_MOVE", 0.4m, "GoldHopper dp step 2 (attack 40 : escape 60)");
        ResetTable(HeroKind.Hbr, 0);
        MonsterModel gold = TableActor(typeof(GoldHopper), 0, out Creature goldActor);
        var goldRng = new Rng(20261110);
        Check(Enumerable.Range(0, 200).All(_ => RollAt(gold, 2, goldRng) == "ESCAPE_MOVE"), "GoldHopper always escapes on its third turn");
        MoveOf(gold, "ESCAPE_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
        Check(Escapes.SequenceEqual([goldActor]) && Attacks.Count == 0 && !((GoldHopper)gold).BonusRewardGranted,
            "GoldHopper's escape leaves the fight without attacking or granting its bonus reward");
        // On the player side (a 30A phantom) the escape rows are skipped.
        ResetTable(HeroKind.Hbr, 0);
        Creature goldPhantom = PhantomActor(typeof(GoldHopper));
        var phantomRng = new Rng(20261111);
        Check(Enumerable.Range(0, 300).All(i => RollAt(goldPhantom.Monster!, i % 3, phantomRng) == "ATTACK_MOVE"),
            "a phantom GoldHopper never rolls its escape and attacks every turn");
        RollAt(goldPhantom.Monster!, 2, phantomRng);
        Equal(((HbrgoMoveTableMonster)goldPhantom.Monster!).MoveTableStep, 1, "the phantom's skipped step 3 rolls on to step 1");

        // Repair β / Ω: every living unit on the healer's side, itself included,
        // regains its share of its own maximum DP; Break and death leave a unit
        // out; no block; the player's DP is untouched.
        foreach (HeroKind kind in HeroKinds)
        {
            ResetTable(kind, 0, dp: 500);
            MonsterModel healer = TableActor(typeof(HealHopper), 0, out Creature healerActor);
            HighDimensionalShellPower own = Shell(healerActor, 100m);
            Creature ally = Actor(typeof(Knocker)), broken = Actor(typeof(Knocker)), dead = Actor(typeof(Knocker));
            HighDimensionalShellPower allyShell = Shell(ally, 100m), brokenShell = Shell(broken, 100m), deadShell = Shell(dead, 100m);
            Power<BreakPower>(broken, 1);
            Set(dead, "_currentHp", 0);
            decimal ownMax = (decimal)Prop(own, "MaxAmount")!, allyMax = (decimal)Prop(allyShell, "MaxAmount")!;
            int heroDp = HeroDpNow;
            MoveOf(healer, "REPAIR_BETA_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
            Equal(own.CurrentDp, Math.Min(ownMax, 100m + decimal.Ceiling(ownMax * 0.1m)), $"{kind} Repair β restores 10% of the healer's own maximum DP");
            Equal(allyShell.CurrentDp, Math.Min(allyMax, 100m + decimal.Ceiling(allyMax * 0.1m)), $"{kind} Repair β restores 10% of an ally's own maximum DP");
            Check(brokenShell.CurrentDp == 100m && deadShell.CurrentDp == 100m, $"{kind} Repair β skips an ally in Break and a dead one");
            Check(HeroDpNow == heroDp && Blocks.Count == 0 && Attacks.Count == 0, $"{kind} Repair β gives no block and leaves the player's DP alone");
            MoveOf(healer, "REPAIR_OMEGA_MOVE").PerformMove(new[] { Hero }).GetAwaiter().GetResult();
            Check(allyShell.CurrentDp == allyMax && own.CurrentDp == ownMax && brokenShell.CurrentDp == 100m,
                $"{kind} Repair Ω fills every ally's DP, not one in Break");
            Cases.Add(new { allyHeal = kind.ToString(), ownMax, allyMax, ally = allyShell.CurrentDp, heroDp = HeroDpNow });

            // A phantom heals its own side: itself, another summon's shell and
            // the players' Deflector Protection; enemies are untouched.
            ResetTable(kind, 0, dp: 500);
            Creature phantom = PhantomActor(typeof(HealHopper)), summon = PhantomActor(typeof(SmallHopper));
            HighDimensionalShellPower phantomShell = Shell(phantom, 100m), summonShell = Shell(summon, 100m);
            Creature enemy = Enemy(dp: 100m);
            Set(Combat, "_allies", new List<Creature> { Hero, phantom, summon });
            try
            {
                int before = HeroDpNow, maxDp = HeroDp?.MaxDp ?? 0;
                decimal phantomMax = (decimal)Prop(phantomShell, "MaxAmount")!, summonMax = (decimal)Prop(summonShell, "MaxAmount")!;
                MoveOf(phantom.Monster!, "REPAIR_BETA_MOVE").PerformMove(new[] { enemy }).GetAwaiter().GetResult();
                Check(phantomShell.CurrentDp == Math.Min(phantomMax, 100m + decimal.Ceiling(phantomMax * 0.1m)) &&
                    summonShell.CurrentDp == Math.Min(summonMax, 100m + decimal.Ceiling(summonMax * 0.1m)),
                    $"{kind} a phantom's Repair β restores 10% to itself and to another summon");
                Check(kind == HeroKind.Original ? HeroDp == null : HeroDpNow == Math.Min(maxDp, before + (int)(maxDp * 0.1m)),
                    kind == HeroKind.Original ? $"{kind} a phantom's Repair β has no DP to restore on an original character"
                        : $"{kind} a phantom's Repair β restores 10% of the summoner's maximum Deflector Protection ({before} -> {HeroDpNow} of {maxDp})");
                Equal(enemy.GetPower<HighDimensionalShellPower>()!.CurrentDp, 100m, $"{kind} a phantom's Repair β leaves the enemy's DP alone");
            }
            finally
            {
                Set(Combat, "_allies", new List<Creature> { Hero });
            }
        }

        // The opening SmallHopper keeps its explicit body-row profile on ATTACK_MOVE.
        Check(ScopeOf(Mutable(typeof(SmallHopper)), "ATTACK_MOVE") == "Players" &&
            ScopeOf(Mutable(typeof(SmallHopper)), "COMBO_MOVE") == "Players",
            "SmallHopper's attack and combo are single-target (player creatures)");
        RestoreHero();
    }

    // The HBR status icons: each file matches its provenance record (hash, size,
    // RGBA, an original BattleIcon asset) and is the icon its power loads. The two
    // older down icons carry the first batch's down arrow since 2026-11-08.
    static void StatusIconCases()
    {
        foreach ((Type power, string file, string asset, bool badged) in new[]
        {
            (typeof(HbrEnemyChargePower), "hbr_enemy_charge_power", "IconCharge", false),
            (typeof(HbrAttackUpPower), "hbr_attack_up_power", "IconBuffAttack", true),
            (typeof(HbrDefenseUpPower), "hbr_defense_up_power", "IconBuffDefense", true),
            (typeof(HbrSpiritDownPower), "hbr_spirit_down_power", "IconSpirit", true),
            (typeof(HbrImprisonPower), "hbr_imprison_power", "IconImprison", false),
            (typeof(HbrBindPower), "hbr_bind_power", "IconBind", false),
            (typeof(HbrStunPower), "hbr_stun_power", "IconStun", false),
            (typeof(HbrContinuousDpDamagePower), "hbr_continuous_dp_damage_power", "IconContinuousDpDamage", false),
            (typeof(HbrAttackDownPower), "hbr_attack_down_power", "IconBuffAttack", true),
            (typeof(HbrDefenseDownPower), "hbr_defense_down_power", "IconBuffDefense", true),
            (typeof(HbrIllusionPower), "hbr_illusion_power", "IconIllusion", false),
            (typeof(HbrConfusionPower), "hbr_confusion_power", "IconConfusion", false),
            (typeof(HbrVirusPower), "hbr_virus_power", "IconVirus", false),
            (typeof(HbrWisdomDownPower), "hbr_wisdom_down_power", "IconWisdom", true),
        })
        {
            string png = Path.Combine(RepositoryRoot, "images/powers", file + ".png");
            using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "images/powers", file + ".sources.json")));
            JsonElement root = record.RootElement;
            (int width, int height, byte color) = Png(png);
            Check(width == 64 && height == 64 && color == 6, $"{file}.png is a 64x64 RGBA icon");
            Check(root.GetProperty("sha256").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(png))).ToLowerInvariant(),
                $"{file}.png matches its provenance hash");
            Check(root.GetProperty("original").GetProperty("assetPath").GetString() == $"Assets/UI/Texture/BattleIcon/{asset}.png",
                $"{file}.png comes from the original {asset}");
            Check(root.GetProperty("transformation").GetString()!.StartsWith("None.") != badged,
                $"{file}.png is {(badged ? "the original with an arrow" : "a byte copy of the original")}");
            string path = (string)power.GetProperty("CustomPackedIconPath")!.GetValue(ModelDb.GetById<PowerModel>(ModelDb.GetId(power)))!;
            Check(path == root.GetProperty("importedPath").GetString() && path == $"res://images/powers/{file}.png",
                $"{power.Name} loads {file}.png");
        }
    }

    // ---- GoldHopper: the original's HP 1 / DP 10 and 99 Intangible ---------------

    // User, 2026-11-08: 「但coldhopper给99层无实体 然后dp和hp跟原作一样」. As an
    // enemy GoldHopper has the original's absolute HP 1 and DP 10
    // (CrestHopperDefaultGold01/02) whatever the mode, enemy HP/DP settings,
    // act, difficulty or party size, and 99 native Intangible, so each hit
    // deals 1: ten hits empty the DP and the eleventh takes the HP. A 30A
    // phantom of it keeps the phantom rules. The effects that get around
    // "1 per hit" are measured here as well (docs/hbr-act-rosters.md).
    static void GoldHopperOriginalResourceCases()
    {
        const string health = "HBRGOSakiMod.Core.Helpers.EnemyHealthScalingHelper";
        const ValueProp HpLoss = ValueProp.Unblockable | ValueProp.Unpowered;
        Type config = typeof(HBRGOSakiMod.Core.Config.HBRGOSakiConfig);
        string[] settingFields = ["_enemyMaxHpPercent", "_enemyMaxHpGrowthPerActPercent",
            "_enemyHighDimensionalShellPercent", "_enemyDpGrowthPerActPercent"];
        object?[] saved = settingFields.Select(name => config.GetField(name, F)!.GetValue(null)).ToArray();
        try
        {
            // The native range is left as it was; it now only feeds a 30A
            // phantom's HP rule (MomentOfReign reads the created creature's HP).
            foreach (int ascension in new[] { 0, 10 })
            {
                Ascension = ascension;
                MonsterModel native = Mutable(typeof(GoldHopper));
                Check(native.MinInitialHp == (ascension == 0 ? 54 : 56) && native.MaxInitialHp == (ascension == 0 ? 58 : 60),
                    $"GoldHopper asc{ascension} keeps its native HP {native.MinInitialHp}-{native.MaxInitialHp} for the phantom rule");
            }
            Ascension = 0;

            // HP and DP: every mode, difficulty, setting, act and party size.
            // Settings: enemy HP %, HP growth per act %, enemy shell %, DP growth per act %.
            (int Hp, int HpGrowth, int Shell, int DpGrowth)[] settings =
                [(100, 0, 20, 0), (300, 50, 100, 50), (50, 0, 0, 0)];
            foreach (HeroKind kind in HeroKinds)
            foreach (int ascension in new[] { 0, 10 })
            foreach (var setting in settings)
            foreach (int act in new[] { 0, 2 })
            foreach (int nativeHp in new[] { 58, 116 })
            {
                config.GetField("_enemyMaxHpPercent", F)!.SetValue(null, setting.Hp);
                config.GetField("_enemyMaxHpGrowthPerActPercent", F)!.SetValue(null, setting.HpGrowth);
                config.GetField("_enemyHighDimensionalShellPercent", F)!.SetValue(null, setting.Shell);
                config.GetField("_enemyDpGrowthPerActPercent", F)!.SetValue(null, setting.DpGrowth);
                ResetTable(kind, ascension);
                FixtureCall("Act", act);
                // 116: the native roll after the two-player HP scaling.
                Creature gold = Actor(typeof(GoldHopper), nativeHp);
                Creature control = Actor(typeof(Knocker), nativeHp);
                Static(health, "ApplyInitialHealth", gold);
                Static(health, "ApplyInitialHealth", control);
                decimal goldDp = HighDimensionalShellPower.CalculateAmount(gold);
                decimal controlDp = HighDimensionalShellPower.CalculateAmount(control);
                string label = $"{kind} asc{ascension} act {act + 1}, enemy HP {setting.Hp}% (+{setting.HpGrowth}%/act), " +
                    $"shell {setting.Shell}% (+{setting.DpGrowth}%/act), native HP {nativeHp}";
                Check(gold.MaxHp == 1 && gold.CurrentHp == 1 && goldDp == 10m,
                    $"GoldHopper {label}: HP 1/1 and DP 10 (a Knocker there: HP {control.MaxHp}, DP {controlDp})");
                if (setting.Hp == 300)
                    Check(control.MaxHp >= nativeHp * 3, $"{label}: the enemy HP setting still scales other enemies ({control.MaxHp})");
                if (setting.Shell == 0)
                    Check(controlDp == 0m, $"{label}: other enemies have no shell at 0%, GoldHopper keeps its 10 DP");
            }
            foreach ((string name, object? value) in settingFields.Zip(saved))
                config.GetField(name, F)!.SetValue(null, value);

            // A 30A phantom of it: the phantom HP and DP rules, no Intangible.
            ResetTable(HeroKind.Hbr, 0);
            Creature goldPhantom = PhantomActor(typeof(GoldHopper)), knockerPhantom = PhantomActor(typeof(Knocker));
            foreach (Creature phantom in new[] { goldPhantom, knockerPhantom })
            {
                Set(phantom, "_maxHp", 500); Set(phantom, "_currentHp", 500);
            }
            Static(health, "ApplyInitialHealth", goldPhantom);
            Await(goldPhantom.Monster!.AfterAddedToRoom());
            decimal phantomDp = HighDimensionalShellPower.CalculateAmount(goldPhantom);
            Check(goldPhantom.MaxHp == 500 && goldPhantom.CurrentHp == 500 &&
                phantomDp == HighDimensionalShellPower.CalculateAmount(knockerPhantom) &&
                !goldPhantom.HasPower<IntangiblePower>() && !((GoldHopper)goldPhantom.Monster!).HasAppliedStartingIntangible,
                $"a phantom GoldHopper keeps the phantom HP (500 stays 500) and DP rule (DP {phantomDp}, as a Knocker phantom) and gets no Intangible");

            foreach (HeroKind kind in HeroKinds)
            {
                // Entry: 99 native Intangible, once (a repeated room callback adds none).
                Creature gold = GoldEnemy(kind, out GoldHopper model, out HighDimensionalShellPower shell);
                Await(model.AfterAddedToRoom());
                Check(gold.GetPowerAmount<IntangiblePower>() == 99 && gold.Powers.Count(p => p is IntangiblePower) == 1 &&
                    model.HasAppliedStartingIntangible,
                    $"{kind} GoldHopper enters as an enemy with 99 native Intangible, applied once");
                IntangiblePower intangible = gold.GetPower<IntangiblePower>()!;
                Equal((decimal)Prop(shell, "MaxAmount")!, 10m, $"{kind} GoldHopper's shell maximum");

                // Ten hits of any size, attack or HP loss (poison and other
                // damage over time use the HP-loss props), each deal 1 and
                // spend 1 DP; the tenth empties the shell.
                decimal[] raws = [6m, 1000m, 2m, 25m, 3m, 6m, 99m, 6m, 12m, 6m];
                for (int hit = 1; hit <= 10; hit++)
                {
                    ValueProp props = hit % 3 == 0 ? HpLoss : ValueProp.Move;
                    decimal amount = GoldHit(gold, raws[hit - 1], props);
                    Check(amount == 1m && shell.TryAbsorb(amount) && shell.CurrentDp == 10 - hit,
                        $"{kind} hit {hit} ({raws[hit - 1]} raw, {props}) deals 1 and leaves GoldHopper {10 - hit} DP");
                }
                Check((bool)GoldField(shell, "_pendingBreak")! && !shell.TryAbsorb(1m),
                    $"{kind} the tenth hit empties GoldHopper's DP and breaks the shell");
                // The Break removes the shell (its stun delays the escape,
                // checked above); the eleventh hit takes the one HP.
                Powers(gold).Remove(shell);
                decimal last = GoldHit(gold, 6m, ValueProp.Move);
                DamageResult killed = gold.LoseHpInternal(GoldHpLoss(intangible, gold, last), ValueProp.Move);
                Check(last == 1m && killed.WasTargetKilled && gold.CurrentHp == 0,
                    $"{kind} the eleventh hit (1) kills GoldHopper: 11 hits in all");

                // Intangible's HP-loss cap: any loss of 1 or more is 1; below
                // 1 it is untouched and native whole-number HP drops it.
                gold = GoldEnemy(kind, out model, out shell);
                intangible = gold.GetPower<IntangiblePower>()!;
                Equal(GoldHpLoss(intangible, gold, 5000m), 1m, $"{kind} Intangible cuts an HP loss of 5000 to 1");
                // HP-only follow-ups below 1 (兜割: 1/3, enhanced 1/2, of the
                // 1 DP it absorbed; the endgame cannon's 25% of its DP damage)
                // stay below 1 through the pipeline (HBR drops them to 0, the
                // legacy shell takes x0.75), Intangible leaves them, and the
                // native whole-number HP loses nothing.
                foreach (decimal follow in new[] { 1m / 3m, 0.5m, 0.25m })
                {
                    decimal dealt = GoldHit(gold, follow, HpLoss);
                    decimal lost = GoldHpLoss(intangible, gold, dealt);
                    DamageResult tiny = gold.LoseHpInternal(lost, HpLoss);
                    Cases.Add(new { goldHopperHpOnlyFollowUp = kind.ToString(), raw = follow, dealt });
                    Check(dealt < 1m && lost == dealt && gold.CurrentHp == 1 && !tiny.WasTargetKilled,
                        $"{kind} an HP-only follow-up of {follow:0.###} reaches GoldHopper as {dealt:0.####} and leaves its HP at 1");
                }
                // A 1-damage hit while the shell is up.
                decimal one = GoldHit(gold, 1m, ValueProp.Move);
                Cases.Add(new { goldHopperOneDamageHit = kind.ToString(), dpSpent = one });
                Check(one == (kind == HeroKind.Hbr ? 1m : 0.75m),
                    $"{kind} a 1-damage hit spends {one} DP" + (kind == HeroKind.Hbr ? "" : " (the legacy shell's x0.75 comes before the cap)"));

                // DP-only damage (棺木, continuous DP damage) is capped to 1 too.
                decimal spent = ((Task<decimal>)Call(shell, "DamageDpOnly", new ThrowingPlayerChoiceContext(), 500m, Hero, null, null)!)
                    .GetAwaiter().GetResult();
                Check(spent == 1m && shell.CurrentDp == 9m, $"{kind} 500 DP-only damage spends 1 of GoldHopper's DP");

                // Virus (a 30A phantom's VirusRandom) lowers the maximum to 7.
                HbrVirusPower virus = Power<HbrVirusPower>(gold, 2);
                Equal((decimal)Prop(shell, "MaxAmount")!, 7m, $"{kind} Virus lowers GoldHopper's maximum DP from 10 to 7");
                Powers(gold).Remove(virus);

                // Doom: HP 1 is doomed by any Doom, its DP untouched.
                DoomPower doom = Power<DoomPower>(gold, 1);
                Check(DoomPower.GetDoomedCreatures(new[] { gold }).Contains(gold) && shell.CurrentDp == 9m,
                    $"{kind} 1 Doom already dooms GoldHopper (HP 1) at its turn end, whatever DP it has left");
                Powers(gold).Remove(doom);
            }

            // 移除 (BreakImmediately) spends all 10 DP in one action: Break, no stun
            // since 2026-11-09; when the Break ends the shell comes back at the original's 10.
            foreach (HeroKind kind in HeroKinds)
            {
                Creature gold = GoldEnemy(kind, out GoldHopper model, out HighDimensionalShellPower shell);
                SetUp(model, "ATTACK_MOVE");
                Await(Invoke(typeof(HighDimensionalShellPower).GetMethod("BreakImmediately", F)!, shell,
                    new ThrowingPlayerChoiceContext(), 0));
                Check(!gold.HasPower<HighDimensionalShellPower>() && gold.GetPowerAmount<BreakPower>() == (kind == HeroKind.Hbr ? 2 : 3) &&
                    model.NextMove.Id == "ATTACK_MOVE" && gold.CurrentHp == 1 && gold.GetPowerAmount<IntangiblePower>() == 99,
                    $"{kind} 移除 (BreakImmediately) takes all 10 of GoldHopper's DP in one action without stunning it " +
                    $"[encounter={Combat.Encounter?.RoomType}, shell={gold.HasPower<HighDimensionalShellPower>()}, " +
                    $"Break={gold.GetPowerAmount<BreakPower>()}, move={model.NextMove.Id}, HP={gold.CurrentHp}, intangible={gold.GetPowerAmount<IntangiblePower>()}]");
                Await(PowerCmd.Remove(gold.GetPower<BreakPower>()));
                Check(gold.GetPower<HighDimensionalShellPower>() is { CurrentDp: 10m },
                    $"{kind} when the Break ends GoldHopper's shell returns at 10 DP");
            }

            // Rapture-弓 adds DP damage to each powered hit; it is capped to 1
            // like the hit, so a bow hit spends 2 DP and five break the shell.
            foreach (HeroKind kind in new[] { HeroKind.Hbr, HeroKind.Legacy })
            {
                Creature gold = GoldEnemy(kind, out _, out HighDimensionalShellPower shell);
                ReptureBowPower bow = Power<ReptureBowPower>(Hero, 1);
                // The constructor's attack ledgers (the fixture skips constructors).
                Set(bow, "_amplifiedAllTargetAttacks", new HashSet<AttackCommand>());
                Set(bow, "_singleOpponentAttacks", new HashSet<AttackCommand>());
                Set(bow, "_activeAttacks", new List<AttackCommand>());
                try
                {
                    var pending = (Stack<decimal>)GoldField(shell, "_pendingExtraDpDamage")!;
                    var extras = new List<decimal>();
                    int hits = 0;
                    while (shell.CurrentDp > 0m && hits < 10)
                    {
                        decimal extra = (decimal)Static("HBRGOSakiMod.Core.Models.Powers.ReptureBowPower",
                            "CalculateExtraDpDamage", gold, ValueProp.Move, Hero, null)!;
                        decimal amount = GoldHit(gold, 6m, ValueProp.Move);
                        extras.Add(extra + amount);
                        pending.Push(extra);
                        try { shell.TryAbsorb(amount); } finally { pending.Pop(); }
                        hits++;
                    }
                    Check(extras.All(spend => spend == 2m) && hits == 5 && (bool)GoldField(shell, "_pendingBreak")!,
                        $"{kind} with Rapture-弓 each hit spends 2 DP (1 + the bow's 1): 5 hits break GoldHopper's 10 DP");
                }
                finally { Powers(Hero).Remove(bow); }
            }
        }
        finally
        {
            foreach ((string name, object? value) in settingFields.Zip(saved))
                config.GetField(name, F)!.SetValue(null, value);
            Ascension = 0;
        }
    }

    // An enemy GoldHopper as it enters: native HP 58, the fixed HP and DP, its
    // 99 Intangible from the real room callback, and a full shell.
    static Creature GoldEnemy(HeroKind kind, out GoldHopper model, out HighDimensionalShellPower shell)
    {
        ResetTable(kind, 0);
        model = (GoldHopper)TableActor(typeof(GoldHopper), 0, out Creature gold, hp: 58);
        Static("HBRGOSakiMod.Core.Helpers.EnemyHealthScalingHelper", "ApplyInitialHealth", gold);
        Await(model.AfterAddedToRoom());
        shell = Shell(gold, HighDimensionalShellPower.CalculateAmount(gold));
        return gold;
    }

    static decimal GoldHit(Creature target, decimal raw, ValueProp props) =>
        (decimal)FixtureCall("Preview", target, Hero, raw, props, null)!;

    // Native IntangiblePower.ModifyHpLostAfterOsty (beta adds a trailing card play).
    static decimal GoldHpLoss(IntangiblePower power, Creature target, decimal amount)
    {
        MethodInfo method = typeof(IntangiblePower).GetMethod(nameof(IntangiblePower.ModifyHpLostAfterOsty))!;
        object?[] args = new object?[method.GetParameters().Length];
        args[0] = target; args[1] = amount; args[2] = ValueProp.Move; args[3] = Hero;
        return (decimal)Invoke(method, power, args)!;
    }

    static object? GoldField(object owner, string name)
    {
        for (Type? type = owner.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, F | BindingFlags.DeclaredOnly) is { } field) return field.GetValue(owner);
        throw new MissingFieldException(owner.GetType().FullName, name);
    }

    static void MoveTableCases()
    {
        Dictionary<string, SnapshotEnemy> snapshot = LoadSnapshot();
        TableDataCases(snapshot);
        TableWalkCases();
        TableBreakCases();
        TableMoveBodyCases();
        StrengthenedEnemyCases();
        StatusSettlementCases();
        PhantomStatusCases();
        AllUnitTargetCases();
        PassThroughCases();
        EncounterVariantCases();
        FourthBatchCases();
        StatusIconCases();
        RestoreHero();
    }
}
