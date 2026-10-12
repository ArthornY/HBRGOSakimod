shader_type canvas_item;

// HBR UIBattleOverdrive/gageImageFlash animates UIEffect.m_ColorFactor
// in additive color mode; alpha remains the source sprite/mask alpha.
uniform float flash_amount : hint_range(0.0, 0.5) = 0.0;
varying vec4 vertex_color;

void vertex() {
    vertex_color = COLOR;
}

void fragment() {
    vec4 source = texture(TEXTURE, UV) * vertex_color;
    COLOR = vec4(min(source.rgb + vec3(flash_amount), vec3(1.0)), source.a);
}
