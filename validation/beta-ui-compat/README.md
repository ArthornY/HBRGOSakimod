# Stable and beta UI compatibility

Run one candidate against each prepared `outputs/beta-compat/profiles` directory:

```powershell
dotnet build validation/beta-ui-compat/Checks.csproj -c Release
dotnet validation/beta-ui-compat/bin/Release/net9.0/Checks.dll `
  '<candidate DLL>' '<candidate SHA256>' '<profile directory>' '<report.json>'
```

The fixture validates every profile DLL hash before loading it. It exercises the
actual cached confirmation action adapter (`accept` on stable, `confirm` on beta)
and select action, including repeated presses. A controlled native input leaf
substitutes for Godot InputMap, so no physical key mapping is hardcoded by the
production helper. Potion removal state runs through the actual field reader or
property getter on an uninitialized managed popup object.

Exactly one deck cleanup patch must be enabled. Its real Harmony patch and the
native derived cleanup body execute against actual `CardPile` event delegates;
the stable base node exit is the only isolated lifecycle leaf. Beta non-predelete
notifications preserve listeners, while predelete releases the custom refresh
and native completion listeners without removing unrelated callbacks. Metadata
checks reject the removed direct member references and per-event/frame reflection.

No Godot process, rendered node, complete beta installation or game session is
claimed. The beta profile's verified test-only telemetry initializer suppression
is documented in its profile manifest. The fixture does not package or upload.
