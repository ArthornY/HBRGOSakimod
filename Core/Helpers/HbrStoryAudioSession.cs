using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;

namespace HBRGOSakiMod.Core.Helpers;

// A reader-owned, local audio scope. It never changes SettingsSave, the run's
// music selection/Progress, an event, or a network action.
internal sealed class HbrStoryAudioSession : IDisposable
{
    private sealed class Channel
    {
        internal AudioStreamPlayer Player;
        internal bool Music;
        internal HbrStoryAudioEnvelope Envelope;
        internal float Gain => Envelope.Gain;
        internal bool Retiring;
    }
    private static HbrStoryAudioLibrary _library;
    private static bool _libraryRead;
    private static readonly HashSet<HbrStoryAudioSession> MusicLeases = new();
    private static readonly HashSet<string> Warnings = new(StringComparer.Ordinal);
    private readonly Node _host;
    private readonly List<Channel> _channels = new();
    private readonly string _voiceBus = "HbrStoryVoice_" + Guid.NewGuid().ToString("N");
    private AudioEffectCapture _capture;
    private AudioEffectReverb _reverb;
    private Channel _bgm, _env, _voice;
    private string _bgmCue, _envCue;
    private TaskCompletionSource<bool> _voiceCompletion;
    private bool _disposed, _covered, _closing;
    private int _voiceBusIndex = -1;
    private float _amplitude;

    internal HbrStoryAudioSession(Node host) { _host = host ?? throw new ArgumentNullException(nameof(host)); }
    internal static bool HasMusicLease => MusicLeases.Count != 0;
    internal float VoiceAmplitude => IsVoicePlaying && !_covered ? _amplitude : 0;
    internal bool IsVoicePlaying => Usable(_voice?.Player) && _voice.Player.Playing;

    internal void Bgm(string cue, float fadeOut = .5f, float fadeIn = .5f, bool instant = false, float volume = 1)
    {
        if (_disposed || _closing) return;
        if (_bgmCue == cue && Usable(_bgm?.Player)) { SetGain(_bgm, Math.Clamp(volume, 0, 2), instant ? 0 : fadeIn); return; }
        AudioStream stream = Load(cue, true);
        if (stream == null) return;
        Retire(_bgm, instant ? 0 : fadeOut);
        _bgmCue = cue;
        _bgm = Start(stream, true, "Master", Math.Clamp(volume, 0, 2), instant ? 0 : fadeIn);
        RefreshMusicLease();
    }
    internal void BgmStop(float fadeOut = .5f, bool instant = false)
    {
        Retire(_bgm, instant ? 0 : fadeOut);
        _bgm = null; _bgmCue = null;
        RefreshMusicLease();
    }
    internal void Env(string cue, float fadeIn = .5f, bool instant = false, float volume = 1)
    {
        if (_disposed || _closing) return;
        if (_envCue == cue && Usable(_env?.Player)) { SetGain(_env, Math.Clamp(volume, 0, 2), instant ? 0 : fadeIn); return; }
        AudioStream stream = Load(cue, true);
        if (stream == null) return;
        Retire(_env, instant ? 0 : .5f);
        _envCue = cue;
        _env = Start(stream, false, "Master", Math.Clamp(volume, 0, 2), instant ? 0 : fadeIn);
    }
    internal void EnvStop(float fadeOut = .5f, bool instant = false)
    {
        Retire(_env, instant ? 0 : fadeOut);
        _env = null; _envCue = null;
    }
    internal void Se(string cue, float volume = 1)
    {
        if (_disposed || _closing || _covered) return;
        AudioStream stream = Load(cue, false);
        if (stream != null) Start(stream, false, "Master", Math.Clamp(volume, 0, 4));
    }
    internal async Task Voice(string cue, bool monologue, CancellationToken cancellationToken)
    {
        StopVoice();
        if (_disposed || _closing || cancellationToken.IsCancellationRequested || string.IsNullOrEmpty(cue)) return;
        AudioStream stream = Load(cue, false);
        if (stream == null) return;
        EnsureVoiceBus();
        _reverb.Wet = monologue ? .6f : 0;
        _capture.ClearBuffer();
        Channel channel = _voice = Start(stream, false, _voiceBus, 1);
        if (channel == null) return;
        TaskCompletionSource<bool> completed = _voiceCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Finished() => completed.TrySetResult(true);
        channel.Player.Finished += Finished;
        using var cancel = cancellationToken.Register(() => completed.TrySetCanceled(cancellationToken));
        try { await completed.Task; }
        catch (OperationCanceledException) { }
        finally
        {
            if (Usable(channel.Player)) channel.Player.Finished -= Finished;
            if (ReferenceEquals(_voice, channel)) StopVoice();
        }
    }
    internal void StopVoice()
    {
        Channel voice = _voice; _voice = null; _amplitude = 0;
        TaskCompletionSource<bool> completed = _voiceCompletion; _voiceCompletion = null;
        Remove(voice);
        completed?.TrySetResult(false);
    }

