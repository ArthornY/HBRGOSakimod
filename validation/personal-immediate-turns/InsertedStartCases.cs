using System.Numerics;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Overclock = HBRGOSakiMod.Core.Models.Relics.Overclock;

// Explicit validation-only members. Four distinct members leave room above
// Intel2, so an accidental duplicate callback cannot be hidden by the cap.
public sealed class InsertedStartMemberFixture : CardModel, IThirtyOneDMemberCard
{
    public ThirtyOneDMember ThirtyOneDMember { get; set; }
    public InsertedStartMemberFixture() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }
}

static partial class EffectChecks
{
    static Action<Player>? PersonalDrawObserver;
    sealed record InsertedOwner(ThirtyOneDCollection Collection, MuuaIWillNotFallPower Muua, ExorcisingStringPower Miya);

    static async Task<InsertedOwner> InsertedAttach(Player player)
    {
        // The legacy personal-turn fixture intentionally empties all relics.
        // Restore only its existing HBR numeric profile for these HBR abilities.
        var manual = Blank<ElectronicSoldierManual>();
        Set(manual, "<IsMutable>k__BackingField", true); Set(manual, "_owner", player);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!.SetValue(manual,
            State<ElectronicSoldierManual>("Manual").HbrBattleProgressData);
        Set(player, "_relics", player.Relics.Append(manual).ToList());
        Check((bool)Invoke(Helper("HbrBattleStatsHelper").GetMethod("IsEnabled", F, null, new[] { typeof(Player) }, null)!,
            null, player)!, "inserted fixture explicitly enables this owner's HBR profile");
        for (int i = 0; i < 4; i++)
        {
            var card = (InsertedStartMemberFixture)ModelDb.Card<InsertedStartMemberFixture>().MutableClone();
            Set(card, "_owner", player); card.ThirtyOneDMember = (ThirtyOneDMember)i;
            player.Deck.AddInternal(card);
        }
        var relic = (ThirtyOneDCollection)ModelDb.Relic<ThirtyOneDCollection>().MutableClone();
        Set(relic, "_owner", player);
        Set(player, "_relics", player.Relics.Append(relic).ToList());
        await relic.BeforeCombatStart();
        var power = (MuuaIWillNotFallPower)ModelDb.Power<MuuaIWillNotFallPower>().MutableClone();
        Set(power, "_owner", player.Creature); Set(power, "_amount", 1);
        // SummerGrace: one exact Faith per own turn. Avoid the five-Faith
        // conversion branch so duplicate entry is visible as 2 versus 3.
        typeof(MuuaIWillNotFallPower).GetProperty("UpgradeMask", F)!.SetValue(power, 1 << 1);
        Powers(player.Creature).Add(power);
        var miya = (ExorcisingStringPower)ModelDb.Power<ExorcisingStringPower>().MutableClone();
        Set(miya, "_owner", player.Creature); Set(miya, "_amount", 1); Powers(player.Creature).Add(miya);
        return new(relic, power, miya);
    }
    static async Task InsertedOrdinary(Player player, InsertedOwner state)
    {
        await state.Collection.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
        await state.Muua.AfterEnergyReset(player);
        await state.Miya.AfterEnergyReset(player);
    }
    static void InsertedCounts(Player player, int intel, int faith, string label)
    {
        Equal(player.Creature.GetPowerAmount<HbrIntelPower>(), intel, label + " Intel");
        Check(HbrFaithResource.Get(player.Creature) == new BigInteger(faith), label + " exact Faith=" + faith);
        var miya = player.Creature.GetPower<ExorcisingStringPower>()!;
        object ledger = typeof(ExorcisingStringPower).GetField("_turns", F)!.GetValue(miya)!;
        Equal((int)ledger.GetType().GetProperty("Count")!.GetValue(ledger)!, intel, label + " independent Miya turn receipt");
    }
    static async Task PersonalInsertedStartCases()
    {
        foreach (Type type in new[] { typeof(InsertedStartMemberFixture), typeof(ThirtyOneDCollection),
            typeof(HbrIntelPower), typeof(HbrFaithPower), typeof(MuuaIWillNotFallPower), typeof(IWillNotFallHbrgoSaki), typeof(Overclock),
            typeof(ExorcisingStringResoundHbrgoSaki), typeof(ExorcisingStringPower), typeof(FragilePower) })
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var harmony = new Harmony(ProductionId + ".inserted-start-native");
        MethodInfo icon = typeof(PowerModel).GetProperty("Icon", F)!.GetMethod!;
        harmony.Patch(icon, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(FormPowerIconLeaf), F)!));
        try
        {
            await InsertedPersonalCase();
            await InsertedOverclockCase();
        }
        finally { PersonalDrawObserver = null; harmony.UnpatchAll(harmony.Id); PersonalCall("Clear"); }
    }
    static async Task InsertedPersonalCase()
    {
        Player peer = PersonalWorld();
        InsertedOwner own = await InsertedAttach(Player), other = await InsertedAttach(peer);
        await InsertedOrdinary(Player, own); await InsertedOrdinary(peer, other);
        InsertedCounts(Player, 1, 1, "ordinary owner baseline");
        InsertedCounts(peer, 1, 1, "ordinary teammate baseline");
        int ownNumber = Player.PlayerCombatState!.TurnNumber, peerNumber = peer.PlayerCombatState!.TurnNumber;
        int peerEnergy = peer.PlayerCombatState!.Energy;
        int observed = 0;
        PersonalDrawObserver = player =>
        {
            Check(player == Player, "personal entry draws only for its ticket owner");
            Check(player.PlayerCombatState!.Energy == 2, "personal start abilities run after native SetEnergy");
            InsertedCounts(Player, 2, 2, "before personal initial Draw");
            InsertedCounts(peer, 1, 1, "before personal Draw teammate remains ordinary");
            observed++;
        };
        var ticket = await PersonalBeginOwner();
        PersonalDrawObserver = null;
        Check(observed == 1, "real recorded Personal Begin reaches one initial Draw after both start abilities");
        await InsertedOrdinary(Player, own);
        InsertedCounts(Player, 2, 2, "duplicate callback inside same real personal token");
        await PersonalNetExecute(true, Player, ticket.Id, ticket.Ordinal);
        InsertedCounts(Player, 2, 2, "duplicate recorded Begin is idempotent");
        Check(Player.PlayerCombatState.TurnNumber == ownNumber && peer.PlayerCombatState!.TurnNumber == peerNumber &&
            peer.PlayerCombatState.Energy == peerEnergy, "personal insertion never advances native own-turn counters or teammate energy");
        await PersonalNetExecute(false, Player, ticket.Id, ticket.Ordinal);
        await InsertedOrdinary(Player, own);
        InsertedCounts(Player, 2, 2, "resumed ordinary turn does not replay either start ability");
        InsertedCounts(peer, 1, 1, "personal finish does not advance teammate start abilities");
        Set(Player.PlayerCombatState, "<TurnNumber>k__BackingField", ownNumber + 1);
        await InsertedOrdinary(Player, own);
        InsertedCounts(Player, 3, 3, "a genuine later ordinary turn still advances exactly once");
    }
    static async Task InsertedOverclockCase()
    {
        Player peer = PersonalWorld();
        InsertedOwner own = await InsertedAttach(Player), other = await InsertedAttach(peer);
        await InsertedOrdinary(Player, own); await InsertedOrdinary(peer, other);
        Type od = Helper("OverclockImmediateTurnCoordinator"), sessionType = od.GetNestedType("Session", F)!;
        var relic = (Overclock)ModelDb.Relic<Overclock>().MutableClone();
        Set(relic, "_owner", Player); Set(relic, "_activeLevel", 1);
        Set(Player, "_relics", Player.Relics.Append(relic).ToList());
        object session = Activator.CreateInstance(sessionType, F, null, new object[] { relic, Combat, new[] { Player } }, null)!;
        FieldInfo current = od.GetField("_current", F)!; object? prior = current.GetValue(null);
        int peerEnergy = peer.PlayerCombatState!.Energy, peerTurn = peer.PlayerCombatState.TurnNumber;
        int observed = 0;
        try
        {
            current.SetValue(null, session);
            Check((bool)Invoke(od.GetMethod("IsActiveFor", F)!, null, Player)! &&
                !(bool)Invoke(od.GetMethod("IsActiveFor", F)!, null, peer)!, "OD hand fixture uses a real participant-scoped active session");
            PersonalDrawObserver = player =>
            {
                Check(player == Player && player.PlayerCombatState!.Energy == 2, "OD start abilities run after native energy setup and before owner Draw");
                InsertedCounts(Player, 2, 2, "before OD initial Draw");
                InsertedCounts(peer, 1, 1, "OD nonparticipant teammate remains ordinary"); observed++;
            };
            MethodInfo deal = od.GetMethod("DealOverclockHand", F)!;
            await (Task)Invoke(deal, null, session, Player, new ThrowingPlayerChoiceContext())!;
            PersonalDrawObserver = null;
            Check(observed == 1, "real DealOverclockHand traverses live CanContinue guards and one initial Draw");
            await InsertedOrdinary(Player, own);
            await (Task)Invoke(deal, null, session, Player, new ThrowingPlayerChoiceContext())!;
            InsertedCounts(Player, 2, 2, "repeat same OD hand/start callback cannot collect twice");
            Check(peer.PlayerCombatState.Energy == peerEnergy && peer.PlayerCombatState.TurnNumber == peerTurn,
                "OD owner hand setup never changes a nonparticipant teammate's energy or native turn");
        }
        finally { PersonalDrawObserver = null; current.SetValue(null, prior); }
        await InsertedOrdinary(Player, own);
        InsertedCounts(Player, 2, 2, "return from OD preserves the ordinary start receipts");
        InsertedCounts(peer, 1, 1, "return from OD does not replay teammate start abilities");
    }
}
