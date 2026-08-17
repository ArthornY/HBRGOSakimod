using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Audio;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoSakiVariantSelector
{
    private const string SelectorNodeName = "HBRGOSakiVariantSelector";

    private static readonly ConditionalWeakTable<
        NCharacterSelectScreen,
        SelectorState> States = new();

    internal static void Attach(NCharacterSelectScreen screen)
    {
        if (States.TryGetValue(screen, out _))
        {
            return;
        }

        HBoxContainer root = new()
        {
            Name = SelectorNodeName,
            Visible = false,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 12);

        Button thirtyOneOButton = CreateButton(
            GetCharacterTitle(ModelDb.Character<HbrgoSaki31O>()));
        Button artificialSaviorButton = CreateButton(
            GetCharacterTitle(ModelDb.Character<HbrgoSaki>()));
        root.AddChild(thirtyOneOButton);
        root.AddChild(artificialSaviorButton);

        SelectorState state = new(
            root,
            thirtyOneOButton,
            artificialSaviorButton,
            HbrgoSakiVariantPreference.GetPreferredVariant());
        States.Add(screen, state);

        thirtyOneOButton.Pressed += () =>
            SelectVariant(screen, HbrgoSakiVariant.ThirtyOneO);
        artificialSaviorButton.Pressed += () =>
            SelectVariant(screen, HbrgoSakiVariant.ArtificialSavior);

        Control infoLayout =
            screen.GetNode<Control>("InfoPanel/VBoxContainer");
        infoLayout.AddChild(root);
        infoLayout.MoveChild(
            root,
            infoLayout.GetNode("DescriptionLabel").GetIndex());
        RefreshButtons(state);
    }

    internal static void ResolveInitialSelection(
        NCharacterSelectScreen screen,
        NCharacterSelectButton charSelectButton,
        ref CharacterModel characterModel)
    {
        if (charSelectButton.IsRandom ||
            charSelectButton.IsLocked ||
            charSelectButton.Character?.GetType() != typeof(HbrgoSaki) ||
            characterModel?.GetType() != typeof(HbrgoSaki))
        {
            return;
        }

        HbrgoSakiVariant variant = States.TryGetValue(screen, out SelectorState state)
            ? state.SelectedVariant
            : HbrgoSakiVariantPreference.GetPreferredVariant();
        characterModel = HbrgoSakiVariantPreference.GetCharacter(variant);
    }

    internal static void Refresh(
        NCharacterSelectScreen screen,
        NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        if (!States.TryGetValue(screen, out SelectorState state))
        {
            return;
        }

        bool isSelectableSaki =
            !charSelectButton.IsRandom &&
            !charSelectButton.IsLocked &&
            characterModel is HbrgoSaki;
        state.Root.Visible = isSelectableSaki;
        if (!isSelectableSaki)
        {
            state.SakiButton = null;
            return;
        }

        state.SakiButton = charSelectButton;
        state.SelectedVariant =
            HbrgoSakiVariantPreference.FromCharacter(characterModel);
        RefreshButtons(state);
        RefreshInteraction(screen);
    }

    internal static void RefreshRemoteSelection(
        NCharacterSelectScreen screen,
        LobbyPlayer player)
    {
        if (screen.Lobby == null ||
            player.id == screen.Lobby.LocalPlayer.id ||
            player.character is not HbrgoSaki31O)
        {
            return;
        }

        NCharacterSelectButton visibleSakiButton = screen
            .GetNode<Control>("CharSelectButtons/ButtonContainer")
            .GetChildren()
            .OfType<NCharacterSelectButton>()
            .FirstOrDefault(button =>
                button.Character?.GetType() == typeof(HbrgoSaki));
        visibleSakiButton?.OnRemotePlayerSelected(player.id);
    }

    internal static void ClearRemoteSelection(
        NCharacterSelectScreen screen,
        LobbyPlayer player)
    {
        if (player.character is not HbrgoSaki31O)
        {
            return;
        }

        NCharacterSelectButton visibleSakiButton = screen
            .GetNode<Control>("CharSelectButtons/ButtonContainer")
            .GetChildren()
            .OfType<NCharacterSelectButton>()
            .FirstOrDefault(button =>
                button.Character?.GetType() == typeof(HbrgoSaki));
        visibleSakiButton?.OnRemotePlayerDeselected(player.id);
    }

    internal static void RefreshInteraction(
        NCharacterSelectScreen screen)
    {
        if (!States.TryGetValue(screen, out SelectorState state))
        {
            return;
        }

        bool canSwitch =
            state.Root.Visible &&
            screen.Lobby != null &&
            !screen.Lobby.LocalPlayer.isReady;
        state.ThirtyOneOButton.Disabled = !canSwitch;
        state.ArtificialSaviorButton.Disabled = !canSwitch;
    }

    private static void SelectVariant(
        NCharacterSelectScreen screen,
        HbrgoSakiVariant variant)
    {
        if (!States.TryGetValue(screen, out SelectorState state) ||
            state.SakiButton == null ||
            state.SelectedVariant == variant)
        {
            RefreshButtons(state);
            return;
        }

        state.SelectedVariant = variant;
        screen.SelectCharacter(
            state.SakiButton,
            HbrgoSakiVariantPreference.GetCharacter(variant));
    }

    private static Button CreateButton(string title)
    {
        Button button = new()
        {
            Text = title,
            ToggleMode = true,
            ClipText = true,
            CustomMinimumSize = new Vector2(190f, 44f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 18);
        return button;
    }

    private static string GetCharacterTitle(CharacterModel character)
    {
        return new LocString(
            "characters",
            character.CharacterSelectTitle).GetFormattedText();
    }

    private static void RefreshButtons(SelectorState state)
    {
        if (state == null)
        {
            return;
        }

        state.ThirtyOneOButton.ButtonPressed =
            state.SelectedVariant == HbrgoSakiVariant.ThirtyOneO;
        state.ArtificialSaviorButton.ButtonPressed =
            state.SelectedVariant == HbrgoSakiVariant.ArtificialSavior;
    }

    private sealed class SelectorState
    {
        internal SelectorState(
            HBoxContainer root,
            Button thirtyOneOButton,
            Button artificialSaviorButton,
            HbrgoSakiVariant selectedVariant)
        {
            Root = root;
            ThirtyOneOButton = thirtyOneOButton;
            ArtificialSaviorButton = artificialSaviorButton;
            SelectedVariant = selectedVariant;
        }

        internal HBoxContainer Root { get; }

        internal Button ThirtyOneOButton { get; }

        internal Button ArtificialSaviorButton { get; }

        internal NCharacterSelectButton SakiButton { get; set; }

        internal HbrgoSakiVariant SelectedVariant { get; set; }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))]
internal static class CharacterSelectScreenReadyPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        HbrgoSakiVariantSelector.Attach(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), "OnPress")]
internal static class CharacterSelectButtonOnPressPatch
{
    private static void Postfix(NCharacterSelectButton __instance)
    {
        __instance.Select();
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class CharacterSelectScreenSelectCharacterPatch
{
    private static void Prefix(
        NCharacterSelectScreen __instance,
        NCharacterSelectButton charSelectButton,
        ref CharacterModel characterModel)
    {
        HbrgoSakiVariantSelector.ResolveInitialSelection(
            __instance,
            charSelectButton,
            ref characterModel);
    }

    private static void Postfix(
        NCharacterSelectScreen __instance,
        NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        HbrgoSakiVariantSelector.Refresh(
            __instance,
            charSelectButton,
            characterModel);

        if (charSelectButton.IsRandom || charSelectButton.IsLocked)
        {
            HbrgoSakiCharacterSelectVoice.Stop();
            return;
        }

        if (characterModel is HbrgoSaki)
        {
            HbrgoSakiCharacterSelectVoice.Play();
        }
        else
        {
            HbrgoSakiCharacterSelectVoice.Stop();
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "RefreshButtonSelectionForPlayer")]
internal static class CharacterSelectRemoteVariantPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        LobbyPlayer player)
    {
        HbrgoSakiVariantSelector.RefreshRemoteSelection(
            __instance,
            player);
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.PlayerChanged))]
internal static class CharacterSelectVariantReadyStatePatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        LobbyPlayer player)
    {
        if (__instance.Lobby != null &&
            player.id == __instance.Lobby.LocalPlayer.id)
        {
            HbrgoSakiVariantSelector.RefreshInteraction(__instance);
        }
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.RemotePlayerDisconnected))]
internal static class CharacterSelectRemoteVariantDisconnectPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        LobbyPlayer player)
    {
        HbrgoSakiVariantSelector.ClearRemoteSelection(
            __instance,
            player);
    }
}
