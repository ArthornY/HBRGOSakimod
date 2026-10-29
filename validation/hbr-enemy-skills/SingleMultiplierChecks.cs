using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    static object? AsyncValue(Type type, string field) => type.GetField(field, F)!.GetValue(null)!
        .GetType().GetProperty("Value")!.GetValue(type.GetField(field, F)!.GetValue(null));
    static void AsyncValue(Type type, string field, object? value)
    {
        object local = type.GetField(field, F)!.GetValue(null)!;
        local.GetType().GetProperty("Value")!.SetValue(local, value);
    }
    static object NewScope(Type helper, string name) => Activator.CreateInstance(helper.GetNestedType(name, F)!, nonPublic: true)!;
    static decimal WithCannon(Creature cannon, decimal amount, Func<decimal> calculation)
    {
        Type helper = Helper("HotaruCannonStatsHelper"); object? previous = AsyncValue(helper, "Pending");
        object intent = NewScope(helper, "Intent");
        Set(intent, "Cannon", cannon); Set(intent, "Target", Enemy); Set(intent, "Dealer", Hero);
        Set(intent, "Amount", amount); Set(intent, "Props", ValueProp.Move);
        AsyncValue(helper, "Pending", intent);
        object?[] args = [new[] { Enemy }, amount, ValueProp.Move, Hero, null, null];
        object scope = helper.GetMethod("Begin", F)!.Invoke(null, args)!;
        try
        {
            Check((bool)helper.GetMethod("IsCurrentDamage", F)!.Invoke(null,
                [Enemy, Hero, null, ValueProp.Move])!, "production cannon Begin claims its matching owner-sourced hit");
            return calculation();
        }
        finally { Call(helper, "Close", scope); AsyncValue(helper, "Pending", previous); }
    }
    static void SingleMultiplierChecks()
    {
        Creature original = Enemy; CalibrationConfig(); ResetSenses(); Progress(true); CalibrationListeners = false;
        Enemy = Creature(Blank<SmallHopper>()); Move(Enemy, "ATTACK_MOVE");
        Set(Combat, "_enemies", new List<Creature> { Enemy }); EnemyMap(0, 1);
        var strike = Card<StrikeIronclad>(); var modStrike = Card<StrikeHbrgoSaki>();
        Equal(strike.DynamicVars.Damage.BaseValue, 6m, "single coefficient native Strike stores raw six units");
        Equal(modStrike.DynamicVars.Damage.BaseValue, 6m, "single coefficient mod Strike stores raw six units");
        Equal(Number(Helper("HbrSkillPower"), "DamagePercent", strike.DynamicVars.Damage.BaseValue), 100m,
            "native Strike maps to existing 100 percent coefficient");
        typeof(StrikeIronclad).GetMethod("OnUpgrade", F)!.Invoke(strike, null);
        typeof(StrikeHbrgoSaki).GetMethod("OnUpgrade", F)!.Invoke(modStrike, null);
        Equal(strike.DynamicVars.Damage.BaseValue, 9m, "native Strike real OnUpgrade retains nine units");
        Equal(modStrike.DynamicVars.Damage.BaseValue, 9m, "mod Strike real OnUpgrade retains nine units");
        Equal(Number(Helper("HbrSkillPower"), "DamagePercent", modStrike.DynamicVars.Damage.BaseValue), 150m,
            "mod Strike upgraded coefficient remains 150 percent");
        foreach (CardModel upgraded in new CardModel[] { strike, modStrike })
        {
            decimal amount = upgraded.DynamicVars.Damage.BaseValue;
            Equal(Hook.ModifyDamage(RunState, Combat, Enemy, Hero, amount, ValueProp.Move, upgraded,
                ModifyDamageHookType.All, CardPreviewMode.None, out _), 5785.71m,
                upgraded.GetType().Name + " upgraded preview uses 150 percent with one linear attribute factor");
            Equal(ActualDamage(Hero, Enemy, amount, card: upgraded), 5785.71m,
                upgraded.GetType().Name + " upgraded actual agrees with native preview without critical scope");
        }
        Creature phantom = Creature(Blank<TestSubject>(), owner: Player); AddPower(phantom, Blank<PhantomPower>());
        Creature cannon = Creature(Blank<HotaruContestCannon>(), owner: Player);
        var millsage = new Millsage(); Set(millsage, "<IsMutable>k__BackingField", true); Set(millsage, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual, millsage });
        Set(Combat, "_allies", new List<Creature> { Hero, phantom, cannon });
        foreach (Creature dealer in new[] { Hero, phantom, cannon })
        {
            Equal(Convert(dealer, Enemy, 6m), 3857.14m,
                dealer.Monster?.GetType().Name + " shares the same owner-attribute 100 percent base conversion");
            Equal(Preview(dealer, Enemy, 6m), 3857.14m,
                dealer.Monster?.GetType().Name + " native preview shares one common damage base");
            Equal(ActualDamage(dealer, Enemy, 6m), 3857.14m,
                dealer.Monster?.GetType().Name + " production actual wrapper shares one common damage base");
        }
        decimal cannonActual = WithCannon(cannon, 6m, () => ActualDamage(Hero, Enemy, 6m));
        Equal(cannonActual, 1928.57m, "owner-sourced cannon applies its default 50 percent Soul Power exactly once");
        int beforePreviewRng = RunState.Rng.Niche.Counter;
        Equal(Number(Helper("HotaruCannonStatsHelper"), "PreviewDamage", cannon, 6m, false, null,
            Enum.Parse(Helper("HotaruCannonPhase"), "End")), decimal.Floor(cannonActual),
            "real cannon intent preview is the displayed floor of actual owner-sourced damage");
        Check(beforePreviewRng == RunState.Rng.Niche.Counter, "real cannon preview never consumes a critical sample");
        Check(!((bool)Helper("HotaruCannonStatsHelper").GetProperty("IsDamagePreview", F)!.GetValue(null)!),
            "real cannon preview restores its preview identity after evaluation");

        Sense("Madness", 150); // STR400 / DEX100: HP reference300, DP reference200.
        Equal(Convert(Hero, Enemy, 6m), 12428.57m, "HP weighted reference300 extends linearly beyond old delta140 threshold");
        var shell = new HighDimensionalShellPower(); AddPower(Enemy, shell);
        typeof(HighDimensionalShellPower).GetProperty("CurrentDpData", F)!.SetValue(shell, "100");
        Equal(Convert(Hero, Enemy, 6m), 8142.85m, "active DP uses STR one DEX two rather than HP weights");
        Equal(ActualDamage(phantom, Enemy, 6m), 8142.85m, "phantom actual damage uses the same DP attribute weights");
        Powers(Enemy).Remove(shell);
        Sense("Madness", 200); Sense("LeadershipCharm", 200); // Both500 against E150 =>2.5x + base1x.
        foreach (Creature dealer in new[] { Hero, phantom, cannon })
            Equal(ActualDamage(dealer, Enemy, 6m), 21000m,
                dealer.Monster?.GetType().Name + " keeps linear growth above old upper endpoint");
        ResetSenses(); Sense("Talent", 50); // INT200.
        foreach (Creature dealer in new[] { Hero, phantom, cannon })
        {
            Equal(Hook.ModifyBlock(Combat, dealer, 5m, ValueProp.Move, null, null, out _), 1000m,
                dealer.Monster?.GetType().Name + " 100 percent block at INT200 is1000");
            Equal(Hook.ModifyBlock(Combat, dealer, 5m, ValueProp.Unpowered, null, null, out _), 5m,
                dealer.Monster?.GetType().Name + " Unpowered block keeps already resolved units");
        }
        Equal(Number(Helper("HbrResourceValueHelper"), "ConvertFixedRecovery", Hero, 5m), 500m,
            "fixed HP/DP recovery remains x100 and does not acquire the block INT factor");
        Type blockHelper = Helper("HotaruCannonBlockHelper"); object? previousGain = AsyncValue(blockHelper, "Current");
        object gain = NewScope(blockHelper, "GainScope"); Set(gain, "Owner", Hero); Set(gain, "Enabled", true);
        Set(gain, "ResolvedAmount", true); AsyncValue(blockHelper, "Current", gain);
        try
        {
            Equal(Hook.ModifyBlock(Combat, Hero, 250m, ValueProp.Move, null, null, out _), 250m,
                "actual-damage-derived cannon block bypasses coefficient conversion at INT200");
        }
        finally { AsyncValue(blockHelper, "Current", previousGain); }
        using ((IDisposable)Call(Helper("HbrAbsoluteBaseDamageContext"), "Begin", Hero, null, Enemy, 250m)!)
            Equal(ActualDamage(Hero, Enemy, 250m), 250m,
                "resource-derived attack bypasses skill and stat conversion in real actual pipeline");
        ResetSenses();
        foreach (Creature dealer in new[] { Hero, phantom, cannon })
            Equal(Hook.ModifyBlock(Combat, dealer, 5m, ValueProp.Move, null, null, out _), 500m,
                dealer.Monster?.GetType().Name + " 100 percent block at INT100 is500");
        object critical = Call(Context, "Begin", Hero, null, ValueProp.Move)!;
        Set(critical, "Sample", 0); int criticalRng = RunState.Rng.Niche.Counter;
        try
        {
            Equal(Preview(Hero, Enemy, 6m), 3857.14m, "forced actual critical scope does not affect preview");
            Equal(ActualDamage(Hero, Enemy, 6m), 9000m,
                "critical reduces E150 to100 before attributes then multiplies6000 by1.5");
            Equal(ActualDamage(Hero, Enemy, 6m), 9000m, "critical repeat reuses the same deterministic sampled hit");
            Check(RunState.Rng.Niche.Counter == criticalRng && Sample(critical) == 0,
                "controlled replay sample is reused without extra RNG draws");
            Equal(WithCannon(cannon, 6m, () => ActualDamage(Hero, Enemy, 6m)), 4500m,
                "critical cannon retains E reduction then1.5 critical and a single50 percent Soul factor");
        }
        finally { Call(Context, "Close", critical); }
        Progress(false);
        Equal(ActualDamage(Hero, Enemy, 6m), 6m, "disabled mode retains original card damage after unified-route checks");
        Equal(Hook.ModifyBlock(Combat, Hero, 5m, ValueProp.Move, null, null, out _), 5m,
            "disabled mode retains original block after unified-route checks");
        Progress(true); ResetSenses(); Powers(Hero).Clear();
        Set(Player, "_relics", new List<RelicModel> { Manual });
        Set(Combat, "_allies", new List<Creature> { Hero });
        Enemy = original; Set(Combat, "_enemies", new List<Creature> { Enemy }); EnemyMap(0, 1, room: null);
    }
}
