# Delayed native damage variables at startup

This narrow host first registers the real native Outbreak through `ModelDb.Inject`, proves a second native constructor is rejected, then performs the **first** production `Contains` call (and therefore the real static initializer). It repeats the actual `Build`, `Contains` and `TryGet` paths and verifies the canonical object and database count do not change.

Stable Outbreak exposes `OutbreakPower`, which remains a future-damage variable. Beta Outbreak exposes `PoisonPower`, which must remain a stack count. The host reads the actual version-specific canonical variables and checks both positive and negative catalog entries. It does not patch ModelDb or constructors, install the whole mod, alter native assembly bytes, or invoke Godot.

Run with the phase runner, using an explicit immutable candidate:

```powershell
& outputs/startup-1131-fix-20261115/run-suite.ps1 -Suite startup-delayed-variables -Profile stable -Candidate <stable-candidate.dll>
& outputs/startup-1131-fix-20261115/run-suite.ps1 -Suite startup-delayed-variables -Profile beta -Candidate <beta-candidate.dll>
# Reproduce the reported failure using the actual previous delivered DLL:
& outputs/startup-1131-fix-20261115/run-suite.ps1 -Suite startup-delayed-variables -Profile beta -Candidate <old-candidate.dll> -CheckArgs @('--expect-startup-collision') -ReportName startup-delayed-variables-negative-report.json
```

Run profiles sequentially because they share this project's intermediate directory. Both reports bind the candidate and unmodified native DLL hashes. This verifies the startup scanner independently; actual game initialization and rendering remain in-game acceptance checks.
