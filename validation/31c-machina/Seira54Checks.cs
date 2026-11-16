using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

public sealed class FutureDuoBondFixture:RelicModel,IActiveDuoBond
{
    public override RelicRarity Rarity=>RelicRarity.Event;
    public bool Active;
    public bool IsBondActive=>Active;
}
static partial class EffectChecks
{
    static int SeiraDraws;
    static bool SeiraDrawLeaf(decimal count,ref Task<IEnumerable<CardModel>> __result)
    {SeiraDraws+=(int)count;__result=Task.FromResult(Enumerable.Empty<CardModel>());return false;}
    static StarsGuideYouHbrgoSaki SeiraReset(params int[] upgrades)
    {MachinaReset(0);UnifiedKeywordCard=null;DesuwaPipeline=null;SeiraDraws=0;return RanaCard<StarsGuideYouHbrgoSaki>(upgrades);}
    static DeflectorProtection SeiraProtection(Player owner,int current=0)
    {
        var protection=(DeflectorProtection)ModelDb.Relic<DeflectorProtection>().ToMutable();Set(protection,"_hasInitializedDp",true);Set(protection,"_dp",current);
        owner.AddRelicInternal(protection,silent:true);return protection;
    }
    static async Task SeiraCases()
    {
        var card=SeiraReset();
        Check(card.Type==CardType.Skill&&card.Rarity==CardRarity.Rare&&card.EnergyCost.Canonical==1&&card.MaxUpgradeLevel==7,"54: Seira is a one-energy rare garden skill with seven independent upgrades");
        Check(card.CanonicalKeywords.Contains(CardKeyword.Exhaust),"54: Seira declares the real canonical native Exhaust keyword");
        Check(card.ThirtyOneCMember==ThirtyOneCMember.SakurabaSeira,"54: Seira is the fifth distinct original31C member rather than a new squad");
        RanaUpgrade(card,6,5,4,3,2,1,0);Equal(card.EnergyCost.GetWithModifiers(CostModifiers.Local),0,"54: taking all seven choices in reverse order reduces energy only once");
        Equal(((IReadOnlyList<int>)Native(card,"GetAvailableUpgradeEffects")!).Count,0,"54: no independent choice remains after all seven upgrades");
        BondSavedCache(typeof(StarsGuideYouHbrgoSaki));var save=SavedProperties.From(card)!;
        var clone=(StarsGuideYouHbrgoSaki)ModelDb.GetById<CardModel>(card.Id).ToMutable();save.Fill(clone);
        Check(clone.UpgradeEffectOrder.SequenceEqual(card.UpgradeEffectOrder),"54: the native saved-properties route preserves the actual seven-choice acquisition order");

        card=SeiraReset();Set(Manual,"<MemoryGardenMetCharacters>k__BackingField",string.Empty);Set(Manual,"_memoryGardenUpgradePoints",1);
        Call("HbrMemoryGarden","RecordEncounters",Player,new[]{"seira"});
        var choices=((System.Collections.IEnumerable)Call("HbrMemoryGarden","GetChoices",Player)!).Cast<object>();
        Check(choices.Any(choice=>choice.GetType().GetProperty("PreviewCard")!.GetValue(choice)is StarsGuideYouHbrgoSaki&&(bool)choice.GetType().GetProperty("IsAvailable")!.GetValue(choice)!),"54: the real garden encounter recorder and choice builder offer Seira after meeting her");

        card=SeiraReset();var own=SeiraProtection(Player);Player peer=MachinaPeer(0);var other=SeiraProtection(peer);
        int hp=Hero.CurrentHp,peerHp=peer.Creature.CurrentHp;
        await RanaPlay(card);
        Equal(own.Dp,decimal.Floor(own.MaxDp*.1m),"54: base actual OnPlay restores10percent of the owner's real maximumDP");
        Equal(other.Dp,decimal.Floor(other.MaxDp*.1m),"54: base actual OnPlay restores the teammate's separate real maximumDP once");
        Check(Hero.CurrentHp==hp&&peer.Creature.CurrentHp==peerHp,"54: percentageDP recovery never writes either player's HP");
        Set(peer.Creature,"_currentHp",0);int previous=other.Dp;await RanaPlay(card);Equal(other.Dp,previous,"54: the all-ally recovery skips a dead teammate");

        card=SeiraReset();own=SeiraProtection(Player);Set(own,"_dp",own.MaxDp-1);await RanaPlay(card);
        Equal(own.Dp,own.MaxDp,"54: ordinary recovery still caps at the original maximumDP");
        card=SeiraReset(4);own=SeiraProtection(Player);Set(own,"_dp",own.MaxDp);await RanaPlay(card);await RanaPlay(card);await RanaPlay(card);
        int ceiling=(int)decimal.Floor(own.MaxDp*1.2m);Equal(own.Dp,ceiling,"54: Provocation stores actual spendableDP only up to120percent after repeated recovery");
        Check(own.HasOvercapDp,"54: overmaximumDP is represented by the real pool's saved overcap flag");
        int maximum=own.MaxDp;decimal absorbed=own.AbsorbDp(1m);Check(absorbed==1m&&own.Dp==ceiling-1&&own.MaxDp==maximum,"54: incoming ordinary DP absorption spends the overcap without increasing maximumDP");
        card=RanaCard<StarsGuideYouHbrgoSaki>();await RanaPlay(card);Equal(own.Dp,ceiling-1,"54: later ordinary recovery never deletes an existing overcap pool");

        card=SeiraReset();own=SeiraProtection(Player);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());
        Creature phantom=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(phantom,1);Set(Combat,"_allies",new List<Creature>{Hero,phantom});
        var shell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();Set(shell,"_owner",phantom);Set(shell,"_amount",1);Powers(phantom).Add(shell);
        decimal before=shell.CurrentDp,max=shell.MaximumDp;await RanaPlay(card);
        Equal(own.Dp,decimal.Floor(own.MaxDp*.1m),"54: all-ally30A recovery addresses the hero's actual pool rather than redirecting it twice to the phantom");
        Equal(shell.CurrentDp,Math.Min(max,before+decimal.Floor(max*.1m)),"54: the living phantom receives its own single10percent restoration");

