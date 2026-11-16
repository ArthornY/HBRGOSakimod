# Card-face presentation validation

The main `HBRGOSakiMod/localization/zhs/cards.json` is the wording authority. The stage card fragments are synchronized to it, including nested `cards` bundles, so later merges cannot restore the earlier verbose card faces.

The host scans all authored descriptions and garden upgrade descriptions for card-face-only language rules, then formats every concrete mod card through native HBR and legacy face/detail/hover paths. It covers ordinary upgrades, older garden sequential upgrades and every individual upgrade of the new garden cards. It checks live attribute changes/color and that inspect retains parenthesized percentages while ordinary faces omit them. All unique base and acquired upgrade effects are displayed in the card body. The duplicate custom Rules hover is removed; normal keyword and ability hovers remain.

Card arithmetic, power application, card legality and combat mechanics remain in their existing production paths. The new helper computes display-only percentages using the same formula as the application; hit counts query the native hook without consuming its powers. Charge Burst and Soul Finisher display their computed damage instead of a written multiplication expression. Existing framework slot/duration details remain in power hover tips.

Full Body Shot's read-only projection is compared with its actual post-debuff damage, including Artifact and the bow-form destruction grant modifier. Resource projections cover DP Soul Strike and Fubuki's base cost/compensation, Gift, Makeup and the known Never Fall zero-DP recovery. They do not predict unknown external-mod callbacks. Captured but closed projection contexts are rejected. No real powers, DP, RNG or rescue receipts change while rendering.

Run the stage `run-suite.ps1 -Suite card-face67 -Profile stable`, then beta with telemetry isolation. Reports include exact candidate SHA. Texture/wait/preview sinks are isolated. No Godot rendering, clipping/layout or mouse/controller session is claimed.

The 73–78 cards have both individual-upgrade checks and native sequential upgrades in HBR/legacy modes. Upgrade formatting must supply secondary attack variables before the base card's `UpgradeEffects` is assembled; calling the card's extra-argument method recursively is not a valid substitute. Card and upgrade rows resolve live percentages and preserve their complete effect text.
