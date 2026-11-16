# Hand and inspect card hover layout regression

The fixture executes both production geometry helpers and installs the actual hand postfix and inspect transpiler against each original runtime. Hand and inspect tips now use one native-width scroll column; long mixed-language titles and descriptions wrap without text trimming. All original entries, lifetime ownership and wheel/click routing remain in their existing paths.

The regression covers the reported card AABB, safe viewport bounds, left/right and above/below fallbacks, narrow windows, translated coordinates, invalid inputs and repeated calls. Forty compact/full-style rebuilds vary the native tip root's translation and scale while keeping the card and viewport fixed. The actual production `InParentFrame` helper cancels each root transform, preserving the same absolute anchor and width. An old root-local placement is retained as a negative control that genuinely shifts. Inspection reserves its full card/navigation region.

Real Harmony installation verifies the native hand and inspect call sites. Native `MegaLabel` / `MegaRichTextLabel` inheritance confirms the production wrapping casts. Source wiring checks cover root `ItemRectChanged` subscription/cleanup, stable parent-frame use, no stale-overflow activation gate, no width growth from previously assigned sizes, preserved native entries and unmodified text content. No per-frame tree scan was added; the hand's existing animation geometry check remains, and inspect layout uses deferred resize/item-rect events.

Final reports bind the same stable release DLL to original stable 0.107.1 and beta 0.113.1 profiles. Both pass 432 assertions, with zero-warning/error host builds. Candidate and complete profile hashes are verified by the existing bootstrap. No telemetry-isolation flag is used.

Boundary: pure Godot rectangle/transform calculations, real native method metadata/Harmony installation and production source wiring. No engine node, actual Shift input, font shaping, rendering, scroll/wheel event or scene cleanup is executed. These checks establish deterministic layout inputs and transforms, not an in-game visual result. The main source build and packaging are handled separately by the parent task.

Exact report paths/hashes: `outputs/dread-mode-1131-20261115/garden-and-hover-final-index.json`.
