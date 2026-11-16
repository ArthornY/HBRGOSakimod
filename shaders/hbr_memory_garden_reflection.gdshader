shader_type canvas_item;

// A memory card mirrored in the garden's water (ui-concepts/memory-garden-20261104): the card's lower part,
// flipped about its bottom edge, rippled, slightly blurred and fading downwards.
// This node sits right under the card; a BackBufferCopy before it captures the card it mirrors.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float opacity = 0.42;
uniform float saturation = 0.8;

void fragment() {
	// Screen distance from the card's bottom edge to this pixel, mirrored back up into the card.
	float back = 2.0 * UV.y / dFdy(UV.y) * SCREEN_PIXEL_SIZE.y;
	float ripple = sin(UV.y * 38.0 + TIME * 2.2) * 1.6 * SCREEN_PIXEL_SIZE.x;
	vec2 src = vec2(SCREEN_UV.x + ripple, SCREEN_UV.y - back);
	vec2 tap = SCREEN_PIXEL_SIZE * 0.8;
	vec4 c = textureLod(screen_texture, src, 0.0) * 0.4
		+ textureLod(screen_texture, src + vec2(tap.x, 0.0), 0.0) * 0.15
		+ textureLod(screen_texture, src - vec2(tap.x, 0.0), 0.0) * 0.15
		+ textureLod(screen_texture, src + vec2(0.0, tap.y), 0.0) * 0.15
		+ textureLod(screen_texture, src - vec2(0.0, tap.y), 0.0) * 0.15;
	float grey = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(mix(vec3(grey), c.rgb, saturation), opacity * (1.0 - UV.y));
}
