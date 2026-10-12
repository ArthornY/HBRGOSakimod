# HBR title UI sources

## Current cover supplied on 2026-10-01

`sakiko_title_cover_4k.png` is a byte-for-byte copy of the replacement supplied by the user:
`C:/Users/ROG/AppData/Local/Temp/codex-clipboard-c4d3d9e5-cd99-4d77-a176-e2d25d937635.png`.

- Dimensions: 3840 × 2160. Size: 5,939,011 bytes.
- SHA-256: `8C7BB1681E26853848795D268B56EA3EB18D7F4143D5BCD09FD98598A3988D20`.
- `NHbrTitleMenu` displays this full cover in the existing 1920 × 1080 reference canvas. No image editing, resizing, denoising or regeneration was applied.
- Clicking the start prompt hides the title controls and shows the original main-menu buttons at their original positions. The saved-run summary remains parented to its native button. Returning from a submenu preserves the opened menu state.
- The native `PatchNotesButton` remains in its original position on the cover and main menu. RitsuLib's independent entry below it retains its own layout, visibility synchronization and navigation. The cover is drawn behind native controls and no longer creates a proxy drawer.

The previous illustrations below remain as source/history references; they are not the active cover.

## Original Slay the Spire 2 title

`sts2_logo_static.png` is a byte-for-byte copy of the locally extracted original game texture:
`E:/stsmod/sts/images/ui/sts2_logo_static.png` (`res://images/ui/sts2_logo_static.png`).
Dimensions: 960 × 484, RGBA. Nonzero-alpha content bounds: `(10,3)` to `(952,471)`.
SHA-256: `D187D3E49E34BCBCEE719BC94A7257F56F6656DF6FF86B39C9C35EBBA7655359`.

The original texture contains white lettering on transparency. The title screen displays the unmodified texture with a runtime dark-violet `SelfModulate` (`5D465F`) so it remains visible on the pale background. The authored control rectangle is `(145,250,650,328)` in the 1920 × 1080 reference canvas, keeps the original aspect ratio via `KeepAspectCentered`, and ends at X=795. This replaces the previously displayed HBR logo. The two former lower-left `ModTitle` and `Credits` labels are no longer created.

## User-provided title illustration

`sakiko_title.png` is a byte-for-byte copy of the first image supplied for this request:
`C:/Users/ROG/AppData/Local/Temp/codex-clipboard-6114e6b4-ca58-460b-b9d6-05b7bf61ac6b.png`.
Dimensions: 1254 × 1254. SHA-256: `CC3C945F7646DE669436D407A28C723F2498C3675828ED594DA3AAE55D5EBCE0`.
The original square file is preserved unchanged as a source reference.

## Redrawn full-width title illustration

`sakiko_title_redraw.png` is the selected built-in `image_gen` edit produced for the user's subsequent request on 2026-10-01. It expands the water/reflections and cherry blossoms across the formerly empty left side and redraws the scythe and both swords against the supplied weapon references. The original `sakiko_title.png` remains available unchanged.

- Actual output dimensions: 1672 × 941, opaque PNG (requested landscape title background).
- SHA-256: `788A13E75DD09F0D7A5E22B836622C8D6B333D33BAE8E117E0DB1878BBD668BB`.
- Generated source: `C:/Users/ROG/.codex/generated_images/01a0904f-302f-7fe3-88ad-17cca1ae0795/exec-eb98b8a7-6914-400a-a792-b2b4740c2d97.png`.
- Scythe reference: `E:/stsmod/resources/saki_rapture_forms/rapture_scythe_transparent.png`.
- Dual-sword reference: `E:/stsmod/resources/saki_rapture_forms/rapture_dual_swords_blue_blade_axial_rotation_80deg.png`.
- User's seam/cutoff example: `C:/Users/ROG/AppData/Local/Temp/codex-clipboard-3d0e83a6-0d51-4ad2-9142-9d77da798b0f.png`.
- Full generation prompt and layout previews: `E:/stsmod/output/hbr-title-menu-redraw-20261001/`.

The background has no baked-in UI/text/logo. The native title and HBR menu/start controls remain separately rendered by the game.

## Completed environment after 75% scaling

