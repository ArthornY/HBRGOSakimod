using System.Reflection;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class EffectChecks
{
    static ThirtyOneACollection CutinRelic()
    {
        var relic = Blank<ThirtyOneACollection>();
        Set(relic, "<IsMutable>k__BackingField", true); Set(relic, "_owner", Player);
        Set(Player, "_relics", Player.Relics.Concat(new RelicModel[] { relic }).ToList());
        return relic;
    }

    public static async Task CutinBondCases()
    {
        foreach (Type type in new[] { typeof(HbrMoralePower), typeof(ThirtyOneAYukiDebuffPower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        bool presentation = HBRGOSakiConfig.ThirtyOneACutInPresentation;
        HBRGOSakiConfig.ThirtyOneACutInPresentation = false;
        try
        {
            for (int kinds = 0; kinds <= 6; kinds++)
                Equal((int)Call("ThirtyOneACutinCombatHelper", "CutinCount", kinds)!, new[] { 0, 0, 2, 2, 3, 3, 6 }[kinds],
                    "31A real tier speaker count for " + kinds + " permanent kinds");
            GardenReset(); var relic = CutinRelic();
            Check(!relic.PendingLive, "31A old/default relic starts without a pending Live");
            foreach (Type type in GardenCards)
            {
                var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
                Player.Deck.AddInternal(card);
            }
            GardenPermanent(GardenCard<MyLegendBeginsHbrgoSaki>());
            Equal(relic.DisplayAmount, 6m, "31A current permanent deck counts six distinct kinds and ignores a duplicate");
            Check(!(bool)typeof(ThirtyOneACollection).GetProperty("HasBlockConversion", F)!.GetValue(relic)! &&
                !(bool)typeof(ThirtyOneACollection).GetProperty("GrantsDodgeStats", F)!.GetValue(relic)!,
                "HBR 31A no longer converts block or grants dodge-linked stats");
            await relic.AfterCombatVictory(null!);
            Check(relic.PendingLive, "winning at six kinds records one pending Live");
            PropertyInfo pending = typeof(ThirtyOneACollection).GetProperty(nameof(ThirtyOneACollection.PendingLive))!;
            Check(pending.IsDefined(typeof(SavedPropertyAttribute), true), "pending Live is a real native saved property");
            await relic.BeforeCombatStart();
            Check(!relic.PendingLive && Hero.GetPower<HbrMoralePower>()?.Amount == 2,
                "next-combat Live grants exactly two real Morale and clears the pending receipt");
            await relic.BeforeCombatStart();
            Check(Hero.GetPower<HbrMoralePower>()?.Amount == 2, "Live cannot apply twice at the same start");

            GardenReset(); var yuki = Power<ThirtyOneAYukiDebuffPower>(Hero, 2);
            Equal(await (Task<decimal>)Call("HbrDebuffApplicationScope", "GetMultiplier", Hero, typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower), Enemy, null)!, 1.3m,
                "Yuki real consumption returns a thirty-percent multiplier");
            Check(yuki.Amount == 1, "Yuki consumes exactly one native power charge");
            await yuki.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, new[] { Hero });
            await yuki.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Enemy, new[] { Enemy });
            Check(yuki.Amount == 1, "Yuki unused charge survives both ordinary side boundaries");
            await (Task<decimal>)Call("HbrDebuffApplicationScope", "GetMultiplier", Hero, typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower), Enemy, null)!;
            Equal(await (Task<decimal>)Call("HbrDebuffApplicationScope", "GetMultiplier", Hero, typeof(MegaCrit.Sts2.Core.Models.Powers.WeakPower), Enemy, null)!, 1m,
                "Yuki absent/exhausted charges do not amplify a third application");
        }
        finally { HBRGOSakiConfig.ThirtyOneACutInPresentation = presentation; }
    }
}
