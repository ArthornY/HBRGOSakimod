shader_type canvas_item;

// Keep the supplied PNG intact, including white metal. A traced silhouette
// removes only the surrounding sheet rather than erasing bright body pixels.
uniform sampler2D silhouette_mask : filter_linear, repeat_disable;
// AtlasTexture's UV addresses the full original PNG. Normalize its crop
// before sampling the independently cropped silhouette (Godot 4.5.1).
uniform vec4 crop_pixels = vec4(58.0, 251.0, 1674.0, 371.0);

void fragment() {
    vec2 mask_uv = (UV / TEXTURE_PIXEL_SIZE - crop_pixels.xy) / crop_pixels.zw;
    COLOR.a *= texture(silhouette_mask, mask_uv).a;
}