`sakiko_title_75pct_completed.png` is a built-in `image_gen` environment edit made on 2026-10-01 to complete the blank left strip left by the user's requested 75% scale. The edit target is an HTML layout screenshot of the existing illustration at `(480,270,1440,810)` on the 1920 × 1080 white reference canvas, with no UI. The generated canvas retains that character/weapon composition on the right and extends the blue water, reflected clouds, and cherry blossoms to the left edge.

- Actual output dimensions: 1672 × 941, opaque PNG.
- SHA-256: `A2606192753D3C5ACAB220EB4C4DACEE627C94DB51EE6AEC7F7C6EC5E4706411`.
- Generated source: `C:/Users/ROG/.codex/generated_images/01a0904f-302f-7fe3-88ad-17cca1ae0795/exec-d84090ee-08f3-41f2-8454-14af353880e9.png`.
- Edit target: `E:/stsmod/output/hbr-title-menu-fill-20261001/background-75pct-reference.png`.
- Exact prompt: `E:/stsmod/output/hbr-title-menu-fill-20261001/imagegen-prompt.txt`.
- Original placement: this completed canvas was displayed in `(0,0,1920,1080)`. The 75% character placement is baked into this asset; it must not be scaled to 75% again. It is now preserved as the source for the fade edit below.

Both prior illustration files remain unchanged. The previews in the same output folder are browser layout checks, not an in-game test. No Godot import, export, or distributable was produced for this edit.

## Short left fade and 30A white hair ribbons (preserved source)

`sakiko_title_fade_white_ribbon.png` is the subsequent built-in `image_gen` edit requested on 2026-10-01. It removes the water and petals from the left title area, quickly fades the environment to white to the left of 30A's scythe, and changes only the scythe-wielding 30A character's hair ribbons to white. The right character retains her dark ribbons. The existing 75% character/weapon composition and separate native logo/start/menu layout are retained.

- Actual output dimensions: 1672 × 941, opaque PNG.
- SHA-256: `FBEA60203E8489BBF956FFC87F14FEC43CC61D62F7CF726F16A7D95EBB6B4D42`.
- Generated source: `C:/Users/ROG/.codex/generated_images/01a0904f-302f-7fe3-88ad-17cca1ae0795/exec-bdd337f6-5e19-4b44-807b-108b19e8ea4d.png`.
- Edit target: `E:/stsmod/HBRGOSakiMod/images/ui/title/sakiko_title_75pct_completed.png`, preserved unchanged.
- Exact prompt and browser layout previews: `E:/stsmod/output/hbr-title-menu-fade-20261001/`.
- Original consumer: `NHbrTitleMenu` displayed the full canvas in `(0,0,1920,1080)`. Do not apply another 75% scale to this asset.

No typography or UI is baked into the background. This asset-only edit has not been imported/exported by Godot, packaged, or tested in the running game.

## Clean illustration and local 4K super-resolution (current asset)

`sakiko_title_clean_4k.png` follows the noise-removal rules from the accessible chat titled `图`, as requested on 2026-10-01. Two built-in `image_gen` edits retain the character/weapon structure and simplify fragmented surface shading. The second edit targets only water, replacing scattered white glints and mottled reflections with coherent blue planes and continuous ripples. The user then explicitly selected local super-resolution. No image API key or paid API workflow was used.

- Final dimensions: **3840 × 2160**, opaque RGB PNG, 4,828,546 bytes.
- SHA-256: `8AA12BD35F0AA468D8F038A5CA7841A0F031F492A3FFD241EC2EB233B63655E4`.
- First built-in output: `C:/Users/ROG/.codex/generated_images/01a0904f-302f-7fe3-88ad-17cca1ae0795/exec-93364302-378c-42dc-9ad7-25344a2d340b.png` (1672 × 941). The initial 4K prompt did not increase the actual pixel dimensions.
- Selected second built-in output: `C:/Users/ROG/.codex/generated_images/01a0904f-302f-7fe3-88ad-17cca1ae0795/exec-71a9dfd1-c1dc-458f-894f-bcb2649f1fdc.png` (1672 × 941), preserved as `denoise-v2.png` in the audit folder below.
- Local model: official Real-ESRGAN `realesrgan-x4plus-anime`, ncnn Vulkan portable release 20220424 from `https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip`.
- Downloaded archive SHA-256: `ABC02804E17982A3BE33675E4D471E91EA374E65B70167ABC09E31ACB412802D`.
- Local inference: scale 4, tile 128, threads 1:1:1, RTX 4070 Laptop GPU. Raw output: 6688 × 3764; SHA-256 `BC649AD0FDFC2562182A62C93C50FFAA315C3A20F5A9F393770ED0E9ED8D2B10`.
- Final sizing: .NET high-quality bicubic downsample to 3840 × 2161, clipping one pixel from the empty white top edge to normalize the source's slight aspect-ratio difference to 16:9. No extra sharpening or whole-image blur was applied.
- Exact prompts, rule evidence, inference/sizing scripts, log and browser previews: `E:/stsmod/output/hbr-title-menu-denoise-20261001/`.
- Current consumer: `NHbrTitleMenu` displays this full canvas in `(0,0,1920,1080)`. The accepted 75% subject size and bottom-right composition remain baked into the art, with 30A's white ribbons and the short fade left of her scythe.

