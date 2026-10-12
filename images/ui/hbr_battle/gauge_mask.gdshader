shader_type canvas_item;
render_mode unshaded;

// ClipChildren=Only uses this material for its final backbuffer composite.
// Match Godot's default clip shader: copy the children's RGB, with the mask's
// alpha. Rendering a white capsule here would hide both color and fill amount.
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform vec2 gauge_size = vec2(250.0, 16.0);
varying float vertex_alpha;

void vertex() {
    vertex_alpha = COLOR.a;
}

void fragment() {
    // Same inner capsule and 0.5px frame underlap as gauge_shape.gdshader.
    vec2 point = UV * gauge_size;
    vec2 low = vec2(3.25, 3.75);
    vec2 high = vec2(gauge_size.x - 2.75, gauge_size.y - 3.25);
    vec2 half_size = max((high - low) * 0.5, vec2(0.001));
    float radius = min(max(gauge_size.y * 0.5 - 3.5, 0.0), min(half_size.x, half_size.y));
    vec2 q = abs(point - (low + high) * 0.5) - half_size + vec2(radius);
    float distance = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius - 0.5;
    float antialias_width = max(fwidth(distance) * 0.5, 0.0001);
    float alpha = 1.0 - smoothstep(-antialias_width, antialias_width, distance);

    vec3 children = textureLod(screen_texture, SCREEN_UV, 0.0).rgb;
    COLOR = vec4(children, alpha * vertex_alpha);
}
