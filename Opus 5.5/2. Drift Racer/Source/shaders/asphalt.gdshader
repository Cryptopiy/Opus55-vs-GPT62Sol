// Road surface (R-03): asphalt noise, dashed centre line (3 m dash / 3 m gap),
// solid edge lines and a checkered start/finish band. UV.x = lateral metres,
// UV.y = distance along the centreline (metres).
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://shaders/noise.gdshaderinc"

uniform float half_width = 7.0;
uniform float center_dash = 3.0;
uniform float center_gap = 3.0;
uniform float center_line_width = 0.15;
uniform float edge_line_width = 0.2;
uniform float edge_line_inset = 0.3;
uniform float start_line_depth = 1.6;
uniform float checker_size = 0.5;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float u = UV.x;
	float v = UV.y;
	vec2 wp = world_pos.xz;
	float cam_dist = length(world_pos - CAMERA_POSITION_WORLD);
	float detail = 1.0 - smoothstep(30.0, 140.0, cam_dist);
	float coarse = fbm(wp * 0.045);
	float mid = fbm(wp * 0.6);
	float grain = value_noise(wp * 9.0);
	vec3 asphalt = vec3(0.085, 0.087, 0.092) * (0.82 + 0.3 * coarse) * (0.9 + 0.2 * mid);
	asphalt += vec3(0.025) * (grain - 0.5) * detail;
	// Slightly darker rubbered-in band in the middle where cars drive most.
	asphalt *= 1.0 - 0.12 * (1.0 - smoothstep(1.0, 4.5, abs(u)));

	float period = center_dash + center_gap;
	float fv = mod(v, period);
	float aav = max(fwidth(v), 0.0001);
	float dash = smoothstep(0.0, aav, fv) * (1.0 - smoothstep(center_dash - aav, center_dash, fv));
	float centre = aa_band(abs(u), center_line_width * 0.5) * dash;
	float edge = aa_band(abs(abs(u) - (half_width - edge_line_inset)), edge_line_width * 0.5);
	float paint = max(centre, edge);
	vec3 paint_col = vec3(0.86, 0.86, 0.84) * (0.92 + 0.08 * mid);
	vec3 col = mix(asphalt, paint_col, paint);
	float rough = mix(0.86 + 0.08 * grain, 0.62, paint);

	if (v < start_line_depth) {
		vec2 cell = floor(vec2(u + half_width, v) / checker_size);
		float checker = mod(cell.x + cell.y, 2.0);
		col = mix(vec3(0.04), vec3(0.9), checker);
		rough = 0.6;
	}
	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = 0.35;
}
