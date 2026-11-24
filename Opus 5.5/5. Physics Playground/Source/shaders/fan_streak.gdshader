// Air streak quad for the fan particles: soft horizontal line that fades with particle lifetime.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 color : source_color = vec4(0.8, 0.95, 1.0, 0.5);

void fragment() {
	// COLOR carries the particle colour/alpha from the process material's colour ramp
	float across = 1.0 - abs(UV.y * 2.0 - 1.0);
	float along = smoothstep(0.0, 0.3, UV.x) * smoothstep(1.0, 0.6, UV.x);
	ALBEDO = color.rgb * COLOR.rgb;
	ALPHA = color.a * COLOR.a * across * along;
}
