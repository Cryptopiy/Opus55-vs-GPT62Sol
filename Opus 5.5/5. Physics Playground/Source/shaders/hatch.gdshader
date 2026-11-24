// Forbidden zone: translucent red diagonal hatching with a solid border.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 color : source_color = vec4(0.95, 0.2, 0.2, 0.5);
uniform float spacing = 0.4;
uniform float border = 0.06;
instance uniform vec2 zone_size = vec2(1.0);

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float s = fract((world_pos.x + world_pos.y) / spacing);
	float stripe = smoothstep(0.42, 0.48, s) - smoothstep(0.92, 0.98, s);
	vec2 dist_px = min(UV, 1.0 - UV) * zone_size;
	float edge = 1.0 - step(border, min(dist_px.x, dist_px.y));
	ALBEDO = color.rgb;
	ALPHA = clamp(color.a * (0.2 + 0.6 * stripe) + edge * 0.7, 0.0, 1.0);
}
