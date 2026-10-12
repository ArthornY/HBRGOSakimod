using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Models.Events;

[CustomID("DOOR_AJAR_HBRGO_SAKI")]
public sealed class DoorAjarHbrgoSaki : ModEventTemplate
{
    private static readonly EventAssetProfile Assets = new(
        InitialPortraitPath: ImageHelper.GetImagePath(
            "events/hbrgo_saki_mod_event_door_ajar_hbrgo_saki.png"));

    public override EventAssetProfile AssetProfile => Assets;

    public override bool IsShared => true;

    public override bool IsAllowed(IRunState runState)
    {
        return false;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return new EventOption[]
        {
            new(this, IgnoreDoor, InitialOptionKey("IGNORE")),
            new(this, LookInside, InitialOptionKey("LOOK_INSIDE"))
        };
    }

    public override Task Resume(AbstractRoom room)
    {
        SetEventFinished(PageDescription("AFTER_BATTLE"));
        return Task.CompletedTask;
    }

    private Task IgnoreDoor()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }

    private Task LookInside()
    {
        SetEventState(
            PageDescription("LOOK_INSIDE"),
            new EventOption[]
            {
                new(
                    this,
                    IgnoreDoor,
                    ModOptionKey("LOOK_INSIDE", "IGNORE")),
                new(
                    this,
                    ObserveCarefully,
                    ModOptionKey("LOOK_INSIDE", "OBSERVE"))
            });
        return Task.CompletedTask;
    }

    private Task ObserveCarefully()
    {
        SetEventState(
            PageDescription("OBSERVE"),
            new EventOption[]
            {
                new(
                    this,
                    IgnoreDoor,
                    ModOptionKey("OBSERVE", "IGNORE")),
                new(
                    this,
                    WakeHer,
                    ModOptionKey("OBSERVE", "WAKE"))
            });
        return Task.CompletedTask;
    }

    private async Task WakeHer()
    {
        try
        {
            await OfferPreparationRewards();
            await UpgradeCards(2);
            await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp);
            int floor = await NararaShibaEventPreparation.Ready(Owner, "DOOR_AJAR/WAKE");
            var encounter = (NararaShibaBoss)ModelDb.Encounter<NararaShibaBoss>().ToMutable();
            encounter.InitialFloor = floor;
            EnterCombatWithoutExitingEvent(
                encounter,
                Array.Empty<Reward>(),
                shouldResumeAfterCombat: true);
        }
        catch (Exception exception)
        {
            NararaShibaEventPreparation.Fail(Owner, "DOOR_AJAR/WAKE", exception);
            throw;
        }
    }

    private Task OfferPreparationRewards()
    {
        bool IsEligible(CardModel card) =>
            HbrgoSaki31OCardFilter.IsAvailableTo(
                Owner.Character,
                card) &&
            card.Rarity == CardRarity.Rare;

        CardCreationOptions options =
            CardCreationOptions.ForNonCombatWithUniformOdds(
                    new[] { Owner.Character.CardPool },
                    IsEligible)
                .WithFlags(CardCreationFlags.NoRarityModification);
        return RewardsCmd.OfferCustom(
            Owner,
            new List<Reward>
            {
                new CardReward(options, 3, Owner),
                new CardRemovalReward(Owner)
            });
    }

    private async Task UpgradeCards(int count)
    {
        for (int index = 0; index < count; index++)
        {
            if (!PileType.Deck.GetPile(Owner).Cards.Any(HbrMemoryGardenCardCatalog.IsOrdinaryUpgradeCandidate))
            {
                return;
            }

            CardModel selected =
                (await CardSelectCmd.FromDeckForUpgrade(
                    Owner,
                    new CardSelectorPrefs(
                        CardSelectorPrefs.UpgradeSelectionPrompt,
                        0, 1)
                    {
                        Cancelable = true,
                        RequireManualConfirmation = true
                    }))
                .FirstOrDefault();
            if (selected == null)
            {
                continue;
            }

            CardCmd.Upgrade(selected);
        }
    }
}
