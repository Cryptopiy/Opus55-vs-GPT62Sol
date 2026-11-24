extends Node3D
## P-25 contact feedback + P-13 confetti. Everything is pooled / preallocated:
## - dust puffs: a fixed pool of one-shot CPUParticles3D emitters reused round-robin,
## - ball trail: one MultiMesh of quads fed from a ring buffer of recent ball positions (no per-frame allocation),
## - confetti: one emitter restarted on success.

const C := preload("res://scripts/core/config.gd")
const Mats := preload("res://scripts/core/materials.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")

const TRAIL_LIFETIME := 0.55
const TRAIL_COLORS := {"steel": Color(0.7, 0.85, 1.0), "rubber": Color(1.0, 0.5, 0.75), "wood": Color(1.0, 0.75, 0.45)}

var sim_running := false

var _dust: Array[CPUParticles3D] = []
var _dust_next := 0
var _confetti: CPUParticles3D
var _trail: MultiMeshInstance3D
var _trail_pts := PackedVector3Array()
var _trail_age := PackedFloat32Array()
var _trail_head := 0
var _trail_count := 0
var _trail_color := Color.WHITE
var _ball: RigidBody3D


func _ready() -> void:
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	for i in C.DUST_POOL:
		_dust.append(_make_dust())
	_confetti = _make_confetti()
	_make_trail()


func _make_dust() -> CPUParticles3D:
	var p := CPUParticles3D.new()
	p.emitting = false
	p.one_shot = true
	p.amount = C.DUST_PARTICLES
	p.lifetime = 0.7
	p.explosiveness = 0.95
	var q := QuadMesh.new()
	q.size = Vector2(0.22, 0.22)
	q.material = Mats.particle(false)
	p.mesh = q
	p.emission_shape = CPUParticles3D.EMISSION_SHAPE_SPHERE
	p.emission_sphere_radius = 0.12
	p.direction = Vector3.UP
	p.spread = 75.0
	p.initial_velocity_min = 0.6
	p.initial_velocity_max = 2.0
	p.gravity = Vector3(0, -1.2, 0)
	p.damping_min = 2.0
	p.damping_max = 3.0
	p.scale_amount_min = 0.6
	p.scale_amount_max = 1.5
	var grow := Curve.new()
	grow.add_point(Vector2(0, 0.5))
	grow.add_point(Vector2(1, 1.6))
	p.scale_amount_curve = grow
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.86, 0.82, 0.74, 0.7))
	ramp.set_color(1, Color(0.86, 0.82, 0.74, 0.0))
	p.color_ramp = ramp
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(p)
	return p


func _make_confetti() -> CPUParticles3D:
	var p := CPUParticles3D.new()
	p.emitting = false
	p.one_shot = true
	p.amount = C.CONFETTI_COUNT
	p.lifetime = 2.6
	p.explosiveness = 0.9
	var q := QuadMesh.new()
	q.size = Vector2(0.16, 0.09)
	q.material = Mats.particle(false)
	p.mesh = q
	p.emission_shape = CPUParticles3D.EMISSION_SHAPE_SPHERE
	p.emission_sphere_radius = 0.3
	p.direction = Vector3.UP
	p.spread = 38.0
	p.initial_velocity_min = 5.0
	p.initial_velocity_max = 9.5
	p.gravity = Vector3(0, -7.0, 0)
	p.damping_min = 0.6
	p.damping_max = 1.4
	p.angle_min = -180.0
	p.angle_max = 180.0
	p.angular_velocity_min = -540.0
	p.angular_velocity_max = 540.0
	var colors := Gradient.new()
	colors.set_color(0, Color(1.0, 0.3, 0.35))
	colors.set_color(1, Color(0.35, 0.6, 1.0))
	colors.add_point(0.25, Color(1.0, 0.85, 0.2))
	colors.add_point(0.5, Color(0.3, 0.95, 0.5))
	colors.add_point(0.75, Color(0.9, 0.4, 1.0))
	p.color_initial_ramp = colors
	var fade := Gradient.new()
	fade.set_color(0, Color(1, 1, 1, 1))
	fade.set_color(1, Color(1, 1, 1, 0))
	fade.add_point(0.75, Color(1, 1, 1, 1))
	p.color_ramp = fade
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(p)
	return p


