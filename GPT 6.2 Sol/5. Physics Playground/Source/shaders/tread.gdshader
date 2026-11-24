shader_type spatial;
uniform float metres = 3.0;
uniform float travel = 0.0;

void fragment() {
	vec2 point = UV;
	float offset = (point.x * metres - travel) * 3.8;
	float tread = smoothstep(0.14, 0.23, fract(offset)) * (1.0 - smoothstep(0.54, 0.65, fract(offset)));
	float rail = step(0.08, point.y) * (1.0 - step(0.92, point.y));
	ALBEDO = mix(vec3(0.05, 0.09, 0.15), vec3(0.13, 0.29, 0.36), tread * rail);
	ROUGHNESS = 0.83;
}
