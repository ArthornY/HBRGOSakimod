using System.Globalization;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NPower), "RefreshAmount")]
internal static class CountedRollPowerDisplayPatch
{
    private const string LabelName = "HbrRemainingRollUses";

    private static void Postfix(NPower __instance, PowerModel ____model, MegaLabel ____amountLabel)
    {
        if (____amountLabel == null) return;
        MegaLabel uses = __instance.GetNodeOrNull<MegaLabel>(LabelName);
        if (____model is HbrFaithPower faith)
        {
            ____amountLabel.SetTextAutoSize(faith.ExactAmount.ToString(CultureInfo.InvariantCulture));
            if (uses != null) uses.Visible = false;
            return;
        }
        int upperAmount;
        Color upperColor;
        if (____model is CountedRollPower power)
        {
            upperAmount = power.AvailableUses;
            upperColor = new Color("E8F5FF");
        }
        else if (____model is SynergyPower synergy)
        {
            // Synergy stays Single to avoid stacking its main buff. Override
            // the native hidden Single amount with the lower-right counter.
            ____amountLabel.SetTextAutoSize(synergy.DualSwordsCount.ToString(CultureInfo.InvariantCulture));
            upperAmount = synergy.BowCount;
            upperColor = new Color("6ED7FF");
        }
        else if (____model is IHbrEffectInstance effect)
        {
            string potency = effect.EffectPotency.ToString("0.#", CultureInfo.InvariantCulture);
            if (____model is HbrComboPower combo)
                potency = combo.DoublesHitCount ? "×2" : "+" + potency;
            else if (____model is HbrEnemyChargePower or HbrImprisonPower or HbrActionLockPower or HbrConfusionPower or HbrNegativityPower)
                potency = string.Empty;
            else if (____model is IchikoVitalityUpPower) potency = "+" + potency;
            else if (____model is HbrIllusionPower or HbrEnergyDrainPower)
                potency = decimal.Floor(effect.EffectPotency).ToString("0", CultureInfo.InvariantCulture);
            else if (____model is not HbrCalamityPower) potency += "%";
            ____amountLabel.SetTextAutoSize(potency);
            upperAmount = effect.Remaining;
            upperColor = new Color("E8F5FF");
        }
        else
        {
            if (uses != null) uses.Visible = false;
            return;
        }
        if (uses == null)
        {
            // Reuse the native font/outline and size handling; only the second
            // counter's placement and colour differ. No per-frame polling.
            uses = (MegaLabel)____amountLabel.Duplicate();
            uses.Name = LabelName;
            uses.UniqueNameInOwner = false;
            uses.MouseFilter = Control.MouseFilterEnum.Ignore;
            uses.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            uses.Position = new Vector2(7f, -7f);
            uses.Size = new Vector2(37f, 23f);
            uses.MinFontSize = 8;
            uses.MaxFontSize = 15;
            uses.AutoSizeEnabled = true;
            uses.AddThemeFontSizeOverride("font_size", 15);
            uses.AddThemeConstantOverride("outline_size", 6);
            uses.AddThemeColorOverride("font_outline_color", new Color("101823"));
            __instance.AddChild(uses);
        }
        uses.Visible = true;
        uses.AddThemeColorOverride("font_color", upperColor);
        uses.SetTextAutoSize(____model is IHbrEffectInstance { EffectProfile.Exit: HbrEffectExit.Eternal }
            ? "∞" : upperAmount.ToString(CultureInfo.InvariantCulture));
    }
}
