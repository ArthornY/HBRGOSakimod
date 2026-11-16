using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static decimal FaceExecutedRaw, FaceExecutedFinal;
    static bool FaceProjectionAttackLeaf(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        FaceExecutedRaw = (decimal)typeof(AttackCommand).GetField("_damagePerHit", F)!.GetValue(__instance)!;
        FaceExecutedFinal = Preview(Enemy, __instance.Attacker, FaceExecutedRaw, __instance.DamageProps, (CardModel)__instance.ModelSource!);
        __result = Task.FromResult(__instance); return false;
    }
    static FragilePower FaceFragile(decimal percent, int remaining)
    {
        var fragile = Power<FragilePower>(Enemy, remaining);
        Native(fragile, "ConfigureHbr", percent, 1);
        return fragile;
    }
    static string FacePlain(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]+\]", "");
    static async Task FaceProjectionCases(Harmony harmony)
    {
        harmony.Patch(typeof(AttackCommand).GetMethod("Execute", F)!, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FaceProjectionAttackLeaf), F)!));
        foreach (bool artifact in new[] { false, true })
        {
            GardenReset(); FormPower(typeof(ReptureBowPower)); var card = FormCard<MimicFullBodyShotHbrgoSaki>();
            Power<BreakPower>(Enemy, 1); Power<DestructionRatePower>(Enemy, 21);
            FragilePower higher = FaceFragile(50m, 3), lower = FaceFragile(10m, 1);
            if (artifact) Power<ArtifactPower>(Enemy, 1);
            PowerModel[] originals = Enemy.Powers.ToArray();
            int niche = RngCount(RunState.Rng.Niche);
            object?[] args = [card, Enemy, 0m, 0m];
            var scope = (IDisposable)Invoke(Helper("HbrCardEffectPreview").GetMethod("FullBodyShot", F)!, null, args)!;
            decimal raw = (decimal)args[2]!, gain = (decimal)args[3]!;
            decimal projected;
            using (scope)
            {
                AbstractModel[] views = ((IEnumerable<AbstractModel>)Call("HbrCardEffectPreview", "EnumerateDamageModifiers", Enemy.Powers.Cast<AbstractModel>(), Enemy, card)!).ToArray();
                Check(views.OfType<FragilePower>().Count() == 2 && !views.Contains(originals.OfType<DestructionRatePower>().Single()),
                    "67 Break preview uses two selected Fragile views and a detached destruction view (Artifact=" + artifact + ")");
                Check(artifact ? views.Contains(lower) : !views.Contains(lower), "67 projected Fragile obeys Artifact and normal weaker-slot replacement (Artifact=" + artifact + ")");
                projected = Preview(Enemy, Hero, raw, ValueProp.Move, card);
                Equal(gain, decimal.Truncate(decimal.Floor(raw) * 1.5m), "67 projected destruction gain also uses the real bow-form power-giving modifier");
            }
            Check(Enemy.Powers.SequenceEqual(originals) && higher.Amount == 3 && lower.Amount == 1,
                "67 preview never changes actual power identity, slot contents or duration (Artifact=" + artifact + ")");
            Equal(RngCount(RunState.Rng.Niche), niche, "67 Break preview never advances critical RNG");
            string face = FacePlain(FaceRender(card));
            Check(face.Contains(decimal.Floor(projected).ToString(System.Globalization.CultureInfo.InvariantCulture)) && !face.Contains("×"),
                "67 FullBody native face prints projected damage instead of multiplication prose: " + face + "; expected=" + projected);
            FaceExecutedRaw = FaceExecutedFinal = -1m;
            await GardenPlay(card);
            Equal(FaceExecutedRaw, raw, "67 projected FullBody base damage equals the actual card after its real debuff application");
            Equal(FaceExecutedFinal, projected, "67 projected FullBody final damage equals the actual post-application damage calculation (gain=" + gain + ", actualDestruction=" + Enemy.GetPower<DestructionRatePower>()?.Amount + ", fragile=" + string.Join(',', Enemy.Powers.OfType<FragilePower>().Select(p => Native(p, "get_HbrEffectPercent"))) + ")");
        }
        GardenReset(); var noBreak = FormCard<MimicFullBodyShotHbrgoSaki>();
        Check(FacePlain(FaceRender(noBreak)).Contains("0点伤害"), "67 FullBody without Break displays zero conditional damage");
        GardenReset(); FormPower(typeof(RaptureScythePower)); var soul = FormCard<DpSoulStrikeHbrgoSaki>();
        var dp = Blank<DeflectorProtection>(); Set(dp, "<IsMutable>k__BackingField", true); Set(dp, "_owner", Player);
        Set(dp, "_hasInitializedDp", true); Set(dp, "_dp", 777);
        Set(Player, "_relics", Player.Relics.Append(dp).ToList());
        int beforeDp = dp.Dp, nicheBefore = RngCount(RunState.Rng.Niche);
        string soulFace = FacePlain(FaceRender(soul));
        decimal spent = Math.Min(beforeDp, (decimal)Call("HbrResourceValueHelper", "Fixed", Hero, soul.DynamicVars["DpLoss"].BaseValue)!);
        decimal absolute = spent * soul.DynamicVars["DamagePercent"].BaseValue / 100m;
        decimal final;
        using ((IDisposable)Call("HbrAbsoluteBaseDamageContext", "Begin", Hero, soul, Enemy, absolute)!)
            final = Preview(Enemy, Hero, absolute, ValueProp.Move, soul);
        Check(soulFace.Contains(decimal.Floor(final).ToString(System.Globalization.CultureInfo.InvariantCulture)) && !soulFace.Contains("×"),
            "67 DP Soul Strike native face prints the actual fixed-DP conversion damage");
        Equal(dp.Dp, beforeDp, "67 DP Soul Strike preview never spends or initializes a live resource pool");
        Equal(RngCount(RunState.Rng.Niche), nicheBefore, "67 DP Soul Strike preview never consumes RNG");
        Check(FaceRender(soul, true).Contains("150%）") && !soulFace.Contains("150%"), "67 absolute DP conversion percentage is inspect-only");
        FaceResourceCases();
    }
    static (decimal Current, decimal Maximum) FaceProjectedDp()
    {
        object?[] args = [Player, 0m, 0m];
        Check((bool)Invoke(Helper("HbrCardEffectPreview").GetMethod("TryGetDp", F)!, null, args)!, "67 pure DP view is available only within its active resource preview");
        return ((decimal)args[1]!, (decimal)args[2]!);
    }
    static void FaceResourceCases()
    {
        GardenReset(); var card = GardenCard<MakeAChangeKillYourselfHbrgoSaki>(4, 5);
        var pool = Blank<DeflectorProtection>(); Set(pool, "<IsMutable>k__BackingField", true); Set(pool, "_owner", Player);
        Set(pool, "_hasInitializedDp", true); Set(Player, "_relics", Player.Relics.Append(pool).ToList());
        decimal max = pool.MaxDp, current = decimal.Floor(max * .4m); Set(pool, "_dp", (int)current);
        Power<NiinaMakeupPower>(Hero, 1);
        decimal afterBase = current - Math.Min(current, decimal.Floor(max * .1m));
        decimal finalDp = afterBase - decimal.Floor(afterBase / 2m);
        using ((IDisposable)Call("HbrCardEffectPreview", "BeginDpCost", card)!)
        {
            var projected = FaceProjectedDp(); Equal(projected.Current, finalDp, "67 Fubuki preview deducts base cost before half-current compensation");
            Equal(projected.Maximum, max, "67 pure payment never changes the DP maximum");
            Equal((decimal)Call("FubukiChangeCombatHelper", "GiftMultiplier", card)!, 1m + .75m * (1m - finalDp / max), "67 Fubuki Gift reads the projected final DP rather than pre-payment DP");
            Equal((decimal)Call("NiinaMakeupCombatHelper", "GetBonusUnits", Hero, card, ValueProp.Move)!, 30m * finalDp / max * 6m / 100m,
                "67 Makeup damage preview reads post-payment DP ratio");
        }
        Equal(pool.Dp, current, "67 Fubuki payment preview leaves the actual DP ledger unchanged");
        var rescue = Power<MuuaIWillNotFallPower>(Hero, 1); Set(rescue, "<UpgradeMask>k__BackingField", 1 << 6);
        Power<DisabledPower>(Hero, 1); Set(pool, "_dp", 1);
        ExecutionContext captured;
        using (var scope = (IDisposable)Call("HbrCardEffectPreview", "BeginDpCost", card)!)
        {
            decimal recovered = decimal.Floor(max * .2m * .5m);
            var projected = FaceProjectedDp(); Equal(projected.Current, recovered - decimal.Floor(recovered / 2m),
                "67 preview applies one actual Never Fall recovery with Disabled before compensation");
            Check((bool)Call("HbrCardEffectPreview", "IsMakeupSuppressed", Player)!, "67 touching zero suppresses Makeup even when Never Fall later refills DP");
            Equal((decimal)Call("NiinaMakeupCombatHelper", "GetBonusUnits", Hero, card, ValueProp.Move)!, 0m,
                "67 Never Fall preview cannot resurrect a Makeup bonus after the zero point");
            captured = ExecutionContext.Capture()!;
        }
        Check(pool.Dp == 1 && !rescue.DpRescueUsed && Hero.GetPower<NiinaMakeupPower>() != null,
            "67 resource projection never spends the actual rescue receipt or removes an actual power");
        ExecutionContext.Run(captured, _ =>
        {
            object?[] args = [Player, 0m, 0m];
            Check(!(bool)Invoke(Helper("HbrCardEffectPreview").GetMethod("TryGetDp", F)!, null, args)! &&
                !(bool)Call("HbrCardEffectPreview", "IsMakeupSuppressed", Player)!, "67 closed resource preview is inactive even inside an earlier captured execution context");
        }, null);
    }
}
