using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Story;

/// <summary>
/// Reusable HBR reader event. JSON describes pages and bounded rewards; it never contains executable script.
/// The vanilla EventSynchronizer remains responsible for sending and replaying gameplay choices.
/// </summary>
public abstract class HbrStoryEvent : ModEventTemplate
{
    public const string ReaderScenePath = "res://scenes/events/custom/library_hbrgo_saki.tscn";
    public const int ExtendedGameplayRuntimeVersion = 1;
    public const int MemoryGardenEncounterVersion = 2;
    internal const string ActEndingDoorEventId = "INSIDETHEDOOR_HBRGO_SAKI";

    private HashSet<string> _claimedRewardChoices = new(StringComparer.Ordinal);
    private HashSet<string> _memoryEncounteredCharacters = new(StringComparer.Ordinal);
    private string _currentStoryPageId;
    private int _stateVersion;
    private bool _choiceInProgress;
    private bool _combatPending;
    private EncounterModel _lastResumedCombat;

    public abstract string DefinitionPath { get; }

    public HbrStoryDefinition Definition
    {
        get
        {
            HbrStoryDefinition definition = HbrStoryDefinitionStore.Load(DefinitionPath);
            if (Id != null && !string.Equals(HbrStoryDefinition.NormalizeEventId(Id.Entry), definition.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"HBR 剧情事件 {Id.Entry} 与定义 {DefinitionPath} 中的 id {definition.Id} 不一致。");
            }
            return definition;
        }
    }

