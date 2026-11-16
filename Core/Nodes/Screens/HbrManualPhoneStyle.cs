using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The phone manual's page style (ui-concepts/manual-phone-20261104), shared by every page inside
// the phone: frosted navy panels with a faint cyan edge, cyan list highlights, the pill search box,
// teal secondary and cyan primary buttons, and a thin cyan scrollbar.
internal static class HbrManualPhoneStyle
{
    // .panel: background rgba(10,30,46,.55), border 1px rgba(132,233,240,.28), radius 18.
    internal static readonly Color PanelFill = new(0.04f, 0.118f, 0.18f, 0.55f);
    internal static readonly Color PanelEdge = new(0.518f, 0.914f, 0.941f, 0.28f);
    internal static readonly Color Hover = new(0.588f, 0.941f, 0.98f, 0.1f);
    internal static readonly Color Selected = new(0.588f, 0.941f, 0.98f, 0.22f);
    internal static readonly Color SelectedEdge = new(0.588f, 0.941f, 0.98f, 0.6f);
    internal static readonly Color Highlight = new(0.588f, 0.941f, 0.98f, 0.14f);
    internal static readonly Color HighlightEdge = new(0.588f, 0.941f, 0.98f, 0.8f);
    internal static readonly Color Rule = new(0.518f, 0.914f, 0.941f, 0.18f);
    internal static readonly Color Text = new("e3f3f5");
    internal static readonly Color Paragraph = new("dfeff1");
    internal static readonly Color Dim = new(0.86f, 0.92f, 0.93f, 0.45f);
    internal static readonly Color PrimaryFill = new("64d4de");
    internal static readonly Color PrimaryHover = new("8af1f6");
    internal static readonly Color PrimaryText = new("06232b");

    internal static StyleBoxFlat Fill(Color fill, int radius, Color? edge = null, int edgeWidth = 1,
        float padX = 14f, float padY = 10f)
    {
        StyleBoxFlat style = new()
        {
            BgColor = fill,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY,
            AntiAliasing = true
        };
        style.SetCornerRadiusAll(radius);
        if (edge.HasValue)
        {
            style.BorderColor = edge.Value;
            style.SetBorderWidthAll(edgeWidth);
        }
        return style;
    }

    internal static StyleBoxFlat Panel(int radius = 18, float padX = 16f, float padY = 12f) =>
        Fill(PanelFill, radius, PanelEdge, 1, padX, padY);

    // A stat card or list entry picked from elsewhere: brighter fill, cyan ring and a soft glow.
    internal static StyleBoxFlat Picked(int radius = 18, float padX = 16f, float padY = 12f)
    {
        StyleBoxFlat style = Fill(Highlight, radius, HighlightEdge, 2, padX, padY);
        style.ShadowColor = new Color(0.47f, 0.91f, 0.95f, 0.35f);
        style.ShadowSize = 18;
        return style;
    }

    internal static Label Label(string text, int size, Color color, bool wrap = false)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (wrap) label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    // Fixed headings and numbers in the HBR face.
    internal static Label Heading(string text, int size, Color color, bool glow = false)
    {
        Label label = HbrManualPhoneArt.Label("Heading", text, size, color, glow);
        label.VerticalAlignment = VerticalAlignment.Top;
        return label;
    }

    // #g-search input: a 56px pill, navy fill, cyan edge that brightens and glows when focused.
    internal static void SearchBox(LineEdit edit, int size = 22)
    {
        float pad = 24f;
        edit.AddThemeStyleboxOverride("normal", Fill(new Color(0.024f, 0.078f, 0.125f, 0.7f), 28,
            new Color(0.518f, 0.914f, 0.941f, 0.5f), 1, pad, 6f));
        StyleBoxFlat focus = Fill(new Color(0.024f, 0.078f, 0.125f, 0.7f), 28, HbrManualPhoneArt.Cyan, 1, pad, 6f);
        focus.ShadowColor = new Color(0.518f, 0.914f, 0.941f, 0.5f);
        focus.ShadowSize = 10;
        edit.AddThemeStyleboxOverride("focus", focus);
        edit.AddThemeFontSizeOverride("font_size", size);
        edit.AddThemeColorOverride("font_color", Colors.White);
        edit.AddThemeColorOverride("font_placeholder_color", new Color(0.745f, 0.863f, 0.882f, 0.55f));
        edit.AddThemeColorOverride("caret_color", HbrManualPhoneArt.Cyan);
        edit.AddThemeColorOverride("selection_color", new Color(0.518f, 0.914f, 0.941f, 0.35f));
    }

    // Teal translucent pill with a cyan edge (清空, 关闭, secondary actions).
    internal static void Secondary(Button button, int size, int radius = 28)
    {
        button.AddThemeStyleboxOverride("normal", Fill(new Color(0.369f, 0.659f, 0.729f, 0.35f), radius,
            new Color(0.518f, 0.914f, 0.941f, 0.5f)));
        button.AddThemeStyleboxOverride("hover", Fill(new Color(0.47f, 0.784f, 0.843f, 0.55f), radius,
            new Color(0.518f, 0.914f, 0.941f, 0.8f)));
        button.AddThemeStyleboxOverride("pressed", Fill(new Color(0.47f, 0.784f, 0.843f, 0.7f), radius, HbrManualPhoneArt.Cyan));
        button.AddThemeStyleboxOverride("focus", Fill(new Color(0f, 0f, 0f, 0f), radius, HbrManualPhoneArt.Cyan, 2));
        button.AddThemeStyleboxOverride("disabled", Fill(new Color(0.2f, 0.25f, 0.29f, 0.45f), radius,
            new Color(0.78f, 0.86f, 0.9f, 0.2f)));
        FontColors(button, Colors.White, new Color(1f, 1f, 1f, 0.4f));
        button.AddThemeFontSizeOverride("font_size", size);
    }

