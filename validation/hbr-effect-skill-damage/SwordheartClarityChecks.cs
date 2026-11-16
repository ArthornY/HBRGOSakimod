using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

// Real card bodies, swordheart cap/spending and native energy gain. Only the
// optional compatibility-relic query, draw/generated-card presentation and
// Lucky Seven's rendering/autoplay leaf are substituted.
static partial class EffectChecks
{
    static bool ClarityForge;
    static readonly List<(Player Owner, decimal Count)> ClarityDraws = [];
    static readonly List<(CardModel Card, PileType Pile)> ClarityGenerated = [];
    static int ClarityFinisherHits;

    static bool ClarityForgeLeaf(ref int __result)
    {
        __result = ClarityForge ? 2 : 0;
        return false;
    }

    static bool ClarityDrawLeaf(Player player, decimal count, ref Task<IEnumerable<CardModel>> __result)
    {
        ClarityDraws.Add((player, count));
        __result = Task.FromResult<IEnumerable<CardModel>>(Array.Empty<CardModel>());
        return false;
    }

    static bool ClarityGeneratedLeaf(CardModel card, PileType newPileType, ref Task<CardPileAddResult> __result)
    {
        ClarityGenerated.Add((card, newPileType));
        __result = Task.FromResult(new CardPileAddResult { success = true, cardAdded = card });
        return false;
    }

    static bool ClarityAutoPlayLeaf(object[] __args, ref Task __result)
    {
        CardModel card = __args.OfType<CardModel>().Single();
        ClarityFinisherHits = (int)Invoke(typeof(MagicSwordLuckySevenHbrgoSaki).GetMethod("GetHitCount", F)!, card)!;
        __result = Task.CompletedTask;
        return false;
    }

    static void ClarityReset(bool hbr = true)
    {
        Reset(hbr); As31O(true); Fate(FateJson(bosses: 0));
        Set(Combat, "_allCards", new List<CardModel>());
        ClarityForge = false; ClarityDraws.Clear(); ClarityGenerated.Clear(); ClarityFinisherHits = -1;
    }

    static T ClarityCard<T>() where T : CardModel
    {
        if (!ModelDb.Contains(typeof(T))) ModelDb.Inject(typeof(T));
        T canonical = ModelDb.Card<T>();
        Set(canonical, "<Id>k__BackingField", ModelDb.GetId(typeof(T)));
        T card = (T)canonical.ToMutable();
        Set(card, "_owner", Player);
        return card;
    }