    private void EnsureVoiceBus()
    {
        if (_voiceBusIndex >= 0) return;
        AudioServer.AddBus();
        _voiceBusIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(_voiceBusIndex, _voiceBus);
        AudioServer.SetBusSend(_voiceBusIndex, AudioServer.GetBusIndex("Voice") >= 0 ? "Voice" : "Master");
        _capture = new AudioEffectCapture { BufferLength = .1f };
        _reverb = new AudioEffectReverb { Dry = 1, Wet = 0 };
        AudioServer.AddBusEffect(_voiceBusIndex, _capture);
        AudioServer.AddBusEffect(_voiceBusIndex, _reverb);
    }
    private Channel Start(AudioStream stream, bool music, string bus, float gain, float fadeIn = 0)
    {
        if (!Usable(_host)) return null;
        var channel = new Channel { Player = new AudioStreamPlayer { Stream = stream, Bus = bus }, Music = music,
            Envelope = new HbrStoryAudioEnvelope(fadeIn > 0 ? 0 : gain) };
        channel.Envelope.FadeTo(gain, fadeIn);
        _channels.Add(channel);
        _host.AddChild(channel.Player);
        ApplyVolume(channel);
        channel.Player.StreamPaused = _covered;
        channel.Player.Play();
        return channel;
    }
    private void Retire(Channel channel, float seconds)
    {
        if (channel == null) return;
        if (!Usable(channel.Player) || seconds <= 0) { Remove(channel); return; }
        channel.Retiring = true; channel.Envelope.FadeTo(0, seconds);
    }
    private void SetGain(Channel channel, float gain, float seconds)
    {
        channel.Envelope.FadeTo(gain, seconds);
        ApplyVolume(channel);
    }
    private void Remove(Channel channel)
    {
        if (channel == null) return;
        _channels.Remove(channel);
        if (Usable(channel.Player)) { channel.Player.Stop(); channel.Player.QueueFree(); }
    }
    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value);

    // Called by the local reader only while this scope is active. No global
    // polling or gameplay RNG; gains follow current sliders even during fades.
    internal void Process(double delta)
    {
        if (_disposed) return;
        if (!Usable(_host) || !_host.IsInsideTree()) { Dispose(); return; }
        foreach (Channel channel in _channels.ToArray())
        {
            if (!Usable(channel.Player)) { _channels.Remove(channel); continue; }
            if (!_covered)
            {
                bool finished = channel.Envelope.Tick(delta);
                if (finished && channel.Retiring) { Remove(channel); continue; }
            }
            ApplyVolume(channel);
            if (!channel.Player.Playing && !channel.Player.StreamPaused && !ReferenceEquals(channel, _voice)) Remove(channel);
        }
        if (_capture != null)
        {
            int available = Math.Min(_capture.GetFramesAvailable(), 8192);
            float peak = 0;
            foreach (Vector2 frame in _capture.GetBuffer(available)) peak = Math.Max(peak, Math.Max(Math.Abs(frame.X), Math.Abs(frame.Y)));
            _amplitude = Mathf.Lerp(_amplitude, Math.Clamp(peak * 4, 0, 1), .65f);
        }
        RefreshMusicLease();
    }
    private void ApplyVolume(Channel channel)
    {
        var settings = SaveManager.Instance?.SettingsSave;
        float master = settings?.VolumeMaster ?? .5f;
        float group = channel.Music ? settings?.VolumeBgm ?? .5f : settings?.VolumeSfx ?? .5f;
        float gain = HbrStoryAudioLibrary.EffectiveGain(channel.Gain, master, group);
        channel.Player.VolumeDb = gain > 0 ? Mathf.LinearToDb(gain) : -80;
    }
    internal void SetCovered(bool covered)
    {
        if (_disposed || _covered == covered) return;
        _covered = covered;
        foreach (Channel channel in _channels)
            if (Usable(channel.Player)) channel.Player.StreamPaused = covered;
        RefreshMusicLease();
    }
    private void RefreshMusicLease()
    {
        bool wants = !_disposed && !_covered && _channels.Any(channel => channel.Music && Usable(channel.Player));
        bool changed = wants ? MusicLeases.Add(this) : MusicLeases.Remove(this);
        if (changed) RestoreCurrentMusicVolume();
    }
    private static void RestoreCurrentMusicVolume()
    {
        // The Prefix below mutes native FMOD and SoundPack's tracked music
        // during a lease. Releasing uses today's setting, never a stale zero.
        NAudioManager.Instance?.SetBgmVol(SaveManager.Instance?.SettingsSave?.VolumeBgm ?? .5f);
    }
    internal async Task CloseAsync(float fadeOut = .5f)
    {
        if (_disposed) return;
        _closing = true; StopVoice();
        if (_covered || !Usable(_host) || !_host.IsInsideTree()) { Dispose(); return; }
        foreach (Channel channel in _channels.ToArray()) Retire(channel, fadeOut);
        var began = DateTime.UtcNow;
        try
        {
            while (!_disposed && _channels.Count > 0 && (DateTime.UtcNow - began).TotalSeconds < Math.Clamp(fadeOut, 0, 120) + .1)
            {
                await Task.Delay(16);
                Process(.016);
            }
        }
        finally { Dispose(); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopVoice();
        foreach (Channel channel in _channels.ToArray()) Remove(channel);
        _bgm = _env = null;
        if (MusicLeases.Remove(this)) RestoreCurrentMusicVolume();
        int index = AudioServer.GetBusIndex(_voiceBus);
        if (index >= 0) AudioServer.RemoveBus(index);
        _capture?.Dispose(); _reverb?.Dispose();
        _capture = null; _reverb = null;
    }

    private static AudioStream Load(string cue, bool loop)
    {
        if (!HbrStoryAudioLibrary.IsCueName(cue)) return null;
        try
        {
            HbrStoryAudioCue entry = null;
            ReadLibrary();
            _library?.Cues.TryGetValue(cue, out entry);
            string path = entry?.Path ?? "res://sounds/thirty_one_a_cutin/" + cue + ".ogg";
            if (!HbrStoryAudioLibrary.IsAudioPath(path) || !ResourceLoader.Exists(path)) { Warn(cue, "Missing story audio"); return null; }
            AudioStream resource = ResourceLoader.Load<AudioStream>(path);
            AudioStream stream = resource?.Duplicate() as AudioStream;
            if (stream is AudioStreamOggVorbis ogg)
            {
                if (entry is { LoopStart: not null, LoopEnd: not null } &&
                    (entry.SampleRate <= 0 || entry.LoopStart < 0 || entry.LoopEnd <= entry.LoopStart || entry.LoopEnd != entry.TotalSamples))
                { Warn(cue, "Invalid loop; imported OGG must end at its declared loop end"); return null; }
                ogg.Loop = loop;
                ogg.LoopOffset = loop && entry?.LoopStart is { } start ? start / (double)entry.SampleRate : 0;
            }
            return stream;
        }
        catch (Exception error) { Warn(cue, error.Message); return null; }
    }
    internal static string ResolveCueResource(string cue)
    {
        if (!HbrStoryAudioLibrary.IsCueName(cue)) return null;
        ReadLibrary();
        string path = _library?.Cues.TryGetValue(cue, out var entry) == true ? entry.Path :
            "res://sounds/thirty_one_a_cutin/" + cue + ".ogg";
        return HbrStoryAudioLibrary.IsAudioPath(path) ? path : null;
    }
    private static void ReadLibrary()
    {
        if (_libraryRead) return;
        _libraryRead = true;
        const string manifest = "res://data/story_audio.json";
        try { if (FileAccess.FileExists(manifest)) _library = HbrStoryAudioLibrary.Parse(FileAccess.GetFileAsString(manifest)); }
        catch (Exception error) { Warn("catalog", error.Message); }
    }
    private static void Warn(string cue, string message)
    {
        if (Warnings.Add(cue + "/" + message)) Log.Warn("[HBRGOSakiMod][StoryAudio] " + message + ": " + cue);
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class HbrStoryMusicLeasePatch
{
    private static void Prefix(ref float volume)
    {
        if (HbrStoryAudioSession.HasMusicLease) volume = 0;
    }
}
