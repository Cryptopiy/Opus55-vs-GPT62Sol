extends RefCounted
## Lighting/look (P-24): bright grey-blue backdrop, soft directional shadows, SSAO and bloom on emissive parts.
## Each Play swaps in a fresh World3D (fresh physics space => deterministic body IDs), so the environment is
## attached to the World3D resource directly instead of a WorldEnvironment node.

const C := preload("res://scripts/core/config.gd")


static func make_environment() -> Environment:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = C.COL_BG_BOTTOM.darkened(0.03)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.86, 0.9, 1.0)
	env.ambient_light_energy = 0.62
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.tonemap_exposure = 1.0
	env.tonemap_white = 6.0
	env.ssao_enabled = true
	env.ssao_radius = 1.4
	env.ssao_intensity = 1.8
	env.ssao_power = 1.4
	env.ssao_detail = 0.5
	env.glow_enabled = true
	env.glow_intensity = 0.7
	env.glow_strength = 1.0
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.1
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SOFTLIGHT
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.08
	return env


static func make_world(env: Environment) -> World3D:
	var w := World3D.new()
	w.environment = env
	return w


static func add_light(parent: Node3D) -> DirectionalLight3D:
	var light := DirectionalLight3D.new()
	light.name = "Sun"
	# from the upper front-left so shadows fall softly onto the back wall and the floor
	light.rotation_degrees = Vector3(-52.0, -28.0, 0.0)
	light.light_energy = 1.25
	light.light_color = Color(1.0, 0.98, 0.94)
	light.shadow_enabled = true
	light.shadow_blur = 2.0
	light.light_angular_distance = 0.0
	light.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	light.directional_shadow_max_distance = 90.0
	light.shadow_bias = 0.04
	light.shadow_normal_bias = 1.2
	parent.add_child(light)
	var fill := DirectionalLight3D.new()
	fill.name = "Fill"
	fill.rotation_degrees = Vector3(-20.0, 150.0, 0.0)
	fill.light_energy = 0.25
	fill.light_color = Color(0.75, 0.85, 1.0)
	parent.add_child(fill)
	return light
