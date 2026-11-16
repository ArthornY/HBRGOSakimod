using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

public sealed class RisaTestShell : HighDimensionalShellPower { protected override decimal MaximumAmount => 100m; }
static class RisaMonsterStandalone { public static void Run(string path) => EffectChecks.RunRisaMonsters(path); }
static partial class EffectChecks
{
    static DeflectorProtection RisaDp(Player player)
    {
        var dp = Blank<DeflectorProtection>(); Set(dp, "<IsMutable>k__BackingField", true); Set(dp, "_owner", player);
        Set(dp, "<Id>k__BackingField", ModelDb.GetId<DeflectorProtection>());
        Set(player, "_relics", player.Relics.Append(dp).ToList()); dp.EnsureDpInitialized(); dp.LoseDpByCurrentPercent(1m); return dp;
    }
    static async Task<RisaTestShell> RisaPet(Player player)
    {
        Creature pet = (Creature)H("Actor", Blank<SmallHopper>(), null, player)!; Power<PhantomPower>(pet, 1);
        Set(Combat, "_allies", Combat.Allies.Append(pet).Distinct().ToList());
        await PowerCmd.Apply<RisaTestShell>(new ThrowingPlayerChoiceContext(), pet, 1m, player.Creature, null);
        return pet.GetPower<RisaTestShell>()!;
    }
    static Task RisaPlay(AlwaysKeepYourChinUpHbrgoSaki card, Creature? target = null)
    {
        var play = Play(card); Set(play, "<Target>k__BackingField", target ?? Hero);
        return (Task)Native(card, "OnPlay", new ThrowingPlayerChoiceContext(), play)!;
    }
    static void RisaReset() { GardenReset(); Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy }); }
    static async Task RisaBodyCases()
    {
        RisaReset(); var dp = RisaDp(Player); var basic = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>();
        Check(basic.MaxUpgradeLevel == 7 && basic.CanonicalKeywords.Contains(CardKeyword.Exhaust) && basic.TargetType == TargetType.Self,
            "61 base card is seven-upgrade Exhaust and selects self automatically when alone");
        Equal(basic.EnergyCost.GetWithModifiers(CostModifiers.All), 1m, "61 base card costs one energy");
        await RisaPlay(basic); Equal(dp.Dp, decimal.Floor(dp.MaxDp * .2m), "61 actual base body restores twenty-percent of the hero's own real DP maximum");
        RisaTestShell pet = await RisaPet(Player); Native(pet, "RestoreWithLimit", 8m, false);
        Check(basic.TargetType == TargetType.AnyAlly, "61 a living phantom enables ally selection");
        Creature unrelatedPet = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
        Set(Combat, "_allies", Combat.Allies.Append(unrelatedPet).ToList());
        Check(basic.IsValidTarget(pet.Owner) && !basic.IsValidTarget(unrelatedPet), "61 native target validation selects actual eligible phantom and rejects unrelated pets instead of silently healing self");
        decimal oldDp = pet.CurrentDp; int heroDp = dp.Dp;
        await RisaPlay(basic, pet.Owner); Equal(pet.CurrentDp, oldDp + 20m, "61 selected phantom receives twenty-percent of its own100DP pool");
        Equal(dp.Dp, heroDp, "61 a selected phantom never redirects its heal into the hero pool");
        var smile = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>(2); Native(pet, "RestoreWithLimit", 100m, false);
        await RisaPlay(basic, pet.Owner); Equal(pet.CurrentDp, 100m, "61 ordinary recovery stays capped at the normal maximum");
        await RisaPlay(smile, pet.Owner); Equal(pet.CurrentDp, 120m, "61 Healing Smile uses the real120percent overcap and preserves maximum100");
        Equal(Convert.ToDecimal(Native(pet, "get_MaxAmount")), 100m, "61 overflow never raises a phantom DP maximum");
        var all = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>(0, 1, 2, 3, 6);
        Player peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature, pet.Owner });
        var peerDp = RisaDp(peer); await PowerCmd.Apply<HbrMoralePower>(new ThrowingPlayerChoiceContext(), peer.Creature, 8m, Hero, null);
        await (Task)Native(pet, "DamageDpOnly", new ThrowingPlayerChoiceContext(), 80m, Enemy, null, null)!;
        decimal petBefore = pet.CurrentDp; heroDp = dp.Dp;
        await RisaPlay(all);
        Check(all.TargetType == TargetType.AllAllies && all.EnergyCost.GetWithModifiers(CostModifiers.All) == 0,
            "61 Saturated Recovery targets all allies and Flash really removes one energy");
        Equal(dp.Dp, Math.Min(decimal.Floor(dp.MaxDp * 1.2m), heroDp + decimal.Floor(dp.MaxDp * .3m)), "61 saturation heals hero pool by thirty-percent exactly once");
        Equal(peerDp.Dp, decimal.Floor(peerDp.MaxDp * .3m), "61 saturation heals the actual teammate pool");
        Equal(pet.CurrentDp, petBefore + 30m, "61 saturation heals phantom pool separately without30A double redirect");
        Check(Hero.GetPowerAmount<HbrMoralePower>() == 4 && peer.Creature.GetPowerAmount<HbrMoralePower>() == 10 && pet.Owner.GetPowerAmount<HbrMoralePower>() == 4,
            "61 Heartful Explosion uses full-team Morale4 and native level10 maximum");
        Check(Enemy.Powers.OfType<HbrAttackDownPower>().Any(power => power.Amount == 2), "61 Ara Ara applies actual framework AttackDown with two enemy turns");
    }
    static async Task RisaCleanseAndPassiveCases()
    {
        RisaReset(); var dp = RisaDp(Player); var card = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>(4);
        await RisaPlay(card); Check(!Hero.HasPower<MindsEyePower>(), "61 Butterfly gives no MindsEye when no debuff was removed");
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), Hero, 2m, Enemy, null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Hero, -1m, Enemy, null);
        await PowerCmd.Apply<HbrCalamityPower>(new ThrowingPlayerChoiceContext(), Hero, 20m, Enemy, null);
        await RisaPlay(card);
        Check(!Hero.HasPower<WeakPower>() && !Hero.HasPower<VulnerablePower>() && !Hero.HasPower<StrengthPower>() && Hero.HasPower<HbrCalamityPower>(),
            "61 Butterfly removes at most three real debuff instances including negative Strength and leaves the fourth");
        Check(Hero.Powers.OfType<MindsEyePower>().Count() == 1, "61 actual debuff removal grants exactly one framework MindsEye instance");
        await RisaPlay(card); Check(!Hero.HasPower<HbrCalamityPower>() && Hero.Powers.OfType<MindsEyePower>().Count() == 2,
            "61 a later actual cleanse can grant a second independent MindsEye slot");
        await RisaPlay(card); Check(Hero.Powers.OfType<MindsEyePower>().Count() == 2, "61 an empty later cleanse never refreshes or replaces existing MindsEye");
        RisaReset(); dp = RisaDp(Player); Native(dp, "RestoreDpByMaxPercentWithLimit", .95m, false);
        RisaTestShell pet = await RisaPet(Player); Native(pet, "RestoreWithLimit", 94m, false);
        var permanent = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>(5); GardenPermanent(permanent);
        var duplicate = GardenCard<AlwaysKeepYourChinUpHbrgoSaki>(5); GardenPermanent(duplicate);
        Player peer = SupportTeammate(); var peerDp = RisaDp(peer); Set(RunState, "_players", new List<Player> { Player, peer });
        Set(Combat, "_allies", new List<Creature> { Hero, pet.Owner, peer.Creature });
        await (Task)Call("RisaRecoveryCombatHelper", "BeforeCombatStart", Player)!;
        Equal(dp.Dp, decimal.Floor(dp.MaxDp * .95m) + decimal.Floor(dp.MaxDp * .1m), "61 permanent Hospitality grants real start-of-combat ten-percent overcap without drawing the card");
        Equal(pet.CurrentDp, 105m, "61 permanent Hospitality includes the owner's independent phantom DP pool");
        Equal(peerDp.Dp, 0m, "61 Hospitality passive does not heal another player's hero");
        int first = dp.Dp; await (Task)Call("RisaRecoveryCombatHelper", "BeforeCombatStart", Player)!;
        Equal(dp.Dp, first, "61 duplicate permanent sources and repeated entry hooks cannot replay Hospitality");
    }
    static async Task RisaMonsterCases()
    {
        foreach (Type type in new[] { typeof(AlwaysKeepYourChinUpHbrgoSaki), typeof(RisaTestShell), typeof(PhantomPower), typeof(MindsEyePower),
            typeof(HbrMoralePower), typeof(HbrCalamityPower), typeof(HbrAttackDownPower), typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        var h = new Harmony(ProductionId + ".risa61");
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HighDimensionalShellNativeAmountPatch", true)!).Patch();
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.RisaRecoveryTargetPatch", true)!).Patch();
            await RisaBodyCases(); await RisaCleanseAndPassiveCases();
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunRisaMonsters(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); RisaMonsterCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual61 card bodies, native DP pools, native power application/removal, owner-scoped permanent passive and framework status instances; visual/texture/wait leaves isolated. No Godot or rendered game session." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Risa/monster checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