Prior illustration files and all separate logo/menu/start textures remain unchanged. This is a locally upscaled redraw, not native 4K imagegen output. No Godot import/export, packaging or in-game verification was performed for this edit.

## Extracted UI assets

Installed on 2026-10-01 by copying the existing decoded PNG files byte-for-byte. No cropping, painting, resizing, AI generation, Godot import, export or packaging was performed. All images retain RGBA transparency and their original decoded dimensions.

The local extraction index is `E:/stsmod/HeavenBurnsRedEditor/extracted/asset-index.jsonl`. Each source directory's `manifest.json` records the original game bundle, serialized file, texture dimensions and decoded PNG hash. Original bundles belong to the local Heaven Burns Red game installation under `E:/game/HeavenBurnsRed/HeavenBurnsRed Game/AssetBundleCache/`.

| Installed file | Dimensions | Original Unity asset | Texture path ID | SHA-256 |
| --- | --- | --- | --- | --- |
| `title_logo.png` | 752 × 1176 | `Assets/UI/Texture/Title/titleLogo.png` | 6083523386126491087 | `BE9A824FA856AD5F08C878F790F46720C5AF64549FFA2277600FE73DAAD55905` |
| `tap_to_start.png` | 556 × 108 | `Assets/UI/Texture/Title/tapToStartMessageDefault.png` | -8440248048689785672 | `CA716F90E70DDE19C737CD3BE4D4EF991A56EEFE84705E7948CD42C141F159D8` |
| `tap_to_start_active.png` | 556 × 108 | `Assets/UI/Texture/Title/tapToStartMessageActive.png` | -1867978087904387040 | `1B84CD043B181DB10EFC66438A00B7CEAA7B211EDB96652A8E276F7D5E5045E0` |
| `title_atlas.png` | 512 × 512 | Packed texture for `Assets/UI/Texture/TitleAtlas/TitleAtlas.spriteatlas` | 4813219734166290873 | `42C71197D31C404FFD03CAC6D8AC7A9F28C00DA16917DD0A0E5C3BDC145F3A86` |

The first three source files are in:

`E:/stsmod/HeavenBurnsRedEditor/extracted/images/cache/eadb342ab0591310d79d2906da77581f/748eb259b28f9e6235c44bdf8ba39b89/`

- `371347e0e330f206_6083523386126491087_titleLogo.png`
- `371347e0e330f206_-8440248048689785672_tapToStartMessageDefault.png`
- `371347e0e330f206_-1867978087904387040_tapToStartMessageActive.png`

Their serialized file is `CAB-4c988972baef777a5863eff36dd68628`. The PNG logo visibly contains the vertical Chinese title 炽焰天穹, emblem, and HEAVEN BURNS RED subtitle; the start textures visibly contain 点击开始.

The packed atlas source file is:

`E:/stsmod/HeavenBurnsRedEditor/extracted/images/cache/f5a279f071872fcc509530cd8ee63337/a1271027ac09c735129e4e4b88371fd0/4692248ce5a87e6a_4813219734166290873_sactx-0-512x512-BC7-TitleAtlas-c89a9480.png`

Its serialized file is `CAB-6f7bdc01ae1432d60ad5ceda42d26966`, and the original SpriteAtlas path ID is 736321733446358767.

## Hamburger button atlas regions

