class_name RaceTrack
extends Node3D

signal gate_entered(index: int, body: Node3D)

const C = preload("res://game/config.gd")
const DATA = preload("res://game/track_data.gd")
const ASPHALT = preload("res://shaders/asphalt.gdshader")
const WALLS = preload("res://shaders/walls.gdshader")

var data: TrackData = DATA.new()
var ground_height := 0.0

func build(seed_value: int) -> void:
	data.generate(seed_value)
	var road_material := ShaderMaterial.new()
	road_material.shader = ASPHALT
	road_material.set_shader_parameter("half_road", C.ROAD_WIDTH * 0.5)
	road_material.set_shader_parameter("stripe_length", C.STRIPE_LENGTH)
	road_material.set_shader_parameter("track_length", data.length)
	var wall_material := ShaderMaterial.new()
	wall_material.shader = WALLS
	wall_material.set_shader_parameter("panel_length", C.STRIPE_LENGTH)
	var road_edge := C.ROAD_WIDTH * 0.5
	var curb_edge := road_edge + C.CURB_WIDTH
	var road := SurfaceTool.new()
	road.begin(Mesh.PRIMITIVE_TRIANGLES)
	ground_height = INF
	for i in data.points.size():
		ground_height = minf(ground_height, data.points[i].y)
		_strip(road, i, -road_edge, road_edge, 0.0, 0.0, Vector3.UP, true)
		_strip(road, i, -curb_edge, -road_edge, 0.035, 0.0, Vector3.UP, true)
		_strip(road, i, road_edge, curb_edge, 0.0, 0.035, Vector3.UP, true)
	var road_mesh := road.commit()
	_add_visual(road_mesh, road_material, "Asphalt and curbs")
	_add_collision(road_mesh, C.ROAD_LAYER, "Road collider")
	var walls := SurfaceTool.new()
	walls.begin(Mesh.PRIMITIVE_TRIANGLES)
	var wall_collision := SurfaceTool.new()
	wall_collision.begin(Mesh.PRIMITIVE_TRIANGLES)
	for side: float in [-1.0, 1.0]:
		var inner := side * curb_edge
		var outer := side * (curb_edge + C.BARRIER_WIDTH)
		for i in data.points.size():
			var inwards := data.right[i] * -side
			_strip(walls, i, inner, inner, 0.015, C.BARRIER_HEIGHT, inwards, false)
			_strip(walls, i, inner, outer, C.BARRIER_HEIGHT, C.BARRIER_HEIGHT, Vector3.UP, false)
			_strip(walls, i, outer, outer, C.BARRIER_HEIGHT, 0.015, -inwards, false)
			# The upper collision extension keeps airborne cars inside the racing surface.
			_strip(wall_collision, i, inner, inner, -0.25, C.BARRIER_COLLISION_HEIGHT, inwards, false)
	_add_visual(walls.commit(), wall_material, "Safety barriers")
	_add_collision(wall_collision.commit(), C.WALL_LAYER, "Continuous barrier collider")
	_tyre_stacks(curb_edge)
	for i in C.CHECKPOINTS:
		var gate := Area3D.new()
		gate.name = "Checkpoint %d" % i
		gate.collision_layer = 0
		gate.collision_mask = C.CAR_LAYER
		gate.monitorable = false
		gate.transform = data.frame_at(i * data.length / C.CHECKPOINTS, 0.0, 1.5)
		var shape := BoxShape3D.new()
		shape.size = Vector3(C.ROAD_WIDTH, 3.8, 0.65)
		var collider := CollisionShape3D.new()
		collider.shape = shape
		gate.add_child(collider)
		gate.body_entered.connect(_on_gate.bind(i))
		add_child(gate)

