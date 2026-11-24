shader_type spatial;
render_mode cull_disabled;

varying vec3 site;

float grit(vec2 p) {
	return fract(sin(dot(p, vec2(131.1, 217.3))) * 16453.12);
}

void vertex() {
	site = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = site.xz;
	float field = sin(p.x * 0.047 + p.y * 0.032) * sin(p.y * 0.061 - p.x * 0.025);
	float fleck = grit(floor(p * 21.0));
	float mowing = smoothstep(-0.3, 0.3, sin(dot(p, vec2(0.33, -0.25))));
	vec3 grass = mix(vec3(0.115, 0.19, 0.13), vec3(0.24, 0.35, 0.16), field * 0.5 + 0.5);
	ALBEDO = grass * (0.88 + 0.18 * fleck + 0.085 * mowing);
	ROUGHNESS = 1.0;
}
