class_name CarEffects
extends Node3D

const C = preload("res://game/config.gd")

var car: DriftCar
var smoke: Array[GPUParticles3D] = []
var sparks: GPUParticles3D
var previous_speed := 0.0
var scrape_burst := 0.0

func build(owner_car: DriftCar) -> void:
	car = owner_car
	for i in 2:
		var outlet := _emitter(false)
		outlet.name = "Rear tyre smoke %d" % (i + 1)
		smoke.append(outlet)
	sparks = _emitter(true)
	sparks.name = "Barrier sparks"

func update_effects(delta: float) -> void:
	for i in 2:
		var wheel := i + 2
		var slip := clampf((car.wheel_slip[wheel] - C.SKID_THRESHOLD * 0.75) / 0.82, 0.0, 1.0)
		smoke[i].emitting = slip > 0.02 and car.wheel_contact[wheel] and car.speed > 3.2
		if smoke[i].emitting:
			smoke[i].global_position = car.wheel_points[wheel] + Vector3.UP * 0.12
			smoke[i].amount_ratio = lerpf(0.1, 1.0, slip)
	scrape_burst = maxf(0.0, scrape_burst - delta)
	if car.wall_contact and maxf(car.speed, previous_speed) * C.KMH > C.SCRAPE_KMH:
		scrape_burst = 0.13
		# A one-tick impact still has time to emit a visible shower at the contact patch.
		var up := car.global_basis.y
		var rear := car.global_basis.z
		var inward := car.global_position - car.wall_point
		inward = inward.slide(up).slide(rear).normalized()
		var origin := car.wall_point + inward * 0.37 + rear * 0.48 + up * 0.24
		var trail := (rear + up * 0.27 + inward * 0.49).normalized()
		sparks.look_at_from_position(origin, origin + trail, up)
	sparks.emitting = scrape_burst > 0.0
	previous_speed = car.speed

func _emitter(is_sparks: bool) -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.emitting = false
	particles.amount = 62 if is_sparks else 92
	particles.lifetime = 0.34 if is_sparks else 1.45
	particles.fixed_fps = 30
	particles.local_coords = false
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	particles.visibility_aabb = AABB(Vector3(-12.0, -3.0, -12.0), Vector3(24.0, 11.0, 24.0))
	var process := ParticleProcessMaterial.new()
	process.direction = Vector3.FORWARD if is_sparks else Vector3.UP
	process.spread = 37.0 if is_sparks else 63.0
	process.initial_velocity_min = 3.2 if is_sparks else 0.65
	process.initial_velocity_max = 8.5 if is_sparks else 2.05
	process.gravity = Vector3.DOWN * 8.2 if is_sparks else Vector3.UP * 0.62
	process.scale_min = 0.45 if is_sparks else 0.34
	process.scale_max = 1.10 if is_sparks else 0.86
	var ramp := Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.16, 0.72, 1.0])
	if is_sparks:
		ramp.colors = PackedColorArray([Color(1.0, 0.88, 0.47, 1.0), Color(1.0, 0.65, 0.20, 0.9),
			Color(0.99, 0.31, 0.05, 0.65), Color(0.72, 0.15, 0.01, 0.0)])
	else:
		ramp.colors = PackedColorArray([Color(0.80, 0.83, 0.83, 0.0), Color(0.82, 0.85, 0.86, 0.51),
			Color(0.77, 0.81, 0.84, 0.18), Color(0.69, 0.76, 0.82, 0.0)])
	var ramp_texture := GradientTexture1D.new()
	ramp_texture.gradient = ramp
	process.color_ramp = ramp_texture
	if not is_sparks:
		var scale_over_life := Curve.new()
		scale_over_life.max_value = 3.0
		scale_over_life.add_point(Vector2(0.0, 0.55))
		scale_over_life.add_point(Vector2(1.0, 2.6))
		var curve := CurveTexture.new()
		curve.curve = scale_over_life
		process.scale_curve = curve
	particles.process_material = process
	var quad := QuadMesh.new()
	quad.size = Vector2(0.23, 0.061) if is_sparks else Vector2(1.18, 1.18)
	var paint := StandardMaterial3D.new()
	paint.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	paint.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	paint.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	paint.vertex_color_use_as_albedo = true
	paint.albedo_texture = _particle_texture(is_sparks)
	if is_sparks:
		paint.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	else:
		paint.proximity_fade_enabled = true
		paint.proximity_fade_distance = 0.50
	quad.material = paint
	particles.draw_pass_1 = quad
	add_child(particles)
	return particles

func _particle_texture(is_sparks: bool) -> GradientTexture2D:
	var fade := Gradient.new()
	fade.offsets = PackedFloat32Array([0.0, 0.45, 1.0])
	fade.colors = PackedColorArray([Color.WHITE, Color(1, 1, 1, 0.52), Color(1, 1, 1, 0)])
	var texture := GradientTexture2D.new()
	texture.width = 24 if is_sparks else 48
	texture.height = texture.width
	texture.gradient = fade
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(0.5, 0.0)
	return texture
