shader_type spatial;
render_mode cull_disabled;

uniform float panel_length;

void fragment() {
	float block = mod(floor(UV.x / panel_length), 8.0);
	float tyre = step(4.0, block);
	if (tyre > 0.5) {
		discard;
	}
	float joint = smoothstep(0.0, 0.035, abs(mod(UV.x + 0.022, panel_length) - 0.022));
	vec3 concrete = mix(vec3(0.35, 0.40, 0.43), vec3(0.73, 0.75, 0.70), step(0.45, UV.y));
	float reflector = step(0.82, UV.y) * (1.0 - step(0.26, mod(UV.x, panel_length)));
	ALBEDO = mix(concrete * (0.35 + 0.65 * joint), vec3(0.94, 0.48, 0.08), reflector);
	ROUGHNESS = 0.91;
}
