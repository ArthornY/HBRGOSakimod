# Magic-sword tooltip lifetime

The production-linked state checks reproduce mouse click focus surviving pointer exit, later turns and the disabled state, and verify real keyboard/gamepad ownership can still show the tooltip. They also cover pointer ownership surviving focus exit, pointer takeover of old navigation focus, and hidden/tree-exit reset.

The optional compiled candidate argument adds narrow IL checks of the actual node's signal subscriptions, event classification, ownership-aware refresh, hidden/blocked cleanup, unchanged-tooltip reuse and preserved accept/hand navigation. No Godot objects or engine are started; this does not establish actual OS pointer geometry, signal delivery/order or rendered positioning.

```powershell
dotnet build validation/magic-sword-hover/Checks.csproj -c Release
dotnet validation/magic-sword-hover/bin/Release/net9.0/Checks.dll <candidate-dll> <sha256> <report-json>
```

Use `- -` instead of the first two arguments for state-only checks. Reports belong to `outputs/magic-hover-elite-load-20261115`; no package, installed files or delivered artifacts are changed.

Final candidate `B4045B5F12D8025B9C8C6BCA6447FCC4A8B31DD54E1746AA84F7C1321DF2DB2F` passed all 37 checks with host exit 0. The final report is `outputs/magic-hover-elite-load-20261115/hover-accepted-report.json`. This includes Shift/letter-key negative cases and actual compiled node input/signal/cleanup connections, while retaining the no-engine boundary above.

The earlier AC3 and 3605 reports remain preserved as superseded evidence; only the B404-bound accepted reports are current.
