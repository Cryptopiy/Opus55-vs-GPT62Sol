// Local 0.25 m snap grid shown around the cursor while placing / dragging (fades out radially).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 color : source_color = vec4(0.18, 0.32, 0.62, 0.55);
uniform float cell = 0.25;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 g = world_pos.xy / cell;
	vec2 fw = max(fwidth(g), vec2(0.0001));
	vec2 d = abs(fract(g - 0.5) - 0.5) / fw;
	float line = 1.0 - clamp(min(d.x, d.y), 0.0, 1.0);
	float fade = 1.0 - smoothstep(0.15, 0.5, length(UV - 0.5));
	ALBEDO = color.rgb;
	ALPHA = color.a * line * fade;
}
