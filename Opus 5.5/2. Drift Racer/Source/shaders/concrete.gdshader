// Concrete barrier sections (R-04): weathered concrete, block joints and an
// alternating red/white painted top band. UV.x = height (m), UV.y = length (m).
shader_type spatial;
render_mode diffuse_burley;

#include "res://shaders/noise.gdshaderinc"

uniform float block_length = 4.0;
uniform float wall_height = 1.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float n = fbm(world_pos.xz * 0.9 + world_pos.y * 2.0);
	vec3 col = vec3(0.62, 0.62, 0.6) * (0.8 + 0.3 * n);
	float block = floor(UV.y / block_length);
	float joint_d = abs(fract(UV.y / block_length + 0.5) - 0.5) * block_length;
	col *= 1.0 - 0.45 * aa_band(joint_d, 0.03);
	float band = smoothstep(wall_height - 0.3, wall_height - 0.29, UV.x);
	vec3 paint = mod(block, 2.0) < 0.5 ? vec3(0.8, 0.08, 0.06) : vec3(0.92, 0.92, 0.9);
	col = mix(col, paint * (0.9 + 0.1 * n), band);
	ALBEDO = col;
	ROUGHNESS = mix(0.9, 0.6, band);
}
