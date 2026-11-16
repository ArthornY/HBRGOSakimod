using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The glossary in the phone manual's page style (ui-concepts/manual-phone-20261104, 词条全解):
// a pill search box across the top, the entry list on the left and the entry on the right.
// The page header shows the count.
public partial class NManualGlossaryPanel : Control
{
    private const float SearchHeight = 56f;
    private const float ListWidth = 300f;
    private readonly Dictionary<string, Texture2D> _icons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _listIcons = new(StringComparer.Ordinal);
    private IReadOnlyList<HbrManualGlossaryEntry> _entries = Array.Empty<HbrManualGlossaryEntry>();
    private IReadOnlyList<HbrManualGlossaryEntry> _filtered = Array.Empty<HbrManualGlossaryEntry>();
    private HBoxContainer _searchRow;
    private VBoxContainer _listPane;
    private LineEdit _search;
    private Button _clear;
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
    private bool? _catalogMode;

    internal Control DefaultFocusedControl => _search;
    internal bool HbrNumericsEnabled { get; set; }
    // "12 / 12 条" for the page header; CountChanged fires whenever the search narrows the list.
    internal string CountText => $"{_filtered.Count} / {_entries.Count} 条";
    internal Action CountChanged { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        Refresh(HbrNumericsEnabled);
        LayoutContent();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutContent();
    }

    internal void Refresh(bool hbrNumericsEnabled)
    {
        HbrNumericsEnabled = hbrNumericsEnabled;
        if (_search == null || _catalogMode == hbrNumericsEnabled) return;
        _catalogMode = hbrNumericsEnabled;
        _entries = HbrManualGlossaryCatalog.Build(hbrNumericsEnabled);
        FilterEntries();
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
            Gradient = new Gradient { Colors = new[] { new Color("#3f8fa0"), new Color("#123040") } }
        };
        _searchRow = new HBoxContainer();
        _searchRow.AddThemeConstantOverride("separation", 12);
        AddChild(_searchRow);
        _search = new LineEdit
        {
            Name = "GlossarySearch", PlaceholderText = "搜索名称或说明",
            TooltipText = "搜索名称、分类或说明（Ctrl+F）",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, SearchHeight),
            ExpandToTextLength = false, FocusMode = FocusModeEnum.All,
            CaretBlink = true
        };
        HbrManualPhoneStyle.SearchBox(_search, 22);
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
        _searchRow.AddChild(_search);
        _clear = new Button
        {
            Text = "清空", TooltipText = "清空搜索", CustomMinimumSize = new Vector2(110, SearchHeight),
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand
        };
        HbrManualPhoneStyle.Secondary(_clear, 22);
        _clear.Pressed += () =>
        {
            _search.Text = string.Empty;
            FilterEntries();
            FocusSearch();
        };
        _searchRow.AddChild(_clear);

        _listPane = new VBoxContainer();
        _listPane.AddThemeConstantOverride("separation", 8);
        AddChild(_listPane);
        _list = new ItemList
        {
            Name = "GlossaryEntries", MaxColumns = 1,
            SelectMode = ItemList.SelectModeEnum.Single,
            IconMode = ItemList.IconModeEnum.Left,
            FixedIconSize = new Vector2I(30, 30),
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AllowReselect = true, AutoHeight = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        HbrManualPhoneStyle.ItemList(_list, 22);
        _list.AddThemeConstantOverride("v_separation", 10);
        _list.AddThemeConstantOverride("h_separation", 10);
        _list.AddThemeConstantOverride("icon_margin", 10);
        _list.ItemSelected += index => { if (!_refreshing) SelectEntry((int)index); };
        _listPane.AddChild(_list);
        PanelContainer empty = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        empty.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(18, 18f, 14f));
        _emptyList = HbrManualPhoneStyle.Label("没有找到匹配的词条\n试试其他关键词，或清空搜索。", 20, HbrManualPhoneArt.Muted, wrap: true);
        _emptyList.VerticalAlignment = VerticalAlignment.Top;
        empty.AddChild(_emptyList);
        _listPane.AddChild(empty);

        _detailPane = new PanelContainer();
        _detailPane.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(18, 22f, 18f));
        AddChild(_detailPane);
        _detailScroll = new ScrollContainer
        {
            Name = "GlossaryDescriptionScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        HbrManualPhoneStyle.Scroll(_detailScroll);
        _detailPane.AddChild(_detailScroll);
        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        _detailScroll.AddChild(content);
        HBoxContainer heading = new();
        heading.AddThemeConstantOverride("separation", 14);
        content.AddChild(heading);
        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(56, 56),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore
        };
        heading.AddChild(_icon);
        VBoxContainer titles = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 4);
        heading.AddChild(titles);
        // #g-detail h3: 30px cyan.
        _title = HbrManualPhoneStyle.Label(string.Empty, 30, HbrManualPhoneArt.Cyan, wrap: true);
        titles.AddChild(_title);
        _category = HbrManualPhoneStyle.Label(string.Empty, 18, HbrManualPhoneArt.Muted, wrap: true);
        titles.AddChild(_category);
        content.AddChild(HbrManualPhoneStyle.Separator());
        _description = new MegaRichTextLabel
        {
            Name = "GlossaryDescription", BbcodeEnabled = true,
            AutoSizeEnabled = false, FitContent = true, ScrollActive = false,
            SelectionEnabled = true, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        _description.AddThemeFontSizeOverride("normal_font_size", 21);
        _description.AddThemeFontSizeOverride("bold_font_size", 21);
        _description.AddThemeColorOverride("default_color", HbrManualPhoneStyle.Text);
        _description.AddThemeConstantOverride("line_separation", 8);
        foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            _description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
        content.AddChild(_description);
    }

    private void FilterEntries()
    {
        _filtered = HbrManualGlossaryCatalog.Search(_entries, _search.Text);
        _clear.Disabled = string.IsNullOrEmpty(_search.Text);
        _refreshing = true;
        _list.Clear();
        foreach (HbrManualGlossaryEntry entry in _filtered)
        {
            int index = _list.AddItem(entry.Title, LoadListIcon(entry));
            _list.SetItemTooltip(index, $"{entry.Title}\n{entry.Category}");
        }
        _list.Visible = _filtered.Count > 0;
        _emptyList.GetParent<Control>().Visible = _filtered.Count == 0;
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
        CountChanged?.Invoke();
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

    // #g-search across the top; #g-list 300px wide on the left, #g-detail beside it 16px apart.
    private void LayoutContent()
    {
        if (_listPane == null || _detailPane == null) return;
        // Anchored, not sized: the empty-result note wraps, and a sized pane would keep the height it
        // reports before it has a width, pushing the list's last entries below the phone screen.
        HbrManualPhoneStyle.Pin(_searchRow, Size, 0f, 0f, Size.X, SearchHeight);
        float top = SearchHeight + 16f;
        float height = Math.Max(0f, Size.Y - top);
        if (Size.X < 650)
        {
            float listHeight = Math.Min(260f, height * 0.42f);
            HbrManualPhoneStyle.Pin(_listPane, Size, 0f, top, Size.X, listHeight);
            HbrManualPhoneStyle.Pin(_detailPane, Size, 0f, top + listHeight + 12f, Size.X, Math.Max(0f, height - listHeight - 12f));
            return;
        }
        HbrManualPhoneStyle.Pin(_listPane, Size, 0f, top, ListWidth, height);
        HbrManualPhoneStyle.Pin(_detailPane, Size, ListWidth + 16f, top, Math.Max(0f, Size.X - ListWidth - 16f), height);
    }
}
