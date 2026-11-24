class_name LabEffects
extends Node3D

const C = preload("res://scripts/config.gd")
const MAX_SPARKS := 96
const TRAIL_COUNT := 20
const TRAIL_INTERVAL := 0.028

var rng := RandomNumberGenerator.new()
var particles: MultiMesh
var trail: MultiMesh
var positions := PackedVector3Array()
var speeds := PackedVector3Array()
var lifetimes := PackedFloat32Array()
var totals := PackedFloat32Array()
var sizes := PackedFloat32Array()
var colors := PackedColorArray()
var trail_points := PackedVector3Array()
var trail_age := PackedFloat32Array()
var particle_index := 0
var trail_index := 0
var trail_timer := 0.0
var alternate := 0
var board: LabBoard

func _ready() -> void:
	rng.seed = 483325
	var shader := ShaderMaterial.new()
	shader.shader = preload("res://shaders/particle.gdshader")
	var square := BoxMesh.new()
	square.size = Vector3(1, 1, 0.3)
	particles = _pool(square, shader, MAX_SPARKS)
	var orb := SphereMesh.new()
	orb.radial_segments = 8
	orb.rings = 4
	trail = _pool(orb, shader, TRAIL_COUNT)
	positions.resize(MAX_SPARKS)
	speeds.resize(MAX_SPARKS)
	lifetimes.resize(MAX_SPARKS)
	totals.resize(MAX_SPARKS)
	sizes.resize(MAX_SPARKS)
	colors.resize(MAX_SPARKS)
	trail_points.resize(TRAIL_COUNT)
	trail_age.resize(TRAIL_COUNT)
	clear()

func _pool(mesh: Mesh, paint: Material, count: int) -> MultiMesh:
	var node := MultiMeshInstance3D.new()
	node.material_override = paint
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var pool := MultiMesh.new()
	pool.transform_format = MultiMesh.TRANSFORM_3D
	pool.use_colors = true
	pool.mesh = mesh
	pool.instance_count = count
	node.multimesh = pool
	add_child(node)
	return pool

func clear() -> void:
	lifetimes.fill(0.0)
	trail_age.fill(0.0)
	particle_index = 0
	trail_index = 0
	trail_timer = 0
	for index in range(MAX_SPARKS):
		_hide_instance(particles, index)
	for index in range(TRAIL_COUNT):
		_hide_instance(trail, index)

func feedback(event: String, point: Vector3, strength: float) -> void:
	if event == "win":
		for index in range(54):
			var color := Color("#ffb44f")
			match index % 4:
				1: color = Color("#62ead2")
				2: color = Color("#eee7dc")
				3: color = Color("#d077e5")
			_spawn(point + Vector3(0, 0.35, 0), Vector3(rng.randf_range(-5, 5), rng.randf_range(3, 7), rng.randf_range(-1.1, 1.5)),
				color, rng.randf_range(0.75, 1.4), rng.randf_range(0.075, 0.17))
	elif event in ["steel", "wood", "rubber", "heavy"] and strength >= C.TIP_IMPACT:
		var dust := Color("#d6ccc3")
		for index in range(8):
			_spawn(point + Vector3(0, -0.16, 0.73), Vector3(rng.randf_range(-1.5, 1.5), rng.randf_range(0.35, 1.6), rng.randf_range(-0.2, 0.3)),
				dust, rng.randf_range(0.22, 0.44), rng.randf_range(0.045, 0.12))

func _spawn(point: Vector3, speed: Vector3, color: Color, span: float, size: float) -> void:
	var index := particle_index
	particle_index = (particle_index + 1) % MAX_SPARKS
	positions[index] = point
	speeds[index] = speed
	lifetimes[index] = span
	totals[index] = span
	sizes[index] = size
	colors[index] = color

func _process(delta: float) -> void:
	if is_instance_valid(board) and board.active and board.outcome.is_empty() and not get_tree().paused:
		trail_timer += delta
		if trail_timer >= TRAIL_INTERVAL and board.ball.linear_velocity.length_squared() > 2.5:
			trail_timer = fmod(trail_timer, TRAIL_INTERVAL)
			trail_points[trail_index] = board.ball.position
			trail_age[trail_index] = 0.29
			trail_index = (trail_index + 1) % TRAIL_COUNT
	for index in range(TRAIL_COUNT):
		if trail_age[index] > 0:
			trail_age[index] -= delta
			var alpha := maxf(0, trail_age[index] / 0.29)
			_show_instance(trail, index, trail_points[index], 0.20 * alpha, Color(0.63, 0.86, 1.0, alpha * 0.30), 0.0)
	for index in range(MAX_SPARKS):
		if lifetimes[index] <= 0:
			continue
		lifetimes[index] -= delta
		if lifetimes[index] <= 0:
			_hide_instance(particles, index)
			continue
		positions[index] += speeds[index] * delta
		speeds[index].y -= 6.0 * delta
		var alpha := lifetimes[index] / totals[index]
		var paint := colors[index]
		paint.a = alpha
		_show_instance(particles, index, positions[index], sizes[index] * (1.12 - 0.30 * alpha), paint, alpha * 7.0 + index)

func _show_instance(pool: MultiMesh, index: int, point: Vector3, size: float, paint: Color, rotation_angle: float) -> void:
	var axes := Basis(Vector3.BACK, rotation_angle).scaled(Vector3.ONE * size)
	pool.set_instance_transform(index, Transform3D(axes, point))
	pool.set_instance_color(index, paint)

func _hide_instance(pool: MultiMesh, index: int) -> void:
	pool.set_instance_transform(index, Transform3D(Basis.from_scale(Vector3.ZERO), Vector3.ZERO))

func active_particles() -> int:
	var count := 0
	for life in lifetimes:
		if life > 0.0:
			count += 1
	return count
