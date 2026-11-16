global using DirectoryInfo = System.IO.DirectoryInfo;
global using DirectoryNotFoundException = System.IO.DirectoryNotFoundException;
global using File = System.IO.File;
global using Path = System.IO.Path;
global using Directory = System.IO.Directory;
global using FileNotFoundException = System.IO.FileNotFoundException;
// Source-development harness only. The production NLibraryEvent owns the real
// UI fields and PaintLine; these terminals are replaced by explicit Harmony
// leaves in Native.cs. Never included by the main project.
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Story;
namespace HBRGOSakiMod.Core.Nodes.Events;
public partial class NLibraryEvent : Control
{
    private readonly List<DialoguePage> _pages=new();
    private int _pageIndex;
    private Control _stageCharacters,_canvas,_continueMarker;
    private TextureRect _background;
    private Button _advanceButton;
    private Label _dialogue;
    private HbrMemoryUpgradeStorySegment _upgradeMemory;
    private MegaCrit.Sts2.Core.Models.EventModel _event;
    private bool _dispatchPending;
    private readonly List<Button> _choiceButtons=new();
    private void ConfigureFocusNavigation(){}
    private bool IsReaderActive()=>true;
    private void SelectOption(int index){}
    private HbrStoryPresentation ReaderPresentation=>_upgradeMemory.EffectivePresentation;
    private string StoryBackgroundPath=>ReaderPresentation.Background;
    private void PaintLine(DialoguePage page){}
    private static Label MakeLabel(string text,int size)=>new(){Text=text};
    private sealed record DialoguePage(string Text,bool Thought,string Speaker,string Portrait,HbrPortraitLayout PortraitLayout,IReadOnlyList<HbrStoryCharacter> Characters,string Background,int SourceLineIndex=-1,HbrStoryLine SourceLine=null);
}
