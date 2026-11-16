using System.Collections.Generic;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Original HBR shop and equipment sprites (UI/Texture/ExchangeShop, HCShop,
// Icon and the accessory/booster/chip icons); provenance is in sources.json.
internal static class HbrEquipmentArt
{
    private const string Root = "res://images/ui/hbr_equipment/";
    private const string IconRoot = Root + "icons/";

    internal const string ShopBackground = Root + "FriendStreetShop.png";
    internal const string Shopkeeper = Root + "MSatsukiDefault.png";
    internal const string ListNormal = Root + "btnExchangeShopListNomal.png";
    internal const string ListActive = Root + "btnExchangeShopListActive.png";
    internal const string ListDisabled = Root + "btnExchangeShopListDisable.png";
    internal const string ItemBackground = Root + "itemBackground.png";
    // Gold is the run's own currency, so the wallet, prices and the sell tab use the top bar's coin,
    // the same one the vanilla merchant shows. HBR's GP icon is the fallback.
    internal const string GoldCoin = "res://images/atlases/ui_atlas.sprites/top_bar/top_bar_gold.tres";
    internal const string Coin = Root + "IconGP.png";
    internal const string ShopIcon = Root + "IconHomeShopDefault.png";
    internal const string AlchemyIcon = Root + "IconHomeCatShopDefault.png";
    internal const string PlusSlotOff = Root + "SelectFlameBaceOff.png";
    internal const string PlusSlotOn = Root + "SelectFlameBaceOn.png";

    internal static readonly Color Magenta = new("#e45bd8");
    internal static readonly Color Pink = new("#ff7fb6");
    internal static readonly Color Lilac = new("#cdb8ff");
    internal static readonly Color Ink = new(0.05f, 0.03f, 0.09f);
    internal static readonly Color Muted = new("#b7adc9");
    internal static readonly Color Gold = new("#ffd27a");

    private static readonly Dictionary<string, Texture2D> Textures = new();

    internal static string Icon(string file) => IconRoot + file;

    internal static Texture2D CoinTexture => Load(GoldCoin) ?? Load(Coin);

    internal static Texture2D Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!Textures.TryGetValue(path, out Texture2D texture) || !GodotObject.IsInstanceValid(texture))
        {
            texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            Textures[path] = texture;
        }

        return texture;
    }

    internal static Label MakeLabel(string text, int fontSize, Color color, int outline = 4)
    {
        Label label = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Ink);
        label.AddThemeConstantOverride("outline_size", outline);
        return label;
    }

    internal static StyleBoxFlat Panel(Color background, Color border, int radius, int borderWidth = 1)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border,
            ContentMarginLeft = 14f,
            ContentMarginRight = 14f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    // The original list capsules (324×140) stretch to any row width. The
    // nine-patch margins follow the row height so the caps never overlap.
    internal static StyleBoxTexture Capsule(string path, float height)
    {
        float rowHeight = Mathf.Max(32f, height);
        StyleBoxTexture style = new() { Texture = Load(path) };
        style.TextureMarginLeft = Mathf.Min(64f, rowHeight * 0.8f);
        style.TextureMarginRight = Mathf.Min(64f, rowHeight * 0.8f);
        style.TextureMarginTop = Mathf.Min(30f, rowHeight * 0.3f);
        style.TextureMarginBottom = Mathf.Min(30f, rowHeight * 0.3f);
        style.ContentMarginLeft = 26f;
        style.ContentMarginRight = 26f;
        style.ContentMarginTop = 6f;
        style.ContentMarginBottom = 6f;
        return style;
    }

    internal static TextureRect Picture(string path, Vector2 size) => Picture(Load(path), size);

    internal static TextureRect Picture(Texture2D texture, Vector2 size)
    {
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    // Rows and tabs in the phone manual's style (ui-concepts/manual-phone-20261104): a frosted navy
    // pill with a faint cyan edge, a cyan wash on hover; a chosen tab is the cyan primary pill.
    internal static void StyleCapsule(Button button, bool selected)
    {
        float height = button.CustomMinimumSize.Y > 0f ? button.CustomMinimumSize.Y : 48f;
        int radius = Mathf.RoundToInt(Mathf.Min(height / 2f, 26f));
        if (selected)
        {
            HbrManualPhoneStyle.Primary(button, button.GetThemeFontSize("font_size"), radius);
            return;
        }
        button.AddThemeStyleboxOverride("normal", HbrManualPhoneStyle.Fill(HbrManualPhoneStyle.PanelFill, radius,
            HbrManualPhoneStyle.PanelEdge, 1, 26f, 6f));
        StyleBoxFlat hover = HbrManualPhoneStyle.Fill(new Color(0.08f, 0.2f, 0.27f, 0.75f), radius,
            new Color(0.588f, 0.941f, 0.98f, 0.7f), 1, 26f, 6f);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", HbrManualPhoneStyle.Fill(HbrManualPhoneStyle.Selected, radius,
            HbrManualPhoneStyle.SelectedEdge, 2, 26f, 6f));
        button.AddThemeStyleboxOverride("focus", HbrManualPhoneStyle.Fill(new Color(0f, 0f, 0f, 0f), radius,
            HbrManualPhoneArt.Cyan, 2, 26f, 6f));
        button.AddThemeStyleboxOverride("disabled", HbrManualPhoneStyle.Fill(new Color(0.12f, 0.16f, 0.2f, 0.45f), radius,
            new Color(0.78f, 0.86f, 0.9f, 0.15f), 1, 26f, 6f));
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
            button.AddThemeColorOverride(color, Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.45f));
        button.AddThemeColorOverride("font_outline_color", Ink);
        button.AddThemeConstantOverride("outline_size", 4);
    }

    internal static Button MakeCapsule(string text, int fontSize, float height, bool selected = false)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(0f, height),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        StyleCapsule(button, selected);
        return button;
    }

    // The round 购入 button of the original shop list, in the phone manual's cyan primary colours.
    internal static Button MakeRoundButton(string text, float diameter = 62f)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(diameter, diameter),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        int radius = Mathf.RoundToInt(diameter / 2f);
        HbrManualPhoneStyle.Primary(button, 17, radius);
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            if (button.GetThemeStylebox(state) is not StyleBoxFlat style) continue;
            style.ContentMarginLeft = 2f;
            style.ContentMarginRight = 2f;
        }
        return button;
    }

    // An item icon on the original sunburst item background, with its stars.
    internal static Control MakeItemIcon(string iconFile, int rarity, float size)
    {
        Control holder = new() { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        TextureRect background = Picture(ItemBackground, new Vector2(size, size));
        background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        background.ClipContents = true;
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(background);
        TextureRect icon = Picture(Icon(iconFile), new Vector2(size, size));
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(icon);
        if (rarity > 0)
        {
            Label stars = MakeLabel(HbrEquipmentStars(rarity), Mathf.Max(10, Mathf.RoundToInt(size * 0.2f)), Gold, 3);
            stars.HorizontalAlignment = HorizontalAlignment.Center;
            stars.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            stars.OffsetTop = -size * 0.28f;
            holder.AddChild(stars);
        }
        return holder;
    }

    private static string HbrEquipmentStars(int rarity) => new('★', Mathf.Clamp(rarity, 0, 6));

    internal static HBoxContainer MakePrice(int price, int fontSize = 20)
    {
        HBoxContainer row = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(Picture(CoinTexture, new Vector2(fontSize + 4, fontSize + 4)));
        row.AddChild(MakeLabel(price.ToString(System.Globalization.CultureInfo.InvariantCulture), fontSize, Colors.White));
        return row;
    }

    internal static void Clear(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
