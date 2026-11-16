using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

// Three trigger effects (2026-11-06), after Eat Passersby. Pain Resonance dealt its stacks as
// Unpowered damage without a declared skill, a literal 6 in HBR; it now deals its holder's skill,
// six units to 100%. Defy Fate Strike already costs its phantom 100 HP per hit in HBR, and Hanging
// Shot already deals its applier's powered skill; their HBR texts now say so.
static partial class EffectChecks
{
    static async Task TriggerTextCases()
    {
        Localization();
        foreach (Type type in new[] { typeof(HangingShotPower), typeof(PainResonancePower) })
            if (!ModelDb.Contains(type))
            {
                ModelDb.Inject(type);
                Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
            }
        string Smart(PowerModel power)
        {
            LocString smart = power.SmartDescription; smart.Add("OwnerName", "测试角色"); smart.Add("OnPlayer", power.Owner == Hero);
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
        T Upgraded<T>() where T : CardModel, new()
        {
            var card = Owned<T>(); card.UpgradeInternal(); card.FinalizeUpgradeInternal(); return card;
        }

        // Real triggers first; their values are recorded before any assertion.
        async Task<Hit> Resonate(bool hbr, int stacks)
        {
            Reset(hbr);
            var power = Power<PainResonancePower>(Hero, stacks);
            Invoke(typeof(PainResonancePower).GetMethod("QueuePhantomDamage", F)!, power);
            await (Task)Native(power, "AfterDamageGiven", new ThrowingPlayerChoiceContext(), Enemy,
                Blank<MegaCrit.Sts2.Core.Entities.Creatures.DamageResult>(), ValueProp.Move, Enemy, null)!;
            Hit hit = Hits.Single();
            Cases.Add(new { source = "PainResonance", hbr, stacks, hit.Raw, hit.Final, props = hit.Props.ToString(), hit.Enabled });
            Clean($"Pain Resonance {stacks} (HBR {hbr})");
            return hit;
        }
        async Task<decimal> PhantomLoss(bool hbr)
        {
            Reset(hbr);
            var phantom = (Creature)H("Actor", Blank<SmallHopper>(), null, Player)!;
            Power<PhantomPower>(phantom, 1);
            decimal before = (decimal)Call("LargeCreatureHealthHelper", "GetCurrentHp", phantom)!;
            await (Task)Call("PhantomHelper", "LoseHp", phantom, 1m)!;
            decimal lost = before - (decimal)Call("LargeCreatureHealthHelper", "GetCurrentHp", phantom)!;
            Cases.Add(new { source = "DefyFateStrike phantom HP cost", hbr, before, lost });
            return lost;
        }
        async Task<Hit> Hang(bool hbr, int stacks, bool dp)
        {
            Reset(hbr);
            if (dp) Power<HighDimensionalShellPower>(Enemy, 2000);
            var power = Power<HangingShotPower>(Enemy, stacks); power.Applier = Hero;
            await (Task)Native(power, "BeforeSideTurnStart", new ThrowingPlayerChoiceContext(), CombatSide.Player,
                new[] { Hero }, Combat)!;
            Hit hit = Hits.Single();
            Cases.Add(new { source = "HangingShot", hbr, stacks, dp, hit.Raw, hit.Final, props = hit.Props.ToString(), hit.Enabled });
            return hit;
        }

        CombatInProgressWithTurn();
        Hit resonance, resonanceUp, resonanceNative, hang, hangDp, hangNative;
        decimal loss, lossNative;
        try
        {
            resonance = await Resonate(true, 6); resonanceUp = await Resonate(true, 9); resonanceNative = await Resonate(false, 6);
            loss = await PhantomLoss(true); lossNative = await PhantomLoss(false);
            hang = await Hang(true, 15, false); hangDp = await Hang(true, 15, true); hangNative = await Hang(false, 15, false);
        }
        finally { CombatInProgress(false); }

        // Pain Resonance: the holder's skill on a random enemy, six units to 100%.
        Check(resonance.Raw == 6m && resonance.Props == ValueProp.Unpowered && ReferenceEquals(resonance.Dealer, Hero) &&
              ReferenceEquals(resonance.Target, Enemy) && resonance.Card == null && resonance.Enabled,
            "real Pain Resonance hit keeps raw6, Unpowered, its holder as dealer and declares the holder's skill");
        Equal(resonance.Final, decimal.Floor(Base(6m)), "HBR Pain Resonance6 resolves a 100% skill instead of a literal 6");
        Equal(resonanceUp.Final, decimal.Floor(Base(9m)), "upgraded stacks 9 resolve a 150% skill");
        Equal(resonanceNative.Final, 6m, "disabled HBR Pain Resonance keeps native6 damage");

        // Defy Fate Strike: each phantom hit already costs the fixed 1 times 100 in HBR.
        Equal(loss, 100m, "HBR Defy Fate Strike's phantom cost through the real PhantomHelper.LoseHp is 100 HP");
        Equal(lossNative, 1m, "disabled HBR phantom cost stays 1 HP");

        // Hanging Shot: its applier's powered skill, 25% more against DP.
        Check(hang.Raw == 15m && hang.Props == ValueProp.Move && ReferenceEquals(hang.Dealer, Hero) && ReferenceEquals(hang.Target, Enemy),
            "real Hanging Shot keeps raw15 as its applier's powered hit");
        Equal(hang.Final, decimal.Floor(Base(15m)), "HBR Hanging Shot15 already resolves its applier's 250% skill");
        Equal(hangDp.Raw, 18.75m, "against DP the raw amount grows 25% before conversion");
        Equal(hangDp.Final, decimal.Floor(Base(18.75m)), "HBR Hanging Shot15 against DP resolves a 312.5% skill");
        Equal(hangNative.Final, 15m, "disabled HBR Hanging Shot keeps native15");

        // Texts state what resolves; disabled HBR keeps every native text.
        Reset();
        Check(Smart(Power<PainResonancePower>(Hero, 6)).Contains("造成[blue]100%倍率[/blue]伤害"), "HBR Pain Resonance power text states six stacks as a 100% skill");
        var resonanceBase = Owned<PainResonanceHbrgoSaki>();
        Check(Face(resonanceBase).Contains("对随机一名敌人造成6000点伤害") && !Face(resonanceBase).Contains('%') &&
            Face(resonanceBase, true).Contains("6000点伤害（100%）") && !Face(resonanceBase, true).Contains("倍率"),
            "67 Pain Resonance ordinary face shows numeric6000 while inspect alone appends100percent");
        var resonanceCard = Upgraded<PainResonanceHbrgoSaki>();
        Check(Face(resonanceCard).Contains("造成9000点伤害") && !Face(resonanceCard).Contains('%') &&
            Face(resonanceCard, true).Contains("（150%）") && !Face(resonanceCard, true).Contains("倍率") && resonanceCard.DynamicVars["PainResonancePower"].BaseValue == 9m,
            "67 finalized upgraded Pain Resonance displays9000 and inspect150percent without changing +3 stack upgrade");
        Check(Face(Owned<DefyFateStrikeHbrgoSaki>()).Contains("失去100点生命X次") && Face(Upgraded<DefyFateStrikeHbrgoSaki>()).Contains("失去100点生命2X次"),
            "HBR Defy Fate Strike faces state the 100 HP phantom cost, normal and upgraded");
        var hanging = Power<HangingShotPower>(Enemy, 15); hanging.Applier = Hero;
        Check(Smart(hanging).Contains("受到施加者[blue]250%倍率[/blue]的伤害"), "HBR Hanging Shot power text states its applier's 250% skill");
        var hangingCard = Owned<HangingShotHbrgoSaki>();
        Check(NativeTipTexts(hangingCard).Any(text => text.Contains("（层数×16.67）%倍率")), "the Hanging Shot tip inside an HBR card's hover states the per-stack coefficient");
        H("Mode", false, 1);
        Check(Smart(Power<PainResonancePower>(Hero, 6)).Contains("造成6点伤害") && Face(Owned<PainResonanceHbrgoSaki>()).Contains("造成6点伤害") &&
              Face(Owned<DefyFateStrikeHbrgoSaki>()).Contains("失去1点生命X次") && Smart(hanging).Contains("等同于层数") &&
              NativeTipTexts(hangingCard).All(text => !text.Contains("倍率")), "disabled HBR keeps every native trigger text");
        H("Mode", true, 1);
    }
}
