shader_type canvas_item;

// Unity Image.Type.Filled crops a fixed-width gradient. Native STS resizes its
// NinePatchRect instead; map the resized UVs back to the original full width.
uniform float fill_span : hint_range(0.0, 1.0) = 1.0;
varying vec4 vertex_tint;

void vertex() {
    vertex_tint = COLOR;
}

void fragment() {
    COLOR = texture(TEXTURE, vec2(UV.x * fill_span, UV.y)) * vertex_tint;
}
