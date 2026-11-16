using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Models.Events;

[CustomID("CHIHAYA_ANON_TOGAWA_SAKIKO")]
public sealed class ChihayaAnonTogawaSakiko : ModAncientEventTemplate
{
    private const int MaxOptionCount = 3;
    private const string ArtworkPath =
        "events/chihaya_anon_togawa_sakiko.png";
    private const string IconPath =
        "events/chihaya_anon_togawa_sakiko_icon.png";

    private static readonly EventAssetProfile Assets = new(
        InitialPortraitPath: ImageHelper.GetImagePath(ArtworkPath));

    private static readonly AncientEventPresentationAssetProfile
        PresentationAssets = new(
            MapIconPath: ImageHelper.GetImagePath(IconPath),
            MapIconOutlinePath: ImageHelper.GetImagePath(IconPath),
            RunHistoryIconPath: ImageHelper.GetImagePath(IconPath),
            RunHistoryIconOutlinePath: ImageHelper.GetImagePath(IconPath));

    public override EventAssetProfile AssetProfile => Assets;

    public override AncientEventPresentationAssetProfile
        AncientPresentationAssetProfile => PresentationAssets;

    public override Color DialogueColor => new("FF8899");

    public override IEnumerable<CharacterModel>
        AnyCharacterDialogueBlacklist =>
        new[] { ModelDb.Character<HbrgoSaki31O>() };

    public override IEnumerable<EventOption> AllPossibleOptions =>
        new EventOption[]
        {
            CreateRelicOption<DustyTome>(),
            CreateRelicOption<TouchOfOrobas>(),
            CreateRelicOption<ArchaicTooth>(),
            CreateRelicOption<GuideLittleHand>(),
            CreateChooseCardOption()
        };

