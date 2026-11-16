using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using HBRGOSakiMod.Core.Models.Relics;

static class CardFaceStandalone { public static void Run(string path) => EffectChecks.RunCardFace(path); }
static partial class EffectChecks
{
    static readonly Type[] FaceCards = [typeof(FreshDeathHbrgoSaki), typeof(TengenBrilliantMoveHbrgoSaki), typeof(PeacefulMemoriesHbrgoSaki),
        typeof(AlwaysKeepYourChinUpHbrgoSaki), typeof(MakeAChangeKillYourselfHbrgoSaki), typeof(AcePlayerIsNotMeHbrgoSaki),
        typeof(DiveAsDeepAsIWantHbrgoSaki), typeof(ImmortalRecollectionHbrgoSaki), typeof(StarsGuideYouHbrgoSaki),
        typeof(WorldIsMyLaboratoryHbrgoSaki), typeof(WorldConquestHbrgoSaki), typeof(ItsNinjaShowtimeHbrgoSaki),
        typeof(InnocentRequiemHbrgoSaki), typeof(HellfireBurnHbrgoSaki), typeof(FrozenMistMeowHbrgoSaki),
        typeof(HelloWorldHbrgoSaki), typeof(MoreThanOneTruthHbrgoSaki), typeof(SaviorGrandEntranceHbrgoSaki),
        typeof(MyLegendBeginsHbrgoSaki), typeof(WorldsFinestHbrgoSaki), typeof(CrushYouLikeABugHbrgoSaki),
        typeof(PaintTheWorldMyColorHbrgoSaki), typeof(EldestOshimaIsHereHbrgoSaki), typeof(AllOrdersConfirmedHbrgoSaki),
        typeof(NarcissismIsFineHbrgoSaki), typeof(YourCombatDeliveryIsHereHbrgoSaki), typeof(AbsolutelyNoEffortHbrgoSaki),
        typeof(OpenTheLockHbrgoSaki), typeof(IWillNotFallHbrgoSaki)];
    static readonly Type[] FaceLateCards = [typeof(NoblesseObligeHbrgoSaki), typeof(ExorcisingStringResoundHbrgoSaki),
        typeof(WindForestFireMountainHbrgoSaki), typeof(HarvestBountifulGrainsHbrgoSaki),
        typeof(CuteIsInvincibleHbrgoSaki), typeof(SlashIfCloseHbrgoSaki),
        typeof(AsTheYoungLadyCommandsHbrgoSaki), typeof(LetMeShowYouClassHbrgoSaki), typeof(ThisIsMyStageHbrgoSaki),
        typeof(LeaveAnyBattlefieldToMeHbrgoSaki), typeof(LegacyFlashHbrgoSaki), typeof(FollowBehindMeHbrgoSaki),
        typeof(ReadySteadyDieHbrgoSaki), typeof(ThereIsOnlyOneTruthHbrgoSaki),
        typeof(WitnessMySupremacyHbrgoSaki), typeof(LoveCharlottaHbrgoSaki),
        typeof(RepentanceTimeHbrgoSaki), typeof(NumbersAreTheSourceHbrgoSaki)];
    static string FaceRender(CardModel card, bool detail = false)
    {
        using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F).Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, detail, false, Enemy)!;
        return card.GetDescriptionForPile(PileType.Hand);
    }
    // Development-only preflight for the root-owned one-line native-variable
    // addition. This mode is explicitly marked in its report and never used
    // as final production evidence.
    static bool FaceNativeUpgradeVariables(CardModel card, string suffix, ref string __result)
    {
        if (card is BoostUpHbrgoSaki && suffix is "0" or "5" or "6" &&
            (bool)AccessTools.PropertyGetter(card.GetType(), "UsesThirtyAEffects").Invoke(card, null)!) suffix += ".30a";
        string key = card.Id.Entry + ".upgrade_effect." + suffix;
        bool enabled = (bool)Call("HbrSkillCardDescription", "IsEnabled", card)!;
        if (enabled && new LocString("cards", key + ".hbr").Exists()) key += ".hbr";
        LocString description = new("cards", key);
        description.Add("energyPrefix", MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPrefix(card));
        card.DynamicVars.AddTo(description);
        Call("HbrSkillCardDescription", enabled ? "AddParameters" : "AddLegacyParameters", card, description);
        Call("HbrSkillCardDescription", "AddSupplementalDamageParameters", card, description);
        __result = description.GetFormattedText(); return false;
    }
    static void CheckNoRulesHover(CardModel card, string label)
    {
        var tips = card.HoverTips.OfType<HoverTip>().ToArray();
        Check(tips.All(tip => !tip.Title.Contains("卡牌规则") &&
            !((string?)Call("HoverTipCompatibility", "KeywordId", tip) ?? string.Empty).StartsWith("HBR_CARD_RULES_", StringComparison.Ordinal)),
            "rules are on the face; ordinary hover remains: " + label);
    }
    static void RestoredEffectCases()
    {
        GardenReset();
        string Text<T>(params int[] upgrades) where T : CardModel, new() => FaceRender(GardenCard<T>(upgrades));
        Check(Text<FragileDoubleStrikeHbrgoSaki>().Contains("效果数值翻倍") && Text<FragileDoubleStrikeHbrgoSaki>().Contains("持续时间不变"), "harmonic includes every fragile effect and unchanged duration on face");
        Check(Text<PiercingShotHbrgoSaki>().Contains("每有1%") && Text<PiercingShotHbrgoSaki>().Contains("6‰"), "piercing face includes fragile coefficient gain");
        Check(Text<StrongShotHbrgoSaki>().Contains("最高") && Text<StrongShotHbrgoSaki>().Contains("6‰"), "strong shot includes highest fragile coefficient gain");
        Check(Text<WorldIsMyLaboratoryHbrgoSaki>().Contains("没有") && Text<WorldIsMyLaboratoryHbrgoSaki>().Contains("DP"), "laboratory no-DP damage condition is on face");
        Check(Text<ThereIsOnlyOneTruthHbrgoSaki>().Contains("随机移除目标1种可解除的增益"), "Irene's dispel is on the card");
        Check(Text<WindForestFireMountainHbrgoSaki>(2).Contains("50%"), "Monaka HP damage upgrade is on face");
        Check(Text<WindForestFireMountainHbrgoSaki>(6).Contains("翻倍"), "Monaka ten-faith upgrade is on face");
        Check(Text<WorldConquestHbrgoSaki>(1).Contains("100%") && Text<WorldConquestHbrgoSaki>(1).Contains("60%"), "Bungo DP and deck-kind upgrade values are on face");
        var oshima = GardenCard<EldestOshimaIsHereHbrgoSaki>();
        Check(oshima.HoverTips.OfType<HoverTip>().Any(tip => tip.Title.Contains("喊名字")), "Oshima's actual keyword hover remains");
        Check(GardenCard<PeacefulMemoriesHbrgoSaki>().HoverTips.OfType<HoverTip>().Any(tip => tip.Title.Contains("无敌之盾")), "Aoi's actual ability hover remains");
        Check(GardenCard<InnocentRequiemHbrgoSaki>().HoverTips.OfType<HoverTip>().Any(tip => tip.Title.Contains("柊木的灵")), "Kozue's actual ability hover remains");
    }
    static void FaceVocabularyCases()
    {
        var table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs/cards.json")))!;
        foreach (var pair in table.Where(pair => !pair.Key.StartsWith("HBRGOSAKIMOD-") &&
            (pair.Key.Contains("Description", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains(".upgrade_effect."))))
        {
            Check(!new[] { "%倍率伤害", "%倍率格挡", "技能系数", "照框架", "最多并存", "不计入份数上限", "不占同种2份名额" }.Any(pair.Value.Contains),
                "67 card face keeps framework/arithmetic out: " + pair.Key);
            Check(!pair.Value.Contains("能量[gold]消耗[/gold]"), "67 energy-cost verb is never mistaken for Exhaust keyword: " + pair.Key);
            Check(!Regex.IsMatch(pair.Value, @"\{[^{}:]*\[/?gold\][^{}:]*(?:\}|:)"),
                "67 keyword styling never enters a SmartFormat variable selector: " + pair.Key);
        }
    }
    static async Task FaceNativeCases()
    {
        Type[] allCards = Mod.GetTypes().Where(type => !type.IsAbstract && typeof(CardModel).IsAssignableFrom(type) &&
            type.Namespace == "HBRGOSakiMod.Core.Models.Cards").ToArray();
        foreach (Type type in allCards.Concat(Mod.GetTypes().Concat(typeof(PowerModel).Assembly.GetTypes()).Where(type => !type.IsAbstract && !type.ContainsGenericParameters &&
            (typeof(PowerModel).IsAssignableFrom(type) || typeof(RelicModel).IsAssignableFrom(type) || typeof(CardPoolModel).IsAssignableFrom(type)))))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var h = new Harmony(ProductionId + ".face67");
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            if (Environment.GetEnvironmentVariable("HBR_CARD_FACE_NATIVE_VARS_PREFLIGHT") == "1")
                Patch(Helper("HbrMemoryGardenUpgradeCatalog").GetMethod("GetEffectDescription", F)!, nameof(FaceNativeUpgradeVariables));
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(AccessTools.PropertyGetter(typeof(RelicModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(AccessTools.Method(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Assets.AssetCache", true)!, "GetTexture2D"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo energyPath in typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!.GetMethods(F)
                .Where(method => method.Name == "GetPath" && method.ReturnType == typeof(string))) Patch(energyPath, nameof(Energy));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method => (method.Name is "Wait" or "CustomScaledWait") && method.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            foreach (string patch in new[] { "CardDescriptionLocPatch", "ThirtyOneBCardDescriptionPatch", "HbrCardFaceRulesHoverPatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + patch, true)!).Patch();
            foreach (string patch in new[] { "HbrEffectApplicationPatch", "HbrFragileStackingPatch", "HbrFragileApplyPatch", "HbrFragileCopyPatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + patch, true)!).Patch();
            foreach (Type type in FaceCards.Concat(FaceLateCards))
            {
                GardenReset(); var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
                Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
                string normal = FaceRender(card);
                Check(!Regex.IsMatch(normal, @"\{(?:Face|DamageDisplay|UpgradeEffects)"), "67 native formatter resolves every new result token for " + type.Name);
                Check(!normal.Contains("%倍率伤害") && !normal.Contains("照框架"), "67 native body shows outcomes without arithmetic/slot prose for " + type.Name);
                foreach (int id in Enumerable.Range(0, type == typeof(SaviorGrandEntranceHbrgoSaki) ? 8 : 7))
                {
                    var preview = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(preview, "_owner", Player);
                    GardenUpgrade(preview, id);
                    string rendered = FaceRender(preview);
                    Check(!rendered.Contains("%倍率伤害") && !rendered.Contains("照框架") && !rendered.Contains("份数上限"), "67 actual selected upgrade body uses the same concise sentence rules: " + type.Name + "/" + id);
                    CheckNoRulesHover(preview, type.Name + "/" + id);
                }
                if (card.DynamicVars.TryGetValue("Damage", out _) && card.Description.GetRawText().Contains("{DamageDisplay}"))
                {
                    string detail = FaceRender(card, true);
                    Check(detail.Contains("%）") && !normal.Contains("%）"), "67 inspect retains only parenthesized percentage and the face omits it: " + type.Name);
                }
            }
            GardenReset(); var fresh = GardenCard<FreshDeathHbrgoSaki>();
            string normalFresh = FaceRender(fresh);
            Check(normalFresh.Contains("选择1名玩家") && normalFresh.Contains("攻击力上升") && !normalFresh.Contains("1份") && normalFresh.Contains("使用1次"), "67 FreshDeath actual base is the requested native-style sentence");
            H("Sense", "Talent", 50); string stronger = FaceRender(fresh);
            Check(stronger.Contains("[green]") && stronger != normalFresh, "67 resolved positive-buff percentage responds to the real intelligence gain and uses green numbers");
            var tengen = GardenCard<TengenBrilliantMoveHbrgoSaki>(6);
            string before = FaceRender(tengen); Native(tengen, "SetGrowth", 5m); string after = FaceRender(tengen);
            Check(after != before && after.Contains("[green]"), "67 permanent damage growth is expressed by the actual damage number, with its restored growth effect");
            foreach (Type type in allCards)
            {
                GardenReset();
                var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
                if (card is HotaruCannonChoiceToken)
                    Native(card, "Configure", "浮游炮测试选项", ModelDb.Power<ChargePower>().Description);
                Player.PlayerCombatState!.Hand.AddInternal(card, -1, true);
                foreach (bool legacy in new[] { false, true })
                {
                    H("Mode", !legacy, 1);
                    string normal = FaceRender(card), detail = FaceRender(card, true);
                    Check(!normal.Contains('{') && !normal.Contains("%倍率伤害"), "67 full authored-card native face renders in " + (legacy ? "legacy: " : "HBR: ") + type.Name);
                    Check(!detail.Contains('{'), "67 full authored-card native detail resolves in " + (legacy ? "legacy: " : "HBR: ") + type.Name);
                    CheckNoRulesHover(card, (legacy ? "legacy: " : "HBR: ") + type.Name);
                }
                for (int level = 0; level < Math.Min(7, card.MaxUpgradeLevel) && card.IsUpgradable; level++)
                {
                    // Native sequential upgrades exercise ordinary upgrade text
                    // and older garden styles, including their prerequisites.
                    card.UpgradeInternal(); card.FinalizeUpgradeInternal();
                    foreach (bool legacy in new[] { false, true })
                    {
                        H("Mode", !legacy, 1);
                        Check(!FaceRender(card).Contains('{') && !FaceRender(card, true).Contains('{'),
                            "67 native upgraded authored-card face/detail resolve in " + (legacy ? "legacy: " : "HBR: ") + type.Name + "/" + (level + 1));
                        CheckNoRulesHover(card, (legacy ? "legacy: " : "HBR: ") + type.Name + "/" + (level + 1));
                    }
                }
            }
            RestoredEffectCases();
            await FaceProjectionCases(h);
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    public static void RunCardFace(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); FaceVocabularyCases(); FaceNativeCases().GetAwaiter().GetResult(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory); File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))), runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            localizationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs/cards.json")))),
            nativeVariablesPreflight = Environment.GetEnvironmentVariable("HBR_CARD_FACE_NATIVE_VARS_PREFLIGHT") == "1",
            boundary = "All authored card/upgrade templates scanned; representative actual native formatters, selected garden upgrades and removal of duplicate rules hovers evaluated. Real formula/color and inspect scopes, isolated texture/wait/preview leaves. No Godot rendering or in-game input check." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + "card-face checks"); if (!ok) throw new Exception("Inspect " + path);
    }
}
