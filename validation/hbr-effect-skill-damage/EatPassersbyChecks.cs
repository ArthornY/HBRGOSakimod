using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

// Eat Passersby (2026-11-06): its power dealt its stacks as Unpowered damage without a declared
// skill, so HBR runs took a literal 3 per Block gain. The stacks are now the holder's skill, six
// units to 100%, like Thorns and the shared relics; its card and power texts state the coefficient.
static partial class EffectChecks
{
    static bool EatPassersbyTextIsolated;

    static async Task EatPassersbyCases()
    {
        async Task<Hit[]> Trigger(bool hbr, int stacks, bool twoEnemies = false)
        {
            Reset(hbr);
            if (twoEnemies)
            {
                var second = (Creature)H("Actor", Blank<SmallHopper>(), null, null)!;
                Set(Combat, "_enemies", new List<Creature> { Enemy, second });
            }
            var power = Power<EatPassersbyPower>(Hero, stacks);
            await (Task)Native(power, "AfterBlockGained", Hero, 500m, ValueProp.Move, null)!;
            Hit[] hits = Hits.ToArray();
            Cases.Add(new { source = "EatPassersby", hbr, stacks, twoEnemies, hits = hits.Select(hit => new { hit.Raw, hit.Final, props = hit.Props.ToString(), hit.Enabled, hit.Sample }).ToArray() });
            Clean($"Eat Passersby {stacks} (HBR {hbr})");
            return hits;
        }

        CombatInProgressWithTurn();
        try
        {
            Hit[] hits = await Trigger(true, 3);
            Check(hits.Length == 1 && ReferenceEquals(hits[0].Target, Enemy) && ReferenceEquals(hits[0].Dealer, Hero) &&
                  hits[0].Card == null && hits[0].Props == ValueProp.Unpowered && hits[0].Raw == 3m,
                "real Eat Passersby Block trigger keeps raw3, Unpowered, its holder as dealer and no card");
            Check(hits[0].Enabled, "HBR Eat Passersby declares its stacks as its holder's skill");
            Equal(hits[0].Final, decimal.Floor(Base(3m)), "HBR Eat Passersby3 resolves a 50% skill through the holder's stats instead of a literal 3");
            Equal((await Trigger(true, 5)).Single().Final, decimal.Floor(Base(5m)), "upgraded stacks 5 resolve an 83.33% skill");
            hits = await Trigger(true, 3, twoEnemies: true);
            Check(hits.Length == 2 && hits.All(hit => hit.Enabled && hit.Final == decimal.Floor(Base(3m))) && hits[0].Sample == hits[1].Sample,
                "both enemies take the same 50% skill from one command and one shared critical sample");
            Equal((await Trigger(false, 3)).Single().Final, 3m, "disabled HBR Eat Passersby keeps native3 damage");
        }
        finally { CombatInProgress(false); }
    }

    static void EatPassersbyTextCases()
    {
        Localization();
        if (!EatPassersbyTextIsolated)
        {
            // The card's hover tip reads the canonical power; the production card-face patch selects .hbr text.
            if (!ModelDb.Contains(typeof(EatPassersbyPower)))
            {
                ModelDb.Inject(typeof(EatPassersbyPower));
                Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(EatPassersbyPower))), "<Id>k__BackingField", ModelDb.GetId(typeof(EatPassersbyPower)));
            }
            Check(new Harmony(ProductionId + ".eat-passersby").CreateClassProcessor(
                Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!).Patch().Count > 0,
                "installs production CardDescriptionLocPatch");
            EatPassersbyTextIsolated = true;
        }
        string Smart(PowerModel power)
        {
            LocString smart = power.SmartDescription; smart.Add("OwnerName", "测试角色"); smart.Add("OnPlayer", true);
            Invoke(AccessTools.Method(typeof(PowerModel), "AddDumbVariablesToDescription"), power, smart, null);
            return smart.GetFormattedText();
        }
        string Face(CardModel card, bool inspect = false)
        {
            using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
                .Single(method => method.Name == "Begin" && method.GetParameters().Length == 4), null, card, inspect, false, null)!;
            LocString text = card.Description; card.DynamicVars.AddTo(text);
            return text.GetFormattedText();
        }

        Reset();
        Check(Smart(Power<EatPassersbyPower>(Hero, 3)).Contains("造成[blue]50%倍率[/blue]伤害"), "HBR Eat Passersby power text states three stacks as a 50% skill");
        Reset();
        Check(Smart(Power<EatPassersbyPower>(Hero, 5)).Contains("造成[blue]83.33%倍率[/blue]伤害"), "HBR five stacks read 83.33%");
        var card = Owned<EatPassersbyHbrgoSaki>();
        string face = Face(card), inspect = Face(card, true);
        Check(face.Contains("对全体敌人造成3000点伤害") && !face.Contains('%') && !face.Contains("倍率") && !face.Contains('{'),
            "67 Eat Passersby ordinary face shows numeric3000 damage without coefficient syntax or unformatted variables");
        Check(inspect.Contains("3000点伤害（50%）") && !inspect.Contains("倍率") && card.DynamicVars["EatPassersbyPower"].BaseValue == 3m,
            "67 Eat Passersby inspect alone adds parenthesized50percent without rewriting actual ability counter3");
        Check(NativeTipTexts(card).Any(text => text.Contains("（层数×16.67）%倍率")), "the power tip inside an HBR card's hover states the per-stack coefficient");
        var upgraded = Owned<EatPassersbyHbrgoSaki>(); upgraded.UpgradeInternal(); upgraded.FinalizeUpgradeInternal();
        Check(Face(upgraded).Contains("对全体敌人造成5000点伤害") && !Face(upgraded).Contains('%') &&
            Face(upgraded, true).Contains("（83.33%）") && !Face(upgraded, true).Contains("倍率") && upgraded.DynamicVars["EatPassersbyPower"].BaseValue == 5m,
            "67 finalized upgraded Eat Passersby shows5000 result and inspect83point33percent while preserving +2 actual stacks");
        H("Mode", false, 1);
        Check(Smart(Power<EatPassersbyPower>(Hero, 3)).Contains("等同于层数的伤害") && Face(card).Contains("对全体敌人造成3点伤害") &&
              NativeTipTexts(card).All(text => !text.Contains("倍率")), "disabled HBR keeps every native Eat Passersby text");
        H("Mode", true, 1);
    }
}
