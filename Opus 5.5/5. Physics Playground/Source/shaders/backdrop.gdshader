// Back wall: light grey-blue vertical gradient with a subtle 1 m grid and stronger 5 m lines (P-24).
shader_type spatial;
render_mode diffuse_burley, specular_disabled;

uniform vec3 top_color : source_color = vec3(0.74, 0.81, 0.9);
uniform vec3 bottom_color : source_color = vec3(0.9, 0.93, 0.97);
uniform vec3 grid_color : source_color = vec3(0.55, 0.62, 0.74);
uniform float y_min = -5.0;
uniform float y_max = 20.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float grid_line(vec2 p) {
	vec2 fw = max(fwidth(p), vec2(0.0001));
	vec2 d = abs(fract(p - 0.5) - 0.5) / fw;
	return 1.0 - clamp(min(d.x, d.y), 0.0, 1.0);
}

void fragment() {
	float t = clamp((world_pos.y - y_min) / (y_max - y_min), 0.0, 1.0);
	vec3 col = mix(bottom_color, top_color, t);
	float minor = grid_line(world_pos.xy);
	float major = grid_line(world_pos.xy / 5.0);
	col = mix(col, grid_color, minor * 0.16 + major * 0.2);
	ALBEDO = col;
	ROUGHNESS = 1.0;
}
