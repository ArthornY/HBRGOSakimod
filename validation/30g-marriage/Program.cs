using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Multiplayer;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

static class ThirtyGMarriageStandalone { public static void Run(string path)=>EffectChecks.RunThirtyGMarriage(path); }
static partial class EffectChecks
{
    static readonly Type[] GMembers=[typeof(MarriageYuinaFixture),typeof(MarriageKiryuFixture),typeof(MarriageMonakaFixture),typeof(MarriageChieFixture),typeof(MarriageHisameFixture),typeof(MarriageSatomiFixture)];
    static RelicModel GRelic=null!;
    static readonly List<AbstractModel> GAttachments=[];
    static readonly List<CardModel> GDeck=[];
    static readonly List<CardModel> GDrawMoves=[];
    static readonly List<object> GPresentations=[];
    static readonly List<int> GSanDice=[];
    static readonly List<int> GReactionDice=[];
    static int GSanRaw=1,GReactionRaw=1;
    static readonly List<int> GOpeningSans=[];
    static readonly List<(Player Player,HBRGOSakiMod.Core.Helpers.ThirtyGRunState State)> GRunNotifications=[];
    static readonly List<CardModel> GCardNotifications=[];
    static bool GListeners(ref IEnumerable<AbstractModel> __result)
    {
        __result=Combat.Creatures.SelectMany(c=>c.Powers).Cast<AbstractModel>().Concat(Player.Relics).Concat(Player.PlayerCombatState!.AllCards).Concat(Player.Deck.Cards).Concat(GAttachments).ToArray();return false;
    }
    static T GOrigin<T>() where T:CardModel,new()
    {var origin=FormCard<T>();MisatoPermanent(origin);((List<CardModel>)typeof(RunState).GetField("_allCards",F)!.GetValue(RunState)!).Add(origin);GDeck.Add(origin);return origin;}
    static CardModel GMirror(CardModel origin,bool inHand=false)
    {
        var card=(CardModel)origin.MutableClone();Set(card,"_owner",origin.Owner);card.DeckVersion=origin;
        (inHand?origin.Owner.PlayerCombatState!.Hand:origin.Owner.PlayerCombatState!.DrawPile).AddInternal(card);
        ((List<CardModel>)typeof(CombatState).GetField("_allCards",F)!.GetValue(Combat)!).Add(card);return card;
    }
    static void GReset(int kinds)
    {
        NewReset();GNextBattle();Set(RunState,"_allCards",new List<CardModel>());Native(Player.GetRelic<HBRGOSakiMod.Core.Models.Relics.ElectronicSoldierManual>()!,"SetMemoryGardenSharedUpgrades",string.Empty);GAttachments.Clear();Set(Player,"<Character>k__BackingField",Blank<HbrgoSaki>());GDeck.Clear();GDrawMoves.Clear();GPresentations.Clear();GOpeningSans.Clear();GRunNotifications.Clear();GCardNotifications.Clear();GSanDice.Clear();GReactionDice.Clear();GSanRaw=GReactionRaw=1;GSceneWait=null;MarriageGardenFixture.Events.Clear();
        Type type=Mod.GetType("HBRGOSakiMod.Core.Models.Relics.ThirtyGCollectionRelic",true)!;
        if(!ModelDb.Contains(type))ModelDb.Inject(type);var canonical=ModelDb.GetById<RelicModel>(ModelDb.GetId(type));Set(canonical,"<Id>k__BackingField",ModelDb.GetId(type));
        Set(Player,"<NetId>k__BackingField",1UL);
        ConstructorInfo rngCtor=typeof(PlayerRngSet).GetConstructors().Single(c=>c.GetParameters().Length==1);Set(Player,"<PlayerRng>k__BackingField",rngCtor.Invoke(new[]{Convert.ChangeType(20261115,rngCtor.GetParameters()[0].ParameterType)}));
        GRelic=(RelicModel)canonical.ToMutable();Set(GRelic,"_owner",Player);Set(Player,"_relics",Player.Relics.Append(GRelic).ToList());
        foreach(Type member in GMembers.Take(kinds))
        {
            var origin=(CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(member)).ToMutable();Set(origin,"_owner",Player);Player.Deck.AddInternal(origin);((List<CardModel>)typeof(RunState).GetField("_allCards",F)!.GetValue(RunState)!).Add(origin);GDeck.Add(origin);
        }
    }
    static void GNextBattle()
    {
        // Keep permanent player/deck/relic identities; only the combat and its
        // transient piles change, as they do at a genuine new encounter.
        var next=Blank<CombatState>();Set(next,"<RunState>k__BackingField",RunState);Set(next,"<RoundNumber>k__BackingField",1);Set(next,"<CurrentSide>k__BackingField",CombatSide.Player);
        Set(next,"_allies",new List<Creature>{Hero});Set(next,"_enemies",new List<Creature>{Enemy});Set(next,"_allCards",new List<CardModel>());Set(next,"_escapedCreatures",new List<Creature>());
        typeof(Checks).GetField("Combat",F)!.SetValue(null,next);Hero.CombatState=Enemy.CombatState=next;Powers(Hero).Clear();Powers(Enemy).Clear();
        Set(Hero,"_currentHp",5000);Set(Hero,"_maxHp",5000);Set(Enemy,"_currentHp",5000);Set(Enemy,"_maxHp",5000);
        var pc=Blank<PlayerCombatState>();Set(pc,"_player",Player);Set(pc,"<TurnNumber>k__BackingField",1);Set(pc,"_pets",new List<Creature>());Set(pc,"_energy",3);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(pc,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<PlayerCombatState>k__BackingField",pc);Set(RunState,"<ActFloor>k__BackingField",RunState.ActFloor+1);
#if HBR_BETA_NATIVE
        object turn=typeof(CombatManager).GetField("_turnState",F)!.GetValue(CombatManager.Instance)!;Set(turn,"<State>k__BackingField",next);Set(turn,"<IsInProgress>k__BackingField",true);
        object tracker=typeof(CombatManager).GetProperty("StateTracker",F)!.GetValue(CombatManager.Instance)!;Set(tracker,"_state",next);
#else
        Set(CombatManager.Instance,"_state",next);Set(CombatManager.Instance,"<IsInProgress>k__BackingField",true);
#endif
    }
    static object GSnapshot(CardModel card)=>Call("ThirtyGProposalState","Get",card)!;
    static bool GGraduated(CardModel card)=>(bool)GSnapshot(card).GetType().GetProperty("Graduated")!.GetValue(GSnapshot(card))!;
    static int GHeart(CardModel card)=>(int)GSnapshot(card).GetType().GetProperty("HeartfeltExtraPlays")!.GetValue(GSnapshot(card))!;
    static Task GPrepare()=> (Task)Call("ThirtyGBondHelper","PrepareBattle",new ThrowingPlayerChoiceContext(),Player)!;
    static void GResetSan()=>Call("ThirtyGBondHelper","ResetSan",Player);
    static async Task GEntry()
    {
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ThirtyGCombatEntryPatch",true)!;
        await (Task)Invoke(patch.GetMethod("PrepareThenNativeStart",F)!,null,RunState,Combat)!;
    }
    static bool GNativeStart(IRunState runState,ICombatState combatState,ref Task __result)
    {MarriageGardenFixture.Events.Add("native-opening-passives");GOpeningSans.Add(Player.GetRelic<HBRGOSakiMod.Core.Models.Relics.ThirtyGCollectionRelic>()?.San??-1);__result=(Task)Call("NiinaMakeupCombatHelper","OnCombatStart",combatState)!;return false;}
    public static void RunThirtyGMarriage(string path)
    {
        bool ok=false;string?error=null;
        try
        {
            Install();Localization();var harmony=new Harmony(ProductionId+".thirtyG72");
            foreach(Type type in GMembers.Concat(new[]{typeof(NarcissismIsFineHbrgoSaki),typeof(AbsolutelyNoEffortHbrgoSaki),typeof(AllOrdersConfirmedHbrgoSaki),typeof(NiinaMakeupPower)}))
            {if(!ModelDb.Contains(type))ModelDb.Inject(type);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
            foreach(Type type in new[]{RunState.GetType(),typeof(CombatState)})harmony.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(MisatoListeners),F)!){priority=Priority.First+100});
            harmony.Patch(typeof(Hook).GetMethod(nameof(Hook.BeforeCombatStart))!,prefix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(GNativeStart),F)!));
            GInstall(harmony);
            GNormalAndKiryuCases().GetAwaiter().GetResult();GPhaseOrderCases().GetAwaiter().GetResult();GSeparateOwnersCases().GetAwaiter().GetResult();GRehydratePhysicalCases().GetAwaiter().GetResult();GSanAndOwnerCases().GetAwaiter().GetResult();GVoteCases().GetAwaiter().GetResult();GListenerAndEndCases().GetAwaiter().GetResult();GIdentityAndSaveCases().GetAwaiter().GetResult();GReactionCases().GetAwaiter().GetResult();ok=true;
        }
        catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual proposal helper and native opening adapter, permanent cards/native mirrors, real garden upgrade coordinator/native catalog and saved-property identity; user/network sync and scene pile/damage leaves controlled. Native hook listener and AutoPlay blocking paths, real unplayable end callback. No Godot rendering or live network session."},new JsonSerializerOptions{WriteIndented=true}));
        if(!ok)throw new Exception("Inspect "+path);
    }

}
