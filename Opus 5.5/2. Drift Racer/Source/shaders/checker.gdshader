// Checkered pattern for the start/finish arch beam. UV in metres.
shader_type spatial;

uniform float cell = 0.45;

void fragment() {
	vec2 c = floor(UV / cell);
	float k = mod(c.x + c.y, 2.0);
	ALBEDO = mix(vec3(0.03), vec3(0.92), k);
	ROUGHNESS = 0.5;
}
