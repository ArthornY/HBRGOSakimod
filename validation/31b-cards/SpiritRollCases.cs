using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
static partial class EffectChecks
{
    static RollGlossaryCapability? BSpiritRoll;
    static readonly List<(Type Power, decimal Amount, decimal Given, CardModel? Source)> BSpiritGrants = [];
    static bool BRealRollGiven(PowerModel power, Creature giver, decimal amount, Creature target, CardModel? cardSource,
        ref IEnumerable<AbstractModel> modifiers, ref decimal __result)
    {
        if (BSpiritRoll == null) return true;
        __result = amount * BSpiritRoll.ModifyPowerAmountGivenMultiplicative(power, giver, amount, target, cardSource!);
        BSpiritGrants.Add((power.GetType(), amount, __result, cardSource));
        modifiers = []; return false;
    }
    static async Task BSpiritRollCase(Harmony h)
    {
        BReset(); var card = GardenCard<InnocentRequiemHbrgoSaki>();
        foreach (Type type in new[] { typeof(TinyMomentsPower), typeof(StrengthPower) })
        { if (!ModelDb.Contains(type)) ModelDb.Inject(type); Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type)); }
        Power<TinyMomentsPower>(Hero, 1);
        BSpiritRoll = RollPipeline(card); LandRoll(BSpiritRoll, card, 100);
        decimal factor = BSpiritRoll.ModifyPowerAmountGivenMultiplicative(ModelDb.Power<StrengthPower>(), Hero, 10m, Hero, card);
        Check(factor >= 2m, "real critical-roll capability and Tiny Moments provide a >=2 ordinary card-grant multiplier");
        MethodInfo hook = typeof(Hook).GetMethod(nameof(Hook.ModifyPowerAmountGiven))!;
        h.Patch(hook, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(BRealRollGiven), F)!));
        BSpiritGrants.Clear();
        try
        {
            await GardenPlay(card);
            Creature spirit = (Creature)Call("KozueSpiritHelper", "GetLivingSpirit", Player)!;
            Equal(spirit.GetPower<KozueSpiritPower>()!.Amount, 1m, "real active-roll grant boundary keeps the spirit marker at exactly1");
            Equal(spirit.GetPower<PhantomPower>()!.Amount, 1m, "real active-roll grant boundary keeps the independent PhantomPower amount at exactly1");
            Equal(spirit.GetPower<MinionPower>()!.Amount, 1m, "real active-roll grant boundary keeps the structural MinionPower amount at exactly1");
            Equal(spirit.GetPower<KozueSpiritShellPower>()!.CurrentDp, decimal.Floor(spirit.MaxHp * .2m), "active critical dice cannot change the spirit's exact20% entryDP");
            foreach (var grant in BSpiritGrants.Where(grant => grant.Power == typeof(KozueSpiritPower) || grant.Power == typeof(PhantomPower) || grant.Power == typeof(MinionPower) || grant.Power == typeof(KozueSpiritShellPower)))
                Check(grant.Source == null && grant.Amount == grant.Given, "structural pet grant bypasses card value scaling before native attachment: " + grant.Power.Name);
        }
        finally { BSpiritRoll = null; h.Unpatch(hook, typeof(EffectChecks).GetMethod(nameof(BRealRollGiven), F)!); }
    }
}
