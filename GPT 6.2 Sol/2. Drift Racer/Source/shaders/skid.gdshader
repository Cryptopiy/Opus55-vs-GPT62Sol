shader_type spatial;
render_mode cull_disabled, blend_mix, depth_draw_never, shadows_disabled;

uniform float race_clock;
uniform float mark_life;
varying float born;
varying float strength;

void vertex() {
	born = INSTANCE_CUSTOM.x;
	strength = INSTANCE_CUSTOM.y;
}

void fragment() {
	float age = clamp((race_clock - born) / mark_life, 0.0, 1.0);
	float feather = smoothstep(0.0, 0.18, min(UV.x, 1.0 - UV.x));
	ALBEDO = vec3(0.021, 0.025, 0.028);
	ROUGHNESS = 0.95;
	ALPHA = strength * (1.0 - age) * feather;
}
