using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// The variables retain native units in storage, upgrades, cloning and saves.
// Only audited consumers and text formatting read them in HBR resource units.
// A variable's CLR type alone does not establish this: LizardTail.Heal and
// FakeLeesWaffle.Heal are percentages, several events store an already
// calculated heal in HealVar, and a few HP losses are stored in a DamageVar.
internal static class HbrNativeFixedResourceHelper
{
    private static readonly AccessTools.FieldRef<DynamicVar, AbstractModel> VariableOwner =
        AccessTools.FieldRefAccess<DynamicVar, AbstractModel>("_owner");

    private static readonly Dictionary<Type, HashSet<string>> FixedVariables = BuildRegistry();

    internal static IEnumerable<Type> ModelTypes => FixedVariables.Keys;

    internal static bool IsFixedVariable(DynamicVar variable)
    {
        AbstractModel owner = variable == null ? null : VariableOwner(variable);
        return owner != null && FixedVariables.TryGetValue(owner.GetType(), out HashSet<string> names) &&
            names.Contains(variable.Name);
    }

    internal static Creature GetResourceOwner(DynamicVar variable)
    {
        AbstractModel owner = variable == null ? null : VariableOwner(variable);
        // Character selection and compendia format canonical templates. Their
        // Owner getters require a mutable instance; no player exists yet.
        if (owner?.IsMutable != true) return null;
        return owner switch
        {
            CardModel card => card.Owner?.Creature,
            RelicModel relic => relic.Owner?.Creature,
            PotionModel potion => potion.Owner?.Creature,
            EventModel eventModel => eventModel.Owner?.Creature,
            PowerModel power => power.Owner,
            _ => null
        };
    }

    private static decimal Convert(DynamicVar variable, decimal value) => IsFixedVariable(variable)
        ? HbrResourceValueHelper.Fixed(GetResourceOwner(variable), value)
        : value;

    internal static decimal ReadBase(DynamicVar variable) => Convert(variable, variable.BaseValue);
    internal static decimal ReadPreview(DynamicVar variable) => Convert(variable, variable.PreviewValue);
    internal static int ReadShown(DynamicVar variable) =>
        (int)Math.Clamp(ReadPreview(variable), int.MinValue, int.MaxValue);
    internal static decimal ReadEnchanted(DynamicVar variable) => Convert(variable, variable.EnchantedValue);
    internal static int ReadInt(DynamicVar variable) => (int)Math.Clamp(ReadBase(variable), int.MinValue, int.MaxValue);

