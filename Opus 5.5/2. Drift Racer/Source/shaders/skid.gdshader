// Skid-mark strip segments (R-15). Each MultiMesh instance stores its birth
// time and strength in INSTANCE_CUSTOM; alpha fades to zero over fade_time.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, shadows_disabled, diffuse_lambert, specular_disabled;

#include "res://shaders/noise.gdshaderinc"

uniform float now = 0.0;
uniform float fade_time = 20.0;

varying float v_alpha;
varying vec3 world_pos;

void vertex() {
	float age = now - INSTANCE_CUSTOM.x;
	v_alpha = clamp(1.0 - age / fade_time, 0.0, 1.0) * INSTANCE_CUSTOM.y;
	if (INSTANCE_CUSTOM.x < -100.0) {
		v_alpha = 0.0;
	}
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float edge = smoothstep(0.0, 0.18, UV.x) * (1.0 - smoothstep(0.82, 1.0, UV.x));
	float tread = 0.75 + 0.25 * value_noise(vec2(UV.x * 12.0, world_pos.x * 3.0 + world_pos.z * 3.0));
	ALBEDO = vec3(0.015);
	ROUGHNESS = 0.9;
	ALPHA = v_alpha * 0.72 * edge * tread;
}