    // The cyan primary pill (确定, 购买): bright fill, dark text, white ring on hover.
    internal static void Primary(Button button, int size, int radius = 28)
    {
        StyleBoxFlat normal = Fill(PrimaryFill, radius, new Color(0.86f, 1f, 1f, 0.5f), 3);
        StyleBoxFlat hover = Fill(PrimaryHover, radius, Colors.White, 3);
        hover.ShadowColor = new Color(0.55f, 0.94f, 0.98f, 0.6f);
        hover.ShadowSize = 12;
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("focus", Fill(new Color(0f, 0f, 0f, 0f), radius, Colors.White, 2));
        button.AddThemeStyleboxOverride("disabled", Fill(new Color("3d4b55"), radius, new Color(0.78f, 0.86f, 0.9f, 0.2f), 2));
        FontColors(button, PrimaryText, new Color(1f, 1f, 1f, 0.45f));
        button.AddThemeFontSizeOverride("font_size", size);
    }

    // A list entry: no fill at rest, a faint cyan wash on hover, a brighter one with a ring when chosen.
    internal static void ListEntry(Button button, bool selected, int size, int radius = 16)
    {
        StyleBoxFlat rest = selected ? Fill(Selected, radius, SelectedEdge, 2) : Fill(new Color(0f, 0f, 0f, 0f), radius);
        StyleBoxFlat hover = selected ? rest : Fill(Hover, radius);
        button.AddThemeStyleboxOverride("normal", rest);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", Fill(Selected, radius, SelectedEdge, 2));
        button.AddThemeStyleboxOverride("focus", Fill(new Color(0f, 0f, 0f, 0f), radius, HbrManualPhoneArt.Cyan, 2));
        button.AddThemeStyleboxOverride("disabled", rest);
        FontColors(button, selected ? Colors.White : Text, Dim);
        button.AddThemeFontSizeOverride("font_size", size);
    }

    internal static void ItemList(ItemList list, int size = 22)
    {
        list.AddThemeStyleboxOverride("panel", Panel(18, 8f, 8f));
        list.AddThemeStyleboxOverride("focus", Fill(new Color(0f, 0f, 0f, 0f), 18, new Color(0.788f, 0.973f, 0.98f, 0.6f), 2));
        list.AddThemeStyleboxOverride("selected", Fill(Selected, 12, null, 0, 14f, 10f));
        list.AddThemeStyleboxOverride("selected_focus", Fill(Selected, 12, SelectedEdge, 1, 14f, 10f));
        list.AddThemeStyleboxOverride("hovered", Fill(Hover, 12, null, 0, 14f, 10f));
        list.AddThemeStyleboxOverride("hovered_selected", Fill(Selected, 12, null, 0, 14f, 10f));
        list.AddThemeStyleboxOverride("cursor", new StyleBoxEmpty());
        list.AddThemeStyleboxOverride("cursor_unfocused", new StyleBoxEmpty());
        list.AddThemeColorOverride("font_color", Text);
        list.AddThemeColorOverride("font_hovered_color", Colors.White);
        list.AddThemeColorOverride("font_selected_color", Colors.White);
        list.AddThemeFontSizeOverride("font_size", size);
        Scrollbar(list.GetVScrollBar());
    }

    // .scroll: a 6px cyan thumb on no track.
    internal static void Scroll(ScrollContainer scroll) => Scrollbar(scroll.GetVScrollBar());

    // Places a control at (x, y, width, height) inside a parent of the given size by anchoring it to
    // the parent's edges. Setting Size instead stores whatever minimum height the control reports at
    // that moment, and an autowrapping label reports one glyph per line before it has a width: the
    // control then kept that height, and a list's last rows ended below the phone screen.
    internal static void Pin(Control control, Vector2 parent, float x, float y, float width, float height)
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        control.OffsetLeft = x;
        control.OffsetTop = y;
        control.OffsetRight = x + width - parent.X;
        control.OffsetBottom = y + height - parent.Y;
    }

    private static void Scrollbar(ScrollBar bar)
    {
        if (bar == null) return;
        StyleBoxFlat thumb = Fill(new Color(0.518f, 0.914f, 0.941f, 0.6f), 3, null, 0, 3f, 0f);
        StyleBoxFlat thumbLit = Fill(new Color(0.518f, 0.914f, 0.941f, 0.85f), 3, null, 0, 3f, 0f);
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxEmpty { ContentMarginLeft = 3f, ContentMarginRight = 3f });
        bar.AddThemeStyleboxOverride("scroll_focus", new StyleBoxEmpty { ContentMarginLeft = 3f, ContentMarginRight = 3f });
        bar.AddThemeStyleboxOverride("grabber", thumb);
        bar.AddThemeStyleboxOverride("grabber_highlight", thumbLit);
        bar.AddThemeStyleboxOverride("grabber_pressed", thumbLit);
    }

    internal static HSeparator Separator()
    {
        HSeparator separator = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        separator.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = PanelEdge, Thickness = 1 });
        return separator;
    }

    private static void FontColors(Button button, Color color, Color disabled)
    {
        foreach (string name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(name, color);
        button.AddThemeColorOverride("font_disabled_color", disabled);
        button.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0f));
        button.AddThemeConstantOverride("outline_size", 0);
    }
}
