shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform vec4 body_tint : source_color = vec4(0.36, 0.24, 0.40, 1.0);
uniform vec4 base_body_color : source_color = vec4(0.36, 0.24, 0.40, 1.0);

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	float source_distance = distance(source.rgb, base_body_color.rgb);
	float mask = 1.0 - smoothstep(0.10, 0.28, source_distance);
	float source_luminance = dot(source.rgb, vec3(0.299, 0.587, 0.114));
	float base_luminance = max(dot(base_body_color.rgb, vec3(0.299, 0.587, 0.114)), 0.01);
	vec3 recolored_body = body_tint.rgb * (source_luminance / base_luminance);
	vec3 final_rgb = mix(source.rgb, recolored_body, mask);
	COLOR = vec4(final_rgb, source.a) * vertex_color;
}
