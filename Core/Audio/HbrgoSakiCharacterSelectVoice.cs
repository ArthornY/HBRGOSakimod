using STS2RitsuLib.Audio;

namespace HBRGOSakiMod.Core.Audio;

internal static class HbrgoSakiCharacterSelectVoice
{
    private const string VoicePath = "res://audio/hbrgo_saki/character_select_intro.ogg";
    private const string VoiceChannel = "HBRGOSakiMod.CharacterSelectVoice";

    private static IAudioHandle player;

    internal static void Play()
    {
        Stop();

        AudioPlayResult result = GameFmod.Playback.PlayOneShot(
            AudioSource.ResourceFile(VoicePath),
            new AudioPlaybackOptions
            {
                Scope = AudioLifecycleScope.Screen,
                AllowFadeOutOnStop = false,
                Routing = new AudioRoutingOptions
                {
                    Channel = VoiceChannel,
                    ChannelMode = AudioChannelMode.ReplaceExisting,
                    AllowFadeOutOnReplace = false
                }
            });
        if (result.Succeeded)
        {
            player = result.Handle;
        }
    }

    internal static void Stop()
    {
        IAudioHandle currentPlayer = player;
        player = null;
        if (currentPlayer == null)
        {
            return;
        }

        currentPlayer.TryStop(allowFadeOut: false);
        currentPlayer.Dispose();
    }
}
