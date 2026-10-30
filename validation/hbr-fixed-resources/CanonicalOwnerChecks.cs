using System.Globalization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;

static partial class Checks
{
    static void CanonicalOwnerChecks()
    {
        // Character selection formats the canonical starting relic, before any
        // Player owns it. Keep its real DynamicVars path and native Owner guard.
        BurningBlood startingRelic = Blank<BurningBlood>();
        DynamicVar startingHeal = startingRelic.DynamicVars.Heal;
        Check(!startingRelic.IsMutable && Read(startingHeal) == 6 && startingHeal.ToString() == "6",
            "character-select canonical BurningBlood formats its real Heal variable without reading Owner");

        foreach (AbstractModel model in new AbstractModel[]
                 { Blank<Feed>(), Blank<BurningBlood>(), Blank<FruitJuice>(), Blank<AbyssalBaths>() })
        {
            // Avoid event asset/ModelDb initialization while retaining the real
            // native model's mutability and guarded Owner property.
            DynamicVar variable = new MaxHpVar(3);
            if (model is BurningBlood) variable = new HealVar(6);
            variable.SetOwner(model);
            decimal raw = variable.BaseValue;
            string text = raw.ToString(CultureInfo.InvariantCulture);
            string label = model.GetType().Name;
            Check(!model.IsMutable && Read(variable) == raw, label + " canonical read keeps native units");
            Check(variable.ToString() == text &&
                ((IConvertible)variable).ToString(CultureInfo.InvariantCulture) == text &&
                variable.ToHighlightedString(false).Contains(text),
                label + " canonical native formatting paths do not access guarded Owner");
            foreach (string reader in new[] { "ReadPreview", "ReadEnchanted", "ReadInt" })
                Check(Convert.ToDecimal(Helper.GetMethod(reader, F)!.Invoke(null, [variable]),
                    CultureInfo.InvariantCulture) == raw, label + " canonical " + reader + " keeps native units");

            Set(model, "<IsMutable>k__BackingField", true);
            Check(Read(variable) == raw && variable.ToString() == text,
                label + " mutable model without a player keeps native units");
            Set(model, model is EventModel ? "<Owner>k__BackingField" : "_owner", Player);
            Check(Read(variable) == raw * 100 && variable.BaseValue == raw,
                label + " owned HBR instance converts once without changing stored units");
            Mode(false);
            Check(Read(variable) == raw && variable.ToString() == text,
                label + " owned instance in disabled mode keeps native units");
            Mode(true);
        }
    }
}
