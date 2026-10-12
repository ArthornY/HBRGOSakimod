shader_type canvas_item;

// Source: BattleAtlas GaugeHPDPBaseBattle / GaugeHPDPFlameBattle (28x16).
// Their complementary alpha already contains the entire frame inset. Draw
// the same capsule geometry at screen resolution instead of stretching its
// 16px raster edge or placing an inset copy inside another inset container.
uniform vec2 gauge_size = vec2(250.0, 16.0);
uniform bool draw_frame = false;
uniform int frame_overlap_count = 0;
// Rows of the transform into the opposite frame's local pixel coordinates;
// geometry packs its translation and its complete width/height.
uniform vec4 frame_overlap_transform_0 = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec4 frame_overlap_transform_1 = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec4 frame_overlap_transform_2 = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec4 frame_overlap_transform_3 = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec4 frame_overlap_geometry_0 = vec4(0.0);
uniform vec4 frame_overlap_geometry_1 = vec4(0.0);
uniform vec4 frame_overlap_geometry_2 = vec4(0.0);
uniform vec4 frame_overlap_geometry_3 = vec4(0.0);
varying vec4 vertex_tint;

void vertex() {
    vertex_tint = COLOR;
}

float capsule_distance(vec2 point, vec2 low, vec2 high, float radius) {
    vec2 half_size = max((high - low) * 0.5, vec2(0.001));
    radius = min(radius, min(half_size.x, half_size.y));
    vec2 q = abs(point - (low + high) * 0.5) - half_size + vec2(radius);
    return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
}

float coverage(float distance) {
    // One screen-pixel transition, including viewport/UI scale changes.
    float antialias_width = max(fwidth(distance) * 0.5, 0.0001);
    return 1.0 - smoothstep(-antialias_width, antialias_width, distance);
}

float outside_opposite_gauge(vec2 point, vec4 transform, vec4 geometry) {
    vec2 opposite_point = vec2(dot(transform.xy, point), dot(transform.zw, point)) + geometry.xy;
    vec2 opposite_size = geometry.zw;
    float distance = capsule_distance(opposite_point,
        vec2(0.0, 0.65),
        vec2(opposite_size.x, opposite_size.y - 0.25),
        max(opposite_size.y * 0.5 - 0.5, 0.0));
    return 1.0 - coverage(distance);
}

void fragment() {
    vec2 point = UV * gauge_size;
    // Gauge resizing changes the fill height, not the source frame's stroke.
    // These fixed insets match the original 16px gauge at every bar height.
    float inner_distance = capsule_distance(point,
        vec2(3.25, 3.75),
        vec2(gauge_size.x - 2.75, gauge_size.y - 3.25), max(gauge_size.y * 0.5 - 3.5, 0.0));

    if (draw_frame) {
        float outer_distance = capsule_distance(point,
            vec2(0.0, 0.65),
            vec2(gauge_size.x, gauge_size.y - 0.25), max(gauge_size.y * 0.5 - 0.5, 0.0));
        // Original upper/lower frame colors sampled from the source sprite.
        vec3 frame_color = mix(vec3(247.0, 223.0, 255.0),
            vec3(247.0, 219.0, 251.0), UV.y) / 255.0;
        // Remove interior seams where the complete HP/DP capsules overlap.
        // Use full geometry even for an empty pool, independent of fill ratio.
        float visible_frame = 1.0;
        if (frame_overlap_count > 0) visible_frame *= outside_opposite_gauge(point, frame_overlap_transform_0, frame_overlap_geometry_0);
        if (frame_overlap_count > 1) visible_frame *= outside_opposite_gauge(point, frame_overlap_transform_1, frame_overlap_geometry_1);
        if (frame_overlap_count > 2) visible_frame *= outside_opposite_gauge(point, frame_overlap_transform_2, frame_overlap_geometry_2);
        if (frame_overlap_count > 3) visible_frame *= outside_opposite_gauge(point, frame_overlap_transform_3, frame_overlap_geometry_3);
        COLOR = vec4(frame_color,
            max(0.0, coverage(outer_distance) - coverage(inner_distance)) * visible_frame) * vertex_tint;
    } else {
        // Slight underlap beneath the frame prevents a dark antialias seam.
        // Only backgrounds use this branch. ClipChildren needs the separate
        // gauge_mask shader, which composites the actual children's colors.
        vec3 base_color = texture(TEXTURE, vec2(0.5)).rgb;
        COLOR = vec4(base_color, coverage(inner_distance - 0.5)) * vertex_tint;
    }
}
