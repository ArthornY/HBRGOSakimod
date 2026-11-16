# Targeted UI localization regression

Runs only the current localization cases while reusing the existing controlled combat fixture: six affected power families, SeaGlass's exact Hotaru key, native energy formatting, actual garden upgrade/rule descriptions, LoveCharlotta's effective X, and ChargeBurst's live charge preview. Actual canonical assertions remain enabled. Ritsu's namespaced IDs reproduce the missing keys observed in the game log.

The source-linked fixture uses the real native `Power.HoverTips`, `LocString` formatters and `Card.GetDescriptionForPile`. Godot resources, logger and energy image-path leaves are controlled; this is not a running game or visual acceptance test. The ChargeBurst cases check that changing current charge changes both face and detail, without treating its zero-charge base display as an error. No card damage logic is changed.

Run profiles sequentially because they share this project's obj directory:

```powershell
./outputs/magic-hover-elite-load-20261115/run-ui-localization-checks.ps1 -Profile stable -Candidate <dll> -ExpectedSha256 <sha> -ReportName accepted-report.json
./outputs/magic-hover-elite-load-20261115/run-ui-localization-checks.ps1 -Profile beta -Candidate <same-dll> -ExpectedSha256 <same-sha> -ReportName accepted-report.json
```

Final evidence is bound to the same source-only stable DLL `B4045B5F12D8025B9C8C6BCA6447FCC4A8B31DD54E1746AA84F7C1321DF2DB2F`: stable 0.107.1 and beta 0.113.1 each passed 65 checks with host exit 0. Reports are `outputs/magic-hover-elite-load-20261115/ui-localization/{stable,beta}/accepted-report.json`. Development and fixture failures remain separate and are not final evidence. The linked historical fixture's nullable warnings are assessed separately from the strict production build.

The earlier AC3 and 3605 reports remain preserved as superseded evidence; only the B404-bound accepted reports are current.