These coordinates address the copied PNG from its top-left, as Godot `AtlasTexture.Region` does. Integer regions reproduce the decoded sprite bounding rectangles. The atlas texture itself remains unchanged.

| State | Original Sprite | Sprite path ID | PNG region (x, y, width, height) | Position within logical 148 × 148 button |
| --- | --- | --- | --- | --- |
| Default | `ButtonTitleMenuDefault` | -5446839594252453295 | `(0, 139, 101, 101)` | approximately `(24, 24)` |
| Hover/active | `ButtonTitleMenuActive` | -8241884302708808591 | `(0, 364, 148, 148)` | `(0, 0)` |
| Disabled | `ButtonTitleMenuDisable` | -864571852535733850 | `(128, 278, 82, 82)` | approximately `(33, 33)` |

All three original sprites have a logical 148 × 148 rectangle and centered pivot. The default and disabled atlas entries are trimmed; preserve one 148 × 148 hit rectangle and place each inner texture at its native trimmed size, centered within that rectangle. Stretching every trimmed sprite to 148 × 148 would change the circle size between states. Active includes the pink glow around the white hamburger circle. No separate outline sprite is needed.

Exact Unity atlas metadata, expressed with a bottom-left texture origin:

- Default `textureRect=(0.0761204,272.076111,100.847763,100.847763)`; `textureRectOffset=(24.0761204,23.0761204)`. Exact PNG top-left Y is `139.076126`; logical top offset is `24.0761166`.
- Active `textureRect=(0,0,148,148)`; offset `(0,0)`.
- Disabled `textureRect=(128.076126,152.076126,81.897125,81.847763)`; offset `(33.0761223,33.0761223)`. Exact PNG top-left Y is `278.076111`; logical top offset is `33.0761147`.
- All three have `settingsRaw=67`: packed, tight, no packing rotation. Corner RGB can be extruded but its alpha is zero; preserve transparency.

## Original title layout reference

Both `Assets/UI/AssetBundle/UITitle.prefab` and `UITitle2.prefab` use the copied logo/start texture and the default menu sprite. The original Unity UI coordinates are design units, not a verified screen-pixel reference:

- Logo: centered anchor, anchored position `(-421,117)`, size `(376,587)`; the source texture is approximately 2× the intended layout size.
- Start message: bottom-center anchor, anchored position `(-428,245)`, size `(556,108)`.
- Menu: top-left anchor, anchored position `(125,-129)`, logical size `(148,148)`.
- Main logo remains left of center; the start prompt aligns underneath it. The menu remains in the upper-left corner.

Original title prefab bundles: `cache/307c9531a8f3191559073b3593305409/972ebbb93c4773e8c9bfda708c69a7d0` and `cache/f19cc5c08e196dcd826934f34021946d/6b0a447006eb0d5753387fff514cbe8e` under `E:/stsmod/HeavenBurnsRedEditor/extracted/unity/`.

## Optional existing effects (not installed here)

`TitleFlare1.png` and `TitleFlare2.png` are 512 × 512 RGBA title glow textures in the same Title source directory, with texture path IDs -4800207324809926568 and -5157934712719565834. Their decoded filenames start with `371347e0e330f206_`. These are light flares, not petals.

Two existing RGBA petal sheets are available in:

`E:/stsmod/HeavenBurnsRedEditor/extracted/images/cache/3879df7a605a32f7534ae5d23014ec58/ccd9a5c5d27c3935746af80393ea3da0/`

- `ea07b8f8868c8f81_-1045388676840917446_SakuraEffectAtlas.png` — 256 × 128; clear pale-pink petal silhouettes.
- `ea07b8f8868c8f81_-8049186174937150961_Sakura2EffectAtlas.png` — 256 × 128; soft glowing/blurred petals.

The original assets are `Assets/Contents/Effect/Adventure/CherryBlossoms/Textures/SakuraEffectAtlas.png` and `Sakura2EffectAtlas.png`. The associated `CherryBlossoms.prefab` explicitly uses a 4-column × 2-row UV sheet, so each cell is 64 × 64. A Godot atlas region can select `(64*column,64*row,64,64)` without editing either PNG. These are verified existing HBR cherry-blossom effects, but have not been proven to be the exact login-screen petal emitter.
