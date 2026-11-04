shader_type canvas_item;
render_mode blend_add;

// Hotaru's rainbow rim is light drawn over a plain pearl base rather than
// painted into the counter texture. TEXTURE is the rim mask (white, alpha
// marks where the rainbow sits); the colours run around the centre, turn
// slowly and breathe a little. Colours follow the Iridescence relic.
uniform float turns_per_second = 0.08;
uniform float intensity = 0.7;

const vec3 RED = vec3(1.0, 0.396, 0.545);
const vec3 ORANGE = vec3(1.0, 0.722, 0.357);
const vec3 YELLOW = vec3(1.0, 0.949, 0.482);
const vec3 GREEN = vec3(0.471, 0.953, 0.678);
const vec3 CYAN = vec3(0.392, 0.906, 1.0);
const vec3 BLUE = vec3(0.580, 0.565, 1.0);
const vec3 PINK = vec3(0.949, 0.600, 1.0);

vec3 band(int i) {
    if (i == 0) {
        return RED;
    }
    if (i == 1) {
        return ORANGE;
    }
    if (i == 2) {
        return YELLOW;
    }
    if (i == 3) {
        return GREEN;
    }
    if (i == 4) {
        return CYAN;
    }
    if (i == 5) {
        return BLUE;
    }
    return PINK;
}

vec3 spectrum(float t) {
    float x = t * 7.0;
    int i = int(floor(x));
    return mix(band(i), band((i + 1) % 7), smoothstep(0.0, 1.0, fract(x)));
}

void fragment() {
    vec2 p = UV - vec2(0.5);
    float around = atan(p.y, p.x) / 6.28318531 + 0.5;
    float pulse = 0.9 + 0.1 * sin(TIME * 1.3);
    COLOR = vec4(spectrum(fract(around + TIME * turns_per_second)), COLOR.a * intensity * pulse);
}
