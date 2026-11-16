shader_type canvas_item;

// Covers a memory card in the garden (ui-concepts/memory-garden-20261104, memory-garden-names-20261106).
// Sealed: the card underneath is desaturated and dimmed, with static, scanlines and a slow bright band over it.
// Full: only a slight wash-out. Both use the uniforms below.
// Ghost (a character not met yet): the art window becomes a two-tone ink/paper silhouette of the card's own art,
// the description hides under static and the rest of the card turns grey, under a lighter layer of the same static.
// `reveal` drains the silhouette away from the bottom up when a character has just been met: into the sealed or
// full cover when `target_cover` is set, otherwise into nothing.
// A BackBufferCopy right before this node captures the card it reads; the energy icon is drawn after it.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform bool ghost = false;
uniform bool target_cover = false;
uniform float saturation = 0.3;
uniform float brightness = 0.9;
uniform float noise_amount = 0.45;
uniform float scanlines = 1.0;
uniform float band = 1.0;
uniform float card_height = 422.0;
// UV rects (left, top, right, bottom) of the art window and the description, from HbrCardStyleLayout.
uniform vec4 art_rect = vec4(0.0467, 0.1398, 0.9533, 0.6280);
uniform vec4 text_rect = vec4(0.0600, 0.6374, 0.9400, 0.9455);
// #1c2344 and #eef0f8, used as is like the other colours here (the 2D pipeline is not linear).
uniform vec3 ink = vec3(0.110, 0.137, 0.267);
uniform vec3 paper = vec3(0.933, 0.941, 0.973);
uniform float reveal = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

bool inside(vec2 uv, vec4 rect) {
	return uv.x >= rect.x && uv.x <= rect.z && uv.y >= rect.y && uv.y <= rect.w;
}

// Static specks, scanlines (2px dark, 1px light) and the sweeping bright band.
vec3 treat(vec3 col, float speck, float amount, bool dark_row, float lines, float sweep, float band_amount) {
	col = 1.0 - (1.0 - col) * (1.0 - vec3(speck) * amount);
	col = dark_row ? mix(col, vec3(0.047, 0.063, 0.133), 0.4 * lines) : mix(col, vec3(1.0), 0.07 * lines);
	return mix(col, vec3(1.0), 0.28 * sweep * band_amount);
}

void fragment() {
	vec3 under = textureLod(screen_texture, SCREEN_UV, 0.0).rgb;
	float grey = dot(under, vec3(0.299, 0.587, 0.114));
	vec2 px = UV * vec2(card_height * 300.0 / 422.0, card_height);
	// Static jumps to a new pattern every 0.6s, like the preview's stepped animation.
	float frame = floor(TIME / 0.6 * 3.0);
	float speck = smoothstep(0.55, 1.0, hash(floor(px / 1.6) + vec2(frame * 37.0, frame * 11.0)));
	bool dark_row = mod(px.y, 3.0) < 2.0;
	// A bright band sweeps down the card every 2.6s.
	float y = mod(TIME, 2.6) / 2.6 * (card_height + 60.0) - 30.0;
	float sweep = 1.0 - smoothstep(0.0, 9.0, abs(px.y - y));
	vec3 cover = treat(mix(vec3(grey), under, saturation) * brightness, speck, noise_amount, dark_row, scanlines, sweep, band);
	if (!ghost) {
		COLOR = vec4(cover, 1.0);
	} else {
		vec3 silhouette;
		float amount = 0.3;
		if (inside(UV, art_rect)) {
			silhouette = mix(ink, paper, smoothstep(0.42, 0.66, grey));
		} else if (inside(UV, text_rect)) {
			silhouette = vec3(0.094, 0.110, 0.204);
			amount = 0.75;
		} else {
			silhouette = vec3(grey) * 0.9;
		}
		silhouette = treat(silhouette, speck, amount, dark_row, 0.45, sweep, 0.5);
		// Meeting: everything below a soft edge rising from the bottom has left the silhouette.
		float wipe = reveal * 1.12;
		float keep = smoothstep(wipe - 0.12, wipe, 1.0 - UV.y);
		COLOR = target_cover ? vec4(mix(cover, silhouette, keep), 1.0) : vec4(silhouette, keep);
	}
}
