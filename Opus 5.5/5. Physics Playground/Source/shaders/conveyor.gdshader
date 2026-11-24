// Conveyor belt: dark rounded slab with chevrons that travel along local +X by `scroll` metres,
// so the texture moves in the same direction and at the same speed as the belt surface (P-10).
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 base_color : source_color = vec3(0.16, 0.17, 0.2);
uniform vec3 stripe_color : source_color = vec3(0.95, 0.75, 0.2);
uniform float spacing = 0.5;
uniform float edge_radius = 0.05;
instance uniform float scroll = 0.0;
instance uniform vec3 half_extents = vec3(1.5, 0.15, 0.5);

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	float rad = min(edge_radius, half_extents.y * 0.9);
	vec3 inner = max(half_extents - vec3(rad), vec3(0.0));
	vec3 q = local_pos - clamp(local_pos, -inner, inner);
	float len = length(q);
	vec3 n_local = len > 0.00001 ? q / len : vec3(0.0, 0.0, 1.0);
	NORMAL = normalize((VIEW_MATRIX * (MODEL_MATRIX * vec4(n_local, 0.0))).xyz);
	// chevrons on the top face (tip pointing +X), straight bars on the sides
	float top = step(0.7, n_local.y);
	float shape = local_pos.x + top * abs(local_pos.z) * 0.6 - scroll;
	float p = fract(shape / spacing);
	float stripe = smoothstep(0.0, 0.04, p) - smoothstep(0.26, 0.3, p);
	float side_band = 1.0 - top;
	stripe *= mix(1.0, step(abs(local_pos.y), half_extents.y * 0.45), side_band);
	ALBEDO = mix(base_color, stripe_color, stripe * 0.85);
	ROUGHNESS = mix(0.8, 0.5, stripe);
}
