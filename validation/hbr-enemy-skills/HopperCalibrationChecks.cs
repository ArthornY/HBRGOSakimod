using System.Globalization;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    static bool CalibrationListeners;
    static object? CalibrationReport;
    sealed record Hit(decimal PipelineDamage, decimal DpBefore, decimal DpAfter, int HpBefore, int HpAfter, int DestructionAfter);
    static List<PowerModel> Powers(Creature creature) => (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(creature)!;
    static T Card<T>() where T : CardModel, new()
    {
        var card = new T(); Set(card, "<IsMutable>k__BackingField", true); Set(card, "_owner", Player);
        return card;
    }
    static decimal NativeCost(CardModel card) => System.Convert.ToDecimal(card.EnergyCost.GetType().GetProperty("Canonical", F)!.GetValue(card.EnergyCost));
    static void CalibrationConfig()
    {
        foreach (var pair in new[] { ("_enemyMaxHpPercent", 100), ("_enemyMaxHpGrowthPerActPercent", 0),
            ("_enemyHighDimensionalShellPercent", 20), ("_enemyDpGrowthPerActPercent", 0),
            ("_enemyDamagePercent", 100), ("_enemyDamageGrowthPerActPercent", 0) })
            typeof(HBRGOSakiConfig).GetField(pair.Item1, F)!.SetValue(null, pair.Item2);
        Set(RunState, "_currentActIndex", 0); Set(RunState, "<AscensionLevel>k__BackingField", 0);
    }
    static Creature Hopper(int nativeHp)
    {
        Enemy = Creature(Blank<SmallHopper>()); Move(Enemy, "ATTACK_MOVE");
        Set(Enemy, "_maxHp", nativeHp); Set(Enemy, "_currentHp", nativeHp);
        Set(Combat, "_enemies", new List<Creature> { Enemy });
        Call(Helper("EnemyHealthScalingHelper"), "ApplyInitialHealth", Enemy);
        var shell = new HighDimensionalShellPower(); AddPower(Enemy, shell);
        typeof(HighDimensionalShellPower).GetProperty("CurrentDpData", F)!.SetValue(shell,
            HighDimensionalShellPower.CalculateAmount(Enemy).ToString(CultureInfo.InvariantCulture));
        return Enemy;
    }
    // The production shell absorber, exact DP writes, native block/HP mutations,
    // AfterDamageReceived receipts and fixed destruction gain all execute. Only
    // the visual/turn lifecycle of attaching Break/Destruction and removing the
    // shell is controlled; no native damage amount or DP decision is replaced.
    static bool BreakLifecycle(HighDimensionalShellPower __instance, ref Task __result)
    {
        Creature owner = __instance.Owner;
        var broken = new BreakPower(); AddPower(owner, broken); Set(broken, "_amount", HighDimensionalShellPower.NormalBreakAmount);
        var destruction = new DestructionRatePower(); AddPower(owner, destruction); Set(destruction, "_amount", DestructionRatePower.BaselineAmount);
        Powers(owner).Remove(__instance);
        __result = Task.CompletedTask; return false;
    }
    static async Task<Hit> HitCard(CardModel card)
    {
        decimal raw = card.DynamicVars.Damage.BaseValue;
        HighDimensionalShellPower? shell = Enemy.GetPower<HighDimensionalShellPower>();
        PowerModel[] receipts = Enemy.Powers.ToArray();
        decimal beforeDp = shell?.CurrentDp ?? 0m; int beforeHp = Enemy.CurrentHp;
        int rng = RngCount(RunState.Rng.Niche);
        using IDisposable? hit = (IDisposable?)Call(Helper("HbrDestructionGainHelper"), "BeginHit", Hero, card, ValueProp.Move);
        decimal damage = ActualDamage(Hero, Enemy, raw, card: card);
        Check(RngCount(RunState.Rng.Niche) == rng && Current == null, "calibration uses explicit noncritical damage without RNG");
        foreach (PowerModel power in receipts) await power.BeforeDamageReceived(null!, Enemy, damage, ValueProp.Move, Hero, card);
        decimal blocked = Enemy.DamageBlockInternal(damage, ValueProp.Move);
        DamageResult result = Enemy.LoseHpInternal(Math.Max(0m, damage - blocked), ValueProp.Move);
        foreach (PowerModel power in receipts) await power.AfterDamageReceived(null!, Enemy, result, ValueProp.Move, Hero, card);
        return new(damage, beforeDp, shell?.CurrentDp ?? 0m, beforeHp, Enemy.CurrentHp,
            Enemy.GetPower<DestructionRatePower>()?.DisplayAmount ?? 0);
    }
    static async Task HopperCalibration(Harmony harmony)
    {
        foreach (string patch in new[] { "CreatureDamageBlockInternalPatch", "HighDimensionalShellNativeAmountPatch" })
            Check(harmony.CreateClassProcessor(PatchType(patch)).Patch().Count > 0, "calibration production patch installs " + patch);
        Patch(harmony, typeof(HighDimensionalShellPower).GetMethod("ResolveBreak", F)!, nameof(BreakLifecycle));
        CalibrationConfig(); ResetSenses(); Progress(true); CalibrationListeners = true;
        var strike = Card<StrikeIronclad>(); var bash = Card<Bash>(); var defend = Card<DefendIronclad>();
        Equal(strike.DynamicVars.Damage.BaseValue, 6m, "native unupgraded Ironclad Strike damage");
        Equal(bash.DynamicVars.Damage.BaseValue, 8m, "native unupgraded Ironclad Bash damage");
        Equal(bash.DynamicVars.Vulnerable.BaseValue, 2m, "native unupgraded Bash Vulnerable amount");
        Equal(defend.DynamicVars.Block.BaseValue, 5m, "native unupgraded Ironclad Defend block");
        Equal(NativeCost(strike), 1m, "native Strike energy cost"); Equal(NativeCost(bash), 2m, "native Bash energy cost");
        Equal(NativeCost(defend), 1m, "native Defend energy cost");
        var rows = new List<object>();
        foreach (int roll in Enumerable.Range(12, 6))
        {
            Set(RunState, "<AscensionLevel>k__BackingField", roll == 17 ? 8 : 0);
            // Updated 2026-11-14: opening total12800, original SmallCrest DP share37.5%.
            // HP floor happens before a fresh DP pool derives from that adapted HP.
            int hp = (int)decimal.Floor(roll * 8000m / 14m);
            decimal dp = decimal.Floor(hp * 4800m / 8000m);
            var sequences = new List<object>();
            foreach (bool bashFirst in new[] { false, true })
            {
                Hopper(roll);
                Equal(Enemy.MaxHp, hp, "redistributed opening Hopper HP: " + roll);
                Equal(Enemy.GetPower<HighDimensionalShellPower>()!.CurrentDp, dp, "redistributed opening Hopper DP: " + roll);
                Equal(Parameter(Enemy), 150m, "opening E150 is independent of resource share: " + roll);
                decimal expectedDp = dp; int expectedHp = hp, destruction = 0;
                var hits = new List<Hit>();
                while (Enemy.CurrentHp > 0 && hits.Count < 8)
                {
                    bool isBash = bashFirst && hits.Count == 0;
                    // Independent arithmetic: coefficient6/8, stats100 versusE150;
                    // ordinary coefficient3 x enemy coefficient2 =6 destruction per hit.
                    decimal skill = decimal.Floor((isBash ? 8000m : 6000m) * (1m - 50m / 140m) * 100m) / 100m;
                    decimal damage = decimal.Floor(skill * (1m + destruction / 100m) * (bashFirst && hits.Count > 0 ? 1.5m : 1m));
                    decimal beforeDp = expectedDp; int beforeHp = expectedHp;
                    if (expectedDp > 0m)
                    {
                        expectedDp = Math.Max(0m, expectedDp - damage);
                        if (expectedDp == 0m) destruction += 6;
                    }
                    else
                    {
                        expectedHp = Math.Max(0, expectedHp - (int)damage);
                        destruction += 6;
                    }
                    Hit actual = await HitCard(isBash ? bash : strike); hits.Add(actual);
                    Equal(actual.PipelineDamage, damage, $"Hopper{roll} sequence{bashFirst} hit{hits.Count} damage");
                    Check(actual.DpBefore == beforeDp && actual.DpAfter == expectedDp && actual.HpBefore == beforeHp && actual.HpAfter == expectedHp,
                        "real shell and HP preserve the independent resource trajectory");
                    Equal(actual.DestructionAfter, destruction, "current skill-coefficient destruction replaces obsolete fixed2 expectation");
                    if (isBash)
                    {
                        var vulnerable = new VulnerablePower(); AddPower(Enemy, vulnerable);
                        Set(vulnerable, "_amount", (int)bash.DynamicVars.Vulnerable.BaseValue);
                    }
                }
                Check(Enemy.CurrentHp == 0 && hits.Count >= 3 && hits.Count <= 5,
                    "unchanged damage with redistributed pools kills within measured3to5 card budget");
                sequences.Add(new { bashFirst, cards = hits.Count, energy = hits.Count + (bashFirst ? 1 : 0), hits });
            }
            rows.Add(new { nativeHp = roll, hp, dp, ascension = roll == 17 ? 8 : 0, sequences });
        }
        Set(RunState, "<AscensionLevel>k__BackingField", 0);
        CalibrationReport = new { rows,
            assumptions = "Current opening profile native14 -> HP8000/DP4800 (total12800 unchanged); native rolls12..16 and difficulty8 max17. Stats100 versusE150, no crit/dice/turn expiration. Current ordinary skill coefficient3 x enemy coefficient2 gives6 destruction per complete hit. Bash native Vulnerable is applied after its attack. Counts are cards, not same-turn playability. Managed real resource pipeline; no rendered game combat." };
        CalibrationListeners = false;
    }

    // The opening Hopper's attacks against a native Defend. Since the fourth
    // move-table batch (2026-11-08) the Hopper follows the original
    // CrestHopperSmall table (normal attack on steps 1 and 3, attack or combo on
    // step 2, combo on steps 4-5). Split out of the historical card-budget
    // calibration so that it runs in the green segment.
    static object? OpeningAttackReport;
    static void HopperOpeningAttack()
    {
        CalibrationConfig(); ResetSenses(); Progress(true); CalibrationListeners = true;
        try
        {
            var defend = Card<DefendIronclad>();
            Equal(defend.DynamicVars.Block.BaseValue, 5m, "native unupgraded Ironclad Defend block");
            Hopper(14); Powers(Hero).Clear(); Set(Hero, "_currentHp", 1000); Set(Hero, "_maxHp", 1000);
            Check(Hero.Block == 0, "Defend calibration starts without existing block");
            decimal block = MegaCrit.Sts2.Core.Hooks.Hook.ModifyBlock(Combat, Hero, defend.DynamicVars.Block.BaseValue, ValueProp.Move, defend, null, out _);
            Equal(block, 500m, "native Defend at INT100 yields 500 HBR block");
            // The table and the damage come from the production class.
            object tables = typeof(SmallHopper).GetField("Tables", F)!.GetValue(null)!;
            object table = tables.GetType().GetProperty("Dp")!.GetValue(tables)!;
            object firstStep = ((System.Collections.IList)table.GetType().GetProperty("Steps")!.GetValue(table)!)[0]!;
            object firstOption = ((System.Collections.IList)firstStep.GetType().GetProperty("Options")!.GetValue(firstStep)!)[0]!;
            Check((string)firstOption.GetType().GetProperty("Skill")!.GetValue(firstOption)! == "CrestHopper_DefaultAttack",
                "the opening SmallHopper starts its dp table with the normal attack");
            int attack = (int)typeof(SmallHopper).GetProperty("AttackDamage", F)!.GetValue(Enemy.Monster)!;
            int combo = (int)typeof(SmallHopper).GetProperty("ComboDamage", F)!.GetValue(Enemy.Monster)!;
            int hits = (int)typeof(SmallHopper).GetField("ComboHits", F)!.GetValue(null)!;
            Check(attack == 3 && combo == 1 && hits == 8, "difficulty-0 SmallHopper: normal attack 3, combo 1 x8");
            Hero.GainBlockInternal(block);
            decimal incoming = ActualDamage(Enemy, Hero, attack);
            Equal(incoming, decimal.Floor(300m * (1m + 50m / 140m)), "opening Hopper E150 and explicit body row floor its normal attack to407");
            decimal absorbed = Hero.DamageBlockInternal(incoming, ValueProp.Move);
            DamageResult defenseResult = Hero.LoseHpInternal(Math.Max(0m, incoming - absorbed), ValueProp.Move);
            Check(Hero.Block == 93 && Hero.CurrentHp == 1000 && defenseResult.UnblockedDamage == 0m,
                "native block500 takes the whole normal attack407 and keeps93");
            Set(Hero, "_block", 0); Set(Hero, "_currentHp", 1000);
            Move(Enemy, "COMBO_MOVE"); Hero.GainBlockInternal(block);
            decimal perHit = ActualDamage(Enemy, Hero, combo);
            Equal(perHit, decimal.Floor(100m * (1m + 50m / 140m)), "each combo hit uses the same body row and floors to135");
            decimal comboLoss = 0m;
            for (int hit = 0; hit < hits; hit++)
            {
                decimal stopped = Hero.DamageBlockInternal(perHit, ValueProp.Move);
                comboLoss += Hero.LoseHpInternal(Math.Max(0m, perHit - stopped), ValueProp.Move).UnblockedDamage;
            }
            Check(Hero.Block == 0 && comboLoss == 580m && Hero.CurrentHp == 420,
                "the 1x8 combo (8x135 = 1080) after native block500 leaves580 HP loss");
            Move(Enemy, "ATTACK_MOVE");
            OpeningAttackReport = new { block, normalAttack = new { attack, incoming, hpLoss = defenseResult.UnblockedDamage },
                combo = new { combo, hits, perHit, hpLoss = comboLoss },
                assumptions = "Opening act-one floor-one monster room (E150), HBR level-one six stats100, native Defend at INT100, no crits, senses or other modifiers; per-hit HBR floor; production SmallHopper move values." };
        }
        finally
        {
            CalibrationListeners = false;
        }
    }
}