        card=SeiraReset();own=SeiraProtection(Player);Set(Player,"<Character>k__BackingField",Blank<HbrgoHotaru>());
        await RanaPlay(card);Equal(own.Dp,decimal.Floor(own.MaxDp*.1m),"54: Hotaru restores her own real DP pool through the same all-ally recovery route");
        foreach(int disabled in new[]{0,1,2})
        {
            card=SeiraReset();own=SeiraProtection(Player);if(disabled>0)Power<DisabledPower>(Hero,disabled);
            await RanaPlay(card);Equal(own.Dp,decimal.Floor(own.MaxDp*.1m*(1m-disabled*.5m)),"54: existing Disabled"+disabled+" applies its normal recovery penalty once");
        }
        card=SeiraReset(4);own=SeiraProtection(Player);Power<LastStandPower>(Hero,1);await RanaPlay(card);
        Equal(own.Dp,0,"54: LastStand blocks even Provocation's overcap recovery on the actual player pool");
        card=SeiraReset(4);own=SeiraProtection(Player);Set(own,"_dp",own.MaxDp);await RanaPlay(card);
        BondSavedCache(typeof(DeflectorProtection));var poolSave=SavedProperties.From(own)!;var restored=(DeflectorProtection)ModelDb.Relic<DeflectorProtection>().ToMutable();Set(restored,"_owner",Player);poolSave.Fill(restored);
        Check(restored.Dp==own.Dp&&restored.HasOvercapDp&&restored.MaxDp==own.MaxDp,"54: native saved properties preserve actual player overcap amount and flag without changing maximumDP");
        restored.AbsorbDp(restored.Dp-restored.MaxDp);Check(!restored.HasOvercapDp&&restored.Dp==restored.MaxDp,"54: spending the last overcapDP clears the pool's extra-cap permission");
        card=SeiraReset(4);own=SeiraProtection(Player);
        Creature spirit=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Set(spirit,"_maxHp",1000);Set(spirit,"_currentHp",1000);Power<KozueSpiritPower>(spirit,1);
        Creature plainPet=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;
        Set(Combat,"_allies",new List<Creature>{Hero,spirit,plainPet});await RanaPlay(card);
        var spiritShell=spirit.GetPower<KozueSpiritShellPower>()!;
        Check(spiritShell!=null&&spiritShell.CurrentDp==20m&&spiritShell.MaximumDp==200m,"54: a living Kozue spirit missing its structural shell receives only the recovered10percent, not a free full shield");
        Check(!plainPet.HasPower<HighDimensionalShellPower>(),"54: recovery does not invent a DP pool for a regular pet without a shell or DP marker");
        for(int i=0;i<12;i++)await RanaPlay(card);
        Check(spiritShell!.CurrentDp==240m&&spiritShell.MaximumDp==200m&&spiritShell.HasOvercapDp,"54: a spirit's real shell stores120percent while retaining its200 maximum");
        BondSavedCache(typeof(KozueSpiritShellPower));var spiritSave=SavedProperties.From(spiritShell)!;var spiritCopy=(KozueSpiritShellPower)ModelDb.Power<KozueSpiritShellPower>().ToMutable();Set(spiritCopy,"_owner",spirit);try{spiritSave.Fill(spiritCopy);}catch{Console.WriteLine("SPIRIT SAVEDPROPS "+System.Text.Json.JsonSerializer.Serialize(spiritSave));Console.WriteLine("SPIRIT SETTERS "+string.Join(";",typeof(KozueSpiritShellPower).GetProperties(F).Where(property=>property.GetCustomAttributes(typeof(SavedPropertyAttribute),true).Length>0).Select(property=>property.Name+":"+(property.GetSetMethod(true)?.ToString()??"MISSING"))));throw;}
        Check(spiritCopy.CurrentDp==240m&&spiritCopy.HasOvercapDp,"54: inherited native shell saved-properties preserve exact spirit overcap amount and permission");
        var ordinary=RanaCard<StarsGuideYouHbrgoSaki>();await RanaPlay(ordinary);Equal(spiritShell.CurrentDp,240m,"54: ordinary recovery cannot truncate an existing spirit overcap");
        spiritShell.TryAbsorb(10m);Check(spiritShell.CurrentDp==230m&&spiritShell.HasOvercapDp,"54: an actual ordinary shell absorption spends excess spiritDP first");
        spiritShell.TryAbsorb(30m);Check(spiritShell.CurrentDp==200m&&!spiritShell.HasOvercapDp,"54: after excess spiritDP is spent the native shell clears overcap state");

