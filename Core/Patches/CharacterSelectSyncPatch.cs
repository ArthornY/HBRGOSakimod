using System;
using HBRGOSakiMod.Core.Compatibility;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoSakiCharacterSelectVariantSelector
{
    private static readonly ConditionalWeakTable<
        NCharacterSelectScreen,
        SelectorState> States = new();

    internal static IReadOnlyList<CharacterModel> NormalizeCharacterCatalog(IEnumerable<CharacterModel> characters)
    {
        List<CharacterModel> result = new();
        bool hasHotaru = false;
        foreach (CharacterModel character in characters ?? Enumerable.Empty<CharacterModel>())
        {
            // Keep the retired base registered for old saves and inherited
            // content, but never advertise it as a playable catalog entry.
            if (HbrgoRetiredCharacters.IsRetired(character)) continue;
            if (character is not HbrgoHotaru)
            {
                result.Add(character);
                continue;
            }
            if (hasHotaru) continue;
            // BaseLib discovery and the Ritsu catalog may both contribute the
            // same type. Keep its canonical model and every existing save alias.
            result.Add(ModelDb.Character<HbrgoHotaru>());
            hasHotaru = true;
        }
        return result;
    }

    internal static void Attach(NCharacterSelectScreen screen)
    {
        if (States.TryGetValue(screen, out _))
        {
            return;
        }

        Button switchButton = new()
        {
            Name = "HBRGOSakiVariantSwitchButton",
            AnchorLeft = 0f,
            AnchorTop = 1f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 18f,
            OffsetTop = -62f,
            OffsetRight = -18f,
            OffsetBottom = -12f,
            ClipText = true,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        switchButton.AddThemeFontSizeOverride("font_size", 18);
        screen.GetNode<Control>("InfoPanel").AddChild(switchButton);
        SelectorState state = new(switchButton);
        States.Add(screen, state);
        switchButton.Pressed += () => Toggle(screen);
        Refresh(state);
    }

    internal static void RestorePreferredVariant(
        NCharacterSelectScreen screen)
    {
        HbrgoSakiVariant variant =
            HbrgoSakiVariantPreference.GetPreferredVariant();
        if (variant == HbrgoSakiVariant.ThirtyA &&
            !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        {
            variant = HbrgoSakiVariant.ThirtyOneO;
        }

        SelectVariant(screen, variant);
    }

    internal static void OnCharacterSelected(
        NCharacterSelectScreen screen,
        CharacterModel character)
    {
        if (character is HbrgoHotaru &&
            AccessTools.Field(typeof(NCharacterSelectScreen), "_hp")?.GetValue(screen) is MegaLabel hp)
            hp.SetTextAutoSize($"{HbrgoHotaru.InitialCurrentHp}/{character.StartingHp}");

        if (!States.TryGetValue(screen, out SelectorState state))
        {
            return;
        }

        if (character is HbrgoSaki30A or HbrgoSaki31O)
        {
            state.SelectedVariant =
                HbrgoSakiVariantPreference.FromCharacter(character);
            HbrgoSakiVariantPreference.SetPreferredVariant(
                state.SelectedVariant);
            state.Button.Visible = true;
        }
        else
        {
            state.Button.Visible = false;
        }

        Refresh(state);
    }

    private static void Toggle(NCharacterSelectScreen screen)
    {
        if (!States.TryGetValue(screen, out SelectorState state))
        {
            return;
        }

        HbrgoSakiVariant next = state.SelectedVariant ==
            HbrgoSakiVariant.ThirtyA
                ? HbrgoSakiVariant.ThirtyOneO
                : HbrgoSakiVariant.ThirtyA;
        if (next == HbrgoSakiVariant.ThirtyA &&
            !HbrgoSakiVariantPreference.IsThirtyAUnlocked())
        {
            Refresh(state);
            return;
        }

        HbrgoSakiVariantPreference.SetPreferredVariant(next);
        SelectVariant(screen, next);
    }

    internal static void RefreshRemoteSharedSelection(
        NCharacterSelectScreen screen,
        object player)
    {
        if (!TryReadPlayer(player, out ulong playerId, out CharacterModel character) ||
            playerId == GetLocalPlayerId(screen) ||
            FindSharedButton(screen) is not { } sharedButton)
        {
            return;
        }

        bool isSakiVariant = character is
            HbrgoSaki30A or HbrgoSaki31O;
        if (isSakiVariant &&
            !sharedButton.RemoteSelectedPlayers.Contains(playerId))
        {
            sharedButton.OnRemotePlayerSelected(playerId);
        }
        else if (!isSakiVariant &&
                 sharedButton.RemoteSelectedPlayers.Contains(playerId))
        {
            sharedButton.OnRemotePlayerDeselected(playerId);
        }
    }

    internal static void RemoveRemoteSharedSelection(
        NCharacterSelectScreen screen,
        object player)
    {
        if (!TryReadPlayer(player, out ulong playerId, out _))
        {
            return;
        }

        if (FindSharedButton(screen) is { } sharedButton &&
            sharedButton.RemoteSelectedPlayers.Contains(playerId))
        {
            sharedButton.OnRemotePlayerDeselected(playerId);
        }
    }

    private static ulong GetLocalPlayerId(
        NCharacterSelectScreen screen)
    {
        object localPlayer = AccessTools.Property(
                screen.Lobby.GetType(),
                "LocalPlayer")?
            .GetValue(screen.Lobby);
        return TryReadPlayer(localPlayer, out ulong playerId, out _)
            ? playerId
            : ulong.MaxValue;
    }

    private static bool TryReadPlayer(
        object player,
        out ulong playerId,
        out CharacterModel character)
    {
        playerId = 0;
        character = null;
        if (player == null)
        {
            return false;
        }

        Type playerType = player.GetType();
        object idValue = AccessTools.Field(playerType, "id")?
            .GetValue(player);
        character = AccessTools.Field(playerType, "character")?
            .GetValue(player) as CharacterModel;
        if (idValue == null)
        {
            return false;
        }

        playerId = Convert.ToUInt64(idValue);
        return true;
    }

    private static void SelectVariant(
        NCharacterSelectScreen screen,
        HbrgoSakiVariant variant)
    {
        NCharacterSelectButton sharedButton = FindSharedButton(screen);
        if (sharedButton == null)
        {
            return;
        }

        bool selectedSharedButton = !sharedButton.IsSelected;
        if (selectedSharedButton)
        {
            sharedButton.Select();
        }

        CharacterModel character =
            HbrgoSakiVariantPreference.GetCharacter(variant);
        if (variant == HbrgoSakiVariant.ThirtyA ||
            !selectedSharedButton)
        {
            screen.SelectCharacterCompat(sharedButton, character);
        }
    }

    private static NCharacterSelectButton FindSharedButton(
        NCharacterSelectScreen screen)
    {
        CharacterModel sharedCharacter =
            HbrgoSakiVariantPreference.GetCharacter(
                HbrgoSakiVariant.ThirtyOneO);
        return screen
            .GetNode<Control>("CharSelectButtons/ButtonContainer")
            .GetChildren()
            .OfType<NCharacterSelectButton>()
            .FirstOrDefault(button => button.Character == sharedCharacter);
    }

    private static void Refresh(SelectorState state)
    {
        bool unlocked = HbrgoSakiVariantPreference.IsThirtyAUnlocked();
        if (state.SelectedVariant == HbrgoSakiVariant.ThirtyA)
        {
            state.Button.Text = "切换至 丰川祥子（31O）";
            state.Button.Disabled = false;
            state.Button.TooltipText = state.Button.Text;
            return;
        }

        state.Button.Text = unlocked
            ? "切换至 丰川祥子（30A）"
            : "丰川祥子（30A）：31O解锁骨龙后解锁";
        state.Button.Disabled = !unlocked;
        state.Button.TooltipText = state.Button.Text;
    }

    private sealed class SelectorState
    {
        internal SelectorState(Button button)
        {
            Button = button;
            SelectedVariant = HbrgoSakiVariant.ThirtyOneO;
        }

        internal Button Button { get; }

        internal HbrgoSakiVariant SelectedVariant { get; set; }
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacters), MethodType.Getter)]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.content")]
[HarmonyPriority(Priority.Last - 10)]
internal static class CharacterCatalogHotaruNormalizationPatch
{
    // BaseLib adds its characters in a pass-through postfix. Use the same
    // return stage so normalization runs after that final catalog append.
    private static IEnumerable<CharacterModel> Postfix(IEnumerable<CharacterModel> __result) =>
        HbrgoSakiCharacterSelectVariantSelector.NormalizeCharacterCatalog(__result);
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))]
[HarmonyAfter(
    "BaseLib",
    "com.ritsukage.sts2-RitsuLib.framework-character-assets")]
