// Flat-colour material that makes box meshes look rounded: the normal is taken from the closest point
// on an inner (shrunk) box, so it bends smoothly over a band of width edge_radius along edges and corners.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 albedo : source_color = vec3(0.8);
uniform float roughness : hint_range(0.0, 1.0) = 0.6;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float edge_radius = 0.05;
uniform float edge_shade : hint_range(0.0, 1.0) = 0.12;
uniform vec3 emission : source_color = vec3(0.0);
uniform float emission_energy = 0.0;
instance uniform vec3 half_extents = vec3(0.5);

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	float rad = min(edge_radius, min(half_extents.x, min(half_extents.y, half_extents.z)) * 0.9);
	vec3 inner = max(half_extents - vec3(rad), vec3(0.0));
	vec3 q = local_pos - clamp(local_pos, -inner, inner);
	float len = length(q);
	vec3 n_local = len > 0.00001 ? q / len : vec3(0.0, 0.0, 1.0);
	NORMAL = normalize((VIEW_MATRIX * (MODEL_MATRIX * vec4(n_local, 0.0))).xyz);
	// slightly darker bevel band keeps silhouettes readable against similar colours
	float face = max(max(abs(n_local.x), abs(n_local.y)), abs(n_local.z));
	ALBEDO = albedo * mix(1.0 - edge_shade, 1.0, smoothstep(0.75, 1.0, face));
	ROUGHNESS = roughness;
	METALLIC = metallic;
	EMISSION = emission * emission_energy;
}
