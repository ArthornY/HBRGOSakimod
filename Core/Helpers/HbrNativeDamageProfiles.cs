using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrNativeDamageBasis { SkillRaw, WorldAmount, PercentHp, FixedCost, Resolved }
internal enum HbrNativeDamageStatSource { Owner, Applier }

internal sealed record HbrNativeDamageProfile(Type ModelType, HbrNativeDamageBasis Basis,
    HbrNativeDamageStatSource StatSource, bool OwnerSelfIsFixedCost = false, bool UsesAmountAsDamage = false);

// Audited native damage sources. Resolve names once, including optional beta
// models; runtime damage dispatch is a type dictionary lookup, never an IL scan.
internal static class HbrNativeDamageProfiles
{
    private static readonly Dictionary<Type, HbrNativeDamageProfile> Profiles = Build();
    internal static IEnumerable<HbrNativeDamageProfile> All => Profiles.Values;

    private static Dictionary<Type, HbrNativeDamageProfile> Build()
    {
        var profiles = new Dictionary<Type, HbrNativeDamageProfile>();
        void Add(string name, HbrNativeDamageBasis basis = HbrNativeDamageBasis.SkillRaw,
            HbrNativeDamageStatSource source = HbrNativeDamageStatSource.Owner, bool selfCost = false)
        {
            Type type = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name, false);
            bool amount = name.StartsWith("Powers.", StringComparison.Ordinal) && basis == HbrNativeDamageBasis.SkillRaw &&
                name is not ("Powers.TheBombPower" or "Powers.InsatiableDominionPower");
            if (type != null) profiles.Add(type, new(type, basis, source, selfCost, amount));
        }
        foreach (string name in new[] { "Cards.CaptureSpirit", "Relics.CharonsAshes", "Relics.Metronome",
            "Relics.Tingsha", "Relics.ObsidianVice", "Powers.BlackHolePower", "Powers.FlameBarrierPower",
            "Powers.GravityPower", "Powers.HailstormPower", "Powers.HauntPower", "Powers.JuggernautPower",
            "Powers.OutbreakPower", "Powers.PanachePower", "Powers.RollingBoulderPower", "Powers.SerpentFormPower",
            "Powers.SleightOfFleshPower", "Powers.SmokestackPower", "Powers.SpeedsterPower", "Powers.TheBombPower",
            "Powers.ThunderPower", "Powers.CacophonyPower", "Powers.ExplosiveArrowsPower", "Powers.ExplosivePower",
            "Powers.GodByrdPower", "Powers.ViolentChordPower", "Orbs.DarkOrb", "Orbs.GlassOrb", "Orbs.LightningOrb" }) Add(name);
        Add("Powers.InfernoPower", selfCost: true);
        Add("Powers.MagicBombPower", source: HbrNativeDamageStatSource.Applier);
        Add("Powers.InsatiableDominionPower", source: HbrNativeDamageStatSource.Applier);
        Add("Powers.ReflectPower", HbrNativeDamageBasis.WorldAmount);
        Add("Powers.NecroMasteryPower", HbrNativeDamageBasis.WorldAmount);
        foreach (string name in new[] { "Cards.TheHangedMan", "Events.ThermaeNonupeipe", "Events.WuppyInTrouble" })
            Add(name, HbrNativeDamageBasis.FixedCost);
        return profiles;
    }

    internal static bool TryGet(AbstractModel model, out HbrNativeDamageProfile profile)
    {
        profile = null;
        return model != null && Profiles.TryGetValue(model.GetType(), out profile);
    }

    internal static bool IsSkillSource(AbstractModel model) => TryGet(model, out var profile) &&
        profile.Basis is HbrNativeDamageBasis.SkillRaw or HbrNativeDamageBasis.WorldAmount;

    internal static Creature ResolveSource(AbstractModel model)
    {
        if (model?.IsMutable != true) return null;
        if (model is PowerModel power)
            return TryGet(model, out var profile) && profile.StatSource == HbrNativeDamageStatSource.Applier
                ? power.Applier : power.Owner;
        return model switch
        {
            CardModel card => card.Owner?.Creature,
            RelicModel relic => relic.Owner?.Creature,
            PotionModel potion => potion.Owner?.Creature,
            OrbModel orb => orb.Owner?.Creature,
            EventModel evt => evt.Owner?.Creature,
            _ => null
        };
    }
}
