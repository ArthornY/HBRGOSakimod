shader_type canvas_item;

// Memory sublimation (ui-concepts/memory-garden-20261104).
// Ring: a conic rainbow masked to a ring that the caller scales outwards and fades.
// Sheen: a soft rainbow band slanting across the screen, moved by the caller.
uniform bool sheen = false;

vec3 rainbow(float t) {
	// conic-gradient(from 20deg, #ff8ab0, #ffc27a, #fff38a, #8ff0b8, #7cd0ff, #a99bff, #ff8ab0)
	vec3 c[7] = vec3[](vec3(1.0, 0.541, 0.690), vec3(1.0, 0.761, 0.478), vec3(1.0, 0.953, 0.541),
		vec3(0.561, 0.941, 0.722), vec3(0.486, 0.816, 1.0), vec3(0.663, 0.608, 1.0), vec3(1.0, 0.541, 0.690));
	float x = fract(t) * 6.0;
	int i = int(floor(x));
	return mix(c[i], c[min(i + 1, 6)], fract(x));
}

void fragment() {
	if (sheen) {
		// linear-gradient(105deg, transparent 32%, rainbow tints at ~.2 between 41% and 59%, transparent 68%)
		float along = dot(UV - 0.5, normalize(vec2(0.966, 0.259))) + 0.5;
		float a = smoothstep(0.32, 0.41, along) * (1.0 - smoothstep(0.59, 0.68, along)) * 0.21;
		COLOR = vec4(rainbow((along - 0.41) / 0.18 * 0.66), a * COLOR.a);
	} else {
		vec2 p = UV - 0.5;
		float r = length(p) * 2.0;
		// Clockwise from the top, starting 20 degrees round; rainbow() wraps the turn.
		float angle = atan(p.x, -p.y) / 6.28318531 - 20.0 / 360.0;
		// radial mask: transparent to 72%, solid 82-88%, gone at 100%
		float ring = smoothstep(0.72, 0.82, r) * (1.0 - smoothstep(0.88, 1.0, r));
		COLOR = vec4(rainbow(angle), ring * COLOR.a);
	}
}
