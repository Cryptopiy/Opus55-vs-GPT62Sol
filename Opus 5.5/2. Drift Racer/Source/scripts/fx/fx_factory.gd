class_name FxFactory
extends RefCounted
## Shared particle resources (created once, reused by every car): tyre smoke
## (R-16) and barrier sparks (R-17). Textures are generated gradients.

static var _cache := {}


static func clear_cache() -> void:
	_cache.clear()


static func make_smoke() -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.name = "TyreSmoke"
	p.amount = Config.SMOKE_AMOUNT
	p.lifetime = Config.SMOKE_LIFETIME
	p.local_coords = false
	p.emitting = false
	p.process_material = _smoke_process()
	p.draw_pass_1 = _smoke_mesh()
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.visibility_aabb = AABB(Vector3(-40.0, -5.0, -40.0), Vector3(80.0, 30.0, 80.0))
	p.draw_order = GPUParticles3D.DRAW_ORDER_VIEW_DEPTH
	return p


static func make_sparks() -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.name = "Sparks"
	p.amount = Config.SPARK_AMOUNT
	p.lifetime = Config.SPARK_LIFETIME
	p.local_coords = false
	p.emitting = false
	p.transform_align = GPUParticles3D.TRANSFORM_ALIGN_Z_BILLBOARD_Y_TO_VELOCITY
	p.process_material = _spark_process()
	p.draw_pass_1 = _spark_mesh()
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	p.visibility_aabb = AABB(Vector3(-15.0, -5.0, -15.0), Vector3(30.0, 15.0, 30.0))
	return p


static func _smoke_process() -> ParticleProcessMaterial:
	if _cache.has("smoke_pm"):
		var cached: ParticleProcessMaterial = _cache["smoke_pm"]
		return cached
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	pm.emission_sphere_radius = 0.25
	pm.direction = Vector3(0.0, 1.0, 0.0)
	pm.spread = 70.0
	pm.initial_velocity_min = 0.6
	pm.initial_velocity_max = 2.2
	pm.gravity = Vector3(0.0, 0.55, 0.0)
	pm.damping_min = 1.0
	pm.damping_max = 2.0
	pm.angle_min = 0.0
	pm.angle_max = 360.0
	pm.angular_velocity_min = -40.0
	pm.angular_velocity_max = 40.0
	pm.scale_min = 0.8
	pm.scale_max = 1.2
	var scale_curve := Curve.new()
	scale_curve.max_value = 2.0
	scale_curve.add_point(Vector2(0.0, 0.4))
	scale_curve.add_point(Vector2(1.0, 1.8))
	var scale_tex := CurveTexture.new()
	scale_tex.curve = scale_curve
	pm.scale_curve = scale_tex
	var grad := Gradient.new()
	grad.set_color(0, Color(0.95, 0.95, 0.93, 0.75))
	grad.set_color(1, Color(0.9, 0.9, 0.9, 0.0))
	grad.add_point(0.4, Color(0.93, 0.93, 0.92, 0.5))
	var ramp := GradientTexture1D.new()
	ramp.gradient = grad
	pm.color_ramp = ramp
	_cache["smoke_pm"] = pm
	return pm


static func _smoke_mesh() -> QuadMesh:
	if _cache.has("smoke_mesh"):
		var cached: QuadMesh = _cache["smoke_mesh"]
		return cached
	var grad := Gradient.new()
	grad.set_color(0, Color(1.0, 1.0, 1.0, 1.0))
	grad.set_color(1, Color(1.0, 1.0, 1.0, 0.0))
	grad.add_point(0.45, Color(1.0, 1.0, 1.0, 0.55))
	var tex := GradientTexture2D.new()
	tex.gradient = grad
	tex.fill = GradientTexture2D.FILL_RADIAL
	tex.fill_from = Vector2(0.5, 0.5)
	tex.fill_to = Vector2(1.0, 0.5)
	tex.width = 64
	tex.height = 64
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	mat.vertex_color_use_as_albedo = true
	mat.albedo_texture = tex
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_PER_VERTEX
	mat.proximity_fade_enabled = true
	mat.proximity_fade_distance = 0.6
	var quad := QuadMesh.new()
	quad.size = Vector2(2.0, 2.0)
	quad.material = mat
	_cache["smoke_mesh"] = quad
	return quad


static func _spark_process() -> ParticleProcessMaterial:
	if _cache.has("spark_pm"):
		var cached: ParticleProcessMaterial = _cache["spark_pm"]
		return cached
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0.0, 0.55, -1.0)
	pm.spread = 38.0
	pm.initial_velocity_min = 5.0
	pm.initial_velocity_max = 13.0
	pm.inherit_velocity_ratio = 0.35
	pm.gravity = Vector3(0.0, -Config.GRAVITY, 0.0)
	pm.scale_min = 0.6
	pm.scale_max = 1.2
	var grad := Gradient.new()
	grad.set_color(0, Color(1.0, 0.9, 0.55, 1.0))
	grad.set_color(1, Color(1.0, 0.2, 0.02, 0.0))
	grad.add_point(0.35, Color(1.0, 0.55, 0.1, 1.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = grad
	pm.color_ramp = ramp
	_cache["spark_pm"] = pm
	return pm


static func _spark_mesh() -> QuadMesh:
	if _cache.has("spark_mesh"):
		var cached: QuadMesh = _cache["spark_mesh"]
		return cached
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.vertex_color_use_as_albedo = true
	mat.albedo_color = Color(6.0, 2.6, 0.6)
	var quad := QuadMesh.new()
	quad.size = Vector2(0.07, 0.42)
	quad.material = mat
	_cache["spark_mesh"] = quad
	return quad
