using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal static class CharacterSelectSyncState
{
    private static CharacterModel selectedStandardCharacter;

    internal static CharacterModel SelectedStandardCharacter => selectedStandardCharacter;

    internal static void Set(CharacterModel character)
    {
        selectedStandardCharacter = character;
    }

    internal static void Clear()
    {
        selectedStandardCharacter = null;
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
    private static void Postfix(NCharacterSelectButton charSelectButton, CharacterModel characterModel)
    {
        if (charSelectButton.IsRandom)
        {
            CharacterSelectSyncState.Clear();
            return;
        }

        if (charSelectButton.IsLocked)
        {
            return;
        }

        CharacterSelectSyncState.Set(characterModel);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "OnEmbarkPressed")]
internal static class CharacterSelectScreenEmbarkPatch
{
    private static readonly FieldInfo SelectedButtonField = AccessTools.Field(
        typeof(NCharacterSelectScreen),
        "_selectedButton");

    private static readonly FieldInfo LobbyField = AccessTools.Field(
        typeof(NCharacterSelectScreen),
        "_lobby");

    private static void Prefix(NCharacterSelectScreen __instance)
    {
        NCharacterSelectButton selectedButton = (NCharacterSelectButton)SelectedButtonField.GetValue(__instance);
        StartRunLobby lobby = (StartRunLobby)LobbyField.GetValue(__instance);
        if (selectedButton == null || selectedButton.IsLocked || lobby == null)
        {
            return;
        }

        if (selectedButton.IsRandom)
        {
            CharacterSelectSyncState.Clear();
            return;
        }

        CharacterModel selectedCharacter = selectedButton.Character;
        CharacterSelectSyncState.Set(selectedCharacter);
        if (lobby.LocalPlayer.character.Id != selectedCharacter.Id)
        {
            lobby.SetLocalCharacter(selectedCharacter);
        }
    }
}

[HarmonyPatch(typeof(StartRunLobby), "BeginRunLocally")]
internal static class StartRunLobbyBeginRunLocallyPatch
{
    private static void Prefix(StartRunLobby __instance)
    {
        CharacterModel selectedCharacter = CharacterSelectSyncState.SelectedStandardCharacter;
        if (selectedCharacter == null || __instance.NetService.Type != NetGameType.Singleplayer)
        {
            return;
        }

        if (__instance.LocalPlayer.character.Id != selectedCharacter.Id)
        {
            __instance.SetLocalCharacter(selectedCharacter);
        }
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
internal static class NGameStartNewSingleplayerRunPatch
{
    private static void Prefix(ref CharacterModel character, GameMode gameMode)
    {
        CharacterModel selectedCharacter = CharacterSelectSyncState.SelectedStandardCharacter;
        if (selectedCharacter == null || gameMode != GameMode.Standard)
        {
            return;
        }

        character = selectedCharacter;
        CharacterSelectSyncState.Clear();
    }
}
