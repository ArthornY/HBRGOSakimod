using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Nodes.Events;

public partial class NLibraryEvent
{
    private HbrMemoryUpgradeStorySegment _upgradeMemory;
    private Player _upgradeMemoryOwner;
    private Action<HbrMemoryUpgradeStoryPlaybackResult> _upgradeMemoryCompleted;
    private bool _upgradeMemoryClosed;
    private Button _upgradeMemorySkip;
    private bool IsUpgradeMemory => _upgradeMemory != null;
    private Player ReaderPlayer => _upgradeMemoryOwner ?? _event?.Owner;
    private HbrStoryPresentation ReaderPresentation => _upgradeMemory?.EffectivePresentation ?? StoryEvent?.Definition.Presentation;

    internal void InitializeUpgradeMemory(Player owner, HbrMemoryUpgradeStorySegment segment,
        Action<HbrMemoryUpgradeStoryPlaybackResult> completed)
    {
        _upgradeMemoryOwner = owner;
        _upgradeMemory = segment;
        _upgradeMemoryCompleted = completed;
    }
    private void RefreshUpgradeMemory()
    {
        _choiceCanvas.Hide();
        _pages.Clear(); _pageIndex = 0; _choicePageIndex = 0;
        _showingChoices = _dispatchPending = false;
        HbrStoryPage page = _upgradeMemory.Page;
        PrepareDirectionPage(page);
        for (int lineIndex = 0; lineIndex < page.Lines.Count; lineIndex++)
        {
            HbrStoryLine line = page.Lines[lineIndex];
            string portrait = !string.IsNullOrEmpty(line.Portrait) ? line.Portrait
                : line.Kind == "thought" ? StoryThoughtPortraitPath : string.Empty;
            string text = line.Text.Replace("\r", string.Empty);
            string[] paragraphs = UseOriginalDialogue ? new[] { text }
                : text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (string paragraph in paragraphs)
                PaginateParagraph(paragraph, line.Kind == "thought", line.Speaker, portrait,
                    line.PortraitLayout ?? ReaderPresentation.PortraitLayout,
                    page.ResolveCharacters(lineIndex), page.ResolveBackground(ReaderPresentation, lineIndex), lineIndex, line);
        }
        _upgradeMemorySkip = MakeButton(Ui("SKIP_UPGRADE_MEMORY", "跳过"), new Rect2(1380, 28, 180, 56),
            () => CompleteUpgradeMemory(HbrMemoryUpgradeStoryPlaybackResult.Skipped));
        _canvas.AddChild(_upgradeMemorySkip);
        if (_pages.Count == 0) CompleteUpgradeMemory(HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
        else { ShowLine(); ConfigureFocusNavigation(); FocusDefault(); }
    }
    internal void CompleteUpgradeMemory(HbrMemoryUpgradeStoryPlaybackResult result)
    {
        if (!IsUpgradeMemory || _upgradeMemoryClosed) return;
        _upgradeMemoryClosed = true;
        if(result==HbrMemoryUpgradeStoryPlaybackResult.Abandoned){DisposeDirections();_upgradeMemoryCompleted?.Invoke(result);}
        else ObserveDirection(FinishUpgradeMemoryAsync(result));
    }
    private async System.Threading.Tasks.Task FinishUpgradeMemoryAsync(HbrMemoryUpgradeStoryPlaybackResult result)
    {
        try{await CloseStoryAudio();}finally{_upgradeMemoryCompleted?.Invoke(result);}
    }
}