    static async Task SwordheartClarityCases()
    {
        var harmony = new Harmony(ProductionId + ".swordheart-clarity-card-leaves");
        var patched = new List<MethodBase>();
        void Patch(MethodBase target, string prefix)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
            patched.Add(target);
        }
        bool wasInProgress = CombatManager.Instance.IsInProgress;
        CardPile oldDeck = Player.Deck;
        try
        {
            CombatInProgressWithTurn();
            foreach (Type type in new[] { typeof(SwordheartClarityPower), typeof(SwordheartPower), typeof(BufferPower),
                         typeof(StrongestShieldPower), typeof(MeditationHbrgoSaki), typeof(MagicSwordLuckySevenHbrgoSaki) })
            {
                if (!ModelDb.Contains(type)) ModelDb.Inject(type);
                Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
            Type compat = Mod.GetType("HBRGOSakiMod.Core.Integration.HbrgoSakiHextechCompatLoader", true)!;
            Patch(compat.GetMethod("GetSwordheartMaximumBonus", F)!, nameof(ClarityForgeLeaf));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(method =>
                         method.Name == "CustomScaledWait" && method.ReturnType == typeof(Task)))
                Patch(wait, nameof(VisualTask));
            foreach (MethodInfo sound in typeof(SfxCmd).GetMethods(F).Where(method => method.Name == "Play"))
                Patch(sound, nameof(Skip));
            Patch(typeof(CardPileCmd).GetMethods(F).Single(method => method.Name == "Draw" &&
                method.GetParameters().Length == 4 && method.GetParameters()[1].ParameterType == typeof(decimal)), nameof(ClarityDrawLeaf));
            foreach (MethodInfo generated in typeof(CardPileCmd).GetMethods(F).Where(method => method.Name == "AddGeneratedCardToCombat"))
                Patch(generated, nameof(ClarityGeneratedLeaf));
            Patch(typeof(CardCmd).GetMethod("AutoPlay", F)!, nameof(ClarityAutoPlayLeaf));
            Type show = Helper("HbrLuckySevenFinisher");
            Patch(show.GetMethod("Begin", F)!, nameof(CrackShow));
            Patch(show.GetMethod("BeforeHits", F)!, nameof(VisualTask));
            Patch(show.GetMethod("AfterHits", F)!, nameof(VisualTask));
            Patch(show.GetMethod("Dispose", F)!, nameof(Skip));

            foreach (bool hbr in new[] { true, false })
            {
                ClarityReset(hbr);
                var clarityCard = ClarityCard<SwordheartClarityHbrgoSaki>();
                Check(clarityCard.EnergyCost.GetWithModifiers(CostModifiers.All) == 2 &&
                    !clarityCard.Keywords.Contains(CardKeyword.Innate), "unupgraded Swordheart Clarity costs two energy and is not Innate");
                Power<SwordheartPower>(Hero, 3);
                await UnifiedPlayCard(clarityCard, false);
                SwordheartClarityPower clarity = Hero.GetPower<SwordheartClarityPower>()!;
                Check(clarity != null && clarity.StackType == MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Single &&
                    Hero.GetPowerAmount<SwordheartPower>() == 3, "playing Swordheart Clarity changes the combat rule without generating swordheart");
                Check(SwordheartPower.GetKeptMaximum(Hero) == 7 && SwordheartPower.GetMaxAmount(Hero) == 14,
                    "Swordheart Clarity doubles seven to fourteen without changing the kept cap");
                int originalAmount = clarity.Amount;
                await UnifiedPlayCard(ClarityCard<SwordheartClarityHbrgoSaki>(), true);
                Check(Hero.Powers.Count(power => power is SwordheartClarityPower) == 1 && clarity.Amount == originalAmount &&
                    SwordheartPower.GetMaxAmount(Hero) == 14, "a second upgraded Clarity card does not stack the rule or its marker");
                var upgrade = ClarityCard<SwordheartClarityHbrgoSaki>();
                upgrade.UpgradeInternal(); upgrade.FinalizeUpgradeInternal();
                Check(upgrade.Keywords.Contains(CardKeyword.Innate) && upgrade.EnergyCost.GetWithModifiers(CostModifiers.All) == 2,
                    "upgraded Swordheart Clarity gains Innate and retains its two-energy cost");
                await clarity.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), Hero.Side, new[] { Hero }, Combat);
                Equal(Hero.GetPowerAmount<SwordheartPower>(), 3m, "Swordheart Clarity no longer generates swordheart at turn start");
                ClarityForge = true;
                Check(SwordheartPower.GetKeptMaximum(Hero) == 9 && SwordheartPower.GetMaxAmount(Hero) == 18,
                    "the compatibility forge's two retained-cap points are doubled as well: nine to eighteen");
                Set(Hero.GetPower<SwordheartPower>()!, "_amount", 17);
                Check(Hero.GetPower<SwordheartPower>()!.TryModifyPowerAmountReceived(
                    Hero.GetPower<SwordheartPower>()!, Hero, 3m, Hero, out decimal room) && room == 1m,
                    "positive swordheart gains are capped at the doubled forged maximum of eighteen");
                ManualCall("KeepSwordheartAfterCombat");
                Check(FateValue<int>("Swordheart") == 9, "combat-only Clarity capacity is discarded at the forged kept cap of nine");
                ClarityForge = false;

