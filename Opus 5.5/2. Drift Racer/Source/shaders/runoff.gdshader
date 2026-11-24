// Run-off strip between curb and barrier: coarse gravel.
shader_type spatial;
render_mode diffuse_burley;

#include "res://shaders/noise.gdshaderinc"

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_pos.xz;
	float stones = value_noise(p * 7.0);
	float patches = fbm(p * 0.25);
	vec3 col = mix(vec3(0.42, 0.37, 0.3), vec3(0.58, 0.52, 0.43), stones);
	col *= 0.8 + 0.3 * patches;
	ALBEDO = col;
	ROUGHNESS = 0.95;
}
