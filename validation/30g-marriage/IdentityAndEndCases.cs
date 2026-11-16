using System.Text.Json;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Saves.Runs;

static partial class EffectChecks
{
    static async Task GIdentityAndSaveCases()
    {
        GReset(2);var origin=GOrigin<NarcissismIsFineHbrgoSaki>();var mirror=GMirror(origin);GSeedFor(2,san:25);await GPrepare();string originalId=GId(origin);
        var options=new JsonSerializerOptions{IncludeFields=true};string saved=JsonSerializer.Serialize(SavedProperties.From(origin),options);
        var copy=(CardModel)origin.MutableClone();Set(copy,"_owner",Player);copy.DeckVersion=origin;JsonSerializer.Deserialize<SavedProperties>(saved,options)!.Fill(copy);Player.Deck.AddInternal(copy,0);
        Call("ThirtyGProposalState","EnsureIdentities",Player);
        Check(GId(origin)==originalId&&GGraduated(origin),"72 true permanent clone inserted before original cannot steal the original's graduated ledger");
        Check(GId(copy)!=originalId&&!GGraduated(copy),"72 permanent native clone with copied saved ID and DeckVersion gets an independent physical identity");
        Check(GId(mirror)==originalId&&GGraduated(mirror),"72 ordinary combat/OD mirror still follows original physical identity");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();mirror=GMirror(origin);var copyMirror=GMirror(copy);await GPrepare();
        Check(GHeart(mirror)==1&&GHeart(copyMirror)==0,"72 later pending heart belongs to original, never the newly inserted permanent copy");
        saved=JsonSerializer.Serialize(SavedProperties.From(GRelic),options);var restored=(ThirtyGCollectionRelic)ModelDb.Relic<ThirtyGCollectionRelic>().ToMutable();JsonSerializer.Deserialize<SavedProperties>(saved,options)!.Fill(restored);
        Check(restored.San==((ThirtyGCollectionRelic)GRelic).San&&restored.Popularity==((ThirtyGCollectionRelic)GRelic).Popularity&&restored.ProposalData==((ThirtyGCollectionRelic)GRelic).ProposalData,"72 actual native relic saved-property JSON preserves SAN/popularity/physical schedules");
    }
    static async Task GListenerAndEndCases()
    {
        GReset(2);var target=GOrigin<MarriageMonakaFixture>();var mirror=GMirror(target,true);GSeedFor(3,san:25);await GPrepare();
        Check(!Combat.IterateHookListeners().Contains(mirror)&&!RunState.IterateHookListeners(Combat).Contains(mirror),"72 both real native listener streams filter graduated battle cards");
        var enchant=Blank<Adroit>();Set(enchant,"<IsMutable>k__BackingField",true);Set(enchant,"_card",mirror);var affliction=Blank<Bound>();Set(affliction,"<IsMutable>k__BackingField",true);Set(affliction,"_card",mirror);GAttachments.Add(enchant);GAttachments.Add(affliction);
        Check(enchant.HasCard&&affliction.HasCard,"72 attached native enchantment and affliction have real card owners");
        Check(!Combat.IterateHookListeners().Contains(enchant)&&!RunState.IterateHookListeners(Combat).Contains(affliction),"72 graduated owner also suppresses native attached enchantment/affliction callbacks");
        MarriageGardenFixture.Events.Clear();await GSideEnd();Check(!MarriageGardenFixture.Events.Any(e=>e==nameof(MarriageMonakaFixture)+":end"),"72 graduated card's actual native hand-end callback is suppressed");
        GReset(2);var yotsuha=GOrigin<AbsolutelyNoEffortHbrgoSaki>();GMirror(yotsuha,true);GSeedFor(2,san:25);await GPrepare();
        await GSideEnd();Check(!Hero.HasPower<HbrAttackUpPower>(),"72 graduated real Yotsuha gives no hand-end passive");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();var returned=GMirror(yotsuha);await GPrepare();Check(GHeart(returned)==1,"72 unplayable Yotsuha receives ordinary heart and bonus hand placement");
        await GSideEnd();Equal(Hero.Powers.OfType<HbrAttackUpPower>().Count(),2,"72 actual Yotsuha hand-end callback settles once plus one heartfelt repeat");
        Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();returned=GMirror(yotsuha);await GPrepare();Check(GHeart(returned)==0,"72 heart EOT repetition ends with the previous encounter");
        GReset(6);yotsuha=GOrigin<AbsolutelyNoEffortHbrgoSaki>();Native(yotsuha,"PrepareUpgradeEffect",5);yotsuha.UpgradeInternal();yotsuha.FinalizeUpgradeInternal();GMirror(yotsuha,true);GSeedFor(6,3,san:25);await GPrepare();Call("ThirtyGBondHelper","EndBattle",Player);GNextBattle();returned=GMirror(yotsuha);await GPrepare();
        Equal(GHeart(returned),2,"72 reaction3 gives unplayable Yotsuha two additional end-of-turn settlements");await GSideEnd();
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper","AttackUpBasis",returned)!,65m,"72 real SleepIn growth proves three actual Yotsuha end callbacks under doubled heart");await GSideEnd();
        Equal((decimal)Call("YotsuhaNoEffortCombatHelper","AttackUpBasis",returned)!,65m,"72 repeated native side-end dispatch cannot settle the same three callbacks twice");
    }
    static Task GSideEnd()
    {
        var method=typeof(Hook).GetMethods(F).Single(m=>m.Name is "BeforeTurnEnd" or "BeforeSideTurnEnd");
        object?[] args=method.GetParameters().Select(p=>p.ParameterType.IsInstanceOfType(RunState)?(object)RunState:p.ParameterType.IsInstanceOfType(Combat)?Combat:
            typeof(PlayerChoiceContext).IsAssignableFrom(p.ParameterType)?new ThrowingPlayerChoiceContext():p.ParameterType==typeof(CombatSide)?CombatSide.Player:
            typeof(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature>).IsAssignableFrom(p.ParameterType)?new[]{Hero}:throw new Exception(p.ToString())).ToArray();return (Task)Invoke(method,null,args)!;
    }
}
