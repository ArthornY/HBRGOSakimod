shader_type canvas_item;
render_mode blend_add;

// Light on Saki's counter, drawn over the unlit base. TEXTURE holds the
// light's colour in RGB and its strength in alpha, so adding it back at full
// strength gives the painted original. 31O's sakura accents breathe; 30A's
// soul flame flickers as noise rises through it.
uniform float intensity = 1.0;
uniform float pulse_speed = 1.6;
uniform float pulse_amount = 0.0;
uniform float flicker_speed = 2.2;
uniform float flicker_amount = 0.0;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
    float pulse = 1.0 - pulse_amount + pulse_amount * sin(TIME * pulse_speed);
    float rising = noise(vec2(UV.x * 7.0, UV.y * 7.0 + TIME * flicker_speed));
    float flicker = 1.0 - flicker_amount + flicker_amount * rising;
    COLOR.a *= intensity * pulse * flicker;
}
