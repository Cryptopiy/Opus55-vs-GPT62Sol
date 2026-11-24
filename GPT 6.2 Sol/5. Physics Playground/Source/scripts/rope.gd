class_name LabRope
extends Node3D

const C = preload("res://scripts/config.gd")
const G = preload("res://scripts/geometry.gd")

var segments: Array[RigidBody3D] = []
var joints: Array[PinJoint3D] = []
var bob: RigidBody3D
var sleeve: MultiMesh
var seams: MultiMesh
var points := PackedVector3Array()
var board: LabBoard
var anchor_position: Vector3
var rope_length: float
var last_creak := -3.0

static func vector_for(spec: Dictionary) -> Vector2:
	var parameters: Dictionary = spec.parameters
	var vector: Vector2 = parameters.get("rope_vector", C.ROPE_DEFAULT)
	if vector.is_zero_approx():
		vector = Vector2.DOWN
	var length := roundf(clampf(vector.length(), C.ROPE_MIN, C.ROPE_MAX) / C.ROPE_SEGMENT) * C.ROPE_SEGMENT
	return vector.normalized().rotated(deg_to_rad(spec.rotation)) * length

func assemble(owner_board: LabBoard, spec: Dictionary) -> void:
	board = owner_board
	anchor_position = C.in_plane(spec.position)
	position = anchor_position
	var rope := vector_for(spec)
	rope_length = rope.length()
	var axis := C.in_plane(rope.normalized())
	var count := roundi(rope_length / C.ROPE_SEGMENT)
	var anchor := StaticBody3D.new()
	anchor.collision_layer = 0
	anchor.collision_mask = 0
	add_child(anchor)
	var bracket := G.cylinder(anchor, 0.22, 0.67, G.material(Color("#7852b0"), 0.33))
	bracket.rotation.x = PI * 0.5
	var segment_shape := CapsuleShape3D.new()
	segment_shape.radius = C.ROPE_RADIUS
	segment_shape.height = C.ROPE_SEGMENT
	var previous: PhysicsBody3D = anchor
	for index in range(count):
		var start := axis * index * C.ROPE_SEGMENT
		var segment := G.mobile(self, start + axis * C.ROPE_SEGMENT * 0.5,
			rad_to_deg(Vector2.UP.angle_to(rope)), C.ROPE_MASS, 0.45, 0, board.active)
		segment.collision_layer = C.ROPE_LAYER
		segment.collision_mask = C.SOLID_LAYER | C.BODY_LAYER
		segment.linear_damp = 0.16
		segment.angular_damp = 0.48
		# The suspension sits behind the cradle, leaving its center clear for the ball.
		G.shape(segment, segment_shape, Vector3(0, 0, C.ROPE_CABLE_DEPTH))
		segments.append(segment)
		board.bodies.append(segment)
		if board.active:
			_pin(previous, segment, start)
		previous = segment
	bob = G.mobile(self, C.in_plane(rope) + axis * C.ROPE_BOB.y * 0.6, 0,
		C.ROPE_BOB_MASS, 0.79, 0.0, board.active)
	bob.angular_damp = 0.85
	G.box_shape(bob, C.ROPE_BOB)
	var bob_paint := G.material(Color("#a884e1"), 0.27)
	G.block(bob, C.ROPE_BOB, bob_paint)
	for offset in [-0.30, 0.30]:
		var cheek := Vector3(offset, 0.18, 0)
		G.box_shape(bob, Vector3(0.13, 0.20, 0.8), cheek)
		G.block(bob, Vector3(0.13, 0.20, 0.8), bob_paint, cheek)
	board.bodies.append(bob)
	if board.active:
		_pin(previous, bob, C.in_plane(rope))
	points.resize(count + 2)
	var cylinder := CylinderMesh.new()
	cylinder.top_radius = C.ROPE_RADIUS
	cylinder.bottom_radius = C.ROPE_RADIUS
	cylinder.height = 1.0
	cylinder.radial_segments = 10
	sleeve = _tube_parts(cylinder, count + 1)
	var sphere := SphereMesh.new()
	sphere.radius = C.ROPE_RADIUS
	sphere.height = C.ROPE_RADIUS * 2.0
	sphere.radial_segments = 10
	sphere.rings = 4
	seams = _tube_parts(sphere, count)
	_update_tube()

func _tube_parts(resource: Mesh, count: int) -> MultiMesh:
	var mesh := MultiMesh.new()
	mesh.transform_format = MultiMesh.TRANSFORM_3D
	mesh.mesh = resource
	mesh.instance_count = count
	var view := MultiMeshInstance3D.new()
	view.multimesh = mesh
	view.material_override = G.material(Color("#7855af"), 0.16)
	add_child(view)
	view.position.z = C.ROPE_CABLE_DEPTH
	return mesh

func _pin(first: PhysicsBody3D, second: PhysicsBody3D, point: Vector3) -> void:
	var joint := PinJoint3D.new()
	add_child(joint)
	joint.position = point
	joint.exclude_nodes_from_collision = true
	joint.node_a = joint.get_path_to(first)
	joint.node_b = joint.get_path_to(second)
	joints.append(joint)

func step() -> void:
	if not is_instance_valid(bob):
		return
	var endpoint := bob.position.distance_to(Vector3.ZERO) - C.ROPE_BOB.y * 0.6
	board.max_rope_stretch = maxf(board.max_rope_stretch, endpoint / rope_length - 1.0)
	if bob.linear_velocity.length() > 2.5 and board.elapsed - last_creak > 1.1:
		last_creak = board.elapsed
		board.feedback.emit("rope", global_position, bob.linear_velocity.length())

func _process(_delta: float) -> void:
	if board.active and board.outcome.is_empty():
		_update_tube()

func _update_tube() -> void:
	points[0] = Vector3.ZERO
	for index in range(segments.size()):
		points[index + 1] = segments[index].position
	points[points.size() - 1] = bob.position
	# Rounded joins cover the seams as the cable bends. The buffers are allocated once.
	for index in range(points.size() - 1):
		var start := points[index]
		var finish := points[index + 1]
		var delta := finish - start
		var angle := atan2(-delta.x, delta.y)
		var axes := Basis(Vector3.BACK, angle).scaled_local(Vector3(1.0, maxf(0.001, delta.length()), 1.0))
		sleeve.set_instance_transform(index, Transform3D(axes, (start + finish) * 0.5))
		if index < seams.instance_count:
			seams.set_instance_transform(index, Transform3D(Basis.IDENTITY, finish))
