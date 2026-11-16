# Fate dice (pure checks)

31O's fate dice and the magic sword switch (2026-11-09, design v3 in
`D:\stsmod\outputs\deck-rethink-31o-20261108\magic-sword-fate-dice-design.md`; player-facing notes in
`docs/hbr-fate-dice.md`). This suite compiles `Core/FateDice/HbrFateDiceCatalog.cs` and
`Core/FateDice/HbrFateDiceState.cs` on their own, without the game, and reads the zhs texts from the checkout.

It checks:

- The fifteen fragments: names, categories, rarities and exact texts; 狂骰 shows 700 under HBR numerics;
  prices 80/120/160; category names and rim colours.
- Slots and cracks: two slots, one more per boss up to five; a crack takes an empty slot, then pushes the
  last fitted fragment back to the bag; each crack beyond the slots is raw −3; a boss's new slot takes an
  overflow crack; 修骰子 removes one crack and leaves the fragments in the bag.
- Fitting: new fragments fill free slots, then the bag; no duplicates or unknown ids; fitting is locked in
  combat; kept swordheart is cut to the kept maximum.
- Rewards and merchants: a rolled set is claimed once; elites offer three common or uncommon fragments,
  bosses three rare ones, topped up from other rarities and never an owned one; a merchant sells one or two
  at its rarity's price, only in its own room, keeps a bought one listed as sold and never lists an owned
  one; the same room rolls the same set.
- The saved record: round trip, broken records, unknown and duplicate fragments, over-full sets; old saves
  read as empty slots for the act they are in.
- The zhs texts of the new swordheart, magic sword, form, keyword, reward and rest site rules, and no text
  left that describes the old swordheart rules.

The rules that need the game (swordheart from rolls, the critical range, crack penalties, the fragments'
roll effects, kept swordheart, the manual's commands) run in `hbr-effect-skill-damage` (`FateDiceChecks.cs`).

Run offline from this folder:

```bash
NUGET_PACKAGES='C:/Users/ROG/.nuget/packages' dotnet restore Checks.csproj --source 'D:/stsmod/Godot_v4.5.1-stable_mono_win64/GodotSharp/Tools/nupkgs' -p:NuGetAudit=false
dotnet build Checks.csproj --no-restore -nologo
dotnet ./bin/Debug/net9.0/Checks.dll ../..
```