                ClarityReset(hbr);
                var swordheart = Power<SwordheartPower>(Hero, 8);
                var mode = Power<MagicSwordModePower>(Hero, 1);
                await UnifiedPlayCard(ClarityCard<SwordheartClarityHbrgoSaki>(), false);
                Equal(swordheart.Amount, 8m, "casting Clarity inside magic sword no longer doubles current swordheart");
                await mode.SpendSwordheart(new ThrowingPlayerChoiceContext(), 2m, ClarityCard<SwordheartClarityHbrgoSaki>());
                Check(swordheart.Amount == 4 && mode.ConsumedSwordheartAmount == 4,
                    "a two-swordheart cost consumes four and records four for Lucky Seven under Clarity");
                Set(Player, "<Deck>k__BackingField", new CardPile(PileType.Deck));
                await (Task<bool>)Invoke(typeof(MagicSwordModePower).GetMethod("PlayMagicSwordLuckySeven", F)!, null,
                    new ThrowingPlayerChoiceContext(), Hero, mode.ConsumedSwordheartAmount)!;
                Check(ClarityFinisherHits == 4, "the actual finisher factory configures four hits from Clarity's doubled payment");
                Check(swordheart.TryModifyPowerAmountReceived(swordheart, Hero, 2m, Hero, out decimal forbidden) && forbidden == 0m,
                    "magic sword has no Clarity exception to its positive-swordheart restriction");

                foreach (bool upgraded in new[] { false, true })
                foreach (bool inMagic in new[] { false, true })
                {
                    ClarityReset(hbr);
                    Power<SwordheartPower>(Hero, 3);
                    if (inMagic) Power<MagicSwordModePower>(Hero, 1);
                    int energy = Player.PlayerCombatState.Energy;
                    await UnifiedPlayCard(ClarityCard<MeditationHbrgoSaki>(), upgraded);
                    Equal(Hero.GetPowerAmount<SwordheartPower>(), inMagic ? 3m : upgraded ? 5m : 4m,
                        "Meditation grants one/two swordheart normally and none in magic sword (upgraded " + upgraded + ", magic " + inMagic + ")");
                    Check(ClarityDraws.SequenceEqual(new[] { (Player, 3m) }) && Player.PlayerCombatState.Energy == energy + 1,
                        "Meditation's actual card body always draws three and gains one native energy, including magic sword");

                    ClarityReset(hbr);
                    Power<SwordheartPower>(Hero, 3);
                    if (inMagic) Power<MagicSwordModePower>(Hero, 1);
                    await UnifiedPlayCard(ClarityCard<AnokoHbrgoSaki>(), upgraded);
                    Check(Hero.GetPowerAmount<BufferPower>() == 2 && Hero.GetPowerAmount<StrongestShieldPower>() == 1 &&
                        !Hero.HasPower<SwordheartClarityPower>() && Hero.GetPowerAmount<SwordheartPower>() == 3,
                        "Anoko grants only its two defensive powers and preserves current swordheart, in either mode");
                    Check(ClarityGenerated.Count == 1 && ClarityGenerated[0].Pile == PileType.Draw &&
                        ClarityGenerated[0].Card is MeditationHbrgoSaki generated && generated.IsUpgraded == upgraded,
                        "Anoko generates exactly one matching base/upgraded Meditation into the draw pile");
                }
            }
            ClarityReset(); As31O(false); Power<SwordheartClarityPower>(Hero, 7);
            Check(SwordheartPower.GetMaxAmount(Hero) == 0 &&
                SwordheartClarityPower.GetSwordheartConsumptionAmount(Hero, 2m) == 2m,
                "Clarity does not add swordheart capacity or double consumption for other characters");
        }
        finally
        {
            foreach (MethodBase target in patched) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
            ClarityForge = false;
            Set(Player, "<Deck>k__BackingField", oldDeck);
            CombatInProgress(wasInProgress);
            Reset(); As31O(false);
        }
    }
}
