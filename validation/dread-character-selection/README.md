# Dread character selection compatibility checks

This native managed host reproduces the actual hidden-character failure: unfiltered Dread catalog → real Ritsu RandomEligible scope during native Button.Init → hidden legacy Saki becomes locked → native Dread.SelectCharacter disables its confirm button. Visible 31O and Hotaru retain their genuine unlocked state and update the actual StartRunLobby.

The repaired native InitCharacterButtons executes its production catalog call-site transpiler. Tests check one shared 31O button, one Hotaru, no legacy/30A standalone entries, independent rotating control, real unlocked selection, and actual lobby character mutation. Preference tests exercise locked/unlocked 30A, shared button restoration including a native-body exception, rotation checkbox identity, actual next-rotation catalog behavior, 30A starting-run alias and stale legacy open preference without stealing vanilla selection.

Five real zhs short-title keys load from the production characters table. Stable 0.107.1 runs the same host and verifies all optional Dread patches safely skip absent native members.

Final candidate `1E52A2999CDB1184E681FD2FBEF817AB11E15B0BA69025308B26711EEFEE1D4A` passes 31 checks on original beta 0.113.1 and seven on stable 0.107.1, using the same stable-built DLL for both profiles. Both hosts build with zero warnings/errors. Final reports are `outputs/dread-mode-1131-20261115/validation/{profile}/final-report.json`, with explicit hash bindings in `owned-final-index-read_req28.json`. The earlier `703EF12C…` development reports remain archived and do not count as final evidence.

Controlled leaves replace Godot scene instantiation/AddChild, texture/geometry/focus setters, confirm animation, unrelated background/chain/records UI, and persisted profile preference reads. Actual native Button.Init, UnlockState.Characters enumeration, real Ritsu Prefix/Finalizer and catalog postfix, Dread SelectCharacter, and StartRunLobby.SetLocalCharacter/ChangeCharacter run. The normal scene opening body is isolated while its production open postfix and shared Select delegate run. All native lock decisions and menu/state transitions remain real. No Godot engine, rendering, audio, network/save or full run launch is performed.

From the repository root, run the stage wrapper with an explicit immutable candidate:

```powershell
& outputs/dread-mode-1131-20261115/run-native-checks.ps1 -Profile stable -Candidate <DLL> -ExpectedSha256 <SHA256>
& outputs/dread-mode-1131-20261115/run-native-checks.ps1 -Profile beta -Candidate <DLL> -ExpectedSha256 <SHA256>
```

Only this validation project is built. Run the profiles sequentially because they share project intermediate output. Reports record both exact candidate and unmodified native profile hashes. Earlier fixture-development failures and the old DLL negative control are not final passing evidence.
