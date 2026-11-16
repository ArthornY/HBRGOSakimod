# Memory garden display regression

The production fix gives every unmet garden entry its own mutable, unowned display copy. Native card visual paths require mutable state even when the garden later masks the face. Canonical models and the existing ghost selection/inspection policy are unchanged.

The standalone native host executes the actual `GetUnmetCards` and `GetChoices` helpers. It first reproduces the old invalid canonical input through native `CardModel.Owner`, which throws `CanonicalModelException`. It then reads the real cost, title, dynamic-variable preview and formatted description paths for all 56 ghost cards and for the screenshot's 8 met / 48 unmet mix. It executes the real screen selection and inspection callbacks with managed native holders to verify ghost guards, and checks canonical snapshots, the actual owned permanent Ruka card, deck identities, encounters, upgrade points and manual ledgers before/after. The owned Ruka path avoids initializing unrelated 30A collection-capability services in this narrow host.

The portrait extension reads the actual `PortraitPath` / `BetaPortraitPath` getters of all 42 mapped models and checks the 41 source PNGs against `fixtures/portraits.json`. These portraits use original HBR Encount, Anger or Chase cut-ins in the existing 300×300 garden frame; Aina uses the original battle MiniCutIn03. The fixture records accepted image hashes and character assignments independently of the production dictionary. This host does not decode or render images, and no AI illustration is used in this portrait set.

Runtime profiles are the original stable 0.107.1 and beta 0.113.1 assemblies. The bootstrap verifies every profile-manifest hash and the candidate hash, then loads the same release DLL against each runtime. There is no telemetry rewriting or native assembly mutation. The linked older effect/stat fixture sources emit 23 existing nullable warnings; this is an ordinary host build with zero errors, not the main strict source-build result.

Native `CurrentTarget`'s getter does not itself assert mutability; native `Owner` does. Both getters are exercised on the fixed previews. The host leaves global canonical assertions enabled. Engine-only string-name/log/icon terminals are isolated. No Godot nodes are initialized or rendered, no full `NCard.UpdateVisuals` engine call is claimed, and mouse input, text shaping, texture appearance and actual scrolling remain in-game acceptance checks.

Original preview/layout-fix reports: `outputs/dread-mode-1131-20261115/garden-and-hover-final-index.json`. Native-portrait release reports: `outputs/package-native-cutins-1400-20261115`. Original development failures remain separate; they were fixture setup defects (blank player state, holder identity and asset-root paths), not relaxed production assertions.


## Aoi portrait regression (2026-11-16)

PeacefulMemoriesHbrgoSaki now explicitly uses `memory_garden/aoi.png` and the existing white cut-in backing. The fixture covers 43 models / 42 characters and requires Aoi to remain present. Aoi uses the original EAoiDefaultAdvCutInAnger prefab, following the existing Encount > Anger > Chase selection policy, with the same 300px frame and original EyeOpen fit. No generated replacement art is used. The old DLL must fail the new actual portrait-getter check.
