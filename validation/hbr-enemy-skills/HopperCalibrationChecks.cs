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
        int rng = RunState.Rng.Niche.Counter;
        using IDisposable? hit = (IDisposable?)Call(Helper("HbrDestructionGainHelper"), "BeginHit", Hero, card, ValueProp.Move);
        decimal damage = ActualDamage(Hero, Enemy, raw, card: card);
        Check(RunState.Rng.Niche.Counter == rng && Current == null, "calibration uses explicit noncritical damage without RNG");
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
        foreach (int roll in Enumerable.Range(12, 5))
        {
            Hopper(roll); int hp = Enemy.MaxHp; decimal dp = Enemy.GetPower<HighDimensionalShellPower>()!.CurrentDp;
            Check(hp == roll * 700 && dp == decimal.Floor(roll / 14m * 3000m), "normal native HP roll maps to 700x HP and independent Hopper DP: " + roll);
            Equal(Parameter(Enemy), 150m, "SmallHopper explicit E stays fixed: " + roll);
            Equal(ActualDamage(Hero, Enemy, 6m, card: strike), 3857.14m, "real shell listener adds no legacy 25% reduction in HBR: " + roll);
            Progress(false); Equal(ActualDamage(Hero, Enemy, 6m, card: strike), 4.5m, "legacy mode retains real shell 25% reduction: " + roll); Progress(true);
            var strikes = new List<Hit>();
            while (Enemy.CurrentHp > 0 && strikes.Count < 8) strikes.Add(await HitCard(strike));
            Check(strikes.Count == 4, "normal Hopper roll takes exactly four native Strikes: " + roll);
            Check(strikes[0].DpAfter == 0m && strikes[0].HpAfter == hp && strikes[0].DestructionAfter == 2,
                "breaking Strike discards overflow, preserves HP and earns 2 destruction points: " + roll);
            Check(strikes[1].DestructionAfter == 4 && strikes[2].DestructionAfter == 6,
                "later HP hits advance destruction after their own damage: " + roll);
            Hopper(roll);
            Hit bashHit = await HitCard(bash);
            Check(!Enemy.HasPower<VulnerablePower>() && bashHit.PipelineDamage == 5142.85m && bashHit.HpAfter == hp,
                "opening Bash deals unvulnerable damage and only breaks DP: " + roll);
            // Native Bash.OnPlay awaits AttackCommand.Execute before applying
            // VulnerablePower. Match that order without replacing its multiplier.
            var vulnerable = new VulnerablePower(); AddPower(Enemy, vulnerable); Set(vulnerable, "_amount", (int)bash.DynamicVars.Vulnerable.BaseValue);
            Hit second = await HitCard(strike); Hit third = await HitCard(strike);
            Equal(second.PipelineDamage, 3857.14m * 1.02m * 1.5m, "first post-Bash Strike uses native Vulnerable and 102% destruction: " + roll);
            Equal(third.PipelineDamage, 3857.14m * 1.04m * 1.5m, "second post-Bash Strike uses native Vulnerable and 104% destruction: " + roll);
            Check(second.HpAfter > 0 && third.HpAfter == 0, "Bash plus two Strikes kills every normal roll in three cards: " + roll);
            Equal(NativeCost(bash) + NativeCost(strike) * 2m, 4m, "Bash plus two Strikes costs four native energy: " + roll);
            rows.Add(new { nativeHp = roll, hp, dp, strikes, bashThenStrikes = new[] { bashHit, second, third } });
        }
        // Native difficulty8 expands the HP roll to13-17; DP enhancement starts
        // later at10. At700x HP, even its highest roll fits both card budgets.
        Set(RunState, "<AscensionLevel>k__BackingField", 8); Hopper(17);
        var hardStrikes = new List<Hit>();
        while (Enemy.CurrentHp > 0 && hardStrikes.Count < 8) hardStrikes.Add(await HitCard(strike));
        Check(hardStrikes.Count == 4 && hardStrikes[2].HpAfter > 0 && hardStrikes[3].HpAfter == 0,
            "difficulty8 native HP17 at700x HP takes exactly four Strikes");
        Hopper(17); Hit hardBash = await HitCard(bash);
        var hardVulnerable = new VulnerablePower(); AddPower(Enemy, hardVulnerable); Set(hardVulnerable, "_amount", (int)bash.DynamicVars.Vulnerable.BaseValue);
        Hit hardSecond = await HitCard(strike); Hit hardThird = await HitCard(strike);
        Check(hardSecond.HpAfter > 0 && hardThird.HpAfter == 0, "difficulty8 native HP17 at700x HP dies to Bash plus two Strikes");
        object highRoll = new { ascension = 8, nativeHp = 17, hp = Enemy.MaxHp, hardStrikes,
            bashThenTwoStrikes = new[] { hardBash, hardSecond, hardThird } };
        Set(RunState, "<AscensionLevel>k__BackingField", 0);
        Hopper(14); Powers(Hero).Clear(); Set(Hero, "_currentHp", 1000); Set(Hero, "_maxHp", 1000);
        Check(Hero.Block == 0, "Defend calibration starts without existing block");
        decimal block = MegaCrit.Sts2.Core.Hooks.Hook.ModifyBlock(Combat, Hero, defend.DynamicVars.Block.BaseValue, ValueProp.Move, defend, null, out _);
        Equal(block, 466m, "native Defend at INT100 yields 466 HBR block"); Hero.GainBlockInternal(block);
        decimal incoming = ActualDamage(Enemy, Hero, 4m);
        Equal(incoming, 400m * (1m + 50m / 140m), "Hopper fixed E150 and explicit body row deal unrounded 542.857 incoming");
        decimal absorbed = Hero.DamageBlockInternal(incoming, ValueProp.Move);
        DamageResult defenseResult = Hero.LoseHpInternal(Math.Max(0m, incoming - absorbed), ValueProp.Move);
        Check(Hero.Block == 0 && Hero.CurrentHp == 924 && defenseResult.UnblockedDamage == 76m,
            "native block-before-HP order truncates 76.857 remainder to 76 HP loss");
        CalibrationReport = new { normalRolls = rows, highRoll, defend = new { block, incoming, hpLoss = defenseResult.UnblockedDamage },
            assumptions = "HBR level-one six stats100; 700x native HP; Hopper DP independently calibrated to3000 at native HP14; default100% HP/damage and20% DP config; no crits, upgrades, senses, other modifiers or turn expiry. Bash order matches installed native IL. Counts are cards/energy budget, not same-turn playability. Normal native HP12-16 and difficulty8 maximum HP17 fit the 3-4 card budget; later difficulty DP/attack changes are not covered. Values are mod calibration data, not original HBR Hopper data." };
        CalibrationListeners = false;
    }
}
