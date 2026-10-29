using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// The variables retain native units in storage, upgrades, cloning and saves.
// Only audited consumers and text formatting read them in HBR resource units.
// A variable's CLR type alone does not establish this: LizardTail.Heal is a
// percentage, and several events store an already calculated heal in HealVar.
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
        return owner switch
        {
            CardModel card => card.Owner?.Creature,
            RelicModel relic => relic.Owner?.Creature,
            PotionModel potion => potion.Owner?.Creature,
            EventModel eventModel => eventModel.Owner?.Creature,
            _ => null
        };
    }

    private static decimal Convert(DynamicVar variable, decimal value) => IsFixedVariable(variable)
        ? HbrResourceValueHelper.Fixed(GetResourceOwner(variable), value)
        : value;

    internal static decimal ReadBase(DynamicVar variable) => Convert(variable, variable.BaseValue);
    internal static decimal ReadPreview(DynamicVar variable) => Convert(variable, variable.PreviewValue);
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
            "Breakthrough", "DemonicShield", "Haunt", "Hemokinesis", "Offering" })
            Add("Cards", name, "HpLoss");
        Add("Cards", "BrightestFlame", "MaxHp");
        Add("Cards", "Feed", "MaxHp");
        Add("Cards", "NotYet", "Heal");
        Add("Cards", "Spur", "Heal");
        foreach (string name in new[] { "Burn", "Decay", "Infection", "Toxic", "Wither" })
            Add("Cards", name, "Damage");

        foreach (string name in new[] { "BigMushroom", "ChosenCheese", "DarkstonePeriapt", "DragonFruit",
            "FakeMango", "LeafyPoultice", "LeesWaffle", "LoomingFruit", "Mango", "NutritiousOyster",
            "Pear", "StoneHumidifier", "Strawberry" })
            Add("Relics", name, "MaxHp");
        foreach (string name in new[] { "BlackBlood", "BloodVial", "BookOfFiveRings", "BookRepairKnife",
            "BurningBlood", "EternalFeather", "FakeBloodVial", "FakeLeesWaffle", "MealTicket",
            "MeatOnTheBone", "Pantograph", "Planisphere", "RegalPillow", "TeaOfDiscourtesy" })
            Add("Relics", name, "Heal");
        Add("Relics", "DistinguishedCape", "HpLoss");
        Add("Relics", "FragrantMushroom", "HpLoss");
        Add("Potions", "FruitJuice", "MaxHp");

        Add("Events", "AbyssalBaths", "MaxHp", "Heal", "Damage");
        Add("Events", "ByrdonisNest", "MaxHp");
        Add("Events", "DenseVegetation", "HpLoss");
        Add("Events", "DrowningBeacon", "HpLoss");
        Add("Events", "EndlessConveyor", "ClamRollHeal", "CaviarMaxHp");
        Add("Events", "MorphicGrove", "MaxHp");
        Add("Events", "SapphireSeed", "Heal");
        Add("Events", "SpiritGrafter", "RejectionHpLoss", "LetItInHealAmount");
        Add("Events", "ThisOrThat", "HpLoss");
        Add("Events", "TrashHeap", "HpLoss");
        Add("Events", "UnrestSite", "MaxHpLoss");
        Add("Events", "WaterloggedScriptorium", "MaxHp");
        Add("Events", "WhisperingHollow", "HpLoss");
        Add("Events", "BrainLeech", "RipHpLoss");
        Add("Events", "DollRoom", "TakeTimeHpLoss", "ExamineHpLoss");
        Add("Events", "JungleMazeAdventure", "SoloHp");
        Add("Events", "RoomFullOfCheese", "Damage");
        Add("Events", "RoundTeaParty", "Damage");
        Add("Events", "TheLegendsWereTrue", "Damage");
        Add("Events", "StoneOfAllTime", "DrinkMaxHpGain", "PushHpLoss");
        Add("Events", "TabletOfTruth", "SmashHPGain", "DecipherMaxHpLoss");
        return result;
    }
}
