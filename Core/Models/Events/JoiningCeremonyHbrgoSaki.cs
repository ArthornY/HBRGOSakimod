using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Models.Events;

[CustomID("JOINING_CEREMONY_HBRGO_SAKI")]
public sealed class JoiningCeremonyHbrgoSaki : ModEventTemplate
{
    private static readonly EventAssetProfile Assets = new(
        InitialPortraitPath: ImageHelper.GetImagePath(
            "events/hbrgo_saki_mod_event_joining_ceremony_hbrgo_saki.png"));

    public override EventAssetProfile AssetProfile => Assets;

    public override bool IsAllowed(IRunState runState)
    {
        return false;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return new EventOption[]
        {
            new(this, ChooseConjecture, InitialOptionKey("CONJECTURE")),
            new(this, OpenNoodles, InitialOptionKey("NOODLES")),
            new(this, OpenEheChumi, InitialOptionKey("EHE_CHUMI"))
        };
    }

    private async Task ChooseConjecture()
    {
        int? choice = await JoiningCeremonyChoiceHelper.Choose(Owner);
        if (!choice.HasValue)
        {
            return;
        }

        await ApplyConjectureReward(choice.Value);
        SetEventFinished(PageDescription("CONJECTURE_RESULT"));
    }

    private Task OpenNoodles()
    {
        SetEventState(
            PageDescription("NOODLES"),
            new EventOption[]
            {
                new(
                    this,
                    CompleteNoodles,
                    ModOptionKey("NOODLES", "TAKE_REWARD"))
            });
        return Task.CompletedTask;
    }

    private async Task CompleteNoodles()
    {
        await IncreaseSense(SixSenseAttribute.Madness, 1);
        await OfferCardReward(rareOnly: false);
        SetEventFinished(PageDescription("NOODLES"));
    }

    private Task OpenEheChumi()
    {
        SetEventState(
            PageDescription("EHE_CHUMI"),
            new EventOption[]
            {
                new(
                    this,
                    CompleteEheChumi,
                    ModOptionKey("EHE_CHUMI", "TAKE_REWARD"))
            });
        return Task.CompletedTask;
    }

    private async Task CompleteEheChumi()
    {
        await IncreaseSense(SixSenseAttribute.Madness, 1);
        Owner.GetRelic<DeflectorProtection>()?.GainMaxDp(10);
        SetEventFinished(PageDescription("EHE_CHUMI"));
    }

    private async Task ApplyConjectureReward(int choice)
    {
        switch (choice)
        {
            case 0:
                await IncreaseSense(SixSenseAttribute.Talent, 2);
                break;
            case 1:
                await IncreaseSense(SixSenseAttribute.Simplicity, 2);
                break;
            case 2:
                await RewardsCmd.OfferCustom(
                    Owner,
                    new List<Reward>
                    {
                        new PotionReward(Owner),
                        new PotionReward(Owner)
                    });
                break;
            case 3:
                await IncreaseSense(
                    SixSenseAttribute.LeadershipCharm,
                    2);
                break;
            case 4:
                await OfferCardReward(rareOnly: true);
                break;
            case 5:
                await IncreaseSense(SixSenseAttribute.Madness, 2);
                break;
            case 6:
                await RewardsCmd.OfferCustom(
                    Owner,
                    new List<Reward>
                    {
                        new CardRemovalReward(Owner)
                    });
                break;
            case 7:
                await IncreaseSense(
                    SixSenseAttribute.MentalStrength,
                    2);
                break;
            case 8:
                await IncreaseSense(SixSenseAttribute.Gentleness, 2);
                break;
        }
    }

    private async Task IncreaseSense(
        SixSenseAttribute attribute,
        int amount)
    {
        ElectronicSoldierManual manual =
            Owner.GetRelic<ElectronicSoldierManual>();
        if (manual == null)
        {
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            await manual.TryIncreaseSense(attribute);
        }
    }

    private Task OfferCardReward(bool rareOnly)
    {
        bool IsEligible(CardModel card) =>
            HbrgoSaki31OCardFilter.IsAvailableTo(
                Owner.Character,
                card) &&
            (!rareOnly || card.Rarity == CardRarity.Rare);

        CardCreationOptions options = rareOnly
            ? CardCreationOptions.ForNonCombatWithUniformOdds(
                    new[] { Owner.Character.CardPool },
                    IsEligible)
                .WithFlags(CardCreationFlags.NoRarityModification)
            : CardCreationOptions.ForNonCombatWithDefaultOdds(
                new[] { Owner.Character.CardPool },
                IsEligible);

        return RewardsCmd.OfferCustom(
            Owner,
            new List<Reward>
            {
                new CardReward(options, 3, Owner)
            });
    }
}
