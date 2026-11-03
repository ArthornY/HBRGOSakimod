using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// HBR divergence-style artwork for the phantom roster (sprites from UIDimensionBattleTop,
// UICharacterList and UIEnhance).
internal static class PhantomRosterArt
{
    private const string Root = "res://images/ui/hbr_phantom_roster/";
    internal const string NightSky = Root + "DimensionNightSky.png";
    internal const string RingLine = Root + "DimentionEffectLine.png";
    internal const string RingDots = Root + "DimentionEffect01.png";
    internal const string RingInner = Root + "DencyoEffect03.png";
    internal const string RingOuter = Root + "DencyoEffect04.png";
    internal const string BackButton = Root + "ButtonBackDefault.png";
    internal const string EmptyLock = Root + "ExtraEnemyLock.png";
    internal const string SelectCursor = Root + "CusorBattleChara.png";
    internal const string CircleMask = Root + "dimensionEnemyBg.png";
    internal const string Frame = Root + "dimensionEnemyFrame.png";
    internal const string FrameActive = Root + "dimensionEnemyFrameActive.png";
    internal const string LevelFrame = Root + "dimensionClassFrame.png";
    internal const string LevelFrameActive = Root + "dimensionClassFrameActive.png";
    internal const string ActiveStar = Root + "IconDimensionBattleStar.png";
    internal const string ButtonNormal = Root + "ButtonFirstNormal.png";
    internal const string ButtonActive = Root + "ButtonFirstActive.png";
    internal const string ButtonDisabled = Root + "ButtonFirstDisable.png";
    internal const string ButtonFocused = Root + "ButtonFirstFocused.png";
    internal const string TitleLine = Root + "MordaTitleLineEdge.png";
    internal const string CircleButton = Root + "ButtonFourthCircleNormal.png";
    internal const string CircleButtonActive = Root + "ButtonFourthCircleActive.png";
    internal const string UpgradeBadge = Root + "IconBreakLimitBatchActive.png";
    internal const string CheckBox = Root + "CheckBoxButton.png";
    internal const string CheckMark = Root + "CheckBoxMarker.png";
    internal const string Halo = Root + "ThumbnailHalo.png";

    internal static readonly Color Pink = new("#ff5fb4");
    internal static readonly Color PinkLight = new("#ffa6d6");
    internal static readonly Color Gold = new("#ffd27a");
    internal static readonly Color Muted = new("#b9b0cf");
    internal static readonly Color Ink = new(0.02f, 0.015f, 0.05f);

    private const string RedCrimsonScene = "res://images/backgrounds/hbrgo_red_crimson/red_crimson_exposed.png";
    private const string FlatHandScene = "res://images/backgrounds/hbrgo_flat_hand/flat_hand_phase1.png";

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly Dictionary<ModelId, Texture2D> SceneCrops = new();

    internal static Texture2D Load(string path)
    {
        if (!Textures.TryGetValue(path, out Texture2D texture) || !GodotObject.IsInstanceValid(texture))
        {
            texture = ResourceLoader.Load<Texture2D>(path);
            Textures[path] = texture;
        }

        return texture;
    }

    // Red Crimson and FlatHand are painted into their boss scenes, so their portrait is a
    // square crop of the scene instead of a rendered creature.
    internal static Texture2D GetPortrait(ModelId monsterId)
    {
        if (SceneCrops.TryGetValue(monsterId, out Texture2D crop) && GodotObject.IsInstanceValid(crop))
        {
            return crop;
        }

        (string scene, Rect2 region)? source =
            monsterId == ModelDb.Monster<RedCrimsonBossMonster>().Id ||
            monsterId == ModelDb.Monster<RedCrimson>().Id
                ? (RedCrimsonScene, new Rect2(900f, 10f, 760f, 760f))
                : monsterId == ModelDb.Monster<FlatHand>().Id
                    ? (FlatHandScene, new Rect2(580f, 20f, 760f, 760f))
                    : null;
        if (source is not { } value)
        {
            return PhantomPortraitHelper.GetPortrait(monsterId);
        }

        crop = new AtlasTexture { Atlas = Load(value.scene), Region = value.region };
        SceneCrops[monsterId] = crop;
        return crop;
    }

    internal static TextureRect Image(string path, Vector2 position, Vector2 size)
    {
        return new TextureRect
        {
            Texture = Load(path),
            Position = position,
            Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    internal static Label MakeLabel(string text, int fontSize, Color color, int outline = 5)
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
            ContentMarginLeft = 18f,
            ContentMarginRight = 18f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        return style;
    }

    // HBR's main pink capsule (ButtonFirst*), drawn at a fixed 0.72 scale so the caps keep
    // their shape; the label is a child so the sprite never stretches unevenly.
    internal static TextureButton MakeButton(string text, bool primary)
    {
        TextureButton button = new()
        {
            TextureNormal = Load(ButtonNormal),
            TextureHover = Load(ButtonActive),
            TexturePressed = Load(ButtonActive),
            TextureDisabled = Load(ButtonDisabled),
            TextureFocused = Load(ButtonFocused),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            CustomMinimumSize = new Vector2(360f, 144f),
            Size = new Vector2(360f, 144f),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        if (!primary)
        {
            // Secondary actions (devour) use the same capsule shifted towards violet.
            button.SelfModulate = new Color(0.82f, 0.74f, 1f);
        }

        Label label = MakeLabel(text, 30, Colors.White, 6);
        label.Name = "Text";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(label);
        return button;
    }

    internal static void SetButtonText(TextureButton button, string text)
    {
        if (button?.GetNodeOrNull<Label>("Text") is { } label)
        {
            label.Text = text;
        }
    }

    internal static void RefreshButtonLabel(TextureButton button)
    {
        if (button?.GetNodeOrNull<Label>("Text") is { } label)
        {
            label.Modulate = button.Disabled ? new Color(1f, 1f, 1f, 0.55f) : Colors.White;
        }
    }

    internal static void StyleCheckBox(CheckBox checkBox, int fontSize)
    {
        checkBox.AddThemeIconOverride("unchecked", Load(CheckBox));
        checkBox.AddThemeIconOverride("checked", Load(CheckMark));
        checkBox.AddThemeIconOverride("unchecked_disabled", Load(CheckBox));
        checkBox.AddThemeIconOverride("checked_disabled", Load(CheckMark));
        checkBox.AddThemeFontSizeOverride("font_size", fontSize);
        checkBox.AddThemeColorOverride("font_color", Colors.White);
        checkBox.AddThemeColorOverride("font_hover_color", PinkLight);
        checkBox.AddThemeColorOverride("font_pressed_color", Colors.White);
        checkBox.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.52f, 0.62f));
        StyleBoxFlat normal = Panel(new Color(0.08f, 0.05f, 0.15f, 0.78f), new Color(1f, 0.65f, 0.84f, 0.45f), 27);
        StyleBoxFlat hover = Panel(new Color(0.14f, 0.07f, 0.22f, 0.85f), Pink, 27, 2);
        checkBox.AddThemeStyleboxOverride("normal", normal);
        checkBox.AddThemeStyleboxOverride("pressed", normal);
        checkBox.AddThemeStyleboxOverride("hover", hover);
        checkBox.AddThemeStyleboxOverride("hover_pressed", hover);
        checkBox.AddThemeStyleboxOverride("focus", hover);
        checkBox.AddThemeStyleboxOverride("disabled", normal);
    }
}