    public string CurrentStoryPageId => _currentStoryPageId ?? Definition.StartPage;
    internal bool IsActEndingDoor => Definition.Id == ActEndingDoorEventId;
    public HbrStoryPage CurrentStoryPage => Definition.GetPage(CurrentStoryPageId);
    internal bool RepeatMemoryRewardsClaimed { get; private set; }
    public override EventLayoutType LayoutType => EventLayoutType.Custom;
    public override bool IsShared
    {
        get
        {
            HbrStoryDefinition definition = Definition;
            // Map memories use the native per-player option/reward replay.
            // Dedicated transitions and combat still require a party choice.
            return definition.EventPool != HbrMemoryFragmentPoolRules.PoolId ||
                definition.Id == HbrMemoryGardenRestStory.EventId ||
                definition.Id == ActEndingDoorEventId ||
                definition.Pages.Any(page => page.Choices.Any(choice => choice.Combat.Length > 0));
        }
    }
    public override EventAssetProfile AssetProfile => new(LayoutScenePath: ReaderScenePath,
        InitialPortraitPath: Definition.Presentation.Background);
    public override LocString InitialDescription => PageDescription(Definition.StartPage);

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        // The base custom-event loader invents one scene per model ID. Every story
        // uses the shared reader instead, plus the images actually declared in JSON.
        HbrStoryDefinition definition = Definition;
        var paths = new List<string>
        {
            ReaderScenePath,
            ImageHelper.GetImagePath("ui/library/hbr_original_ui_atlas.png"),
            definition.Presentation.Background,
            definition.Presentation.ChoicePortrait,
            definition.Presentation.ThoughtPortrait
        };
        foreach (HbrStoryPage page in definition.Pages)
        {
            paths.Add(page.Background);
            paths.AddRange(page.Characters.Select(character => character.Image));
            foreach (HbrStoryLine line in page.Lines)
            {
                paths.Add(line.Background);
                paths.Add(line.Portrait);
                if (line.Characters != null)
                    paths.AddRange(line.Characters.Select(character => character.Image));
            }
        }
        return paths.Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.Ordinal);
    }

    public override bool IsAllowed(IRunState runState) => IsAllowedForStoryPool(runState, allowReplay: false);

    internal bool IsAllowedForStoryPool(IRunState runState, bool allowReplay) => runState is RunState state
        // Dedicated transitions enter these stories; ordinary event pools cannot.
        && !IsActEndingDoor
        && Definition.Id != HbrMemoryGardenRestStory.EventId
        && Definition.Conditions.IsEligible(state.Players.Select(player => player.Character.GetType().Name),
            state.CurrentActIndex + 1, state.VisitedEventIds.Select(eventId => eventId.Entry), Definition.Id, allowReplay);

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            // Preserve the engine's guard against loading non-ancient events as pre-finished.
            base.SetInitialEventState(isPreFinished);
            return;
        }
        _claimedRewardChoices = new HashSet<string>(StringComparer.Ordinal);
        _memoryEncounteredCharacters = new HashSet<string>(StringComparer.Ordinal);
        RepeatMemoryRewardsClaimed = false;
        _choiceInProgress = false;
        _combatPending = false;
        _lastResumedCombat = null;
        EnterPage(Definition.StartPage);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => CreateOptions(Definition.GetPage(Definition.StartPage));

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _claimedRewardChoices = new HashSet<string>(StringComparer.Ordinal);
        _memoryEncounteredCharacters = new HashSet<string>(StringComparer.Ordinal);
        RepeatMemoryRewardsClaimed = false;
        _currentStoryPageId = null;
        _stateVersion = 0;
        _choiceInProgress = false;
        _combatPending = false;
        _lastResumedCombat = null;
    }

    private IReadOnlyList<EventOption> CreateOptions(HbrStoryPage page)
    {
        int stateVersion = _stateVersion;
        return page.Choices.Select(choice => new EventOption(this,
            () => Choose(page.Id, choice, stateVersion), ModOptionKey(page.Id, choice.Id))).ToArray();
    }

    private void EnterPage(string pageId)
    {
        HbrStoryPage page = Definition.GetPage(pageId);
        _currentStoryPageId = page.Id;
        _stateVersion++;
        if (Definition.EventPool == HbrMemoryFragmentPoolRules.PoolId)
        {
            HbrMemoryGarden.RecordEncounters(Owner, page.MetCharacters);
            _memoryEncounteredCharacters.UnionWith(page.MetCharacters);
        }
        if (page.Choices.Count == 0)
        {
            if (HbrMemoryGarden.NeedsRepeatMemoryRewardStep(Owner, this, _memoryEncounteredCharacters))
                ShowRepeatMemoryRewardOption(page);
            else SetEventFinished(PageDescription(page.Id));
        }
        else
        {
            SetEventState(PageDescription(page.Id), CreateOptions(page));
        }
    }

    private void ShowRepeatMemoryRewardOption(HbrStoryPage page)
    {
        int version = _stateVersion;
        const string key = "HBR_MEMORY_GARDEN.repeatReward";
        SetEventState(PageDescription(page.Id), new[]
        {
            new EventOption(this, () => ClaimRepeatMemoryRewards(page.Id, version),
                new LocString("rest_site_ui", key + ".title"),
                new LocString("rest_site_ui", key + ".description"), key,
                Array.Empty<MegaCrit.Sts2.Core.HoverTips.IHoverTip>())
        });
    }

    private async Task ClaimRepeatMemoryRewards(string pageId, int version)
    {
        if (IsFinished || _choiceInProgress || _combatPending || version != _stateVersion ||
            CurrentStoryPageId != pageId || CurrentStoryPage.Choices.Count != 0) return;
        _choiceInProgress = true;
        try
        {
            if (HbrMemoryGarden.HasRepeatMemoryRewards(Owner, this, _memoryEncounteredCharacters))
                await HbrMemoryGarden.ClaimRepeatMemoryRewards(Owner, this, _memoryEncounteredCharacters);
            // Finish this player's reward step; a prevented reward remains
            // unclaimed and a closed run cancels above.
            RepeatMemoryRewardsClaimed = true;
            SetEventFinished(PageDescription(pageId));
        }
        finally { _choiceInProgress = false; }
    }

    private async Task Choose(string pageId, HbrStoryChoice choice, int stateVersion)
    {
        // Old/repeated button callbacks cannot change a later page or apply another reward.
        if (IsFinished || _choiceInProgress || _combatPending || stateVersion != _stateVersion || CurrentStoryPageId != pageId) return;
        string preparationId = $"{Definition.Id}/{pageId}/{choice.Id}/{stateVersion}";
        _choiceInProgress = true;
        try
        {
            string rewardKey = $"{pageId}/{choice.Id}";
            _claimedRewardChoices ??= new HashSet<string>(StringComparer.Ordinal);
            if (_claimedRewardChoices.Add(rewardKey))
                await ApplyRewards(choice.Rewards, canSkipUpgrades: choice.Combat == "nararaShibaBoss");
            if (choice.Combat.Length == 0) EnterPage(choice.NextPage);
            else
            {
                // A deliberate graph loop can enter combat again; its previously claimed rewards cannot repeat.
                var encounter = (NararaShibaBoss)ModelDb.Encounter<NararaShibaBoss>().ToMutable();
                encounter.StoryState = new HbrStoryCombatState
                {
                    EventId = Definition.Id, NextPage = choice.NextPage, ClaimedChoices = _claimedRewardChoices.Order(StringComparer.Ordinal).ToArray(),
                    MemoryEncounteredCharacters = _memoryEncounteredCharacters.Order(StringComparer.Ordinal).ToArray()
                };
                encounter.StoryState.ValidateFor(Definition);
                encounter.InitialFloor = await NararaShibaEventPreparation.Ready(Owner, preparationId);
                _combatPending = true;
                _stateVersion++;
                try { EnterCombatWithoutExitingEvent(encounter, Array.Empty<Reward>(), shouldResumeAfterCombat: true); }
                catch
                {
                    _combatPending = false;
                    EnterPage(pageId);
                    throw;
                }
            }
        }
        catch (Exception exception)
        {
            if (choice.Combat.Length != 0) NararaShibaEventPreparation.Fail(Owner, preparationId, exception);
            throw;
        }
        finally
        {
            _choiceInProgress = false;
        }
    }

    public override Task Resume(AbstractRoom exitedRoom)
    {
        if (exitedRoom is not CombatRoom { Encounter: NararaShibaBoss encounter } || encounter.StoryState == null)
            throw new InvalidOperationException("剧情战斗缺少续页信息，无法安全恢复剧情。");
        if (ReferenceEquals(_lastResumedCombat, encounter)) return Task.CompletedTask;
        HbrStoryCombatState state = encounter.StoryState;
        state.ValidateFor(Definition);
        _claimedRewardChoices = state.ClaimedChoices.ToHashSet(StringComparer.Ordinal);
        _memoryEncounteredCharacters.UnionWith(state.MemoryEncounteredCharacters);
        _lastResumedCombat = encounter;
        _combatPending = false;
        _choiceInProgress = false;
        // Earlier door-event combat saves shared the leave page with the victory
        // branch. Resume those fights at the new victory dialogue as well.
        string nextPage = IsActEndingDoor && state.NextPage == "SCENE" &&
            Definition.Pages.Any(page => page.Id == "AFTER_BATTLE")
                ? "AFTER_BATTLE"
                : state.NextPage;
        EnterPage(nextPage);
        return Task.CompletedTask;
    }

    private async Task ApplyRewards(HbrStoryRewards rewards, bool canSkipUpgrades)
    {
        ElectronicSoldierManual manual = Owner.GetRelic<ElectronicSoldierManual>();
        if (manual != null && rewards.SenseAmount > 0)
        {
            SixSenseAttribute attribute = Enum.Parse<SixSenseAttribute>(rewards.Sense, ignoreCase: false);
            // The synchronized event choice already runs on each peer. Apply directly,
            // without dispatching another network action or awarding allocation points.
            for (int index = 0; index < rewards.SenseAmount; index++) await manual.ApplySenseIncrease(attribute);
        }

        var offered = new List<Reward>();
        if (rewards.CardChoices > 0)
        {
            bool IsEligible(CardModel card) => HbrgoSaki31OCardFilter.IsAvailableTo(Owner.Character, card)
                && (rewards.CardRarity != "rare" || card.Rarity == CardRarity.Rare);
            CardCreationOptions options = rewards.CardRarity == "rare"
                ? CardCreationOptions.ForNonCombatWithUniformOdds(new[] { Owner.Character.CardPool }, IsEligible).WithFlags(CardCreationFlags.NoRarityModification)
                : CardCreationOptions.ForNonCombatWithDefaultOdds(new[] { Owner.Character.CardPool }, IsEligible);
            offered.Add(new CardReward(options, rewards.CardChoices, Owner));
        }
        for (int index = 0; index < rewards.Potions; index++) offered.Add(new PotionReward(Owner));
        for (int index = 0; index < rewards.CardRemovals; index++) offered.Add(new CardRemovalReward(Owner));
        if (offered.Count > 0) await RewardsCmd.OfferCustom(Owner, offered);
        for (int index = 0; index < rewards.CardUpgrades; index++)
        {
            if (!PileType.Deck.GetPile(Owner).Cards.Any(HbrMemoryGardenCardCatalog.IsOrdinaryUpgradeCandidate)) break;
            CardModel selected = (await CardSelectCmd.FromDeckForUpgrade(Owner,
                new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, canSkipUpgrades ? 0 : 1, 1)
                {
                    Cancelable = canSkipUpgrades,
                    RequireManualConfirmation = canSkipUpgrades
                })).FirstOrDefault();
            if (selected == null)
            {
                if (canSkipUpgrades) continue;
                break;
            }
            CardCmd.Upgrade(selected);
        }
        if (rewards.HealFull) await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp);
        if (rewards.MaxDp > 0) Owner.GetRelic<DeflectorProtection>()?.GainMaxDp(
            HbrResourceValueHelper.FixedInt(Owner.Creature, rewards.MaxDp));
    }
}
