using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HBRGOSakiMod.Core.Audio;

internal static class HbrgoSakiCharacterSelectVoice
{
    private const string VoicePath = "res://audio/hbrgo_saki/character_select_intro.ogg";

    private static readonly StringName SfxBus = new("SFX");

    private static AudioStream voice;
    private static AudioStreamPlayer player;

    internal static void Play(NCharacterSelectScreen screen)
    {
        Stop();

        voice ??= ResourceLoader.Load<AudioStream>(VoicePath);
        if (voice == null)
        {
            return;
        }

        AudioStreamPlayer newPlayer = new()
        {
            Name = "HbrgoSakiCharacterSelectVoice",
            Stream = voice,
            Bus = SfxBus
        };

        player = newPlayer;
        screen.AddChild(newPlayer);
        newPlayer.Finished += new Action(() => OnFinished(newPlayer));
        newPlayer.Play();
    }

    internal static void Stop()
    {
        AudioStreamPlayer currentPlayer = player;
        player = null;
        if (currentPlayer == null || !GodotObject.IsInstanceValid(currentPlayer))
        {
            return;
        }

        currentPlayer.Stop();
        currentPlayer.QueueFree();
    }

    private static void OnFinished(AudioStreamPlayer finishedPlayer)
    {
        if (player == finishedPlayer)
        {
            player = null;
        }

        if (GodotObject.IsInstanceValid(finishedPlayer))
        {
            finishedPlayer.QueueFree();
        }
    }
}
