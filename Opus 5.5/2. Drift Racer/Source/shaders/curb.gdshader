// Curb strips (R-03): red/white alternating segments of `stripe` metres.
// UV.x = 0..1 across the curb, UV.y = distance along this curb edge (metres).
shader_type spatial;
render_mode diffuse_burley;

#include "res://shaders/noise.gdshaderinc"

uniform float stripe = 3.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float phase = mod(UV.y, 2.0 * stripe);
	float aa = max(fwidth(UV.y), 0.0001);
	float red = smoothstep(stripe - aa, stripe + aa, phase) * (1.0 - smoothstep(2.0 * stripe - aa, 2.0 * stripe, phase));
	vec3 col = mix(vec3(0.9, 0.9, 0.88), vec3(0.78, 0.06, 0.05), red);
	float dirt = fbm(world_pos.xz * 1.3);
	col *= 0.86 + 0.14 * dirt;
	ALBEDO = col;
	ROUGHNESS = 0.55;
	SPECULAR = 0.4;
}
