class_name EnvironmentBuilder
extends RefCounted
## Procedural sky with sun, soft exponential fog, tone mapping, glow and the
## shadow-casting sun light limited to 150 m shadow distance (R-05, R-31).

const SKY_TOP := Color(0.22, 0.42, 0.78)
const SKY_HORIZON := Color(0.68, 0.78, 0.88)
const GROUND_HORIZON := Color(0.5, 0.56, 0.5)
const GROUND_BOTTOM := Color(0.2, 0.26, 0.17)


static func build(parent: Node3D) -> DirectionalLight3D:
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-Config.SUN_ELEVATION_DEG, Config.SUN_AZIMUTH_DEG, 0.0)
	sun.light_color = Color(1.0, 0.95, 0.88)
	sun.light_energy = 1.25
	sun.light_angular_distance = 0.6
	sun.shadow_enabled = true
	sun.shadow_bias = 0.04
	sun.shadow_normal_bias = 1.2
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_max_distance = Config.SHADOW_DISTANCE
	sun.directional_shadow_blend_splits = true
	sun.directional_shadow_fade_start = 0.85
	parent.add_child(sun)

	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = SKY_TOP
	sky_mat.sky_horizon_color = SKY_HORIZON
	sky_mat.sky_curve = 0.12
	sky_mat.ground_horizon_color = GROUND_HORIZON
	sky_mat.ground_bottom_color = GROUND_BOTTOM
	sky_mat.sun_angle_max = 25.0
	sky_mat.sun_curve = 0.12
	var sky := Sky.new()
	sky.sky_material = sky_mat

	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	# Part sky, part warm grey: keeps shadows from turning too blue.
	env.ambient_light_sky_contribution = 0.7
	env.ambient_light_color = Color(0.62, 0.6, 0.55)
	env.ambient_light_energy = 1.0
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_AGX
	env.tonemap_exposure = 1.05
	env.ssao_enabled = true
	env.ssao_radius = 1.2
	env.ssao_intensity = 1.4
	env.glow_enabled = true
	env.glow_intensity = 0.55
	env.glow_bloom = 0.03
	env.glow_hdr_threshold = 1.1
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SOFTLIGHT
	env.fog_enabled = true
	env.fog_mode = Environment.FOG_MODE_EXPONENTIAL
	env.fog_density = Config.FOG_DENSITY
	env.fog_light_color = SKY_HORIZON
	env.fog_light_energy = 1.0
	env.fog_sun_scatter = 0.12
	env.fog_aerial_perspective = 0.35
	env.fog_sky_affect = 0.2
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.12
	env.adjustment_contrast = 1.04
	var world_env := WorldEnvironment.new()
	world_env.name = "WorldEnvironment"
	world_env.environment = env
	parent.add_child(world_env)
	return sun