func _tyre_stacks(edge: float) -> void:
	var tyre := TorusMesh.new()
	tyre.inner_radius = 0.124
	tyre.outer_radius = 0.34
	tyre.rings = 8
	tyre.ring_segments = 6
	var rubber := CircuitGeometry.material(Color("#22282b"), 0.95)
	var model := SurfaceTool.new()
	model.begin(Mesh.PRIMITIVE_TRIANGLES)
	for tier in 4:
		model.append_from(tyre, 0, Transform3D(Basis.IDENTITY, Vector3.UP * (0.13 + tier * 0.245)))
	model.set_material(rubber)
	var stack_mesh := model.commit()
	var per_pattern := C.STRIPE_LENGTH * 8.0
	for block in ceili(data.length / per_pattern):
		var start := block * per_pattern + per_pattern * 0.5
		var count := maxi(0, floori((minf(data.length, (block + 1) * per_pattern) - start) / 0.64))
		if count == 0:
			continue
		var batch := MultiMesh.new()
		batch.transform_format = MultiMesh.TRANSFORM_3D
		batch.mesh = stack_mesh
		batch.instance_count = count * 2
		var origin := data.point_at(start)
		for i in count:
			for side_index in 2:
				var side := -1.0 if side_index == 0 else 1.0
				var pose := data.frame_at(start + (i + 0.5) * 0.64, side * (edge + 0.34), 0.0)
				pose.origin -= origin
				batch.set_instance_transform(i * 2 + side_index, pose)
		var visual := MultiMeshInstance3D.new()
		visual.multimesh = batch
		visual.position = origin
		visual.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(visual)

func _on_gate(body: Node3D, index: int) -> void:
	gate_entered.emit(index, body)

func _strip(surface: SurfaceTool, i: int, x0: float, x1: float, y0: float, y1: float,
		normal: Vector3, road_uv: bool) -> void:
	var j: int = data.wrap_index(i + 1)
	var a: Vector3 = data.points[i] + data.right[i] * x0 + Vector3.UP * y0
	var b: Vector3 = data.points[i] + data.right[i] * x1 + Vector3.UP * y1
	var c: Vector3 = data.points[j] + data.right[j] * x1 + Vector3.UP * y1
	var d: Vector3 = data.points[j] + data.right[j] * x0 + Vector3.UP * y0
	var along0: float = i * data.step
	var along1: float = data.length if j == 0 else j * data.step
	var ua := Vector2(x0, along0) if road_uv else Vector2(along0, y0 / C.BARRIER_HEIGHT)
	var ub := Vector2(x1, along0) if road_uv else Vector2(along0, y1 / C.BARRIER_HEIGHT)
	var uc := Vector2(x1, along1) if road_uv else Vector2(along1, y1 / C.BARRIER_HEIGHT)
	var ud := Vector2(x0, along1) if road_uv else Vector2(along1, y0 / C.BARRIER_HEIGHT)
	if (c - a).cross(b - a).dot(normal) >= 0.0:
		_vertex(surface, a, ua, normal)
		_vertex(surface, b, ub, normal)
		_vertex(surface, c, uc, normal)
		_vertex(surface, a, ua, normal)
		_vertex(surface, c, uc, normal)
		_vertex(surface, d, ud, normal)
	else:
		_vertex(surface, a, ua, normal)
		_vertex(surface, c, uc, normal)
		_vertex(surface, b, ub, normal)
		_vertex(surface, a, ua, normal)
		_vertex(surface, d, ud, normal)
		_vertex(surface, c, uc, normal)

func _vertex(surface: SurfaceTool, position: Vector3, uv: Vector2, normal: Vector3) -> void:
	surface.set_normal(normal)
	surface.set_uv(uv)
	surface.add_vertex(position)

func _add_visual(mesh: Mesh, material: Material, label: String) -> void:
	var instance := MeshInstance3D.new()
	instance.name = label
	instance.mesh = mesh
	instance.material_override = material
	add_child(instance)

func _add_collision(mesh: Mesh, layer: int, label: String) -> void:
	var body := StaticBody3D.new()
	body.name = label
	body.collision_layer = layer
	body.collision_mask = C.CAR_LAYER
	var collider := CollisionShape3D.new()
	var shape := mesh.create_trimesh_shape()
	shape.backface_collision = true
	collider.shape = shape
	body.add_child(collider)
	add_child(body)
