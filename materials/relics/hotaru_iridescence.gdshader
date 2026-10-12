shader_type canvas_item;
render_mode unshaded;

// Match the native relic material so UpdateTexture retains its state handling.
uniform float pulse = 0.0;
uniform float is_used = 0.0;
uniform float is_wax = 0.0;

varying vec4 vertex_tint;

vec3 rainbow(float hue) {
    vec3 bands = abs(fract(vec3(hue) + vec3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0);
    return mix(vec3(1.0), clamp(bands - 1.0, 0.0, 1.0), 0.64);
}

void vertex() {
    vertex_tint = COLOR;
}

void fragment() {
    vec4 source = texture(TEXTURE, UV);
    float luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
    float travel = dot(UV, vec2(0.72, -0.68));
    float moving_time = TIME * (1.0 - max(is_used, is_wax));
    vec3 flowing = rainbow(fract(travel - moving_time * 0.16));
    float light_band = smoothstep(0.8, 1.0, sin(travel * 12.56637 - moving_time * 2.6));
    float line_mask = smoothstep(0.17, 0.44, luminance);
    vec3 line_color = flowing * (0.74 + luminance * 0.36) + vec3(light_band * 0.17);
    vec3 result = mix(source.rgb, line_color, line_mask * 0.88) * vertex_tint.rgb;
    result += abs(sin(TIME * 3.0)) * 0.2 * pulse;

    float average = (result.r + result.g + result.b) / 3.0;
    result = mix(result, vec3(average), clamp(is_used, 0.0, 1.0));
    result = mix(result, vec3(average) * vec3(1.0, 0.8, 0.8), clamp(is_wax, 0.0, 1.0));
    COLOR = vec4(result, source.a * vertex_tint.a);
}
