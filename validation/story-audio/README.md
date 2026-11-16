# Story audio contract checks

`dotnet run --project validation/story-audio/Checks.csproj -c Release -- data/story_audio.json`

Links actual production audio library/envelope without Godot. Covers typed cue/path validation, duplicate/unknown JSON, stereo/intro/sample loop metadata, native squared volume curves, fade replacement, instant Ctrl/rewind and fade-out completion. The production AS_Recollection_Coming manifest is also parsed. 51 checks; no audio output, FMOD/SoundPack lease integration, real user settings or engine rendering is claimed.
