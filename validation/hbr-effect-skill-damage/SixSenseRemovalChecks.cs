using System.Reflection;
using System.Text.Json.Nodes;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

// Six senses keep only their level points (user, 2026-11-09: 「要不给六感机制删了」「就这么删」,
// 「萤也一起删」, Narara's nullification 「这条先关掉」). Cards carry no attribute, enemies no
// resistance; the cards built on resistances were retired or given the replacements below.
static partial class EffectChecks
{
    static readonly string[] SenseProperties = { "Madness", "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity" };
    static readonly string[] SenseStats = { "Strength", "Dexterity", "Intelligence", "Vitality", "Spirit", "Luck" };

    static ElectronicSoldierManual Manual => (ElectronicSoldierManual)State<RelicModel>("Manual");
    static string Source(string relative) => File.ReadAllText(Path.Combine(RepositoryRoot, relative));

    static void InjectPower<T>() where T : PowerModel
    {
        if (ModelDb.Contains(typeof(T))) return;
        ModelDb.Inject(typeof(T));
        Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(T))), "<Id>k__BackingField", ModelDb.GetId(typeof(T)));
    }

    static void SixSenseRemovalStructureCases()
    {
        // Cards get no attribute and enemies no resistance: the patches and nodes are gone.
        foreach (string removed in new[]
                 {
                     "HBRGOSakiMod.Core.Patches.SixSenseCardAttributePatch",
                     "HBRGOSakiMod.Core.Patches.Combat.SixSenseResistanceCombatStartPatch",
                     "HBRGOSakiMod.Core.Patches.Combat.SixSenseResistanceCreatureAddedPatch",
                     "HBRGOSakiMod.Core.Patches.Combat.SixSenseResistanceHoverPatch",
                     "HBRGOSakiMod.Core.Nodes.Cards.NSixSenseEnergyIcon", "HBRGOSakiMod.Core.Nodes.Cards.SixSenseHandFocus",
                     "HBRGOSakiMod.Core.Nodes.Combat.NSixSenseResistanceRow", "HBRGOSakiMod.Core.Nodes.Combat.NSixSenseResistanceCallout",
                     "HBRGOSakiMod.Core.Nodes.Combat.NSixSenseBadge"
                 })
            Check(Mod.GetType(removed) == null, removed + " no longer exists");
        Type starting = Mod.GetType("HBRGOSakiMod.Core.Patches.SixSenseStartingInventoryPatch", true)!;
        Check(starting.IsDefined(typeof(HarmonyLib.HarmonyPatch), false), "the starting-inventory patch stays: run seed, HBR numerics and the starting points");
        string startingSource = Source("Core/Patches/SixSenseCardAttributePatch.cs");
        Check(startingSource.Contains("InitializeHbrBattleNumerics();") && startingSource.Contains("GrantStartingSixSense();") &&
              !startingSource.Contains("AssignIfMissing") && !startingSource.Contains("AddCard"),
            "the starting patch still initializes HBR numerics and the fifteen points and assigns no card attribute");
        string pack = Source("Core/Integration/HBRGOSakiContentPack.cs");
        Check(pack.Contains("RegisterModelCapability<SixSenseCardCapability>()") && !pack.Contains("\"six_sense_attribute\""),
            "the six-sense capability stays registered for saved cards but is no default capability of new cards");

        // The capability keeps no state: a saved attribute is read and ignored.
        Type capability = typeof(SixSenseCardCapability);
        Check(capability.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0,
            "SixSenseCardCapability holds no attribute");
        string[] declared = capability.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Check(declared.SequenceEqual(new[] { "LoadAdditionalState", "SaveAdditionalState" }),
            "SixSenseCardCapability overrides only its save and load: " + string.Join(",", declared));
        var shell = new SixSenseCardCapability();
        Invoke(capability.GetMethod("LoadAdditionalState", F)!, shell, JsonNode.Parse("{\"attribute\":3}"), 1);
        Check(Invoke(capability.GetMethod("SaveAdditionalState", F)!, shell) == null,
            "a card loaded with an old attribute writes nothing back");

        // Retired powers load from older states and do nothing.
        foreach (Type power in new[]
                 {
                     typeof(SixSenseResistancePower), typeof(SixSenseCombatPower), typeof(SixSenseLeadershipPower),
                     typeof(SixSenseGentlenessPower), typeof(SixSenseMentalStrengthPower), typeof(GodspeedPower),
                     typeof(HastePower), typeof(BreakInsightPower)
                 })
        {
            string[] hooks = power.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.Name != "AfterCloned").Select(method => method.Name).ToArray();
            Check(hooks.Length == 0, $"{power.Name} declares no hook or effect: {string.Join(",", hooks)}");
        }
        foreach (Type power in new[] { typeof(SixSenseResistancePower), typeof(SixSenseCombatPower), typeof(SixSenseLeadershipPower),
                     typeof(SixSenseGentlenessPower), typeof(SixSenseMentalStrengthPower) })
        {
            PowerModel instance = (PowerModel)Invoke(typeof(EffectChecks).GetMethod(nameof(Blank), F)!.MakeGenericMethod(power), null)!;
            Check(!(bool)power.GetProperty("IsVisibleInternal", F)!.GetValue(instance)!, power.Name + " is hidden");
        }
        string[] applying = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Core"), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file) is var text && new[] { "SixSenseResistancePower", "SixSenseCombatPower", "GodspeedPower",
                "HastePower", "BreakInsightPower" }.Any(power => text.Contains("Apply<" + power + ">")))
            .Select(file => Path.GetRelativePath(RepositoryRoot, file)).ToArray();
        Check(applying.Length == 0, "no source applies a retired power: " + string.Join(", ", applying));

        // The six resistance cards leave every pool but stay loadable.
        var retired = (IReadOnlySet<Type>)Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!
            .GetField("RetiredCardTypes", F)!.GetValue(null)!;
        foreach (Type card in new[] { typeof(GodspeedHbrgoSaki), typeof(HasteHbrgoSaki), typeof(RuinStrikeHbrgoSaki),
                     typeof(ResistanceBulwarkHbrgoSaki), typeof(BreakInsightHbrgoSaki), typeof(SenseShatterHbrgoSaki) })
            Check(retired.Contains(card), card.Name + " is retired");
        // Six-sense/fate retirements plus 连斩, 萝莉形态 and 快速思考; all remain loadable.
        Check(retired.Count == 12 && retired.Contains(typeof(QuickThinkingHbrgoSaki)) &&
              retired.Contains(typeof(ConsecutiveSlashHbrgoSaki)) &&
              retired.Contains(typeof(LoliFormHbrgoSaki)) && pack.Contains("if (RetiredCardTypes.Contains(cardType))") &&
              pack.IndexOf("RegisterCard(typeof(DeprecatedCardPool), cardType", pack.IndexOf("if (RetiredCardTypes.Contains(cardType))", StringComparison.Ordinal), StringComparison.Ordinal) > 0,
            "retired cards register in the deprecated pool before any character pool");
        var glossaryRetired = (IReadOnlySet<Type>)Helper("HbrManualGlossaryCatalog").GetField("RetiredPowerTypes", F)!.GetValue(null)!;
        Check(glossaryRetired.SetEquals(new[] { typeof(SixSenseResistancePower), typeof(SixSenseCombatPower), typeof(SixSenseLeadershipPower),
                typeof(SixSenseGentlenessPower), typeof(SixSenseMentalStrengthPower), typeof(GodspeedPower), typeof(HastePower),
                typeof(BreakInsightPower), typeof(StrengthenPower),
                typeof(NextRollBonusPower), typeof(ExtremeDodgeRollPenaltyPower), typeof(CrushYouLikeABugReplayPower),
                typeof(DualSwordsCounterPower), typeof(BowCounterPower), typeof(QuickSwitchPower) }),
            "the manual leaves out retired six-sense/roll powers; Narara now uses an attribute-free contest");

        // Requirement 46 supersedes the temporary 2026-11-09 switch-off.
        Check(Source("Core/Models/Monsters/NararaShibaBossMonster.cs").Contains("Apply<NararaNullificationPower>"),
            "Narara installs the replacement attribute-free contest");
        // Replacements that need the cannon pipeline are checked in their source.
        string millsage = Source("Core/Models/Relics/Millsage.cs"), stats = Source("Core/Helpers/HotaruCannonStatsHelper.cs");
        Check(millsage.Contains("HbrMissingDebuffHelper.DebuffKinds(target) * HotaruCannonSkillPowerHelper.ContestResistanceDamage") &&
              stats.Contains("amount += HbrMissingDebuffHelper.DebuffKinds(target) *"),
            "Contest's extra damage counts the target's debuff kinds, in its hit and in its preview");
        Check(millsage.Contains("HotaruRareCannonHelper.OnEnemyDebuffGained(context, Owner, power, amount, source)"),
            "Millsage reports every power change to Six Sense Remnants");
        string support = Source("Core/Helpers/HotaruCannonSupportUpgradeHelper.cs");
        int longing6 = support.IndexOf("HasCannonUpgrade(HotaruCannonKind.Longing, 6)", StringComparison.Ordinal);
        Check(longing6 > 0 && support.IndexOf("HbrMissingDebuffHelper.ApplyRandomMissing", longing6, StringComparison.Ordinal) is int at &&
              at > longing6 && at - longing6 < 200, "Longing Lv.6 gives a missing debuff after each actual hit");
        string light = Source("Core/Helpers/HotaruUncommonCannonHelper.cs");
        Check(light.Contains("await HbrMissingDebuffHelper.ApplyRandomMissing(context, target, scope.Cannon, scope.Source,") &&
              !light.Contains("SixSenseCombatHelper"), "Light Points gives missing debuffs to every enemy after the whole action");
        string phantom = Source("Core/Models/Powers/PhantomPower.cs");
        Check(phantom.Contains("temporaryPercent += TargetAttackBonusPercent * GetTargetAttackCount(target);") &&
              !phantom.Contains("SixSenseCombatHelper"), "the main phantom's bonus reads its attack count on the target");
        // The critical success's extra six-sense point (QueueSixSenseReward) is a point source and stays.
        foreach (string file in new[] { "Core/Models/Capabilities/RollGlossaryCapability.cs", "Core/Helpers/HbrCombatDamagePipeline.cs" })
            Check(!Source(file).Contains("SixSenseCardCapability") && !Source(file).Contains("SixSenseCombatPower"),
                file + ": no roll bonus or damage multiplier comes from a card attribute");
    }

    static async Task SixSenseRemovalCases()
    {
        await SixSensePointCases();
        Reset(false); var resistance = Power<SixSenseResistancePower>(Enemy, 1);
        typeof(SixSenseResistancePower).GetProperty("Resistances", F)!.SetValue(resistance, new int[6]);
        Equal((decimal)Invoke(typeof(DestructionRatePower).GetMethod("GetDamageIncreasePerStack", F)!, null, Enemy)!, 0.005m,
            "native Destruction Rate gives a flat 0.5% per stack, even beside an old fully broken resistance power");
        Reset(true);
        Equal((decimal)Invoke(typeof(DestructionRatePower).GetMethod("GetDamageIncreasePerStack", F)!, null, Enemy)!, 0.01m,
            "HBR Destruction Rate keeps 1% per stack");

        foreach (Type power in new[] { typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower), typeof(FragilePower),
                     typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower), typeof(ChargePower) })
            Invoke(typeof(EffectChecks).GetMethod(nameof(InjectPower), F)!.MakeGenericMethod(power), null);
        CombatInProgressWithTurn();
        try
        {
            await MissingDebuffCases(false);
            await MissingDebuffCases(true);
            await PhantomAttackCountCases();
            await SixSenseRemnantsCases();
            TraceOldScarCases();
            NararaContestReplacementCases();
        }
        finally { CombatInProgress(false); }
        ReplacementCardShapeCases();
    }

    static async Task SixSensePointCases()
    {
        Reset(true);
        foreach ((CardType type, string pair) in new[] { (CardType.Attack, "Madness,LeadershipCharm"), (CardType.Skill, "Simplicity,MentalStrength"),
                     (CardType.Power, "Talent,Gentleness"), (CardType.Status, "") })
        {
            var senses = (SixSenseAttribute[])Invoke(typeof(ElectronicSoldierManual).GetMethod("GetSensePair", F)!, null, type)!;
            Check(string.Join(",", senses) == pair, $"{type} cards still pair {pair}");
        }

        // Each confirmed level still adds 2 to its battle stat, and nothing else.
        for (int i = 0; i < SenseProperties.Length; i++)
        {
            Reset(true); H("Sense", SenseProperties[i], 10);
            object stats = H("Stats", Hero)!;
            for (int j = 0; j < SenseStats.Length; j++)
                Equal((decimal)H("Stat", stats, SenseStats[j])!, i == j ? 120m : 100m,
                    $"{SenseProperties[i]} 10 gives {SenseStats[j]}");
            var attribute = Enum.Parse<SixSenseAttribute>(SenseProperties[i]);
            Equal((decimal)Call("SixSenseCombatHelper", "Level", Player, attribute)!, 10m, $"the six-sense level of {attribute} is still readable");
        }

        // The fifteen starting points and the per-victory +1 still go by card-type pairs.
        Reset(true);
        PropertyInfo rngProperty = typeof(MegaCrit.Sts2.Core.Entities.Players.Player).GetProperty("PlayerRng", F)!;
        object? previousRng = rngProperty.GetValue(Player);
        ConstructorInfo rngConstructor = typeof(PlayerRngSet).GetConstructors().Single(c => c.GetParameters().Length == 1);
        Set(Player, "<PlayerRng>k__BackingField", rngConstructor.Invoke(new[] { Convert.ChangeType(20261109, rngConstructor.GetParameters()[0].ParameterType) }));
        Set(Manual, "<StartingSixSenseGranted>k__BackingField", false);
        Set(Manual, "_sixSensePoints", 0);
        try
        {
            int Level(string name) => Manual.GetSenseAmount(Enum.Parse<SixSenseAttribute>(name));
            Invoke(typeof(ElectronicSoldierManual).GetMethod("GrantStartingSixSense", F)!, Manual);
            Check(Level("Madness") + Level("LeadershipCharm") == 5 && Level("Simplicity") + Level("MentalStrength") == 5 &&
                  Level("Talent") + Level("Gentleness") == 5 && Manual.StartingSixSenseGranted,
                "the starting fifteen points go five to each card-type pair");
            int[] start = SenseProperties.Select(Level).ToArray();
            Invoke(typeof(ElectronicSoldierManual).GetMethod("GrantStartingSixSense", F)!, Manual);
            Check(SenseProperties.Select(Level).SequenceEqual(start), "the starting points are granted once");
            Invoke(typeof(ElectronicSoldierManual).GetMethod("GrantCombatSixSenseIncreases", F)!, Manual);
            Check(Level("Madness") + Level("LeadershipCharm") == 6 && Level("Simplicity") + Level("MentalStrength") == 6 &&
                  Level("Talent") + Level("Gentleness") == 6, "a victory raises one sense of each pair by 1");
        }
        finally { Set(Player, "<PlayerRng>k__BackingField", previousRng); }

        // The memory garden gains a point for each new 60-point six-sense tier.
        Reset(true);
        Set(Manual, "_memoryGardenSixSenseUpgradeTier", 0); Set(Manual, "_sixSensePoints", 0);
        int points = Manual.MemoryGardenUpgradePoints;
        H("Sense", "Madness", 59); Invoke(typeof(ElectronicSoldierManual).GetMethod("SynchronizeMemoryGardenUpgradePoints", F)!, Manual);
        Check(Manual.MemoryGardenSixSenseUpgradeTier == 0 && Manual.MemoryGardenUpgradePoints == points, "59 total six-sense points reach no tier");
        H("Sense", "Talent", 1); Invoke(typeof(ElectronicSoldierManual).GetMethod("SynchronizeMemoryGardenUpgradePoints", F)!, Manual);
        Check(Manual.MemoryGardenSixSenseUpgradeTier == 1 && Manual.MemoryGardenUpgradePoints == points + 1, "60 total points give the first garden point");
        H("Sense", "Gentleness", 61); Invoke(typeof(ElectronicSoldierManual).GetMethod("SynchronizeMemoryGardenUpgradePoints", F)!, Manual);
        Check(Manual.MemoryGardenSixSenseUpgradeTier == 2 && Manual.MemoryGardenUpgradePoints == points + 2, "121 total points give the second");
        Set(Manual, "_memoryGardenSixSenseUpgradeTier", 0);
        await Task.CompletedTask;
    }

    static async Task MissingDebuffCases(bool hbr)
    {
        string mode = hbr ? "HBR" : "native";
        Type attackDown = hbr ? typeof(HbrAttackDownPower) : typeof(WeakPower);
        Type defenseDown = hbr ? typeof(HbrDefenseDownPower) : typeof(VulnerablePower);
        int Kinds(Creature target) => (int)Call("HbrMissingDebuffHelper", "DebuffKinds", target)!;
        int Missing(Creature target) => ((System.Collections.ICollection)Call("HbrMissingDebuffHelper", "Missing", target)!).Count;
        bool Has(Type type) => Enemy.Powers.Any(power => power.GetType() == type && power.Amount > 0);
        Task<int> GiveRandom(int times) => (Task<int>)Call("HbrMissingDebuffHelper", "ApplyRandomMissing",
            new ThrowingPlayerChoiceContext(), Enemy, Hero, null, times)!;

        Reset(hbr);
        Check(Kinds(Enemy) == 0 && Missing(Enemy) == 3, $"{mode}: a clean enemy has no debuff and misses all three");
        Check(await GiveRandom(2) == 2 && Kinds(Enemy) == 2 && Missing(Enemy) == 1,
            $"{mode}: two stacks are two different missing debuffs");
        Check(await GiveRandom(5) == 1 && Kinds(Enemy) == 3 && Missing(Enemy) == 0 && Has(attackDown) && Has(defenseDown) && Has(typeof(FragilePower)),
            $"{mode}: more times give only the last missing one");
        Check(await GiveRandom(1) == 0 && Kinds(Enemy) == 3, $"{mode}: with all three nothing more is given");
        Check(Enemy.Powers.Where(power => power.GetType() == attackDown || power.GetType() == defenseDown || power is FragilePower)
                .All(power => power.Amount == (hbr ? power.Amount : 1)), $"{mode}: each is one stack");

        Reset(hbr); Power<WeakPower>(Enemy, 2);
        int before = Kinds(Enemy);
        await (Task)Call("HbrMissingDebuffHelper", "ApplyAllMissing", new ThrowingPlayerChoiceContext(), Enemy, Hero, null)!;
        Check(Has(defenseDown) && Has(typeof(FragilePower)), $"{mode}: Gentle Touch's rule gives each missing debuff");
        if (!hbr)
            Check(Enemy.GetPower<WeakPower>()!.Amount == 2 && Kinds(Enemy) == 3 && before == 1,
                "native: a debuff the enemy already has is left as it is");
        else
            Check(Has(typeof(HbrAttackDownPower)), "HBR: native Weak is no HBR attack down, so attack down is still given");
        Reset(hbr);
    }

    static async Task PhantomAttackCountCases()
    {
        Reset(true);
        var phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        var power = Power<PhantomPower>(phantom, 1);
        uint? enemyId = Enemy.CombatId; Enemy.CombatId = 41;
        try
        {
            MethodInfo count = typeof(PhantomPower).GetMethod("GetTargetAttackCount", F)!;
            int Count() => (int)Invoke(count, power, Enemy)!;
            AttackCommand Attack(Creature attacker, int hits, ValueProp props = ValueProp.Move)
            {
                var attack = new AttackCommand(6m);
                Set(attack, "<Attacker>k__BackingField", attacker); Set(attack, "<DamageProps>k__BackingField", props);
                attack.Targeting(Enemy);
                for (int i = 0; i < hits; i++) attack.AddResultsInternal(new[] { new DamageResult(Enemy, props) });
                return attack;
            }
            Check(Count() == 0, "a new phantom has attacked nobody");
            await power.AfterAttack(new ThrowingPlayerChoiceContext(), Attack(phantom, 3));
            Check(Count() == 1, "one three-hit attack counts once");
            await power.AfterAttack(new ThrowingPlayerChoiceContext(), Attack(Hero, 1));
            await power.AfterAttack(new ThrowingPlayerChoiceContext(), Attack(phantom, 1, ValueProp.Unpowered));
            Check(Count() == 1, "the summoner's attack and an unpowered hit do not count");
            for (int i = 0; i < 8; i++) await power.AfterAttack(new ThrowingPlayerChoiceContext(), Attack(phantom, 1));
            Check(Count() == 6, "the count stops at six (+90%)");
            Check((decimal)typeof(PhantomPower).GetField("TargetAttackBonusPercent", F)!.GetValue(null)! == 15m &&
                  (int)typeof(PhantomPower).GetField("MaximumTargetAttackBonuses", F)!.GetValue(null)! == 6,
                "each counted attack adds 15%, at most six");
            var minion = Power<PhantomPower>((Creature)H("Actor", Blank<SmallHopper>(), null, Player)!, 1);
            Set(minion, "<IsSummonedMinion>k__BackingField", true);
            await minion.AfterAttack(new ThrowingPlayerChoiceContext(), Attack(minion.Owner, 1));
            Check((int)Invoke(count, minion, Enemy)! == 0, "summoned minions keep no count");
            int[] saved = power.TargetAttackCounts;
            Check(saved.SequenceEqual(new[] { 41, 6 }), "the count is saved with the phantom as enemy id and count");
        }
        finally { Enemy.CombatId = enemyId; Reset(true); }
    }

    static async Task SixSenseRemnantsCases()
    {
        Reset(true);
        var relic = new Millsage(); Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual, relic });
        uint? enemyId = Enemy.CombatId; Enemy.CombatId = 42;
        var second = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!; second.CombatId = 43;
        Set(Combat, "_enemies", new List<Creature> { Enemy, second });
        try
        {
            var remnants = Power<HotaruSixSenseRemnantsPower>(Hero, 1);
            Set(remnants, "<ChargeReward>k__BackingField", 2); Set(remnants, "<GrowthReward>k__BackingField", 0);
            int Charge() => Hero.GetPower<ChargePower>()?.Amount ?? 0;
            Task Gain(PowerModel power, decimal amount) => (Task)Call("HotaruRareCannonHelper", "OnEnemyDebuffGained",
                new ThrowingPlayerChoiceContext(), Player, power, amount, null)!;
            var weak = Power<WeakPower>(Enemy, 1);
            await Gain(weak, 1m);
            Check(Charge() == 2, "an enemy's first Weak gives the reward once");
            await Gain(weak, 1m);
            Check(Charge() == 2, "the same enemy gaining Weak again gives nothing");
            await Gain(Power<VulnerablePower>(Enemy, 1), 1m);
            Check(Charge() == 4, "its first Vulnerable is another kind and counts");
            await Gain(Power<WeakPower>(second, 1), 1m);
            Check(Charge() == 6, "another enemy's first Weak counts for that enemy");
            await Gain(Power<StrengthPower>(Enemy, 2), 2m);
            await Gain(Power<VigorPower>(Hero, 2), 2m);
            Check(Charge() == 6, "a buff, or a power on the player's side, does not count");
            var state = relic.GetType().GetProperty("RareCannonState", F)!.GetValue(relic)!;
            var first = (System.Collections.IDictionary)state.GetType().GetProperty("FirstDebuffs")!.GetValue(state)!;
            Check(first.Count == 2 && first.Contains(42u) && first.Contains(43u), "first debuffs are recorded per enemy id");
            // Recorded even before the power is held: a kind gained without it never counts later.
            Powers(Hero).Remove(remnants);
            await Gain(Power<FragilePower>(Enemy, 1), 1m);
            Powers(Hero).Add(remnants);
            await Gain(Enemy.GetPower<FragilePower>()!, 1m);
            Check(Charge() == 6, "a kind first gained before the power was held does not count later");
        }
        finally { Enemy.CombatId = enemyId; Set(Combat, "_enemies", new List<Creature> { Enemy }); Reset(true); }
    }

    static void TraceOldScarCases()
    {
        Localization(); Reset(true);
        var ledger = Power<HotaruTraceOldScarPower>(Hero, 1);
        Set(ledger, "<OrdinaryTurnNumber>k__BackingField", Player.PlayerCombatState!.TurnNumber);
        typeof(HotaruTurnLedgerPower).GetProperty("OrdinaryGrants", F)!.SetValue(ledger,
            "[{\"Value\":1,\"Remaining\":2147483647,\"Tag\":" + (int)CardType.Attack + "}]");
        decimal Cost(CardModel card, decimal original)
        {
            object?[] args = { card, original, 0m };
            bool changed = (bool)Invoke(typeof(HotaruTraceOldScarPower).GetMethod("TryModifyEnergyCostInCombat", F)!, ledger, args)!;
            return changed ? (decimal)args[2]! : original;
        }
        Equal(Cost(Owned<StrikeIronclad>(), 2m), 1m, "Trace Old Scar on attacks: an attack costs 1 less this turn");
        Equal(Cost(Owned<StrikeIronclad>(), 0m), 0m, "the discount stops at 0");
        Equal(Cost(Owned<DefendIronclad>(), 2m), 2m, "a skill is not discounted when attacks were chosen");
        string description = ledger.Description.GetFormattedText();
        Check(description.Contains("攻击牌：-1") && !description.Contains("六感"), "the discount power names the card type: " + description);
        var card = Owned<HotaruTraceOldScar>();
        Check(card.TargetType == TargetType.Self && card.DynamicVars.Keys.SequenceEqual(new[] { "CostReduction" }),
            "Trace Old Scar targets nobody and keeps only its cost reduction");
        Reset(true);
    }

    static void NararaContestReplacementCases()
    {
        Reset(true);
        var boss = (Creature)H("Actor", Blank<NararaShibaBossMonster>(), null, null)!;
        Set(Combat, "_enemies", new List<Creature> { boss });
        try
        {
            var nullification = Power<NararaNullificationPower>(boss, 1);
            var card = Owned<StrikeIronclad>();
            Check((bool)typeof(NararaNullificationPower).GetProperty("IsActive", F)!.GetValue(nullification)!,
                "HBR enemy Narara activates the replacement contest");
            Equal((decimal)typeof(NararaNullificationPower).GetMethod("GetNullificationDamageCap", F)!.Invoke(nullification, new object?[] { boss, card })!,
                decimal.MaxValue, "a card outside a failed comparison has no immunity cap");
            Check(Mod.GetType("HBRGOSakiMod.Core.Helpers.NararaNullificationPenaltyHelper") == null &&
                Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.NararaNullificationCostPatch") == null,
                "all six-sense roll/cost penalty classes have been removed");
        }
        finally { Set(Combat, "_enemies", new List<Creature> { Enemy }); Reset(true); }
    }

    static void ReplacementCardShapeCases()
    {
        string[] Vars(CardModel card) => card.DynamicVars.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Localization();
        void Shape<T>(string expected, string label) where T : CardModel, new()
        {
            string actual = string.Join(",", Vars(Owned<T>()));
            Check(actual == expected, $"{label} keeps only {expected}: {actual}");
        }
        Shape<HotaruBuoy>("FragilePower,Times,VulnerablePower,WeakPower", "Hollow Gaze");
        Shape<HotaruLightPoints>("Times", "Light Points");
        Shape<HotaruGentleTouch>("Cards,FragilePower,VulnerablePower,WeakPower", "Gentle Touch");
        Check(!Vars(Owned<HotaruEchoingWounds>()).Contains("ResistanceLoss") && Owned<HotaruEchoingWounds>().DynamicVars["ChargePower"].BaseValue == 2m,
            "Echoing Wounds' attack-down branch gives 2 charge");
        Check(Vars(Owned<HotaruMissingOutline>()).Contains("DebuffDamage") && !Vars(Owned<HotaruMissingOutline>()).Contains("BreakDamage"),
            "Missing Outline counts debuff kinds");
        Check(Vars(Owned<HotaruBeneathFracture>()).Contains("DebuffDamage") && !Vars(Owned<HotaruBeneathFracture>()).Contains("BreakDamage"),
            "Beneath Fracture needs a debuff");
        Check(!Vars(Owned<ConsecutiveSlashHbrgoSaki>()).Contains("ResistanceDamage"), "Consecutive Slash has no resistance bonus");
        Shape<SenseShatterHbrgoSaki>("Cards", "retired Sense Shatter");
        Check(!Vars(Owned<RuinStrikeHbrgoSaki>()).Contains("Repeat") && !Vars(Owned<ResistanceBulwarkHbrgoSaki>()).Contains("Repeat"),
            "retired Ruin Strike and Resistance Bulwark act once");
        var buoy = Owned<HotaruBuoy>(); Native(buoy, "OnUpgrade");
        Equal(buoy.DynamicVars["Times"].BaseValue, 2m, "upgraded Hollow Gaze gives two");
        var light = Owned<HotaruLightPoints>(); Native(light, "OnUpgrade");
        Equal(light.DynamicVars["Times"].BaseValue, 2m, "upgraded Light Points gives two per enemy");
        var wounds = Owned<HotaruEchoingWounds>(); Native(wounds, "OnUpgrade");
        Equal(wounds.DynamicVars["ChargePower"].BaseValue, 3m, "upgraded Echoing Wounds gives 3 charge");
    }

    // Runs after the production card-face patch is installed (EatPassersbyTextCases): the replaced
    // cards' real faces in both modes, kept in the report for the record.
    static void SixSenseCardFaceCases()
    {
        Localization();
        CardModel[] Cards() => new CardModel[]
        {
            Owned<HotaruBuoy>(), Owned<HotaruLightPoints>(), Owned<HotaruEchoingWounds>(), Owned<HotaruBeneathFracture>(),
            Owned<HotaruMissingOutline>(), Owned<HotaruSixSenseRemnants>(), Owned<HotaruTraceOldScar>(), Owned<HotaruGentleTouch>(),
            Owned<ConsecutiveSlashHbrgoSaki>(), Owned<GodspeedHbrgoSaki>(), Owned<HasteHbrgoSaki>(), Owned<RuinStrikeHbrgoSaki>(),
            Owned<ResistanceBulwarkHbrgoSaki>(), Owned<BreakInsightHbrgoSaki>(), Owned<SenseShatterHbrgoSaki>()
        };
        string Face(CardModel card)
        {
            Set(Player.PlayerCombatState!, "_pets", new List<Creature>());
            return card.GetDescriptionForPile(PileType.None);
        }
        foreach (bool hbr in new[] { true, false })
            foreach (bool upgraded in new[] { false, true })
            {
                Reset(hbr);
                foreach (CardModel card in Cards())
                {
                    if (upgraded) Native(card, "OnUpgrade");
                    string face = Face(card);
                    Cases.Add(new { source = "Six-sense card face", card = card.GetType().Name, hbr, upgraded, face });
                    string label = $"{card.GetType().Name} ({(hbr ? "HBR" : "native")}{(upgraded ? ", upgraded values" : "")})";
                    Check(!face.Contains('{') && !face.Contains('}') && face.Length > 0, label + " formats every variable: " + face);
                    Check(!face.Contains("抗性") || face.Contains("六感抗性已删除"), label + " mentions no resistance: " + face);
                }
            }
        Reset(true);
        string Hover(PowerModel power)
        {
            LocString text = power.Description;
            Invoke(HarmonyLib.AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, text, null);
            return text.GetFormattedText();
        }
        var remnants = Power<HotaruSixSenseRemnantsPower>(Hero, 1);
        Set(remnants, "<ChargeReward>k__BackingField", 1); Set(remnants, "<GrowthReward>k__BackingField", 1);
        string remnantsHover = Hover(remnants);
        Cases.Add(new { source = "Six-sense power hover", power = "HotaruSixSenseRemnantsPower", hover = remnantsHover });
        Check(remnantsHover.Contains("首次获得某种负面效果") && !remnantsHover.Contains("抗性"), "Six Sense Remnants' hover: " + remnantsHover);
        Reset(true);
    }

    static void SixSenseRemovalTextCases()
    {
        Localization(); Reset(true);
        string Raw(string table, string key) => LocManager.Instance.GetTable(table).GetRawText(key);
        LocTable keywords = LocManager.Instance.GetTable("card_keywords");
        string[] senseKeys = keywords.GetLocStringsWithPrefix("HBRGO_SIX_SENSE").Select(text => text.LocEntryKey).ToArray();
        string[] allowed = new[] { "HBRGO_SIX_SENSE.title", "HBRGO_SIX_SENSE.description", "HBRGO_SIX_SENSE.hbrDescription" }
            .Concat(SenseProperties.SelectMany(sense => new[] { $"HBRGO_SIX_SENSE.{sense}.title", $"HBRGO_SIX_SENSE.{sense}.menuDescription",
                $"HBRGO_SIX_SENSE.{sense}.hbrMenuDescription" })).ToArray();
        Check(senseKeys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(allowed.OrderBy(k => k, StringComparer.Ordinal)),
            "the six-sense keywords keep only the point rules: " + string.Join(",", senseKeys.Except(allowed)));
        foreach (string key in allowed)
        {
            string text = Raw("card_keywords", key);
            Check(!text.Contains("抗性") && !text.Contains("出牌") && !text.Contains("出目") && !text.Contains("属性牌"),
                key + " mentions no card effect, roll or resistance");
        }
        Check(Raw("card_keywords", "HBRGO_SIX_SENSE.hbrDescription").Contains("疯狂→力量") &&
              Raw("card_keywords", "HBRGO_SIX_SENSE.description").Contains("记忆之庭升级点"),
            "the six-sense rules state the battle stats and the garden tiers");

        foreach (string key in new[] { "ELECTRONIC_SOLDIER_MANUAL", "ELECTRONIC_SOLDIER_MANUAL_31O", "ELECTRONIC_SOLDIER_MANUAL_31_O", "KETSU",
                     "HOTARU_MANUAL_SIX_SENSE_RULES" })
            foreach (string field in new[] { "description", "hbrDescription" })
            {
                string text = Raw("relics", $"{key}.{field}");
                Check(!text.Contains("抗性") && !text.Contains("属性牌") && !text.Contains("出牌效果") && text.Contains("上限[blue]99[/blue]级"),
                    $"{key}.{field} describes only the points");
            }

        foreach (string key in new[] { "CONSECUTIVE_SLASH_HBRGO_SAKI.description", "CONSECUTIVE_SLASH_HBRGO_SAKI.description.hbr",
                     "HOTARU_BUOY.description", "HOTARU_BUOY.description.hbr", "HOTARU_LIGHT_POINTS.description",
                     "HOTARU_LIGHT_POINTS.description.hbr", "HOTARU_ECHOING_WOUNDS.description", "HOTARU_ECHOING_WOUNDS.description.hbr",
                     "HOTARU_BENEATH_FRACTURE.description", "HOTARU_BENEATH_FRACTURE.description.hbr", "HOTARU_MISSING_OUTLINE.description",
                     "HOTARU_MISSING_OUTLINE.description.hbr", "HOTARU_SIX_SENSE_REMNANTS.description", "HOTARU_SIX_SENSE_REMNANTS.description.hbr",
                     "HOTARU_TRACE_OLD_SCAR.description", "HOTARU_TRACE_OLD_SCAR.choiceDescription", "HOTARU_GENTLE_TOUCH.description",
                     "HOTARU_GENTLE_TOUCH.description.hbr", "HELLO_WORLD_HBRGO_SAKI.upgrade_effect.1" })
            Check(!Raw("cards", key).Contains("抗性"), key + " mentions no resistance");
        foreach (string key in new[] { "GODSPEED_HBRGO_SAKI.description", "HASTE_HBRGO_SAKI.description", "BREAK_INSIGHT_HBRGO_SAKI.description" })
            Check(Raw("cards", key).Contains("六感抗性已删除"), key + " says the retired card does nothing now");
        foreach (string key in new[] { "MORE_THAN_ONE_TRUTH_HBRGO_SAKI.upgrade_effect.2", "MORE_THAN_ONE_TRUTH_HBRGO_SAKI.upgrade_effect.5",
                     "SAVIOR_GRAND_ENTRANCE_HBRGO_SAKI.upgrade_effect.6" })
            Check(!Raw("cards", key).Contains("暂时没有效果") && !Raw("cards", key).Contains("六感抗性"), key + " now has its approved replacement effect without retired resistance rules");
        Check(Raw("cards", "HOTARU_TRACE_OLD_SCAR.description").Contains("选择一种牌型（攻击、技能、能力）"), "Trace Old Scar picks a card type");
        Check(Raw("cards", "HOTARU_SIX_SENSE_REMNANTS.description").Contains("首次获得某种负面效果"), "Six Sense Remnants counts first debuffs");

        foreach (string key in new[] { "PHANTOM_POWER.description", "PHANTOM_POWER.thunderDescription", "PHANTOM_POWER.compactDescription",
                     "PHANTOM_POWER.thunderCompactDescription" })
        {
            string text = Raw("powers", key);
            Check(!text.Contains("抗性") && text.Contains("+15%") && text.Contains("最多6次"), key + " states the per-attack bonus");
        }
        Check(!Raw("powers", "DESTRUCTION_RATE_POWER.description").Contains("110%"), "Destruction Rate drops the resistance multiplier");
        Check(Raw("powers", "HOTARU_TRACE_OLD_SCAR_POWER.description").Contains("牌型"), "the Trace Old Scar power speaks of card types");

        // The manual: points only, no retired power.
        InjectGlossaryModels();
        foreach (bool hbr in new[] { true, false })
        {
            var entries = ((System.Collections.IEnumerable)Call("HbrManualGlossaryCatalog", "Build", hbr)!).Cast<object>().ToArray();
            string Id(object entry) => (string)entry.GetType().GetProperty("Id")!.GetValue(entry)!;
            string Text(object entry) => (string)entry.GetType().GetProperty("Description")!.GetValue(entry)!;
            foreach (string removed in new[] { "SIX_SENSE_RESISTANCE_POWER", "SIX_SENSE_COMBAT_POWER", "SIX_SENSE_LEADERSHIP_POWER",
                         "SIX_SENSE_GENTLENESS_POWER", "SIX_SENSE_MENTAL_STRENGTH_POWER", "GODSPEED_POWER", "HASTE_POWER",
                         "BREAK_INSIGHT_POWER" })
                Check(entries.All(entry => !Id(entry).EndsWith(":" + removed, StringComparison.Ordinal) &&
                                           !Id(entry).EndsWith("-" + removed, StringComparison.Ordinal)),
                    $"the {(hbr ? "HBR" : "native")} manual lists no {removed}");
            object[] senses = entries.Where(entry => Id(entry).StartsWith("card_keywords:HBRGO_SIX_SENSE", StringComparison.Ordinal)).ToArray();
            Check(senses.Length == 7 && senses.All(entry => !Text(entry).Contains("抗性") && !Text(entry).Contains("出目")),
                $"the {(hbr ? "HBR" : "native")} manual has the six-sense rule and six senses, none about rolls or resistances");
            string madness = Text(senses.Single(entry => Id(entry) == "card_keywords:HBRGO_SIX_SENSE.Madness"));
            Check(hbr ? madness.StartsWith("每级使战斗属性力量 +2") && madness.Contains("记忆之庭") : madness.Contains("记忆之庭"),
                $"the {(hbr ? "HBR" : "native")} Madness entry: {madness}");
        }
        MethodInfo rule = Mod.GetType("HBRGOSakiMod.Core.Nodes.Screens.NSixSenseStatusScreen", true)!.GetMethod("BuildSenseRule", F)!;
        Check(((string)Invoke(rule, null, SixSenseAttribute.Talent, false)!).Contains("金币+2%") &&
              ((string)Invoke(rule, null, SixSenseAttribute.Madness, true)!).Contains("记忆之庭"),
            "the six-sense page keeps Talent's gold and the garden total");
        Check(ElectronicSoldierManual.GetSenseChoiceDescription(SixSenseAttribute.Madness) == "疯狂等级计入记忆之庭的六感总点数。" &&
              Manual.GetSenseShortDescriptionForOwner(SixSenseAttribute.Madness) == "每级使战斗属性力量+2。上限99级。",
            "the six-sense choice texts no longer mention card rolls or resistances");
    }
}
