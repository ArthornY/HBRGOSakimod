# Enemy skill and actor regression

Build the mod with `SkipModOutputCopy=true`, then build this fixture with
`dotnet build validation/hbr-enemy-skills/Checks.csproj -c Release`. Supply
`-p:GodotSharpAssemblyPath=<GodotSharp.dll path>` if its NuGet root is unavailable.
Run the resulting `Checks.dll` with the same four arguments as the sibling
enemy-resource fixture: built mod DLL, expected SHA256, runtime dependency
directory containing `sts2.dll`, and matching GodotSharp DLL path. The entry
point checks the hash before loading the mod; `report.json` records that hash
and all assertion names.

The fixture uses actual saved manual progress, native Player/Creature models
and stat accessors. It verifies that enemies do not expose player six stats,
that their map parameter E is independent of player stat allocations, and that
players, phantoms and cannons retain their existing inheritance. It exercises
the three explicitly assigned RedCrimsonBoss ray/beam moves and the fixed
vitality fallback, including an additional living ally and an unrelated monster
with the same move name. Endpoint cases check the incoming maximum and 1 floor;
legacy mode, Unpowered and the STS enemy block support bridge have separate cases.

Preview calculations enter actual native `Hook.ModifyDamage`, then the
production `ModifyDamageInternal` replacement and complete common pipeline.
Actual calculations enter the production StrongestShield wrapper, which marks
the native Hook call as actual, and reach the same pipeline. This checks numeric
intent/actual agreement for each selected move. Rendering, displayed rounding,
enemy AI execution and real resource mutation are outside the fixture.

The canonical `CreatureCmd.Damage` method has the real production critical
scope prefix/postfix/finalizer installed. Its terminal HP work is replaced with
an asynchronous driver that consumes targets once and calls the production
actual wrapper. A separate Harmony owner explicitly orders this controlled
terminal after the production prefix. Tests verify no early target enumeration,
no enemy critical scope/RNG usage, lazy player sampling, sample reuse, nested
command isolation, and cleanup after a controlled fault inside the actual
wrapper. The native RNG itself is used; no specific lucky/unlucky roll is assumed.

Global hook-listener enumeration returns an empty collection, and logging and
Godot StringName construction are controlled leaves. Thus these checks do not
validate every modifier combination, UI preview context, a complete native
damage task, replay/save restoration or original per-enemy skill data. No Godot
process, real game save/config IO or packaging is performed.
