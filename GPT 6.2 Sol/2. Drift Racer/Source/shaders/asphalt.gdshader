shader_type spatial;
render_mode cull_disabled;

uniform float half_road;
uniform float stripe_length;
uniform float track_length;
uniform vec3 asphalt : source_color = vec3(0.105, 0.118, 0.135);
varying vec3 world_position;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float grit = hash(floor(world_position.xz * 53.0));
	float patch = sin(world_position.x * 0.47) * sin(world_position.z * 0.29);
	vec3 color = asphalt * (0.83 + grit * 0.32 + patch * 0.11);
	float along = UV.y;
	float lateral = abs(UV.x);
	float edge = 1.0 - smoothstep(0.055, 0.095, abs(lateral - (half_road - 0.20)));
	float center = (1.0 - smoothstep(0.045, 0.080, lateral)) *
		step(mod(along, stripe_length * 2.0), stripe_length);
	float checker = mod(floor((UV.x + half_road) / 0.5) + floor(along / 0.5), 2.0);
	float finish = (1.0 - step(1.0, along)) * checker;
	float last = 1.0 - step(0.01, track_length - along);
	color = mix(color, vec3(0.82, 0.86, 0.84), clamp(max(edge, max(center, finish)) - last, 0.0, 1.0));
	float curb = step(half_road + 0.001, lateral);
	vec3 paint = mix(vec3(0.67, 0.055, 0.055), vec3(0.89, 0.87, 0.80),
		step(stripe_length, mod(along, stripe_length * 2.0)));
	ALBEDO = mix(color, paint * (0.89 + grit * 0.17), curb);
	ROUGHNESS = 0.92;
}
