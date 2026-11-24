shader_type spatial;
render_mode unshaded, cull_disabled;

varying vec2 drawing;
void vertex() {
	drawing = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xy;
}
float ink(vec2 coordinate, float width) {
	vec2 lines = abs(fract(coordinate + 0.5) - 0.5) / max(fwidth(coordinate), vec2(0.001));
	return 1.0 - smoothstep(width, width + 0.85, min(lines.x, lines.y));
}
void fragment() {
	float elevation = smoothstep(-8.0, 21.0, drawing.y);
	vec3 paper = mix(vec3(0.44, 0.57, 0.69), vec3(0.82, 0.88, 0.91), elevation);
	float focus = exp(-0.0035 * dot(drawing - vec2(-1.0, 6.0), drawing - vec2(-1.0, 6.0)));
	paper += focus * vec3(0.08, 0.065, 0.06);
	float minor = ink(drawing, 0.16);
	float major = ink(drawing / 5.0, 0.32);
	ALBEDO = paper - vec3(0.13, 0.115, 0.10) * (minor * 0.23 + major * 0.47);
}
