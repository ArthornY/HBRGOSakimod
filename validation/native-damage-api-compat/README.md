# Native damage command compatibility

The stable native command owns an async `IEnumerable<Creature>` body. In 0.113,
the eight-parameter Creature overload forwards to an async target-data overload;
native `AttackCommand.Execute` also calls that leaf directly. This fixture checks
the production selection and adapter against the actual game assembly.

Build once against stable references, then run in a fresh process per profile:

```text
dotnet build validation/native-damage-api-compat/Checks.csproj -c Release
dotnet validation/native-damage-api-compat/bin/Release/net9.0/Checks.dll MOD SHA PROFILE GODOT REPORT
```

Use an explicitly verified telemetry-isolated beta test profile. Every profile
file and the candidate DLL are checked against their recorded SHA-256. No game
assembly is modified by this fixture.

Checks cover the shared leaf/MoveNext selected by all seventeen production
patches, the three real IL transpilers, absence of a beta-only target-data TypeRef,
stable target pass-through, native duplicate/VFX metadata, target filtering and
redirection, unchanged caller records, one-shot enumeration, unchanged damage
arguments and one boundary for direct leaf and Creature-wrapper dispatch.

The Harmony dispatch probe returns before the native damage body. These checks
do not claim numerical damage, complete effect-scope lifetimes, combat/save
flows, actual visuals, networking, Godot rendering or an in-game run. Existing
effect-skill, damage-cap, phantom/cannon and stat suites remain necessary.
