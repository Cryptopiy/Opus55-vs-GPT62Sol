// Translucent ghost / hint preview with a fresnel rim so the silhouette stays readable.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled;

uniform vec4 color : source_color = vec4(0.25, 0.95, 0.45, 0.45);

void fragment() {
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.0);
	ALBEDO = color.rgb;
	ALPHA = clamp(color.a + fres * 0.35, 0.0, 1.0);
}
