using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Nodes.Events;

/// <summary>
/// The library's local visual-novel reader. Only actual event choices are synchronized;
/// reading speed and going back a line never change the event or its rewards.
/// </summary>
public partial class NLibraryEvent : Control, ICustomEventNode
{
    public const string ScenePath = "res://scenes/events/custom/library_hbrgo_saki.tscn";
    public const string BackgroundPath = "events/hbrgo_saki_mod_event_library_hbrgo_saki.png";
    private const float DesignWidth = 1600f;
    private const float DesignHeight = 900f;
    private const float OriginalDialogueTop = 625f;
    private const float OriginalDialogueLineHeight = 60f;
    private static readonly Color Accent = new(0.99f, 0.72f, 0.99f);
    private static readonly Color Ink = new(0.035f, 0.025f, 0.05f, 0.78f);

    private readonly List<DialoguePage> _pages = new();
    private readonly List<Button> _choiceButtons = new();
    private readonly List<(Button Button, NinePatchRect Pill)> _choicePills = new();
    private readonly LibraryHudVisibility _hud = new();
    private EventModel _event;
    private Control _canvas;
    private Control _stageCharacters;
    private IReadOnlyList<HbrStoryCharacter> _displayedCharacters;
    private Control _choiceCanvas;
    private Control _nameplate;
    private Panel _nameplateBackground;
    private Control _storyPortrait;
    private ColorRect _dialogueShade;
    private ShaderMaterial _backgroundMaterial;
    private TextureRect _background;
    private string _displayedBackground;
    private TextureRect _choicePortrait;
    private TextureRect _storyPortraitImage;
    private Panel _storyPortraitRing;
    private int _choicePageIndex;
    private Label _dialogue;
    private Label _speaker;
    private Control _continueMarker;
    private Button _advanceButton;
    private Texture2D _pillTexture;
    private Texture2D _selectedPillTexture;
    private Texture2D _uiAtlas;
    private int _pageIndex;
    private double _visibleCharacters;
    private double _readDelay;
    private double _pulseTime;
    private bool _showingChoices;
    private bool _dispatchPending;
    private bool _initialized;

    public IScreenContext CurrentScreenContext => this;
    private HbrStoryEvent StoryEvent => _event as HbrStoryEvent;
    private bool UseOriginalDialogue => ReaderPresentation?.DialogueStyle == "original";
    private int DialogueFontSize => UseOriginalDialogue ? 44 : 40;
    private string StoryBackgroundPath => ReaderPresentation?.Background
        ?? ImageHelper.GetImagePath(BackgroundPath);
    private string StoryChoicePortraitPath => ReaderPresentation?.ChoicePortrait
        ?? ImageHelper.GetImagePath("characters/hbrgo_saki_normal.png");
    private string StoryThoughtPortraitPath => ReaderPresentation?.ThoughtPortrait
        ?? ImageHelper.GetImagePath("ui/top_panel/character_icon_hbrgo_saki.png");
    public Control DefaultFocusedControl => _showingChoices
        ? _choiceButtons.FirstOrDefault(button => !button.Disabled) ?? (Control)_advanceButton
        : _advanceButton;

    public void Initialize(EventModel eventModel)
    {
        _event = eventModel;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();
        _initialized = true;
        ResizeCanvas();
        if (_event != null) _event.StateChanged += RefreshEventState;
        ActiveScreenContext.Instance.Updated += OnActiveScreenUpdated;
        if (IsUpgradeMemory) RefreshUpgradeMemory();
        else RefreshEventState(_event);
        _hud.SetActive(IsReaderActive());
    }

    public override void _ExitTree()
    {
        if (_event != null)
        {
            _event.StateChanged -= RefreshEventState;
        }
        ActiveScreenContext.Instance.Updated -= OnActiveScreenUpdated;
        _hud.Dispose();
        DisposeDirections();
        if (IsUpgradeMemory) CompleteUpgradeMemory(HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && _initialized)
        {
            ResizeCanvas();
        }
    }

