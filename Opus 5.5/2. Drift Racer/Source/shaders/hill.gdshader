// Distant hills / mountains (R-05): colour by height and slope, snow caps.
shader_type spatial;
render_mode diffuse_burley;

#include "res://shaders/noise.gdshaderinc"

uniform float peak_height = 200.0;
uniform float snow_line = 0.82;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float h = clamp(world_pos.y / max(peak_height, 1.0), 0.0, 1.0);
	float n = fbm(world_pos.xz * 0.01);
	float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);
	vec3 grass = mix(vec3(0.16, 0.27, 0.1), vec3(0.24, 0.33, 0.14), n);
	vec3 rock = mix(vec3(0.33, 0.31, 0.3), vec3(0.45, 0.42, 0.39), n);
	vec3 col = mix(grass, rock, smoothstep(0.35, 0.7, h + slope * 0.6 + (n - 0.5) * 0.3));
	float snow = smoothstep(snow_line - 0.05, snow_line + 0.05, h + (n - 0.5) * 0.12);
	col = mix(col, vec3(0.92, 0.93, 0.96), snow * (1.0 - smoothstep(0.55, 0.85, slope)));
	ALBEDO = col;
	ROUGHNESS = 0.95;
}
