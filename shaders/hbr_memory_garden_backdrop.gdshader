shader_type canvas_item;

// Memory garden backdrop (ui-concepts/memory-garden-20261104): a white space over shallow water.
// The horizon sits where the first row of cards stands; the water brightens towards it.
uniform vec2 size = vec2(1920.0, 1080.0);
uniform float horizon = 551.0;

vec3 sky(vec2 p) {
	// radial-gradient(ellipse 80% 62% at 50% 16%, #ffffff, #f8f9fd 55%, #edf0f8)
	float r = length((p / size - vec2(0.5, 0.16)) / vec2(0.8, 0.62));
	vec3 mid = vec3(0.973, 0.976, 0.992);
	return r < 0.55 ? mix(vec3(1.0), mid, r / 0.55) : mix(mid, vec3(0.929, 0.941, 0.973), clamp((r - 0.55) / 0.45, 0.0, 1.0));
}

void fragment() {
	vec2 p = UV * size;
	vec3 col = sky(p);
	float below = p.y - horizon;
	if (below >= 0.0) {
		// linear-gradient(rgba(214,226,245,.6), rgba(190,208,236,.78) 55%, rgba(176,197,231,.92))
		float t = below / max(1.0, size.y - horizon);
		vec4 w = t < 0.55
			? mix(vec4(0.839, 0.886, 0.961, 0.60), vec4(0.745, 0.816, 0.925, 0.78), t / 0.55)
			: mix(vec4(0.745, 0.816, 0.925, 0.78), vec4(0.690, 0.773, 0.906, 0.92), (t - 0.55) / 0.45);
		col = mix(col, w.rgb, w.a);
		// The water is lit just below the horizon: white at .75, gone after 140px.
		col = mix(col, vec3(1.0), 0.75 * clamp(1.0 - below / 140.0, 0.0, 1.0));
	}
	// Horizon: a 2px white line that fades out at both sides, with a soft glow.
	float side = smoothstep(0.0, 0.18, UV.x) * (1.0 - smoothstep(0.82, 1.0, UV.x));
	float line = 1.0 - smoothstep(1.0, 2.0, abs(below));
	float glow = 0.55 * exp(-below * below / 72.0);
	col = mix(col, vec3(1.0), clamp((line + glow) * side, 0.0, 1.0));
	COLOR = vec4(col, 1.0);
}