        card=SeiraReset(4);Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki30A>());var reign=BondRelic<MomentOfReign>();
        Creature main=(Creature)H("Actor",Blank<SmallHopper>(),null,Player)!;Power<PhantomPower>(main,1);Set(Combat,"_allies",new List<Creature>{Hero,main});
        var mainShell=(PhantomHighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(),mainShell,main,1m,Hero,null);
        decimal initialMaximum=mainShell.MaximumDp;Native(mainShell,"SetDpAmount",initialMaximum,false);Native(mainShell,"RestoreWithLimit",initialMaximum*.2m,true);
        Check(mainShell.CurrentDp==decimal.Floor(initialMaximum*1.2m)&&mainShell.HasOvercapDp,"54: a real30A main phantom starts with spendable120percent DP before a permanent maximum-DP cost");
        decimal lost=(decimal)Native(reign,"LosePhantomMaxDp",initialMaximum)!;
        Check(lost==initialMaximum&&mainShell.MaximumDp==0m&&mainShell.CurrentDp==0m&&!mainShell.HasOvercapDp,"54: the actual permanent LosePhantomMaxDp path clamps an overcap phantom to exactly0DP without a health-scaling minimum1");
        await RanaPlay(card);Check(mainShell.CurrentDp==0m&&mainShell.MaximumDp==0m,"54: Provocation cannot restore any DP while that real main phantom's maximumDP is0");
        await PowerCmd.Remove(mainShell);await RanaPlay(card);
        Check(!main.HasPower<HighDimensionalShellPower>(),"54: after the depleted shell is removed, Provocation never creates a new shell for a real0-maximum phantom");

        foreach(int roll in new[]{47,48,100})
        {
            card=SeiraReset(1);own=SeiraProtection(Player);var pipeline=DesuwaAttach(card);UnifiedKeywordCard=card;LandRoll(pipeline,card,roll);
            int rng=RngCount(RunState.Rng.Niche);await RanaPlay(card);
            Equal(own.Dp,decimal.Floor(own.MaxDp*(roll>=48?.2m:.1m)),"54: Fortune threshold"+roll+" uses the already-resolved card die and the specified recovery");
            Equal(SeiraDraws,roll>=48?1:0,"54: Fortune threshold"+roll+" issues exactly the specified draw request");
            Equal(RngCount(RunState.Rng.Niche),rng,"54: reading the current Fortune result never rerolls or consumes another random sample");
        }
        card=SeiraReset(1);own=SeiraProtection(Player);var real=DesuwaAttach(card);UnifiedKeywordCard=card;
        await UnifiedApply(false,25,1,card);await UnifiedApply(true,1,1,card);UnifiedDice(10,40);
        var play=Play(card);await real.BeforeCardPlayed(play);Equal(real.CurrentRoll,50,"54: Fortune actually uses the normal advantage and25percent-bonus dice pipeline");
        await RanaPlay(card);Equal(own.Dp,decimal.Floor(own.MaxDp*.2m),"54: the normal modified final50 is accurate even though both raw dice were below48");
        Equal(UnifiedRawReads,2,"54: one advantage roll samples exactly the normal two raw dice");
        await real.AfterCardPlayed(new ThrowingPlayerChoiceContext(),play);
        Check(!UnifiedPowers().Any()&&!UnifiedPowers(true).Any(),"54: the normal after-play pipeline consumes both one-use dice modifier reservations");

        card=SeiraReset(5);own=SeiraProtection(Player);
        Equal((int)Call("StarsGuideCombatHelper","ActiveBondCount",Player)!,0,"54: no active duo bond leaves base recovery10percent");
        BondPermanent<MatchaParfaitHbrgoSaki>();var snack=BondRelic<TigersMouthSnackBond>();
        Equal((int)Call("StarsGuideCombatHelper","ActiveBondCount",Player)!,0,"54: an inactive one-member duo marker adds no recovery");
        BondPermanent<ByakkoRoarHbrgoSaki>();BondPermanent<HellfireBurnHbrgoSaki>();BondPermanent<FrozenMistMeowHbrgoSaki>();BondRelic<FamilyIsLikeThisBond>();
        BondPermanent<WorldIsMyLaboratoryHbrgoSaki>();BondPermanent<WorldConquestHbrgoSaki>();BondRelic<DemonKingAndFamiliarBond>();
        Equal((int)Call("StarsGuideCombatHelper","ActiveBondCount",Player)!,3,"54: all three existing active duo relics opt into the same activation interface");
        var future=(FutureDuoBondFixture)ModelDb.Relic<FutureDuoBondFixture>().ToMutable();future.Active=true;Player.AddRelicInternal(future,silent:true);
        Equal((int)Call("StarsGuideCombatHelper","ActiveBondCount",Player)!,4,"54: an independently authored future duo relic is automatically recognized without editing a whitelist");
        string face=card.GetDescriptionForPile(PileType.Hand,null);
        string plain=System.Text.RegularExpressions.Regex.Replace(face,@"\[/?[^\]]+\]",string.Empty);
        Check(System.Text.RegularExpressions.Regex.IsMatch(plain,@"50(?:\.0+)?%")&&!face.Contains("{DpRecoveryPercent}"),"54: the real native card-face description shows current50percent recovery from four active duos");
        await RanaPlay(card);Equal(own.Dp,decimal.Floor(own.MaxDp*.5m),"54: LoveDivination's additive ten-percentage-point bonuses flow into actual recovery");
        future.Active=false;Equal((int)Call("StarsGuideCombatHelper","ActiveBondCount",Player)!,3,"54: a future duo becoming inactive stops its contribution immediately");

        card=SeiraReset(2,3,6);own=SeiraProtection(Player);await RanaPlay(card);
        var crit=Hero.GetPower<HbrCriticalRateUpPower>()!;var critDamage=Hero.GetPower<HbrCriticalDamageUpPower>()!;
        Check(crit.Amount==5&&critDamage.Amount==5,"54: StardustRoute applies actual critical-rate and critical-damage instances lasting five turns");
        Check(crit.HbrLimitOverride==3&&critDamage.HbrLimitOverride==3,"54: the original independent effect explicitly opts out of the ordinary two-slot limit");
        var attack=Hero.GetPower<HbrAttackUpPower>()!;var defense=Hero.GetPower<HbrDefenseUpPower>()!;
        Check(attack.Amount==1&&attack.HbrExitOverride==1&&defense.Amount==2&&defense.HbrExitOverride==3,"54: FrozenWonder uses one actual attack use and two normal enemy-side defense turns");
        var down=Enemy.GetPower<HbrAttackDownPower>()!;Check(down.Amount==2,"54: RadiantNarrator attaches an actual two-turn attack-down instance");
        Equal((decimal)Call("HbrPercentDebuffHelper","GetEffectPercent",down)!,19m,"54:20percent base enemy attack-down follows the shared stat-versusE debuff formula");
        await crit.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});await critDamage.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(),CombatSide.Player,new[]{Hero});
        Check(crit.Amount==4&&critDamage.Amount==4,"54: original critical-pair duration really decrements once at the owner's player-side turn end");
        await RanaPlay(card);await RanaPlay(card);
        Check(Hero.Powers.OfType<HbrCriticalRateUpPower>().Count()==3&&Hero.Powers.OfType<HbrCriticalDamageUpPower>().Count()==3,"54: three independent five-turn critical grants coexist through the real effect framework");
    }
}
