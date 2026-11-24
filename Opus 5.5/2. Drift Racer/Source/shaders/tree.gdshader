// Low-poly trees (R-05). Vertex colour holds the base colour and fake AO;
// UV.x = 1 marks foliage, which is tinted per instance (INSTANCE_CUSTOM.rgb).
shader_type spatial;
render_mode diffuse_burley;

varying vec3 tint;

void vertex() {
	tint = mix(vec3(1.0), INSTANCE_CUSTOM.rgb, UV.x);
}

void fragment() {
	ALBEDO = COLOR.rgb * tint;
	ROUGHNESS = 0.92;
	SPECULAR = 0.25;
}