    private static Dictionary<Type, HashSet<string>> BuildRegistry()
    {
        var result = new Dictionary<Type, HashSet<string>>();
        void Add(string category, string name, params string[] variables)
        {
            Type model = typeof(CardModel).Assembly.GetType($"MegaCrit.Sts2.Core.Models.{category}.{name}", true);
            result.Add(model, new HashSet<string>(variables, StringComparer.Ordinal));
        }

        foreach (string name in new[] { "BadLuck", "Beckon", "Bloodletting", "BloodWall", "Brand",
            "Breakthrough", "DemonicShield", "Hemokinesis", "Offering" })
            Add("Cards", name, "HpLoss");
        // Haunt's HpLossVar is enemy damage stored in its later HauntPower,
        // not an owner HP cost. Scaling it here would multiply the raw skill
        // stacks by 100 before the damage profile applies actor attributes.
        // Added by the current beta; absent models must not prevent a stable
        // runtime from loading this otherwise shared registry.
        void AddOptional(string category, string name, params string[] variables)
        {
            Type type = typeof(CardModel).Assembly.GetType($"MegaCrit.Sts2.Core.Models.{category}.{name}", false);
            if (type != null) result.Add(type, new HashSet<string>(variables, StringComparer.Ordinal));
        }
        AddOptional("Cards", "TheHangedMan", "HpLoss");
        AddOptional("Events", "ThermaeNonupeipe", "DrinkHpLoss");
        // WuppyInTrouble writes 5% of current HP into HpLoss at runtime;
        // that percentage result is already in the current resource scale.
        Add("Cards", "BrightestFlame", "MaxHp");
        Add("Cards", "Feed", "MaxHp");
        Add("Cards", "NotYet", "Heal");
        Add("Cards", "Spur", "Heal");

        foreach (string name in new[] { "BigMushroom", "ChosenCheese", "DarkstonePeriapt", "DragonFruit",
            "FakeMango", "LeafyPoultice", "LeesWaffle", "LoomingFruit", "Mango", "NutritiousOyster",
            "Pear", "StoneHumidifier", "Strawberry" })
            Add("Relics", name, "MaxHp");
        foreach (string name in new[] { "BlackBlood", "BloodVial", "BookOfFiveRings", "BookRepairKnife",
            "BurningBlood", "EternalFeather", "FakeBloodVial", "MealTicket",
            "MeatOnTheBone", "Pantograph", "Planisphere", "RegalPillow", "TeaOfDiscourtesy" })
            Add("Relics", name, "Heal");
        // v0.111.0 moved Distinguished Cape's max-HP cost to Sere Talon; each
        // runtime has the variable on only one of them.
        Add("Relics", "DistinguishedCape", "HpLoss");
        Add("Relics", "SereTalon", "HpLoss");
        Add("Relics", "FragrantMushroom", "HpLoss");
        // HP losses without an attacker, stored in a DamageVar like Fragrant
        // Mushroom's HpLoss. Not skill damage: nobody deals them.
        Add("Relics", "PrecariousShears", "Damage");
        Add("Relics", "RoyalPoison", "Damage");
        Add("Relics", "TungstenRod", "HpLossReduction");
        // Its text writes the 20 literally; see HbrNativeFixedLiteralDescriptionPatch.
        Add("Relics", "BeatingRemnant", "MaxHpLoss");
        // A Block threshold: player Block resolves at about a hundred per native point.
        Add("Relics", "ParryingShield", "Block");
        Add("Potions", "FruitJuice", "MaxHp");
        // One raw HP per copy played, lost at the start of each turn.
        Add("Powers", "CrimsonMantlePower", "SelfDamage");
        Add("Powers", "InfernoPower", "SelfDamage");

        Add("Events", "AbyssalBaths", "MaxHp", "Heal");
        Add("Events", "ByrdonisNest", "MaxHp");
        Add("Events", "DenseVegetation", "HpLoss");
        Add("Events", "DrowningBeacon", "HpLoss");
        Add("Events", "EndlessConveyor", "ClamRollHeal", "CaviarMaxHp");
        Add("Events", "MorphicGrove", "MaxHp");
        Add("Events", "SapphireSeed", "Heal");
        // Text only: Hold On's actual loss reads a property, see SlipperyBridgeHpLossPatch.
        Add("Events", "SlipperyBridge", "HpLoss");
        Add("Events", "SpiritGrafter", "RejectionHpLoss", "LetItInHealAmount");
        Add("Events", "ThisOrThat", "HpLoss");
        Add("Events", "TrashHeap", "HpLoss");
        Add("Events", "UnrestSite", "MaxHpLoss");
        Add("Events", "WaterloggedScriptorium", "MaxHp");
        Add("Events", "WhisperingHollow", "HpLoss");
        Add("Events", "BrainLeech", "RipHpLoss");
        Add("Events", "DollRoom", "TakeTimeHpLoss", "ExamineHpLoss");
        Add("Events", "JungleMazeAdventure", "SoloHp");
        Add("Events", "StoneOfAllTime", "DrinkMaxHpGain", "PushHpLoss");
        Add("Events", "SunkenStatue", "HpLoss");
        Add("Events", "TabletOfTruth", "SmashHPGain", "DecipherMaxHpLoss");
        return result;
    }
}