    public override void _Process(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        if (IsUpgradeMemory && !HbrMemoryUpgradeStoryPresentation.ObserveReader(this)) return;
        bool readerActive = IsReaderActive() && DisplayServer.WindowIsFocused();
        ProcessDirections(delta, readerActive, readerActive && Input.IsKeyPressed(Key.Ctrl));
        UpdateChoiceHighlight(readerActive && _showingChoices && !_dispatchPending);
        if (!readerActive) return;

        _pulseTime += delta;
        _continueMarker.Modulate = new Color(1f, 1f, 1f,
            0.65f + 0.35f * (float)Math.Sin(_pulseTime * 3.8));
        if (_dispatchPending || _showingChoices || _pages.Count == 0 || _directionBusy)
        {
            return;
        }

        // Poll the held key instead of toggling a mode, so releasing Ctrl or
        // returning from another window cannot leave fast-forward switched on.
        bool fastForward = Input.IsKeyPressed(Key.Ctrl);
        if (!IsLineComplete())
        {
            _visibleCharacters += delta * (fastForward ? 1600 : 42);
            _dialogue.VisibleCharacters = Math.Min(
                (int)_visibleCharacters, _pages[_pageIndex].Text.Length);
            _continueMarker.Visible = !UseOriginalDialogue && IsLineComplete();
            return;
        }

        _readDelay += delta;
        if (fastForward && _readDelay >= 0.08)
        {
            AdvanceStory();
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!_initialized || !IsReaderActive() || inputEvent.IsEcho() || !inputEvent.IsPressed())
        {
            return;
        }
        if (inputEvent.IsActionPressed(MegaInput.pauseAndBack)
            && LibraryHudVisibility.OpenPause(ReaderPlayer?.RunState))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_directionBusy && inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            // A local skip button keeps its normal GUI path; other clicks only
            // satisfy this direction's tap gate and cannot advance the text too.
            if (_upgradeMemorySkip is { Disabled: false } && _upgradeMemorySkip.GetGlobalRect().HasPoint(mouse.Position)) return;
            SignalDirectionAdvance(); GetViewport().SetInputAsHandled(); return;
        }
        // Native confirmation was renamed in beta. Route the active game's
        // confirmation/select actions once before Godot Button GUI handling.
        if (LibraryInputCompatibility.IsAdvancePressed(inputEvent))
        {
            Control focused = GetViewport().GuiGetFocusOwner();
            if (!ReferenceEquals(focused, _upgradeMemorySkip) && SignalDirectionAdvance()) { GetViewport().SetInputAsHandled(); return; }
            if (focused is Button button && IsAncestorOf(button))
            {
                if (!button.Disabled)
                {
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                }
            }
            else if (!_showingChoices)
            {
                AdvanceStory();
            }
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent is InputEventKey key)
        {
            switch (key.Keycode)
            {
                case Key.Backspace:
                    PreviousLine();
                    break;
                case Key.Ctrl:
                    break;
                default:
                    return;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        _background = new()
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_background);
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ShowBackground(StoryBackgroundPath);
        _backgroundMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = "shader_type canvas_item; uniform float blur_amount = 0.0; void fragment() { vec2 d = TEXTURE_PIXEL_SIZE * blur_amount; vec4 c = texture(TEXTURE, UV) * 0.25; c += texture(TEXTURE, UV + vec2(d.x, 0.0)) * 0.125; c += texture(TEXTURE, UV - vec2(d.x, 0.0)) * 0.125; c += texture(TEXTURE, UV + vec2(0.0, d.y)) * 0.125; c += texture(TEXTURE, UV - vec2(0.0, d.y)) * 0.125; c += texture(TEXTURE, UV + d) * 0.0625; c += texture(TEXTURE, UV - d) * 0.0625; c += texture(TEXTURE, UV + vec2(d.x, -d.y)) * 0.0625; c += texture(TEXTURE, UV + vec2(-d.x, d.y)) * 0.0625; COLOR = c; }"
            }
        };
        _background.Material = _backgroundMaterial;

        _stageCharacters = new Control { Name = "StageCharacters", MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        AddChild(_stageCharacters);
        _stageCharacters.Size = new Vector2(DesignWidth, DesignHeight);

        _dialogueShade = new ColorRect
        {
            Color = UseOriginalDialogue ? new Color(0, 0, 0, 0.68f) : Ink,
            AnchorTop = UseOriginalDialogue ? 0 : 0.692f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_dialogueShade);

        _canvas = new Control { Name = "HbrLibraryCanvas", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        _canvas.Size = new Vector2(DesignWidth, DesignHeight);
        _uiAtlas = PreloadManager.Cache.GetTexture2D(
            ImageHelper.GetImagePath("ui/library/hbr_original_ui_atlas.png"));
        _pillTexture = AtlasSlice(new Rect2(1088, 1310, 200, 88));
        _selectedPillTexture = AtlasSlice(new Rect2(1088, 1398, 200, 88));

        _choiceCanvas = new Control { Name = "ThoughtChoices", MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_choiceCanvas);
        _choiceCanvas.Size = _canvas.Size;
        _choiceCanvas.Hide();

        _advanceButton = MakeButton(string.Empty, UseOriginalDialogue
            ? new Rect2(0, OriginalDialogueTop, DesignWidth, DesignHeight - OriginalDialogueTop)
            : new Rect2(0, 623, 1600, 277), AdvanceStory);
        TransparentStyles(_advanceButton);
        _advanceButton.TooltipText = Ui("NEXT", "继续");
        _canvas.AddChild(_advanceButton);

        _nameplate = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_nameplate);
        _storyPortrait = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_storyPortrait);
        _storyPortraitImage = AddPortrait(_storyPortrait, new Rect2(0, 0, 184, 184));
        _storyPortraitRing = _storyPortrait.GetChild<Panel>(0);
        BuildNameplate();

        _dialogue = MakeLabel(string.Empty, DialogueFontSize);
        _dialogue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _dialogue.VerticalAlignment = VerticalAlignment.Top;
        _dialogue.AddThemeConstantOverride("line_spacing", UseOriginalDialogue
            ? (int)Math.Round(OriginalDialogueLineHeight - _dialogue.GetThemeFont("font").GetHeight(DialogueFontSize)) : 8);
        if (UseOriginalDialogue) ApplyOriginalTextStyle(_dialogue);
        Put(_dialogue, _canvas, UseOriginalDialogue
            ? new Rect2(230, 680, 1240, 174) : new Rect2(278, 671, 1150, 151));
        _continueMarker = new TextureRect
        {
            Texture = AtlasSlice(new Rect2(467, 950, 30, 28)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Put(_continueMarker, _canvas, new Rect2(1366, 833, 36, 34));
        BuildDirectionUi();

    }

    private void BuildNameplate()
    {
        if (UseOriginalDialogue)
        {
            AddLine(_nameplate, new Vector2(93, 549), new Vector2(93, 605), Colors.Black, 3);
            AddDot(_nameplate, new Vector2(93, 538), 7, Colors.Black);
            AddDot(_nameplate, new Vector2(93, 616), 7, Colors.Black);
        }
        else
        {
            AddLine(_nameplate, new Vector2(88, 559), new Vector2(321, 559), Colors.White, 1.2f);
            AddDot(_nameplate, new Vector2(88, 559), 4, Colors.White);
        }
        _nameplateBackground = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _nameplateBackground.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, UseOriginalDialogue ? 0.60f : 0.48f)
        });
        Put(_nameplateBackground, _nameplate, UseOriginalDialogue
            ? new Rect2(114, 540, 225, 74) : new Rect2(100, 560, 220, 48));
        _speaker = MakeLabel(Ui("SAKIKO", "丰川祥子"), UseOriginalDialogue ? 36 : 26);
        if (UseOriginalDialogue)
        {
            ApplyOriginalTextStyle(_speaker);
            _speaker.ClipText = true;
            _speaker.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        Put(_speaker, _nameplate, UseOriginalDialogue
            ? new Rect2(143, 540, 169, 74) : new Rect2(116, 560, 210, 48));
    }

    private static void ApplyOriginalTextStyle(Label label)
    {
        label.AddThemeColorOverride("font_shadow_color", new Color(0.16f, 0.16f, 0.16f, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.AddThemeColorOverride("font_outline_color", new Color(0.55f, 0.55f, 0.55f, 0.65f));
        label.AddThemeConstantOverride("outline_size", 1);
    }

    private void ResizeCanvas()
    {
        float scale = Math.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        _canvas.Scale = Vector2.One * scale;
        // Bottom align the reader so text stays inside the full-width dialogue band
        // at 4:3 and ultra-wide sizes as well as 16:9.
        _canvas.Position = new Vector2((Size.X - DesignWidth * scale) / 2f, Size.Y - DesignHeight * scale);
        _stageCharacters.Scale = _canvas.Scale;
        _stageCharacters.Position = _canvas.Position;
        if (UseOriginalDialogue)
        {
            // Keep the full-width band aligned with the bottom-aligned design canvas at every aspect ratio.
            _dialogueShade.OffsetTop = _canvas.Position.Y + OriginalDialogueTop * scale;
        }
        UpdatePortraitExtent();
        ResizeDirections();
    }

    private void RefreshEventState(EventModel eventModel)
    {
        if (_showingChoices && eventModel is HbrStoryEvent { IsFinished: true, RepeatMemoryRewardsClaimed: true })
        {
            // The reward closes on the same page; do not replay its dialogue.
            _dispatchPending = false;
            ShowChoices();
            return;
        }
        _dispatchPending = false;
        _showingChoices = false;
        _choiceCanvas.Hide();
        _pageIndex = 0;
        _choicePageIndex = 0;
        _pages.Clear();
        if (eventModel is HbrStoryEvent story)
        {
            PrepareDirectionPage(story.CurrentStoryPage);
            for (int lineIndex = 0; lineIndex < story.CurrentStoryPage.Lines.Count; lineIndex++)
            {
                HbrStoryLine line = story.CurrentStoryPage.Lines[lineIndex];
                string portrait = !string.IsNullOrEmpty(line.Portrait) ? line.Portrait
                    : line.Kind == "thought" ? StoryThoughtPortraitPath : string.Empty;
                string text = line.Text.Replace("\r", string.Empty);
                // Original-style dialogue preserves the editor's deliberate line breaks inside one frame.
                string[] paragraphs = UseOriginalDialogue ? new[] { text }
                    : text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (string paragraph in paragraphs)
                {
                    PaginateParagraph(paragraph, line.Kind == "thought", line.Speaker, portrait,
                        line.PortraitLayout ?? story.Definition.Presentation.PortraitLayout,
                        story.CurrentStoryPage.ResolveCharacters(lineIndex),
                        story.CurrentStoryPage.ResolveBackground(story.Definition.Presentation, lineIndex), lineIndex, line);
                }
            }
        }
        else
        {
            string text = PlainText(eventModel.Description?.GetFormattedText() ?? string.Empty);
            foreach (string paragraph in text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                bool thought = paragraph.TrimStart().StartsWith('（') || paragraph.TrimStart().StartsWith('(');
                PaginateParagraph(paragraph.Trim(), thought, string.Empty, thought ? StoryThoughtPortraitPath : string.Empty,
                    new HbrPortraitLayout(), Array.Empty<HbrStoryCharacter>(), StoryBackgroundPath);
            }
        }
        ConfigureFocusNavigation();
        if (_pages.Count == 0)
        {
            ShowChoices();
        }
        else
        {
            ShowLine();
        }
        FocusDefault();
    }

    private void PaginateParagraph(string paragraph, bool thought, string speaker, string portrait,
        HbrPortraitLayout portraitLayout, IReadOnlyList<HbrStoryCharacter> characters, string background, int sourceLineIndex = -1, HbrStoryLine sourceLine = null)
    {
        Font font = _dialogue.GetThemeFont("font");
        int[] boundaries = StringInfo.ParseCombiningCharacters(paragraph);
        int start = 0;
        while (start < boundaries.Length)
        {
            // A separator that caused the page break must not add a blank first row to the next frame.
            if (start > 0)
            {
                while (start < boundaries.Length && paragraph[boundaries[start]] == '\n') start++;
                if (start >= boundaries.Length) break;
            }
            int low = start + 1;
            int high = boundaries.Length;
            int best = low;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                int endOffset = mid < boundaries.Length ? boundaries[mid] : paragraph.Length;
                string candidate = paragraph[boundaries[start]..endOffset];
                // The classic reader retains its existing pagination; the original style measures its actual spacing.
                if (FitsDialogueFrame(candidate, font))
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            int end = best < boundaries.Length ? boundaries[best] : paragraph.Length;
            _pages.Add(new DialoguePage(paragraph[boundaries[start]..end], thought, speaker, portrait, portraitLayout, characters, background, sourceLineIndex, sourceLine));
            start = best;
        }
    }

    private bool FitsDialogueFrame(string text, Font font)
    {
        const TextServer.LineBreakFlag breakFlags = TextServer.LineBreakFlag.Mandatory
            | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive;
        if (!UseOriginalDialogue)
        {
            return font.GetMultilineStringSize(text, HorizontalAlignment.Left, _dialogue.Size.X,
                DialogueFontSize, -1, breakFlags).Y <= _dialogue.Size.Y - 24;
        }
        using TextParagraph paragraph = new()
        {
            Width = _dialogue.Size.X,
            BreakFlags = breakFlags,
            LineSpacing = _dialogue.GetThemeConstant("line_spacing")
        };
        paragraph.AddString(text, font, DialogueFontSize);
        // Match the editor's two-row original-game dialogue frame, including explicit line breaks.
        return paragraph.GetLineCount() <= 2 && paragraph.GetSize().Y <= _dialogue.Size.Y;
    }

    private void PaintLine(DialoguePage page)
    {
        _dialogue.Text = page.Text;
        _dialogue.VisibleCharacters = 0;
        _visibleCharacters = 0;
        _readDelay = 0;
        _continueMarker.Hide();
        SetChoicePresentation(false);
        if (_directionStage == null) { ShowBackground(page.Background); ShowStageCharacters(page.Characters); }
        _storyPortrait.Visible = !string.IsNullOrEmpty(page.Portrait);
        if (_storyPortrait.Visible)
        {
            _storyPortraitImage.Texture = PreloadManager.Cache.GetTexture2D(page.Portrait);
            ApplyPortraitLayout(page.PortraitLayout, page.Portrait);
        }
        _nameplate.Visible = !string.IsNullOrEmpty(page.Speaker);
        _speaker.Text = page.Speaker;
        if (UseOriginalDialogue)
        {
            float width = Math.Clamp(_speaker.GetThemeFont("font").GetStringSize(page.Speaker,
                HorizontalAlignment.Left, -1, 36).X + 56, 225, 700);
            _nameplateBackground.Size = new Vector2(width, 74);
            _speaker.Size = new Vector2(width - 56, 74);
            _speaker.TooltipText = page.Speaker;
        }
        _advanceButton.Disabled = false;
        ConfigureFocusNavigation();
    }

    private bool IsLineComplete() => _dialogue.VisibleCharacters < 0
        || _dialogue.VisibleCharacters >= _pages[_pageIndex].Text.Length;

    private void AdvanceStory()
    {
        if (SignalDirectionAdvance()) return;
        if (_dispatchPending || _showingChoices || !IsReaderActive() || _pages.Count == 0)
        {
            return;
        }
        if (!IsLineComplete())
        {
            _dialogue.VisibleCharacters = -1;
            _continueMarker.Visible = !UseOriginalDialogue;
            _readDelay = 0;
            return;
        }
        if (_pageIndex + 1 < _pages.Count)
        {
            _pageIndex++;
            ShowLine();
        }
        else
        {
            ShowChoices();
        }
    }

    private void PreviousLine()
    {
        if (_dispatchPending || !IsReaderActive() || _pages.Count == 0)
        {
            return;
        }
        if (_showingChoices)
        {
            _showingChoices = false;
            _choiceCanvas.Hide();
        }
        else if (_pageIndex > 0)
        {
            _pageIndex--;
        }
        _restoreDirectionRequested = true;
        ShowLine();
        _dialogue.VisibleCharacters = -1;
        _continueMarker.Visible = !UseOriginalDialogue;
        FocusDefault();
    }

    private void ShowChoices()
    {
        if (IsUpgradeMemory)
        {
            CompleteUpgradeMemory(HbrMemoryUpgradeStoryPlaybackResult.Completed);
            return;
        }
        _showingChoices = true;
        _advanceButton.Disabled = true;
        _continueMarker.Hide();
        _choicePills.Clear();
        foreach (Node child in _choiceCanvas.GetChildren())
        {
            _choiceCanvas.RemoveChild(child);
            child.QueueFree();
        }
        _choiceButtons.Clear();
        ShowBackground(StoryEvent?.CurrentStoryPage.ResolveBackground(StoryEvent.Definition.Presentation,
            StoryEvent.CurrentStoryPage.Lines.Count - 1) ?? StoryBackgroundPath);
        ShowStageCharacters(StoryEvent?.CurrentStoryPage.ResolveCharacters(StoryEvent.CurrentStoryPage.Lines.Count - 1)
            ?? Array.Empty<HbrStoryCharacter>());
        _choicePortrait = null;
        if (TryShowCombinationChoices())
        {
            SetChoicePresentation(true);
            _choiceCanvas.Show();
            ConfigureFocusNavigation();
            FocusDefault();
            return;
        }
        int count = _event.IsFinished ? 1 : _event.CurrentOptions.Count;
        string choicePrompt = StoryEvent?.CurrentStoryPage.ChoicePrompt ?? Ui("CHOOSE", "请选择");
        if (!_event.IsFinished && count > 0 && !string.IsNullOrWhiteSpace(choicePrompt))
        {
            AddChoiceHeading(choicePrompt);
        }
        AddThoughtPortrait();
        int pageCount = Math.Max(1, (count + 2) / 3);
        _choicePageIndex = Math.Clamp(_choicePageIndex, 0, pageCount - 1);
        int startIndex = _choicePageIndex * 3;
        int visibleCount = Math.Min(3, count - startIndex);
        for (int slot = 0; slot < visibleCount; slot++)
        {
            int i = startIndex + slot;
            int index = i;
            string title = _event.IsFinished
                ? Ui("PROCEED", "返回塔内")
                : StoryEvent != null && i < StoryEvent.CurrentStoryPage.Choices.Count ? StoryEvent.ChoiceTitle(StoryEvent.CurrentStoryPage.Choices[i])
                : PlainText(_event.CurrentOptions[i].Title?.GetFormattedText() ?? string.Empty);
            string reward = _event.IsFinished
                ? string.Empty
                : StoryEvent != null && i < StoryEvent.CurrentStoryPage.Choices.Count ? StoryChoiceDescription(StoryEvent.CurrentStoryPage.Choices[i])
                : PlainText(_event.CurrentOptions[i].Description?.GetFormattedText() ?? string.Empty);
            Rect2 bounds = visibleCount == 1
                ? new Rect2(532, 453, 1063, 149)
                : visibleCount == 2 ? new Rect2(527, 330 + slot * 225, 1063, 149)
                : slot switch
                {
                    0 => new Rect2(527, 245, 1063, 149),
                    1 => new Rect2(532, 453, 1063, 149),
                    _ => new Rect2(486, 655, 1063, 149)
                };
            AddChoice(index, bounds, title, reward);
        }
        if (pageCount > 1)
        {
            AddChoicePager(pageCount);
        }
        SetChoicePresentation(true);
        _choiceCanvas.Show();
        ConfigureFocusNavigation();
        FocusDefault();
    }

    private string StoryChoiceDescription(HbrStoryChoice choice)
    {
        var names = HbrStoryChoicePreview.GetEncounterNames(StoryEvent.Definition, StoryEvent.CurrentStoryPage, choice);
        string description = StoryEvent.ChoiceDescription(choice);
        if (names.Count == 0)
        {
            return description;
        }

        string encounters = Ui("ENCOUNTER_CHARACTERS", "登场角色") + "：" + string.Join("、", names);
        return string.IsNullOrWhiteSpace(description)
            ? encounters
            : description + "\n" + encounters;
    }

    private void AddThoughtPortrait()
    {
        if (string.IsNullOrEmpty(StoryChoicePortraitPath))
        {
            _choicePortrait = null;
            return;
        }
        _choicePortrait = new TextureRect
        {
            Texture = PreloadManager.Cache.GetTexture2D(StoryChoicePortraitPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = "shader_type canvas_item; uniform float left_extension = 0.0; uniform bool original_portrait = false; uniform vec2 image_scale = vec2(1.0); void fragment() { vec2 p = vec2(UV.x * (535.0 + left_extension) - left_extension, UV.y * 900.0); float edge = pow(max(p.x, 0.0) / 507.4, 2.0) + pow((p.y - 437.4) / 670.7, 2.0); vec2 portrait_uv = vec2(p.x / 535.0, p.y / 900.0); if (original_portrait) { portrait_uv = vec2(0.4, 0.02) + portrait_uv * vec2(0.31, 0.282); } else { portrait_uv = (vec2(1.0) - image_scale) * 0.5 + portrait_uv * image_scale; } vec4 c = texture(TEXTURE, portrait_uv); c.a *= 1.0 - smoothstep(0.994, 1.0, edge); COLOR = c; }"
                }
            }
        };
        ((ShaderMaterial)_choicePortrait.Material).SetShaderParameter("original_portrait",
            StoryChoicePortraitPath.EndsWith("/hbrgo_saki_normal.png", StringComparison.Ordinal));
        float sourceAspect = (float)_choicePortrait.Texture.GetWidth() / _choicePortrait.Texture.GetHeight();
        float targetAspect = 535f / 900f;
        ((ShaderMaterial)_choicePortrait.Material).SetShaderParameter("image_scale",
            sourceAspect > targetAspect ? new Vector2(targetAspect / sourceAspect, 1) : new Vector2(1, sourceAspect / targetAspect));
        Put(_choicePortrait, _choiceCanvas, new Rect2(0, 0, 535, 900));
        UpdatePortraitExtent();
        Line2D arc = new() { Width = 4, DefaultColor = Colors.White, Antialiased = true };
        for (int y = 0; y <= 900; y += 9)
        {
            arc.AddPoint(new Vector2(ThoughtArcX(y), y));
        }
        _choiceCanvas.AddChild(arc);
    }

    private void AddChoicePager(int pageCount)
    {
        Label position = MakeLabel($"{_choicePageIndex + 1} / {pageCount}", 26);
        position.HorizontalAlignment = HorizontalAlignment.Center;
        Put(position, _choiceCanvas, new Rect2(1110, 831, 250, 52));
        AddPagerButton("‹", 965, -1, _choicePageIndex == 0);
        AddPagerButton("›", 1410, 1, _choicePageIndex + 1 >= pageCount);
    }

    private void AddPagerButton(string title, float x, int direction, bool disabled)
    {
        Button button = MakeButton(title, new Rect2(x, 831, 120, 52), () =>
        {
            if (_dispatchPending || !_showingChoices || !IsReaderActive()) return;
            _choicePageIndex += direction;
            ShowChoices();
        });
        button.Disabled = disabled;
        button.AddThemeFontSizeOverride("font_size", 32);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            button.AddThemeStyleboxOverride(state, RoundStyle(new Color(0.1f, 0.05f, 0.15f, 0.8f), Accent));
        }
        _choiceCanvas.AddChild(button);
        _choiceButtons.Add(button);
    }

    private void UpdatePortraitExtent()
    {
        if (_choicePortrait == null || !GodotObject.IsInstanceValid(_choicePortrait)
            || _choicePortrait.IsQueuedForDeletion() || _canvas.Scale.X <= 0)
        {
            return;
        }
        float extension = Math.Max(0, _canvas.Position.X / _canvas.Scale.X);
        _choicePortrait.Position = new Vector2(-extension, 0);
        _choicePortrait.Size = new Vector2(535 + extension, 900);
        ((ShaderMaterial)_choicePortrait.Material).SetShaderParameter("left_extension", extension);
    }

    private void AddChoiceHeading(string prompt)
    {
        Panel heading = new() { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeStyleboxOverride("panel", RoundStyle(Colors.White, Colors.White, 40));
        Label title = MakeLabel(prompt, 29, new Color(0.09f, 0.06f, 0.14f));
        float width = Math.Clamp(title.GetThemeFont("font").GetStringSize(prompt,
            HorizontalAlignment.Left, -1, 29).X + 94, 354, 710);
        Put(heading, _choiceCanvas, new Rect2(355, 55, width, 78));
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        ScrollContainer textScroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Pass,
            TooltipText = prompt
        };
        Put(textScroll, _choiceCanvas, new Rect2(432, 59, width - 94, 70));
        title.CustomMinimumSize = new Vector2(width - 114, 0);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        textScroll.AddChild(title);
    }

    private void SetChoicePresentation(bool choosing)
    {
        _dialogueShade.Visible = !choosing;
        _dialogue.Visible = !choosing;
        _advanceButton.Visible = !choosing;
        if (_directionStage != null) _directionStage.ChoiceBlur(choosing);
        else _backgroundMaterial.SetShaderParameter("blur_amount", choosing ? 8f : 0f);
        if (choosing)
        {
            _nameplate.Hide();
            _storyPortrait.Hide();
        }
    }

    private static float ThoughtArcX(float y) => 507.4f * MathF.Sqrt(
        Math.Max(0, 1 - MathF.Pow((y - 437.4f) / 670.7f, 2)));

    private static TextureRect AddPortrait(Node parent, Rect2 rect)
    {
        Panel ring = new() { MouseFilter = MouseFilterEnum.Ignore };
        ring.AddThemeStyleboxOverride("panel", RoundStyle(new Color(0.09f, 0.06f, 0.14f, 0.66f), Colors.White, (int)(rect.Size.X / 2)));
        Put(ring, parent, rect);
        ShaderMaterial material = new()
        {
            Shader = new Shader
            {
                Code = "shader_type canvas_item; uniform vec2 crop_origin = vec2(0.0); uniform vec2 crop_scale = vec2(1.0); uniform float image_scale = 1.0; uniform vec2 image_offset = vec2(0.0); void fragment() { vec2 local_uv = (UV - vec2(0.5) - image_offset) / image_scale + vec2(0.5); vec2 source_uv = crop_origin + local_uv * crop_scale; float inside = step(0.0, source_uv.x) * step(source_uv.x, 1.0) * step(0.0, source_uv.y) * step(source_uv.y, 1.0); vec4 c = texture(TEXTURE, clamp(source_uv, vec2(0.0), vec2(1.0))); c.a *= inside * (1.0 - smoothstep(0.48, 0.5, distance(UV, vec2(0.5)))); COLOR = c; }"
            }
        };
        TextureRect image = new()
        {
            Material = material,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Put(image, parent, new Rect2(rect.Position + Vector2.One * 5, rect.Size - Vector2.One * 10));
        return image;
    }

    private void ApplyPortraitLayout(HbrPortraitLayout layout, string imagePath)
    {
        _storyPortrait.Position = new Vector2(layout.X, layout.Y);
        _storyPortrait.Size = Vector2.One * layout.Size;
        _storyPortraitRing.Size = Vector2.One * layout.Size;
        _storyPortraitRing.AddThemeStyleboxOverride("panel", RoundStyle(new Color(0.09f, 0.06f, 0.14f, 0.66f),
            Colors.White, (int)(layout.Size / 2)));
        float inset = layout.Size * 5f / 184f;
        float innerSize = layout.Size - inset * 2;
        _storyPortraitImage.Position = Vector2.One * inset;
        _storyPortraitImage.Size = Vector2.One * innerSize;
        Vector2 cropScale = Vector2.One;
        Vector2 cropOrigin = Vector2.Zero;
        if (imagePath.EndsWith("/character_icon_hbrgo_saki.png", StringComparison.Ordinal))
        {
            cropOrigin = new Vector2(0.25f, 0.2333333f);
            cropScale = Vector2.One * 0.75f;
        }
        else
        {
            float aspect = (float)_storyPortraitImage.Texture.GetWidth() / _storyPortraitImage.Texture.GetHeight();
            cropScale = aspect > 1 ? new Vector2(1 / aspect, 1) : new Vector2(1, aspect);
            cropOrigin = (Vector2.One - cropScale) / 2;
        }
        ShaderMaterial material = (ShaderMaterial)_storyPortraitImage.Material;
        material.SetShaderParameter("crop_origin", cropOrigin);
        material.SetShaderParameter("crop_scale", cropScale);
        material.SetShaderParameter("image_scale", layout.ImageScale);
        material.SetShaderParameter("image_offset", new Vector2(layout.ImageOffsetX, layout.ImageOffsetY) / innerSize);
    }

    private void ShowBackground(string path)
    {
        if (_displayedBackground == path) return;
        _background.Texture = PreloadManager.Cache.GetTexture2D(path);
        _displayedBackground = path;
    }

    private void ShowStageCharacters(IReadOnlyList<HbrStoryCharacter> characters)
    {
        if (ReferenceEquals(_displayedCharacters, characters)) return;
        _displayedCharacters = characters;
        foreach (Node child in _stageCharacters.GetChildren())
        {
            _stageCharacters.RemoveChild(child);
            child.QueueFree();
        }
        foreach (HbrStoryCharacter character in characters)
        {
            Texture2D texture = PreloadManager.Cache.GetTexture2D(character.Image);
            float width = character.Height * texture.GetWidth() / texture.GetHeight();
            TextureRect image = new()
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                FlipH = character.FlipX,
                Modulate = new Color(1, 1, 1, character.Opacity),
                MouseFilter = MouseFilterEnum.Ignore
            };
            Put(image, _stageCharacters, new Rect2(character.X - width / 2, character.Y - character.Height, width, character.Height));
        }
    }

    private void AddChoice(int index, Rect2 bounds, string title, string reward)
    {
        float y = bounds.GetCenter().Y;
        if (!string.IsNullOrEmpty(StoryChoicePortraitPath))
        {
            AddDot(_choiceCanvas, new Vector2(ThoughtArcX(y), y), 12.5f, Colors.White);
        }
        Control root = new() { MouseFilter = MouseFilterEnum.Ignore };
        Put(root, _choiceCanvas, bounds);
        // The atlas sprite is 88 px high including glow; its white capsule border
        // spans only y=24..64. Match the reference's 149 px visible capsule height,
        // letting the original glow extend outside the option's content bounds.
        float pillScale = bounds.Size.Y / 40f;
        NinePatchRect pill = new()
        {
            Texture = _pillTexture,
            PatchMarginLeft = 44,
            PatchMarginRight = 44,
            PatchMarginTop = 0,
            PatchMarginBottom = 0,
            Position = Vector2.One * (-24f * pillScale),
            Scale = Vector2.One * pillScale,
            Size = new Vector2(bounds.Size.X / pillScale + 48, 88),
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(pill);
        Button button = MakeButton(string.Empty, new Rect2(Vector2.Zero, bounds.Size), () => SelectOption(index));
        button.TooltipText = string.IsNullOrEmpty(reward) ? title : $"{title}\n{reward}";
        TransparentStyles(button);
        button.Disabled = !_event.IsFinished && _event.CurrentOptions[index].IsLocked;
        root.AddChild(button);
        _choiceButtons.Add(button);
        _choicePills.Add((button, pill));
        Label label = MakeLabel(title, 38);
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ScrollContainer textScroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Pass
        };
        Put(textScroll, button, new Rect2(72, 23, bounds.Size.X - 145, bounds.Size.Y - 46));
        label.CustomMinimumSize = new Vector2(bounds.Size.X - 165, 0);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.SizeFlagsVertical = SizeFlags.ExpandFill;
        textScroll.AddChild(label);
    }

    private void UpdateChoiceHighlight(bool allowHover)
    {
        bool foundHovered = false;
        foreach ((Button button, NinePatchRect pill) in _choicePills)
        {
            // Include the label's scroll area. Child controls and retained keyboard
            // focus must not interrupt or latch the mouse highlight.
            bool hovered = allowHover && !foundHovered && !button.Disabled
                && new Rect2(Vector2.Zero, button.Size).HasPoint(button.GetLocalMousePosition());
            Texture2D texture = hovered ? _selectedPillTexture : _pillTexture;
            if (pill.Texture != texture) pill.Texture = texture;
            foundHovered |= hovered;
        }
    }

    private void SelectOption(int index)
    {
        if (!_showingChoices || _dispatchPending || !IsReaderActive())
        {
            return;
        }
        if (!_event.IsFinished && (index < 0 || index >= _event.CurrentOptions.Count || _event.CurrentOptions[index].IsLocked))
        {
            return;
        }
        _dispatchPending = true;
        foreach (Button button in _choiceButtons)
        {
            button.Disabled = true;
        }
        ConfigureFocusNavigation();
        if (_event.IsFinished)
        {
            if (!_closingStoryAudio && _storyAudio != null) { ObserveDirection(CloseThenProceed(index, _event)); return; }
            if (TryProceedFromRestStory()) return;
            if (_event is HbrStoryEvent { IsActEndingDoor: true }
                && RunManager.Instance.DebugOnlyGetState()?.Act is HbrgoActFour)
            {
                // Keep this reader locked while the synchronized next-act vote is pending.
                RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();
                return;
            }
            TaskHelper.RunSafely(NEventRoom.Proceed());
            // Reopening the event from the map must retain a usable Proceed button.
            _dispatchPending = false;
            foreach (Button button in _choiceButtons)
            {
                button.Disabled = false;
            }
            ConfigureFocusNavigation();
            return;
        }
        RunManager.Instance.EventSynchronizer.ChooseLocalOption(index);
    }

    private void ConfigureFocusNavigation()
    {
        List<Button> focusable = _choiceButtons.Where(button => _showingChoices && !button.Disabled).ToList();
        if (!_showingChoices && !_advanceButton.Disabled)
        {
            focusable.Add(_advanceButton);
        }
        if (IsUpgradeMemory && _upgradeMemorySkip is { Disabled: false }) focusable.Add(_upgradeMemorySkip);
        for (int i = 0; i < focusable.Count; i++)
        {
            Button current = focusable[i];
            NodePath previous = current.GetPathTo(focusable[(i + focusable.Count - 1) % focusable.Count]);
            NodePath next = current.GetPathTo(focusable[(i + 1) % focusable.Count]);
            current.FocusNeighborTop = previous;
            current.FocusNeighborBottom = next;
            current.FocusNeighborLeft = previous;
            current.FocusNeighborRight = next;
            current.FocusPrevious = previous;
            current.FocusNext = next;
        }
    }

    private bool IsReaderActive() => IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(this);

    private void OnActiveScreenUpdated()
    {
        if (IsUpgradeMemory && !HbrMemoryUpgradeStoryPresentation.ObserveReader(this)) return;
        this.UpdateControllerNavEnabled();
        _hud.SetActive(IsReaderActive());
        // Fast-forward pauses while cards, the map, or other screens cover the
        // event, and never consumes a reward choice in the background.
        if (IsReaderActive())
        {
            FocusDefault();
        }
    }

    private void FocusDefault()
    {
        if (IsReaderActive())
        {
            DefaultFocusedControl?.GrabFocus();
        }
    }

    private static Button MakeButton(string text, Rect2 rect, Action pressed)
    {
        Button button = new()
        {
            Text = text,
            Position = rect.Position,
            Size = rect.Size,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            MouseFilter = MouseFilterEnum.Stop
        };
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", Colors.White);
        button.Pressed += pressed;
        return button;
    }

    private static Label MakeLabel(string text, int fontSize, Color? color = null)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        // Keep the Chinese script's glyphs and pagination metrics stable across systems.
        Font localeFont = FontManager.GetSubstituteFont("zhs", FontType.Regular)
            ?? ResourceLoader.Load<Font>("res://themes/kreon_regular_shared.tres");
        label.AddThemeFontOverride("font", localeFont);
        label.AddThemeColorOverride("font_color", color ?? Colors.White);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.78f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    private static void Put(Control control, Node parent, Rect2 rect)
    {
        parent.AddChild(control);
        control.Position = rect.Position;
        control.Size = rect.Size;
    }

    private static void AddLine(Node parent, Vector2 from, Vector2 to, Color color, float width)
    {
        Line2D line = new() { Width = width, DefaultColor = color, Antialiased = true };
        line.AddPoint(from);
        line.AddPoint(to);
        parent.AddChild(line);
    }

    private static void AddDot(Node parent, Vector2 center, float radius, Color color)
    {
        Panel dot = new() { MouseFilter = MouseFilterEnum.Ignore };
        dot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = (int)radius,
            CornerRadiusTopRight = (int)radius,
            CornerRadiusBottomLeft = (int)radius,
            CornerRadiusBottomRight = (int)radius
        });
        Put(dot, parent, new Rect2(center - Vector2.One * radius, Vector2.One * radius * 2));
    }

    private static void TransparentStyles(Button button)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
    }

    private static StyleBoxFlat RoundStyle(Color fill, Color outline, int radius = 28) => new()
    {
        BgColor = fill,
        BorderColor = outline,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        BorderWidthTop = 2,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius
    };

    private AtlasTexture AtlasSlice(Rect2 region) => new() { Atlas = _uiAtlas, Region = region, FilterClip = true };

    private static string Ui(string key, string fallback)
    {
        LocString loc = new("events", $"UI.LIBRARY_HBRGO_SAKI.{key}");
        return loc.Exists() ? loc.GetFormattedText() : fallback;
    }

    private static string PlainText(string text) => Regex.Replace(text, @"\[[^\]]*\]", string.Empty)
        .Replace(" NL ", "\n");

    private sealed record DialoguePage(string Text, bool Thought, string Speaker, string Portrait,
        HbrPortraitLayout PortraitLayout, IReadOnlyList<HbrStoryCharacter> Characters, string Background,
        int SourceLineIndex = -1, HbrStoryLine SourceLine = null);
}
