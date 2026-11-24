// Selection outline, used as material_overlay: an inflated back-face shell drawn in a flat colour.
shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled;

uniform vec4 color : source_color = vec4(1.0, 0.95, 0.4, 1.0);
uniform float width = 0.045;

void vertex() {
	VERTEX += NORMAL * width;
}

void fragment() {
	ALBEDO = color.rgb;
}
