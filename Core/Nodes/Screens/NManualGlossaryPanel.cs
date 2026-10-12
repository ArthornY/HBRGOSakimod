using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NManualGlossaryPanel : Control
{
    private static readonly Color Accent = new("#ed72aa");
    private static readonly Color Muted = new("#a7afb7");
    private readonly Dictionary<string, Texture2D> _icons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _listIcons = new(StringComparer.Ordinal);
    private IReadOnlyList<HbrManualGlossaryEntry> _entries = Array.Empty<HbrManualGlossaryEntry>();
    private IReadOnlyList<HbrManualGlossaryEntry> _filtered = Array.Empty<HbrManualGlossaryEntry>();
    private VBoxContainer _listPane;
    private LineEdit _search;
    private Button _clear;
    private Label _count;
    private ItemList _list;
    private Label _emptyList;
    private PanelContainer _detailPane;
    private ScrollContainer _detailScroll;
    private TextureRect _icon;
    private Label _title;
    private Label _category;
    private MegaRichTextLabel _description;
    private Texture2D _fallbackIcon;
    private string _selectedId;
    private bool _refreshing;

    internal Control DefaultFocusedControl => _search;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        _entries = HbrManualGlossaryCatalog.Build();
        FilterEntries();
        LayoutContent();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutContent();
    }

    internal void FocusSearch()
    {
        if (_search == null || !IsVisibleInTree()) return;
        _search.GrabFocus();
        _search.Edit();
    }

    internal void ReleaseSearchFocus()
    {
        if (_search?.HasFocus() == true) _search.ReleaseFocus();
    }

    private void BuildUi()
    {
        _fallbackIcon = new GradientTexture2D
        {
            Width = 32, Height = 32,
            Gradient = new Gradient { Colors = new[] { new Color("#776178"), new Color("#383340") } }
        };
        _listPane = new VBoxContainer();
        _listPane.AddThemeConstantOverride("separation", 8);
        AddChild(_listPane);
        HBoxContainer searchRow = new();
        searchRow.AddThemeConstantOverride("separation", 6);
        _listPane.AddChild(searchRow);
        _search = new LineEdit
        {
            Name = "GlossarySearch", PlaceholderText = "搜索名称或说明",
            TooltipText = "搜索名称、分类或说明（Ctrl+F）",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 42),
            ExpandToTextLength = false, FocusMode = FocusModeEnum.All,
            CaretBlink = true
        };
        _search.AddThemeFontSizeOverride("font_size", 17);
        _search.AddThemeStyleboxOverride("normal", Surface(new Color("#24272e"), new Color("#4a414b"), 1));
        _search.AddThemeStyleboxOverride("focus", Surface(Colors.Transparent, Accent, 2));
        _search.TextChanged += _ => FilterEntries();
        _search.TextSubmitted += _ => FocusResults();
        _search.GuiInput += input =>
        {
            if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Down })
            {
                FocusResults();
                _search.AcceptEvent();
            }
        };
        searchRow.AddChild(_search);
        _clear = new Button
        {
            Text = "清空", TooltipText = "清空搜索", CustomMinimumSize = new Vector2(54, 42),
            FocusMode = FocusModeEnum.All
        };
        _clear.AddThemeFontSizeOverride("font_size", 16);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
            _clear.AddThemeStyleboxOverride(state, Surface(state == "normal" ? new Color("#25282e") : new Color("#3c303b"), Accent, state == "focus" ? 2 : 0));
        _clear.Pressed += () =>
        {
            _search.Text = string.Empty;
            FilterEntries();
            FocusSearch();
        };
        searchRow.AddChild(_clear);
        _count = MakeLabel(string.Empty, 15, Muted);
        _count.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _listPane.AddChild(_count);
        _list = new ItemList
        {
            Name = "GlossaryEntries", MaxColumns = 1,
            SelectMode = ItemList.SelectModeEnum.Single,
            IconMode = ItemList.IconModeEnum.Left,
            FixedIconSize = new Vector2I(32, 32),
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AllowReselect = true, AutoHeight = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        _list.AddThemeFontSizeOverride("font_size", 18);
        _list.AddThemeConstantOverride("v_separation", 12);
        _list.AddThemeConstantOverride("h_separation", 10);
        _list.AddThemeConstantOverride("icon_margin", 10);
        _list.AddThemeStyleboxOverride("panel", Surface(new Color("#22252b"), new Color("#493e49"), 1));
        _list.AddThemeStyleboxOverride("focus", Surface(Colors.Transparent, Accent, 2));
        _list.AddThemeStyleboxOverride("selected", Surface(new Color("#f2dbe6"), Accent, 2));
        _list.AddThemeStyleboxOverride("selected_focus", Surface(Colors.White, Accent, 2));
        _list.AddThemeStyleboxOverride("hovered", Surface(new Color("#3c303b"), Accent, 0));
        _list.AddThemeColorOverride("font_color", new Color("#e5e5e9"));
        _list.AddThemeColorOverride("font_selected_color", new Color("#7c3058"));
        _list.ItemSelected += index => { if (!_refreshing) SelectEntry((int)index); };
        _listPane.AddChild(_list);
        _emptyList = MakeLabel("没有找到匹配的词条\n试试其他关键词，或清空搜索。", 18, Muted);
        _emptyList.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _emptyList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _listPane.AddChild(_emptyList);

        _detailPane = new PanelContainer();
        _detailPane.AddThemeStyleboxOverride("panel", Surface(new Color("#22252b"), new Color("#493e49"), 1, 18));
        AddChild(_detailPane);
        _detailScroll = new ScrollContainer
        {
            Name = "GlossaryDescriptionScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        _detailPane.AddChild(_detailScroll);
        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 16);
        _detailScroll.AddChild(content);
        HBoxContainer heading = new();
        heading.AddThemeConstantOverride("separation", 14);
        content.AddChild(heading);
        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(64, 64),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore
        };
        heading.AddChild(_icon);
        VBoxContainer titles = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 8);
        heading.AddChild(titles);
        _title = MakeLabel(string.Empty, 26, Colors.White);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titles.AddChild(_title);
        _category = MakeLabel(string.Empty, 16, Accent);
        _category.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titles.AddChild(_category);
        content.AddChild(new HSeparator());
        _description = new MegaRichTextLabel
        {
            Name = "GlossaryDescription", BbcodeEnabled = true,
            AutoSizeEnabled = false, FitContent = true, ScrollActive = false,
            SelectionEnabled = true, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        _description.AddThemeFontSizeOverride("normal_font_size", 20);
        _description.AddThemeFontSizeOverride("bold_font_size", 20);
        _description.AddThemeColorOverride("default_color", new Color("#d7dce1"));
        foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            _description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
        content.AddChild(_description);
    }

    private void FilterEntries()
    {
        _filtered = HbrManualGlossaryCatalog.Search(_entries, _search.Text);
        _clear.Disabled = string.IsNullOrEmpty(_search.Text);
        _count.Text = string.IsNullOrWhiteSpace(_search.Text)
            ? $"{_filtered.Count} 个词条 · A–Z / 中文拼音"
            : $"找到 {_filtered.Count} / {_entries.Count} 个词条";
        _refreshing = true;
        _list.Clear();
        foreach (HbrManualGlossaryEntry entry in _filtered)
        {
            int index = _list.AddItem(entry.Title, LoadListIcon(entry));
            _list.SetItemTooltip(index, $"{entry.Title}\n{entry.Category}");
        }
        _list.Visible = _filtered.Count > 0;
        _emptyList.Visible = _filtered.Count == 0;
        if (_filtered.Count > 0)
        {
            int selected = _filtered.ToList().FindIndex(entry => entry.Id == _selectedId);
            selected = Math.Max(0, selected);
            _list.Select(selected);
            _list.EnsureCurrentIsVisible();
            SelectEntry(selected);
        }
        else
        {
            _icon.Texture = _fallbackIcon;
            _title.Text = "没有匹配的词条";
            _category.Text = "词条全解";
            _description.Text = "试试其他名称、分类或说明中的关键词，或点击“清空”查看全部词条。";
            _detailScroll.ScrollVertical = 0;
        }
        _refreshing = false;
    }

    private void SelectEntry(int index)
    {
        if (index < 0 || index >= _filtered.Count) return;
        HbrManualGlossaryEntry entry = _filtered[index];
        bool changed = _selectedId != entry.Id || _title.Text != entry.Title;
        _selectedId = entry.Id;
        _icon.Texture = LoadIcon(entry);
        _title.Text = entry.Title;
        _category.Text = entry.Category;
        _description.Text = entry.Description;
        if (changed) _detailScroll.ScrollVertical = 0;
    }

    private Texture2D LoadIcon(HbrManualGlossaryEntry entry)
    {
        if (_icons.TryGetValue(entry.Id, out Texture2D icon)) return icon;
        icon = _fallbackIcon;
        if (!string.IsNullOrWhiteSpace(entry.IconPath))
        {
            try
            {
                if (ResourceLoader.Exists(entry.IconPath))
                {
                    Texture2D texture = PreloadManager.Cache.GetTexture2D(entry.IconPath);
                    if (texture != null)
                        icon = entry.IconRegion is { } region
                            ? new AtlasTexture { Atlas = texture, Region = region }
                            : texture;
                }
            }
            catch (Exception)
            {
                // A missing optional icon must not prevent reading the entry.
            }
        }
        _icons[entry.Id] = icon;
        return icon;
    }

    private Texture2D LoadListIcon(HbrManualGlossaryEntry entry)
    {
        if (_listIcons.TryGetValue(entry.Id, out Texture2D icon)) return icon;
        icon = LoadIcon(entry);
        Vector2 size = icon.GetSize();
        if (size.X > 0 && size.Y > 0 && !Mathf.IsEqualApprox(size.X, size.Y))
        {
            // ItemList forces FixedIconSize. Pad to a square first so portrait
            // icons keep their ratio; preserve any existing atlas crop/margin.
            float side = Math.Max(size.X, size.Y);
            Vector2 padding = new Vector2(side, side) - size;
            if (icon is AtlasTexture atlas)
            {
                icon = new AtlasTexture
                {
                    Atlas = atlas.Atlas, Region = atlas.Region,
                    Margin = new Rect2(atlas.Margin.Position + padding / 2f, atlas.Margin.Size + padding),
                    FilterClip = atlas.FilterClip
                };
            }
            else
            {
                icon = new AtlasTexture
                {
                    Atlas = icon, Region = new Rect2(Vector2.Zero, size),
                    Margin = new Rect2(padding / 2f, padding), FilterClip = true
                };
            }
        }
        _listIcons[entry.Id] = icon;
        return icon;
    }

    private void FocusResults()
    {
        if (_filtered.Count == 0) return;
        _list.GrabFocus();
        _list.EnsureCurrentIsVisible();
    }

    private void LayoutContent()
    {
        if (_listPane == null || _detailPane == null) return;
        bool stacked = Size.X < 650;
        _listPane.Position = Vector2.Zero;
        if (stacked)
        {
            float listHeight = Math.Min(260, Math.Max(_listPane.GetCombinedMinimumSize().Y, Size.Y * 0.42f));
            listHeight = Math.Min(listHeight, Math.Max(0, Size.Y - 32));
            _listPane.Size = new Vector2(Size.X, listHeight);
            float detailTop = _listPane.Size.Y + 12;
            _detailPane.Position = new Vector2(0, detailTop);
            _detailPane.Size = new Vector2(Size.X, Math.Max(0, Size.Y - detailTop));
        }
        else
        {
            float listWidth = Math.Clamp(Size.X * 0.3f, 240, 280);
            _listPane.Size = new Vector2(listWidth, Size.Y);
            _detailPane.Position = new Vector2(listWidth + 18, 0);
            _detailPane.Size = new Vector2(Math.Max(0, Size.X - listWidth - 18), Size.Y);
        }
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat Surface(Color background, Color border, int width, int padding = 10) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        ContentMarginLeft = padding, ContentMarginRight = padding,
        ContentMarginTop = 8, ContentMarginBottom = 8,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
    };
}
