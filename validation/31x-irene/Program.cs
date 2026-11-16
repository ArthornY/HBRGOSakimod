using System.Reflection;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

static class IreneStandalone { public static void Run(string path) => EffectChecks.RunIrene90(path); }
static partial class EffectChecks
{
    static ThereIsOnlyOneTruthHbrgoSaki I90(params int[] ids) => XGarden<ThereIsOnlyOneTruthHbrgoSaki>(ids);
    static long ICount(Player? owner = null) => (long)Call("IreneExposeCombatHelper", "GetDispelledKindCount", owner ?? Player)!;
    static Task<int> IDispel(int count = 3, Player? owner = null, Creature? target = null) => (Task<int>)Call("PowerDispelRules", "RandomRemoveBuffKinds", owner ?? Player, target ?? Enemy, count)!;
    static void IThree(Creature? target = null) { target ??= Enemy; Power<XDispelBuffA>(target, 10); Power<XDispelBuffB>(target, 20); Power<XDispelBuffC>(target, 30); }
    static async Task IreneBaseCases90()
    {
        XCardReset(); var card = I90();
        Check(card is IExposeCard { ExposeKindCount: 1 } && ((IThirtyOneXMemberCard)card).ThirtyOneXMember == ThirtyOneXMember.IreneRedmayne, "90 native card has generic Expose1 and Irene31X");
        Check(card.Type == CardType.Attack && card.TargetType == TargetType.AnyEnemy && card.Rarity == CardRarity.Rare && card.MaxUpgradeLevel == 7, "90 one-energy rare single-target attack has seven unique upgrades");
        Equal(card.EnergyCost.Canonical, 1, "90 canonical cost1"); Equal(card.DynamicVars.Damage.BaseValue, 6.54m, "90 exact109percent stored as6point54 native units");
        Check(!card.Keywords.Contains(CardKeyword.Exhaust), "90 reusable base has no Exhaust");
        Power<XDispelBuffA>(Enemy, 10); await ChasePlay(card);
        Equal(ICount(), 1, "90 actual pre-damage expose counted one removed kind"); Check(!Enemy.HasPower<XDispelBuffA>(), "90 actual native removal deletes enemy buff");
        Check(XCardAttacks.Single() is { Raw: 6.54m, Hits: 1, Qed: 1 }, "90109percent one-hit native command sees already-updated QED ledger");
        Equal(XCardHits.Single().Destruction, 1m / 3m, "90 direct total destruction coefficient1 normalized once"); Equal(Player.PlayerCombatState!.Energy, 19, "90 native wrapper spends one energy");
        Check(card.Pile == Player.PlayerCombatState.DiscardPile, "90 normal wrapper returns card to discard");
        XCardReset(); card = I90(0); Equal(card.EnergyCost.GetWithModifiers(CostModifiers.All), 0, "90 Flash native cost1to0");
        XCardReset(); card = I90(); await PowerCmd.Apply<HighDimensionalShellPower>(new ThrowingPlayerChoiceContext(), Enemy, 10000000m, Hero, null); await ChasePlay(card); Equal(XCardHits.Single().Resource, 1.3m, "90 activeDP target receives conditional30percent");
        XCardReset(); card = I90(); await ChasePlay(card); Equal(XCardHits.Single().Resource, 1m, "90 exposedHP receives no DP bonus");
        XCardReset(); card = I90(1); await ChasePlay(card);
        Equal(XCardAttacks.Single().CritRate, 35, "90 StayAwake grants actual35percent critical rate before attack"); Equal(XCardAttacks.Single().Hits, 2, "90 same attack immediately consumes one large combo to double1to2");
        Check(!Hero.HasPower<HbrCriticalRateUpPower>() && !Hero.HasPower<HbrComboLargePower>(), "90 real AfterAttack consumes each counted buff once for whole two-hit attack");
        XCardReset(); card = I90(2); Creature second = XExtraEnemy(); Power<XDispelBuffA>(Enemy, 4); Power<XDispelBuffB>(second, 5); await ChasePlay(card);
        Check(card.TargetType == TargetType.AllEnemies && XCardAttacks.Single().Targets.ToHashSet().SetEquals(new[] { Enemy, second }), "90 Insight actual native targeting attacks both enemies");
        Check(!Enemy.HasPower<XDispelBuffA>() && !second.HasPower<XDispelBuffB>() && ICount() == 2 && XCardHits.Count == 2, "90 each enemy is exposed separately before one all-target attack");
        XCardReset(); card = I90(3); IThree(); int niche = RngCount(RunState.Rng.Niche); await ChasePlay(card);
        Check(card.ExposeKindCount == 3 && ICount() == 3 && !Enemy.Powers.Any(p => p is XDispelBuffA or XDispelBuffB or XDispelBuffC), "90 PhantomThief really removes all three distinct kinds");
        Check(XCardHits.Single().Forced, "90 actual canonical hit has same narrow forced-critical scope as Hisame");
        Equal(RngCount(RunState.Rng.Niche), niche, "90 shared actual forced-critical pipeline needs no random critical sample");
        XCardReset(); card = I90(5); await ChasePlay(card);
        Check(XCardAttacks.Single().Attack == 50 && XCardAttacks.Single().CritDamage == 30, "90 Mystery grants both original counted buffs before damage");
        Check(!Hero.HasPower<HbrAttackUpPower>() && !Hero.HasPower<HbrCriticalDamageUpPower>(), "90 Mystery buffs are consumed by this whole attack");
        XCardReset(); card = I90(3, 6); IThree(); XCardNested = true; await ChasePlay(card);
        Check(XCardHits.Where(hit => hit.Depth == 0).All(hit => hit.Amp == 30 && hit.Forced), "90 QED includes current three-kind expose in same direct damage");
        Check(XCardHits.Where(hit => hit.Depth != 0).All(hit => hit.Amp == 0 && !hit.Forced && hit.Resource == 1 && hit.Destruction == 1), "90 nested same-source raw damage inherits neither QED, forced crit nor card resource/destruction modifiers");
        decimal boosted = Preview(Enemy, Hero, 6.54m, ValueProp.Move, card);
        var noQed = I90(3); decimal unboosted = Preview(Enemy, Hero, 6.54m, ValueProp.Move, noQed);
        Check(boosted > unboosted && Math.Abs(boosted - unboosted * 1.3m) <= 1m, "90 shared numeric preview applies current three-kind QED as30percent ordinary attack bonus exactly once");
        Equal((decimal)Call("IreneExposeCombatHelper", "GetDamageAmplificationPercent", card, Hero, Enemy, ValueProp.Unpowered)!, 0, "90 unpowered preview never borrows QED");
        XCardReset(); card = I90(3, 6); Power<XDispelBuffA>(Enemy, 1); await IDispel(1); await XActualCannon(card);
        Check(XCardHits.Single() is { Amp: 0m, Resource: 1m, Destruction: 1m, Forced: false }, "90 real CannonStats command retaining card source inherits neither QED nor critical/resource/destruction modifiers");
        XCardReset(); card = I90(0, 1, 2, 3, 4, 5, 6); XSaved(card, "90"); IThree(); await ChasePlay(card);
        Check(XCardAttacks.Single().Hits == 2 && card.Pile == Player.PlayerCombatState!.DiscardPile, "90 all seven upgrades keep correct native two-hit reusable card");
    }
    static async Task IreneDispelCases90()
    {
        XCardReset(); Power<XDispelBuffA>(Enemy, 3); Power<XDispelBuffA>(Enemy, 5); Equal(await IDispel(1), 1, "90 two independent instances of one ID count as one kind"); Check(!Enemy.HasPower<XDispelBuffA>() && ICount() == 1, "90 every original instance removed, one QED receipt");
        XCardReset(); Power<MinionPower>(Enemy, 1); int rng = RngCount(RunState.Rng.CombatCardGeneration); Equal(await IDispel(), 0, "90 intrinsic Minion never selected for dispel"); Equal(ICount(), 0, "90 protected mechanism grants no QED"); Equal(RngCount(RunState.Rng.CombatCardGeneration), rng, "90 empty eligible pool consumes no random sample");
        XCardReset(); Power<XDispelBuffA>(Enemy, 2); XDispelBuffA.Recreate = true; Equal(await IDispel(1), 1, "90 after-removal same-kind recreation does not erase successful receipt"); Check(Enemy.HasPower<XDispelBuffA>() && ICount() == 1, "90 new instance remains but original removal counts exactly once");
        XCardReset(); Power<XDispelBuffA>(Enemy, 2); XDispelBuffA.ThrowAfter = true;
        try { await IDispel(1); throw new Exception("missing callback fault"); } catch (InvalidOperationException error) { Check(error.Message.Contains("controlled"), "90 original after-removal exception preserved"); }
        Equal(ICount(), 1, "90 successful original removal still credited if later callback throws");
        XCardReset(); Power<XDispelBuffA>(Enemy, 2); await PowerCmd.Remove(Enemy.GetPower<XDispelBuffA>()!); Equal(ICount(), 0, "90 ordinary lifecycle Remove without explicit source is not a player dispel");
        XCardReset(); Power<XDispelBuffA>(Enemy, 1); await PowerCmd.Decrement(Enemy.GetPower<XDispelBuffA>()!); Equal(ICount(), 0, "90 natural decrement-to-zero is not evidence");
        XCardReset();
        var apply = Helper("HbrEffectApplication").GetMethods(F).Single(m => m.Name == "Apply" && m.IsGenericMethodDefinition).MakeGenericMethod(typeof(HbrAttackUpPower));
        foreach (decimal basis in new[] { 10m, 20m, 30m }) await (Task)Invoke(apply, null, new ThrowingPlayerChoiceContext(), Enemy, basis, Enemy, null, null, null, false, null)!;
        Check(Enemy.Powers.OfType<HbrAttackUpPower>().Count() == 2 && ICount() == 0, "90 actual two-slot replacement removes an old buff without crediting an intentional player dispel");
        XCardReset(); var cleanse = Power<WeakPower>(Hero, 2); await (Task<bool>)Call("PowerDispelRules", "TryPlayerDispel", cleanse)!; Equal(ICount(), 0, "90 allied negative-status cleanse gives no QED");
        XCardReset(); Power<StrengthPower>(Enemy, 5); Set(Player.PlayerCombatState!, "_energy", 1); await ChasePlay(FCard(typeof(Malaise))); Equal(ICount(), 0, "90 actual native negative Strength application is not an explicit kind dispel");
        XCardReset(); Power<ArtifactPower>(Enemy, 3); var expose = FCard(typeof(Expose)); await ChasePlay(expose);
        Check(!Enemy.HasPower<ArtifactPower>() && Enemy.HasPower<VulnerablePower>() && ICount() == 1, "90 actual native Expose removes Artifact and credits one source-owned kind before applying Vulnerable");
        XCardReset(); await ChasePlay(FCard(typeof(Expose))); Equal(ICount(), 0, "90 real native Expose without Artifact grants no receipt");
        XCardReset(); Power<ArtifactPower>(Enemy, 3); XRecreateArtifact = true; await ChasePlay(FCard(typeof(Expose)));
        Check(Enemy.HasPower<ArtifactPower>() && ICount() == 1, "90 explicit native Expose receipt survives a replacement Artifact created by removal callback");
        XCardReset(); Player peer = SupportTeammate(); Set(RunState, "_players", new List<Player> { Player, peer }); Set(Combat, "_allies", new List<Creature> { Hero, peer.Creature });
        Power<XDispelBuffA>(Enemy, 1); await IDispel(1, peer); Check(ICount(peer) == 1 && ICount() == 0, "90 another player's actual dispel credits only that owner");
        Power<XDispelBuffA>(Enemy, 1); await IDispel(1); Check(ICount(peer) == 1 && ICount() == 1, "90 separate owners maintain independent battle counters");
        var ended = typeof(CombatManager).GetField("CombatEnded", F)!.GetValue(CombatManager.Instance) as Delegate;
        Check(ended != null, "90 actual combat-end lifecycle callback is registered"); ended!.DynamicInvoke(new object?[] { null });
        Check(ICount() == 0 && ICount(peer) == 0, "90 actual CombatEnded dispatch clears both owner ledgers while old combat object remains");
        GNextBattle(); Equal(ICount(), 0, "90 changing real combat identity clears QED even before next play");
    }
    static async Task IreneEnergyCases90()
    {
        XCardReset(); var card = I90(3, 4); IThree(); Set(Player.PlayerCombatState!, "_energy", 10); await ChasePlay(card); Equal(Player.PlayerCombatState!.Energy, 11, "90 three removals immediately refund only two energy after one payment");
        IThree(); await ChasePlay(I90(3, 4)); Equal(Player.PlayerCombatState.Energy, 10, "90 another physical copy shares owner ordinary-turn2energy budget");
        foreach (bool od in new[] { false, true })
        {
            using (MisatoExtra(od)) { IThree(); await ChasePlay(I90(3, 4)); Equal(Player.PlayerCombatState.Energy, 11, "90 distinct real inserted-turn token gets independent2budget OD=" + od); }
            IThree(); await ChasePlay(I90(3, 4)); Equal(Player.PlayerCombatState.Energy, 10, "90 resuming ordinary turn never resets used allowance OD=" + od);
        }
        Set(Player.PlayerCombatState!, "<TurnNumber>k__BackingField", 2); IThree(); await ChasePlay(I90(3, 4)); Equal(Player.PlayerCombatState.Energy, 11, "90 native extra turn with new TurnNumber gets fresh allowance even same round");
        XCardReset(); card = I90(0, 3, 4); Hero.GetPower<XPlayObserver89>()!.Replays = 1; IThree(); Set(Player.PlayerCombatState!, "_energy", 0); await ChasePlay(card);
        Check(XCardAttacks.Count == 2 && Hero.GetPower<XPlayObserver89>()!.Paid.SequenceEqual(new[] { 0 }), "90 actual wrapper replay does not pay a second time"); Equal(Player.PlayerCombatState.Energy, 2, "90 replay cannot exceed owner-turn2refund cap");
        XCardReset(); card = I90(6); Power<XDispelBuffA>(Enemy, 1); await IDispel(1); XCardThrow = true;
        try { await ChasePlay(card); throw new Exception("missing attack fault"); } catch (InvalidOperationException error) { Check(error.Message.Contains("controlled"), "90 real attack fault preserved"); }
        Check(AsyncValue("IreneExposeCombatHelper", "Current") == null && ICount() == 1, "90 fault closes receipt without erasing completed earlier evidence");
        XCardReset(false); var ordinary = I90(6); Power<XDispelBuffA>(Enemy, 1); await IDispel(1); Equal(ICount(), 0, "90 legacy mode never gains HBR QED state");
    }
    public static void RunIrene90(string path) => XRun(path, "90", () => { IreneBaseCases90().GetAwaiter().GetResult(); IreneDispelCases90().GetAwaiter().GetResult(); IreneEnergyCases90().GetAwaiter().GetResult(); });
}
