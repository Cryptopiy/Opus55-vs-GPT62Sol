shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec2 extent = vec2(1.0, 1.0);
void fragment() {
	vec2 p = UV * extent;
	float stripe = step(0.72, fract((p.x + p.y) * 4.5));
	vec2 edge = min(p, extent - p);
	float frame = 1.0 - smoothstep(0.025, 0.050, min(edge.x, edge.y));
	ALBEDO = vec3(0.72, 0.18, 0.17);
	ALPHA = clamp(0.09 + 0.19 * stripe + 0.45 * frame, 0.0, 0.75);
}
