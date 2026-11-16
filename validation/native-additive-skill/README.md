# Audited native additive skill damage

This fixture links the established `hbr-effect-skill-damage` and `hbr-stat-powers` hosts. It executes real native additive hooks through the actual production `Hook.ModifyDamageInternal` replacement and StrongestShield calculation; it does not replace bonus formulas or native eligibility.

The explicit registry retains MiniatureCannon and adds AccuracyPower, CalcifyPower, LeadershipPower, PhantomBladesPower, TaintedPower, FakeStrikeDummy, MysticLighter and the separate mod PhantomPollutionPower. Native counters/variables remain raw. Six raw damage units represent 100% skill power; a positive eligible bonus uses the same hit's actor attributes, target and critical sample before the existing multipliers/caps.

Coverage includes every registered source, real first-Shiv history, native tag/enchantment/Osty exclusions, hero/enemy/phantom directions, zero base/amount, signed nonpositive passthrough, legacy and Unpowered behavior, the independent component minimum, critical RNG, a final native target cap, and unchanged Tainted versus PhantomPollution removal timing. The real VitalSpark card-entered and card-played hooks run through native CardCmd.Afflict and PowerCmd.Apply while preserving three stacks.

Only the shared host's terminal HP writes, native logging/StringName and waiting leaves are controlled. This is source/native logic verification, not a Godot UI, complete game turn, multiplayer or save acceptance test.

```powershell
$candidate = 'D:/absolute/frozen/HBRGOSakiMod.dll'
$profile = 'D:/absolute/native-profile'
$sha = (Get-FileHash -LiteralPath $candidate).Hash
$out = 'D:/absolute/fixture-output'
dotnet build validation/native-additive-skill/Checks.csproj --nologo `
  -p:ModAssemblyPath=$candidate -p:NativeAssemblyPath="$profile/sts2.dll" `
  -p:NativeRitsuAssemblyPath="$profile/STS2-RitsuLib.dll" -p:BaseLibAssemblyPath="$profile/BaseLib.dll" `
  -p:HarmonyAssemblyPath="$profile/0Harmony.dll" `
  -p:OutputPath="$out/bin/" -p:BaseIntermediateOutputPath=obj/
dotnet "$out/bin/Checks.dll" $candidate $sha $profile `
  C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0/GodotSharp.dll "$out/report.json"
```

For beta, build with `-p:BetaNativeChecks=true` and separate outputs. Use `--isolate-telemetry` only for the beta profile's exact verified telemetry module initializer. The profile source remains unchanged and the report records the isolated copy/proof.

The profile must include `profile-manifest.json`; the bootstrap verifies every copied dependency SHA before loading it and binds the final report to the exact candidate and native SHA. Beta telemetry isolation uses the linked established `hotaru-overcharge/TelemetryIsolation.cs` proof, preserving all native gameplay instructions. In beta, the real first-Shiv predicate reads `CardPlay.Player`; the fixture initializes that optional native field as well as the card owner. Stable derives the same qualification from `Card.Owner`.

When building the staged source directly, pass `-p:RepositoryRoot=D:/stsmod/HBRGOSakiMod` for the accepted shared host. Both fixture builds retain its 23 existing nullable warnings and add no new warnings. Main-mod strict compilation is a separate check.

`--tainted-only` executes the same first Tainted assertion, with the same expected converted result, against an old DLL. The old candidate must fail that assertion while the report preserves its real native raw result and literal-plus-three damage. It is not an alternative passing expectation and must not be reported as fixed-candidate validation.
