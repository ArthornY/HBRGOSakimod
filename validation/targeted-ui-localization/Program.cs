using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

static class CardFaceStandalone { public static void Run(string path) => EffectChecks.RunTargetedUiLoc(path); }
static partial class EffectChecks
{
    static readonly List<object> UiLocTexts = new();
    static bool UiLocEnergyPath(string prefix, ref string __result)
    { __result = "res://images/packed/sprite_fonts/" + prefix + "_energy_icon.png"; return false; }
    static string UiCard(CardModel card, bool detail = false)
    {
        using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F).Single(m => m.Name == "Begin" && m.GetParameters().Length == 4), null, card, detail, false, Enemy)!;
        return card.GetDescriptionForPile(PileType.Hand);
    }
    static void UiLocCases()
    {
        Type[] powers = [typeof(HbrIntelPower), typeof(AoiInvincibleShieldPower), typeof(RollBonusPower), typeof(AoiMiracleShieldPower), typeof(ProtectiveCharmPower), typeof(PillarOverdriveDetectionPower)];
        foreach (Type type in powers.Concat(new[] { typeof(LoveCharlottaHbrgoSaki), typeof(ChargeBurstHbrgoSaki), typeof(NoblesseObligeHbrgoSaki), typeof(EldestOshimaIsHereHbrgoSaki), typeof(ChargePower) }))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var h = new Harmony(ProductionId + ".targeted-ui");
        h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!).Patch();
        foreach (MethodInfo method in typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!.GetMethods(F)
            .Where(m => m.Name == "GetPath" && m.GetParameters() is var p && p.Length == 1 && p[0].ParameterType == typeof(string)))
            h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(UiLocEnergyPath), F)!));
        h.Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf), F)!));
        foreach (Type type in powers)
        {
            // Ritsu's installed runtime requests these exact namespaced keys;
            // preserve the canonical assertion while testing that real lookup.
            var canonical = (PowerModel)ModelDb.GetById<AbstractModel>(ModelDb.GetId(type));
            ModelId id = canonical.Id;
            Set(canonical, "<Id>k__BackingField", new ModelId(id.Category, "HBRGOSAKIMOD-" + id.Entry));
            bool assertion = false;
            try { _ = canonical.Owner; } catch (Exception e) when (e.GetType().Name == "CanonicalModelException") { assertion = true; }
            Check(assertion, "native canonical Owner assertion remains: " + type.Name);
            string title = canonical.Title.GetFormattedText();
            Check(title.Length > 0 && !title.Contains(".title") && !title.Contains("HBRGOSAKIMOD-"), "actual prefixed power title resolves: " + type.Name);
            var instance = (PowerModel)canonical.MutableClone(); Set(instance, "_owner", Hero); Set(instance, "_amount", 2);
            if (instance is RollBonusPower) Set(instance, "<RemainingUses>k__BackingField", 3);
            string PowerHover(PowerModel power)
            {
                object tip = ((System.Collections.IEnumerable)typeof(PowerModel).GetProperty("HoverTips", F)!.GetValue(power)!).Cast<object>().First();
                return (string)tip.GetType().GetProperty("Description")!.GetValue(tip)!;
            }
            string owned = PowerHover(instance);
            Check(owned.Length > 0 && !owned.Contains(".description") && !owned.Contains("HBRGOSAKIMOD-") && !Regex.IsMatch(owned, @"\{[A-Za-z]"), "owned power native formatter resolves variables: " + type.Name);
            string reference = PowerHover(canonical);
            Check(reference.Length > 0 && !reference.Contains("HBRGOSAKIMOD-"), "canonical power actual description resolves: " + type.Name);
            UiLocTexts.Add(new { power = type.Name, title, owned, reference });
        }
        string seaGlass = new LocString("relics", "SEA_GLASS.HBRGO_SAKI_MOD_CHARACTER_HBRGO_HOTARU.title").GetFormattedText();
        Check(seaGlass == "海玻璃", "native SeaGlass character-specific exact title key resolves");
        string simple = BaseLib.Patches.Localization.SimpleLoc.TrySimplify("#[EE]");
        Check(simple == "{energyPrefix:energyIcons(2)}", "replacement template equals the actual installed BaseLib SimpleLoc output");
        FormReset();
        var noble = FormCard<NoblesseObligeHbrgoSaki>(); GardenUpgrade(noble, 0);
        string effect = (string)Call("HbrMemoryGardenUpgradeCatalog", "GetEffectDescription", noble, "0")!;
        Check(!effect.Contains("[EE]") && !effect.Contains("[E]") && effect.Contains("[img"), "actual garden upgrade source formats energy icons without bare BBCode");
        var dollRule = FormCard<LoveCharlottaHbrgoSaki>(); GardenUpgrade(dollRule, 4);
        var rules = (IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip>)Call("HbrCardFaceDescription", "Rules", dollRule)!;
        string text = string.Join("\n", rules.Select(r => (string)r.GetType().GetProperty("Description")!.GetValue(r)!));
        Check(text.Contains("[img") && !Regex.IsMatch(text, @"\[E+\]"), "actual card rule hover formats energy icons without bare tags");
        UiLocTexts.Add(new { energyEffect = effect, ruleHover = text });
        foreach (int energy in new[] { 0, 1, 3 })
        {
            FormReset(); Set(Player.PlayerCombatState!, "_energy", energy);
            var love = FormCard<LoveCharlottaHbrgoSaki>();
            string face = UiCard(love), detail = UiCard(love, true);
            Check(face.Contains("当前结算X：" + energy) && face.Contains("3次"), "actual X-card face shows effective X with fixed three hits: " + energy);
            Check(detail.Contains((64 * energy).ToString() + "%"), "actual X-card inspect coefficient is64percent times effective X: " + energy);
            UiLocTexts.Add(new { card = "LoveCharlotta", energy, face, detail });
        }
        FormReset(); Set(Player.PlayerCombatState!, "_energy", 1);
        var extra = FormCard<LoveCharlottaHbrgoSaki>(); GardenUpgrade(extra, 0);
        Check(UiCard(extra).Contains("当前结算X：2"), "Flash changes displayed effective X without changing paid energy1");
        foreach (bool upgraded in new[] { false, true })
        {
            FormReset(); var chargeCard = FormCard<ChargeBurstHbrgoSaki>(upgraded);
            string zero = UiCard(chargeCard), zeroDetail = UiCard(chargeCard, true);
            Power<ChargePower>(Hero, 2);
            string two = UiCard(chargeCard), twoDetail = UiCard(chargeCard, true);
            Check(zero != two, "actual ChargeBurst card face responds to current charge change: upgraded=" + upgraded);
            Check(zeroDetail != twoDetail && !two.Contains("FaceCharge"), "actual ChargeBurst inspect and face both resolve live-charge parameters: upgraded=" + upgraded);
            UiLocTexts.Add(new { card = "ChargeBurst", upgraded, zero, zeroDetail, two, twoDetail });
        }
        var cards = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs/cards.json")))!;
        Check(cards.Values.All(v => !Regex.IsMatch(v, @"\[E+\]")), "all authored card values have no unconverted fixed-energy tags");
        foreach (string prefix in new[] { "", "HBRGOSAKIMOD-" })
            Check(!cards[prefix+"ELDEST_OSHIMA_IS_HERE_HBRGO_SAKI.upgrade_rules.5"].Contains("才能"), "military-pay rule removes only the retired Talent-gold interaction: "+prefix);
    }
    public static void RunTargetedUiLoc(string reportPath)
    {
        bool ok=false; string? error=null;
        try { Install(); Localization(); UiLocCases(); ok=true; }
        catch(Exception e) { error=e.ToString(); Console.Error.WriteLine(e); }
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new {ok,checks=Passed.Count,passed=Passed,error,texts=UiLocTexts,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Actual native LocString/SmartDescription/Card.GetDescriptionForPile, garden upgrade/rule formatters, and unchanged BaseLib SimpleLoc output. Explicit Ritsu namespaced model IDs reproduce observed lookup; Godot icon/path/logger terminal leaves isolated. No engine/signals/render/package."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok) throw new Exception("Inspect "+reportPath);
        Console.WriteLine("PASS "+Passed.Count+" targeted UI localization checks");
    }
}
