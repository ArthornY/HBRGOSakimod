# Satsuki shop UI source checks

Run `dotnet run --project validation/hbr-shop55-layout/Checks.csproj -c Release -- <repository> <report.json>`.

The host runs the production pure price-presentation and comparison-layout functions, with320–1100px logical page widths and360–1180px heights. It checks equal readable columns, padding, contained panels, nonoverlap, preserved quoted prices and original-price strike markup. Source-wiring checks cover the actual RichTextLabel type, policy/raw-act cache invalidation, the authoritative transaction IDs, real equipment text helpers, fixed footer choices, and native focus/scroll navigation.

Current result:101 checks. These are pure geometry and source-wiring checks; no Godot process was launched and no rendered mouse/keyboard/controller session is claimed. Production UI file hashes are recorded in `outputs/31c-mari55-20261115/shop-ui-source-manifest.json`. The root's final build and integration-audit hashes are recorded separately.
