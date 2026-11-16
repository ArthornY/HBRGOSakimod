using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

static partial class CompactHoverChecks
{
    // Keep formatting and texture loading as host leaves. Identity, constructors,
    // immutable flags and the production Harmony getter patch remain real native code.
    static bool HoverApiFormatLeaf(LocString __instance, ref string __result)
    {
        __result = __instance.Variables.TryGetValue("title", out object title) ? title?.ToString() :
            __instance.Variables.TryGetValue("text", out object text) ? text?.ToString() : __instance.LocEntryKey;
        return false;
    }

    static bool HoverApiIconLeaf(ref Godot.Texture2D __result)
    {
        __result = null;
        return false;
    }

    static void HoverTipCompatibilityChecks()
    {
        Harmony harmony = new(Owner + ".hover-api");
        try
        {
            harmony.Patch(typeof(LocString).GetMethod(nameof(LocString.GetFormattedText)),
                prefix: new HarmonyMethod(typeof(CompactHoverChecks).GetMethod(nameof(HoverApiFormatLeaf), F)));
            harmony.Patch(typeof(PowerModel).GetProperty(nameof(PowerModel.Icon)).GetMethod,
                prefix: new HarmonyMethod(typeof(CompactHoverChecks).GetMethod(nameof(HoverApiIconLeaf), F)));
            Type compatibility = Mod.GetType("HBRGOSakiMod.Core.Helpers.HoverTipCompatibility", true);
            Type patch = Mod.GetType("HBRGOSakiMod.Core.Helpers.ImmutableCardPenaltyHoverTipPatch", true);
            bool immutable = typeof(HoverTip).GetProperty(nameof(HoverTip.IsDebuff)).SetMethod == null;
            List<MethodInfo> patched = harmony.CreateClassProcessor(patch).Patch();
            Check((patched?.Count ?? 0) == (immutable ? 1 : 0),
                "card-penalty getter patch installs only when the native debuff setter is unavailable");

            HoverTip Create(string method, params object[] args) =>
                (HoverTip)compatibility.GetMethod(method, F).Invoke(null, args);
            string KeywordId(HoverTip tip) =>
                (string)compatibility.GetMethod("KeywordId", F).Invoke(null, new object[] { tip });
            LocString title = new("card_keywords", "compat.title");
            LocString body = new("card_keywords", "compat.description");
            HoverTip native = new(title, body);
            HoverTip keyword = Create("CreateKeyword", title, body, "HBR_COMPAT_KEYWORD");
            Check(keyword.Title == native.Title && keyword.Description == native.Description,
                "keyword compatibility preserves native formatted title and description");
            Check(keyword.Id == (immutable ? native.Id : "HBR_COMPAT_KEYWORD"),
                "immutable runtime keeps native identity; writable runtime keeps legacy keyword identity");
            Check(KeywordId(keyword) == "HBR_COMPAT_KEYWORD",
                "module keyword lookup recognizes both native and legacy identities");
            Check(!keyword.IsDebuff && !keyword.IsSmart && !keyword.IsInstanced,
                "ordinary keyword flags remain native defaults");

            HoverTip penalty = Create("CreateCardPenalty",
                new LocString("powers", "NARARA_NULLIFICATION_POWER.title"),
                new LocString("powers", "NARARA_NULLIFICATION_POWER.cardPenaltyDescription"));
            Check(penalty.IsDebuff, "Narara card penalty retains debuff presentation on both runtimes");
            Check(KeywordId(penalty) == "NARARA_NULLIFICATION_CARD_PENALTY",
                "Narara card penalty remains recognizable without reusing the real power identity");
            Check(!native.IsDebuff && !keyword.IsDebuff && !default(HoverTip).IsDebuff,
                "ordinary and default native tooltips remain unaffected by the card-penalty patch");

            ModelDb.Inject(typeof(NararaNullificationPower));
            PowerModel power = ModelDb.Power<NararaNullificationPower>();
            HoverTip actualPower = new(power, "real Narara buff", true);
            actualPower.SetCanonicalModel(power);
            Check(!actualPower.IsDebuff, "real Narara power tooltip remains a Buff");
            Check(actualPower.Id != penalty.Id, "real Narara Buff and card penalty have independent native identities");

            // Use the native public/init setter in the host, never write a readonly backing field.
            object boxed = actualPower;
            typeof(HoverTip).GetProperty(nameof(HoverTip.ShouldOverrideTextOverflow)).SetValue(boxed, true);
            HoverTip original = (HoverTip)boxed;
            Type compact = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrCompactHoverTipHelper", true);
            HoverTip copy = (HoverTip)compact.GetMethod("Copy", F).Invoke(null,
                new object[] { original, "short title", "short body" });
            Check(copy.Title == "short title" && copy.Description == "short body",
                "compact copy replaces only the displayed text");
            Check(copy.Id == original.Id && copy.IsSmart == original.IsSmart &&
                  copy.IsDebuff == original.IsDebuff && copy.IsInstanced == original.IsInstanced &&
                  copy.ShouldOverrideTextOverflow == original.ShouldOverrideTextOverflow,
                "compact copy preserves all native identity and immutable presentation flags");
            Check(ReferenceEquals(copy.CanonicalModel, power) && ReferenceEquals(copy.Icon, original.Icon),
                "compact copy preserves canonical model and icon without mutating shared objects");
            Check(original.Description == "real Narara buff" && original.Title != copy.Title &&
                  original.IsSmart && original.ShouldOverrideTextOverflow,
                "rewriting a tooltip does not change the original struct value");
            HoverTip penaltyCopy = (HoverTip)compact.GetMethod("Copy", F).Invoke(null,
                new object[] { penalty, "short penalty", "short penalty body" });
            Check(penaltyCopy.IsDebuff && penaltyCopy.Id == penalty.Id,
                "the card-penalty identity and debuff presentation survive compact copying");

            ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Powers.KnockdownPower));
            HoverTip instanced = new(ModelDb.Power<MegaCrit.Sts2.Core.Models.Powers.KnockdownPower>(),
                "instanced debuff", false);
            HoverTip instancedCopy = (HoverTip)compact.GetMethod("Copy", F).Invoke(null,
                new object[] { instanced, "short instanced", "short instanced body" });
            Check(instanced.IsDebuff && instanced.IsInstanced && instancedCopy.IsDebuff &&
                  instancedCopy.IsInstanced && !instancedCopy.IsSmart && instancedCopy.Id == instanced.Id,
                "copy retains true instanced/debuff flags as well as false smart flags");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
