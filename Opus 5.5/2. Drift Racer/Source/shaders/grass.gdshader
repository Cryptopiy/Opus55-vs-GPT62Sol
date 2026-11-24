// Ground (R-05): procedural grass with colour variation and dry patches.
shader_type spatial;
render_mode diffuse_burley;

#include "res://shaders/noise.gdshaderinc"

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_pos.xz;
	float cam_dist = length(world_pos - CAMERA_POSITION_WORLD);
	float detail = 1.0 - smoothstep(40.0, 220.0, cam_dist);
	float big = fbm(p * 0.012);
	float mid = fbm(p * 0.09);
	float fine = value_noise(p * 3.1);
	vec3 g_dark = vec3(0.13, 0.27, 0.07);
	vec3 g_light = vec3(0.24, 0.39, 0.11);
	vec3 dry = vec3(0.42, 0.4, 0.2);
	vec3 col = mix(g_dark, g_light, mid);
	col = mix(col, dry, smoothstep(0.58, 0.82, big) * 0.55);
	col *= 0.88 + 0.24 * mix(0.5, fine, detail);
	ALBEDO = col;
	ROUGHNESS = 0.96;
	SPECULAR = 0.2;
}