[HarmonyPriority(Priority.Last)]
internal static class CharacterSelectScreenReadyVariantPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        HbrgoSakiCharacterSelectVariantSelector.Attach(__instance);
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.OnSubmenuOpened))]
[HarmonyPriority(Priority.Last)]
internal static class CharacterSelectScreenOpenVariantPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        HbrgoSakiCharacterSelectVariantSelector.RestorePreferredVariant(
            __instance);
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.SelectCharacter))]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-character-assets")]
[HarmonyPriority(Priority.Last)]
internal static class CharacterSelectScreenSelectedVariantPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        object[] __args)
    {
        HbrgoSakiCharacterSelectVariantSelector.OnCharacterSelected(
            __instance,
            NativeUiApiCompatibility.SelectedCharacterFromArguments(__args));
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

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.PlayerConnected))]
internal static class CharacterSelectRemoteConnectedVariantPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        object player)
    {
        HbrgoSakiCharacterSelectVariantSelector
            .RefreshRemoteSharedSelection(__instance, player);
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.PlayerChanged))]
internal static class CharacterSelectRemoteChangedVariantPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        object player)
    {
        HbrgoSakiCharacterSelectVariantSelector
            .RefreshRemoteSharedSelection(__instance, player);
    }
}

[HarmonyPatch(
    typeof(NCharacterSelectScreen),
    nameof(NCharacterSelectScreen.RemotePlayerDisconnected))]
internal static class CharacterSelectRemoteDisconnectedVariantPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        object player)
    {
        HbrgoSakiCharacterSelectVariantSelector
            .RemoveRemoteSharedSelection(__instance, player);
    }
}