    public override bool IsValidForAct(ActModel act)
    {
        return act is HbrgoActFour;
    }

    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            FirstVisitEverDialogue = new AncientDialogue(""),
            CharacterDialogues = new Dictionary<
                string,
                IReadOnlyList<AncientDialogue>>
            {
                [CharKey<HbrgoSaki31O>()] = new[]
                {
                    new AncientDialogue("")
                    {
                        IsRepeating = true
                    }
                }
            },
            AgnosticDialogues = new[]
            {
                new AncientDialogue("")
                {
                    IsRepeating = true
                }
            }
        };
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        List<OptionKind> candidates = new();
        if (Owner.GetRelic<DustyTome>() == null &&
            GetDustyTomeCardCandidates().Count > 0)
        {
            candidates.Add(OptionKind.DustyTome);
        }

        if (Owner.GetRelic<TouchOfOrobas>() == null)
        {
            candidates.Add(OptionKind.TouchOfOrobas);
        }

        if (Owner.GetRelic<ArchaicTooth>() == null)
        {
            candidates.Add(OptionKind.ArchaicTooth);
        }

        if (Owner.GetRelic<GuideLittleHand>() == null &&
            RollKeywordRules.IsRollEnabled(Owner))
        {
            candidates.Add(OptionKind.GuideLittleHand);
        }

        candidates.Add(OptionKind.ChooseCard);
        Rng.ShuffleCompat(candidates);

        List<EventOption> options = new(MaxOptionCount);
        foreach (OptionKind candidate in candidates)
        {
            EventOption option = CreateOption(candidate);
            if (option != null)
            {
                options.Add(option);
            }

            if (options.Count >= MaxOptionCount)
            {
                break;
            }
        }

        return options;
    }

    private EventOption CreateOption(OptionKind kind)
    {
        switch (kind)
        {
            case OptionKind.DustyTome:
            {
                List<CardModel> cardCandidates =
                    GetDustyTomeCardCandidates();
                if (cardCandidates.Count == 0)
                {
                    return null;
                }

                DustyTome relic =
                    (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
                relic.AncientCard = Owner.PlayerRng.Rewards
                    .NextItem(cardCandidates)
                    .Id;
                return CreateRelicOption(relic);
            }
            case OptionKind.TouchOfOrobas:
            {
                TouchOfOrobas relic =
                    (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>()
                        .ToMutable();
                return relic.SetupForPlayer(Owner)
                    ? CreateRelicOption(relic)
                    : null;
            }
            case OptionKind.ArchaicTooth:
            {
                ArchaicTooth relic =
                    (ArchaicTooth)ModelDb.Relic<ArchaicTooth>()
                        .ToMutable();
                return relic.SetupForPlayer(Owner)
                    ? CreateRelicOption(relic)
                    : null;
            }
            case OptionKind.GuideLittleHand:
                return CreateRelicOption<GuideLittleHand>();
            case OptionKind.ChooseCard:
                return CreateChooseCardOption();
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private List<CardModel> GetDustyTomeCardCandidates()
    {
        return Owner.Character.CardPool
            .GetUnlockedCards(
                Owner.UnlockState,
                Owner.RunState.CardMultiplayerConstraint)
            .Where(card =>
                card.Rarity == CardRarity.Ancient &&
                card.MaxUpgradeLevel > 0 &&
                !ArchaicTooth.TranscendenceCards.Contains(card) &&
                HbrgoSaki31OCardFilter.IsAvailableTo(
                    Owner.Character,
                    card))
            .GroupBy(card => card.Id.Entry, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private EventOption CreateChooseCardOption()
    {
        string textKey = InitialOptionKey("CHOOSE_CARD");
        return new EventOption(
            this,
            ChooseCardFromLibrary,
            L10NLookup(textKey + ".title"),
            L10NLookup(textKey + ".description"),
            textKey,
            Array.Empty<IHoverTip>());
    }

    private EventOption CreateRelicOption<T>() where T : RelicModel
    {
        return CreateRelicOption(ModelDb.Relic<T>().ToMutable());
    }

    private EventOption CreateRelicOption(RelicModel relic)
    {
        relic.AssertMutable();
        string textKey = InitialOptionKey(relic.Id.Entry);
        if (Owner == null)
        {
            return EventOption.FromRelic(
                relic,
                this,
                null,
                textKey);
        }

        var owner = Owner;
        relic.Owner = owner;
        return EventOption.FromRelic(
            relic,
            this,
            async () =>
            {
                await RelicCmd.Obtain(relic, owner);
                Done();
            },
            textKey);
    }

    private async Task ChooseCardFromLibrary()
    {
        List<CardCreationResult> cards = Owner.Character.CardPool
            .GetUnlockedCards(
                Owner.UnlockState,
                Owner.RunState.CardMultiplayerConstraint)
            .Where(card =>
                card.Rarity is CardRarity.Common or
                    CardRarity.Uncommon or
                    CardRarity.Rare)
            .Where(card =>
                HbrgoSaki31OCardFilter.IsAvailableTo(
                    Owner.Character,
                    card))
            .GroupBy(card => card.Id.Entry, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .Select(card => new CardCreationResult(
                Owner.RunState.CreateCard(card, Owner)))
            .ToList();

        if (cards.Count > 0)
        {
            CardSelectorPrefs prefs = new(
                L10NLookup(
                    Id.Entry +
                    ".pages.CHOOSE_CARD.selectionScreenPrompt"),
                1)
            {
                Cancelable = false
            };
            CardModel selected = (await CardSelectCmd
                    .FromSimpleGridForRewards(
                        new BlockingPlayerChoiceContext(),
                        cards,
                        Owner,
                        prefs))
                .FirstOrDefault();
            if (selected != null)
            {
                CardCmd.PreviewCardPileAdd(
                    await CardPileCmd.Add(selected, PileType.Deck));
            }
        }

        Done();
    }

    private enum OptionKind
    {
        DustyTome,
        TouchOfOrobas,
        ArchaicTooth,
        GuideLittleHand,
        ChooseCard
    }
}
