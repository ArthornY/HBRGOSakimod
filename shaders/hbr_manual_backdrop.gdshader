shader_type canvas_item;

// Behind the phone manual (ui-concepts/manual-phone-20261104): the scene blurred, darkened and
// slightly desaturated, like the preview's filter: blur(7px) brightness(.5) saturate(.85).
// The blur samples a mip level of the screen copy.
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 2.5;
uniform float brightness = 0.5;
uniform float saturation = 0.85;

void fragment() {
	vec3 colour = textureLod(screen_texture, SCREEN_UV, blur_lod).rgb;
	float grey = dot(colour, vec3(0.2126, 0.7152, 0.0722));
	COLOR = vec4(mix(vec3(grey), colour, saturation) * brightness, 1.0);
}
