# Fate fragment linked reward completion

This is a narrow managed regression host for `HbrFateFragmentRewardUiPatch`. It runs the same explicit stable mod DLL with the untouched stable 0.107.1 and beta 0.113.1 runtime profiles. Bootstrap verifies the candidate SHA and all profile-manifest file hashes before loading assemblies. There is no telemetry-isolation switch or game-engine initialization.

The 77 checks per runtime cover:

- Actual Harmony installation on native `NLinkedRewardSet.Reload`, its unique zero-argument factory replacement, and unchanged remaining IL.
- Actual managed `Callable.From<NRewardButton>` construction, matching the native child signal argument, shared per-node completion receipts, and unchanged ordinary callbacks/groups.
- Actual Ritsu `LinkedRewardSetOnSelectPatch` and native `Reward.SelectUnsynchronized`: each of the three physical selections, parent/child success flags, sibling skip/removal, failed/throwing claim and retry, and ordinary TakeAll behavior.
- Parent-success-only completion, notification after ChooseOne has cleared its children, duplicate and concurrent completion suppression, and native group-signal argument/one-emission IL contracts.

Only the fragment's claim `OnSelect` result is controlled; this host does not change or test claim/network transactions. Unrelated hook listeners, logging and the `StringName` native constructor are isolated leaves. No `Callable.Call`, `EmitSignal`, reward screen, Godot rendering or multiplayer transport is executed. Typed callable creation and backend selection are real production/native code, not mocked equivalents.

The released DCEF candidate is a separate four-check negative control in each runtime. Its actual zero-argument Callable delegate rejects one argument with `TargetParameterCountException` before the callback body. This is a CLR delegate-arity repro of the native signal mismatch, not a claim that Godot dispatch ran in the host.

Compiled hosts and logs live in `outputs/fate-fragment-reward-stall-1131-20261115/{stable,beta}/`. Rebind without rebuilding the host:

```powershell
& ./outputs/fate-fragment-reward-stall-1131-20261115/rebind-reward-ui.ps1 `
  -Candidate <absolute-final-stable-DLL> -ExpectedSha256 <SHA256>
```

The runner emits `reward-ui-final-index.json` and each runtime's `reward-ui-final-report.json`, retaining process exits and loaded candidate/native hashes. Earlier AE76 reports are explicitly preserved as `reward-ui-before-infection-*`; they are development evidence when later independent fixes change the final candidate.