func _make_trail() -> void:
	_trail_pts.resize(C.TRAIL_POINTS)
	_trail_age.resize(C.TRAIL_POINTS)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	var q := QuadMesh.new()
	q.size = Vector2(1, 1)
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.vertex_color_use_as_albedo = true
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mat.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	q.material = mat
	mm.mesh = q
	mm.instance_count = C.TRAIL_POINTS
	mm.visible_instance_count = 0
	_trail = MultiMeshInstance3D.new()
	_trail.multimesh = mm
	_trail.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_trail)


func attach_ball(ball: RigidBody3D, ball_type: String) -> void:
	_ball = ball
	_trail_color = TRAIL_COLORS.get(ball_type, Color.WHITE)
	_trail_count = 0
	_trail.multimesh.visible_instance_count = 0


func clear() -> void:
	_ball = null
	_trail_count = 0
	_trail.multimesh.visible_instance_count = 0
	for p in _dust:
		p.emitting = false
	_confetti.emitting = false


func dust(pos: Vector3, strength: float) -> void:
	var p := _dust[_dust_next]
	_dust_next = (_dust_next + 1) % _dust.size()
	p.global_position = pos
	p.initial_velocity_max = 1.4 + minf(strength, 12.0) * 0.15
	p.restart()


func confetti(at: Vector2) -> void:
	_confetti.global_position = Vector3(at.x, at.y + 0.3, 0.2)
	_confetti.restart()


func active_particles() -> int:
	var n := 0
	for p in _dust:
		if p.emitting:
			n += p.amount
	if _confetti.emitting:
		n += _confetti.amount
	return n


func _process(delta: float) -> void:
	if _ball == null or not is_instance_valid(_ball):
		return
	# age in simulation seconds: the trail freezes while the simulation is paused
	var dt := delta if sim_running else 0.0
	for i in _trail_count:
		_trail_age[i] += dt
	var p := _ball.get_global_transform_interpolated().origin
	var last := _trail_pts[(_trail_head - 1 + C.TRAIL_POINTS) % C.TRAIL_POINTS]
	if _trail_count == 0 or p.distance_to(last) >= C.TRAIL_MIN_STEP:
		_trail_pts[_trail_head] = p
		_trail_age[_trail_head] = 0.0
		_trail_head = (_trail_head + 1) % C.TRAIL_POINTS
		_trail_count = mini(_trail_count + 1, C.TRAIL_POINTS)
	_update_trail_mesh()


func _update_trail_mesh() -> void:
	var mm := _trail.multimesh
	var n := 0
	for k in range(1, _trail_count):
		# walk from newest to oldest
		var i_new := (_trail_head - k + C.TRAIL_POINTS) % C.TRAIL_POINTS
		var i_old := (_trail_head - k - 1 + C.TRAIL_POINTS) % C.TRAIL_POINTS
		var age := _trail_age[i_old]
		if age > TRAIL_LIFETIME:
			break
		var a := _trail_pts[i_new]
		var b := _trail_pts[i_old]
		var d := a - b
		var seg_len := d.length()
		if seg_len < 0.0001:
			continue
		var fade := 1.0 - age / TRAIL_LIFETIME
		var dir := d / seg_len
		var side := Vector3(-dir.y, dir.x, 0.0) * C.TRAIL_WIDTH * fade
		mm.set_instance_transform(n, Transform3D(Basis(dir * seg_len, side, Vector3.BACK), (a + b) * 0.5 + Vector3(0, 0, 0.05)))
		mm.set_instance_color(n, Color(_trail_color.r, _trail_color.g, _trail_color.b, 0.55 * fade))
		n += 1
	mm.visible_instance_count = n
