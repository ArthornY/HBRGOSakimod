using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;

static partial class EffectChecks
{
    static async Task ClassIsolationCases()
    {
        ClassReset();
        foreach (var row in new[] { (typeof(LetterArrivesThirtyA), RelicRarity.Common), (typeof(FormerFoesThirtyA), RelicRarity.Uncommon), (typeof(CheckmateThirtyA), RelicRarity.Uncommon), (typeof(TicketStubThirtyA), RelicRarity.Uncommon), (typeof(PhantomsOfBygoneDaysThirtyA), RelicRarity.Rare), (typeof(ShelterRationCouponThirtyA), RelicRarity.Shop) })
        {
            RelicModel canonical = ModelDb.GetById<RelicModel>(ModelDb.GetId(row.Item1));
            Check(canonical is IThirtyAClassRelic && canonical.Rarity == row.Item2, "actual class marker and native rarity: " + row.Item1.Name);
            var tips = (IEnumerable<IHoverTip>)typeof(ThirtyAClassRelic).GetProperty("ExtraHoverTips", F)!.GetValue(canonical)!;
            Equal(tips.Count(), 4, "phantom/roster/experience/revival hover terms are all exposed: " + row.Item1.Name);
        }
        ClassRelic<LetterArrivesThirtyA>(); ClassRelic<CheckmateThirtyA>(); ClassRelic<TicketStubThirtyA>(); ClassRelic<PhantomsOfBygoneDaysThirtyA>(); var coupon = ClassRelic<ShelterRationCouponThirtyA>();
        Player peer = Blank<Player>(); Set(peer, "<NetId>k__BackingField", 202UL); Set(peer, "_runState", RunState); Set(peer, "_relics", new List<RelicModel>());
        Set(peer, "<Character>k__BackingField", Blank<HbrgoSaki30A>());
        Creature pet = ClassPhantom(); Set(pet, "_petOwner", peer);
        await ClassAct(pet); Equal(ClassDraws.Count, 0m, "teammate phantom action cannot trigger holder's letter");
        Set(pet, "_currentHp", 5); await ClassReign.AfterCurrentHpChanged(pet, 5m); Equal(ClassEnergy.Count, 0m, "teammate native revival notification cannot grant holder's energy");
        Equal((int)Call("ThirtyAClassRelicsHelper", "TicketExperience", peer, 4L)!, 4m, "holder's Ticket cannot multiply another player's phantom experience");
        var checkmate = Player.GetRelic<CheckmateThirtyA>()!;
        var foreignAttack = ClassCard<HBRGOSakiMod.Core.Models.Cards.StrikeHbrgoSaki>(); Set(foreignAttack, "_owner", peer); await ClassAfterPlay(checkmate, foreignAttack);
        Check(!checkmate.OrdinaryArmed, "teammate's attack card cannot arm holder's Checkmate");
        ClassCall("RememberSummonedForClassRelics", ClassEntry(1)); await coupon.AfterItemPurchased(peer, new ClassMerchantEntry(peer), 10); Equal(ClassXp(1), 0m, "another player's purchase cannot grant holder's roster experience");

        ClassReset(); var letter = ClassRelic<LetterArrivesThirtyA>(); var ticket = ClassRelic<TicketStubThirtyA>(); var revive = ClassRelic<PhantomsOfBygoneDaysThirtyA>(); checkmate = ClassRelic<CheckmateThirtyA>(); coupon = ClassRelic<ShelterRationCouponThirtyA>();
        foreach (RelicModel relic in new RelicModel[] { letter, ticket, revive, checkmate, coupon }) relic.IsMelted = true;
        pet = ClassPhantom(); await ClassAct(pet); Equal(ClassDraws.Count, 0m, "melted letter does not draw through direct GetRelic lookup");
        Equal((int)ClassCall("AddCombatExperienceBonus", 4)!, 4m, "melted Ticket is ignored by the central experience helper");
        Set(pet, "_currentHp", 5); await ClassReign.AfterCurrentHpChanged(pet, 5m); Equal(ClassEnergy.Count, 0m, "melted revival relic does not grant rewards through HP callback");
        await ClassAfterPlay(checkmate, ClassCard<HBRGOSakiMod.Core.Models.Cards.StrikeHbrgoSaki>()); Check(!checkmate.OrdinaryArmed, "melted Checkmate does not arm on a real attack-card receipt");
        ClassCall("RememberSummonedForClassRelics", ClassEntry(1)); await coupon.AfterItemPurchased(Player, new ClassMerchantEntry(Player), 10); Equal(ClassXp(1), 0m, "melted coupon does not award experience");
        ClassReset(); ClassRelic<FormerFoesThirtyA>().IsMelted = true; ClassRoomType = RoomType.Elite; ClassCall("SetRosterEntries", ClassEntries());
        var victim = (Creature)H("Actor", ModelDb.Monster<HBRGOSakiMod.Core.Models.Monsters.SmallHopper>().ToMutable(), null, null)!; Set(victim, "_currentHp", 0);
        await ClassReign.AfterDeath(new ThrowingPlayerChoiceContext(), victim, false, 0f);
        object acquired = ((System.Collections.IEnumerable)ClassCall("GetRosterEntries")!).Cast<object>().Single();
        Equal((int)ClassEntryType.GetProperty("Level", F)!.GetValue(acquired)!, 1m, "melted Former Foes cannot raise the initial level of an actual elite capture");
    }
}
