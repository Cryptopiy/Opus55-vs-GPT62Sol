class_name LabGeometry
extends RefCounted

static var materials: Dictionary = {}
static var boxes: Dictionary = {}
static var bevels: Dictionary = {}
static var spheres: Dictionary = {}

static func material(color: Color, metal := 0.0, emission := 0.0) -> StandardMaterial3D:
	var key := Vector3i(color.to_rgba32(), roundi(metal * 100), roundi(emission * 100))
	if not materials.has(key):
		var value := StandardMaterial3D.new()
		value.albedo_color = color
		value.roughness = 0.59
		value.metallic = metal
		if color.a < 1.0:
			value.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			value.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			value.no_depth_test = false
		if emission > 0.0:
			value.emission_enabled = true
			value.emission = color
			value.emission_energy_multiplier = emission
		materials[key] = value
	return materials[key]

static func mesh(parent: Node3D, resource: Mesh, paint: Material, at := Vector3.ZERO) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = resource
	node.material_override = paint
	parent.add_child(node)
	node.position = at
	return node

static func block(parent: Node3D, size: Vector3, paint: Material, at := Vector3.ZERO) -> MeshInstance3D:
	if not boxes.has(size):
		var primitive := BoxMesh.new()
		primitive.size = size
		boxes[size] = primitive
	return mesh(parent, boxes[size], paint, at)

static func rounded(parent: Node3D, size: Vector3, paint: Material, radius: float, at := Vector3.ZERO) -> MeshInstance3D:
	var key := Vector4(size.x, size.y, size.z, radius)
	if not bevels.has(key):
		bevels[key] = _beveled_mesh(size, radius)
	return mesh(parent, bevels[key], paint, at)

static func _beveled_mesh(size: Vector3, radius: float) -> ArrayMesh:
	var half := size * 0.5
	var inset := half - Vector3.ONE * radius
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for normal in [Vector3.RIGHT, Vector3.LEFT, Vector3.UP, Vector3.DOWN, Vector3.FORWARD, Vector3.BACK]:
		var horizontal: Vector3 = Vector3.RIGHT if absf(normal.y) > 0.5 else Vector3.UP
		var vertical: Vector3 = normal.cross(horizontal)
		var span_u := half.dot(horizontal.abs())
		var span_v := half.dot(vertical.abs())
		var plane: Vector3 = normal * half.dot(normal.abs())
		var u_marks := _bevel_marks(span_u, radius)
		var v_marks := _bevel_marks(span_v, radius)
		for iu in range(u_marks.size() - 1):
			for iv in range(v_marks.size() - 1):
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu], v_marks[iv], span_u, span_v)
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu], v_marks[iv + 1], span_u, span_v)
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu + 1], v_marks[iv + 1], span_u, span_v)
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu], v_marks[iv], span_u, span_v)
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu + 1], v_marks[iv + 1], span_u, span_v)
				_rounded_vertex(surface, plane, inset, radius, horizontal, vertical, u_marks[iu + 1], v_marks[iv], span_u, span_v)
	surface.index()
	return surface.commit()

static func _bevel_marks(extent: float, radius: float) -> PackedFloat32Array:
	return PackedFloat32Array([-extent, -extent + radius * 0.54, -extent + radius,
		extent - radius, extent - radius * 0.54, extent])

static func _rounded_vertex(surface: SurfaceTool, plane: Vector3, inset: Vector3, radius: float,
		horizontal: Vector3, vertical: Vector3, u: float, v: float, span_u: float, span_v: float) -> void:
	var point := plane + horizontal * u + vertical * v
	var inner := point.clamp(-inset, inset)
	var normal := (point - inner).normalized()
	surface.set_normal(normal)
	surface.set_uv(Vector2(u / span_u, v / span_v) * 0.5 + Vector2.ONE * 0.5)
	surface.add_vertex(inner + normal * radius)

static func sphere(parent: Node3D, radius: float, paint: Material, at := Vector3.ZERO) -> MeshInstance3D:
	if not spheres.has(radius):
		var primitive := SphereMesh.new()
		primitive.radius = radius
		primitive.height = radius * 2.0
		primitive.radial_segments = 24
		primitive.rings = 12
		spheres[radius] = primitive
	return mesh(parent, spheres[radius], paint, at)

static func cylinder(parent: Node3D, radius: float, length: float, paint: Material, at := Vector3.ZERO) -> MeshInstance3D:
	var primitive := CylinderMesh.new()
	primitive.top_radius = radius
	primitive.bottom_radius = radius
	primitive.height = length
	primitive.radial_segments = 24
	return mesh(parent, primitive, paint, at)

static func physics_material(friction: float, bounce := 0.0, absorbent := false) -> PhysicsMaterial:
	var value := PhysicsMaterial.new()
	value.friction = friction
	value.bounce = bounce
	value.absorbent = absorbent
	return value

static func shape(parent: CollisionObject3D, resource: Shape3D, at := Vector3.ZERO) -> CollisionShape3D:
	var node := CollisionShape3D.new()
	node.shape = resource
	parent.add_child(node)
	node.position = at
	return node

static func box_shape(parent: CollisionObject3D, size: Vector3, at := Vector3.ZERO) -> CollisionShape3D:
	var box := BoxShape3D.new()
	box.size = size
	return shape(parent, box, at)

static func solid(parent: Node3D, size: Vector3, point: Vector3, angle: float, friction: float, paint: Material) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.collision_layer = LabConfig.SOLID_LAYER
	body.collision_mask = LabConfig.BODY_LAYER | LabConfig.ROPE_LAYER
	body.physics_material_override = physics_material(friction)
	parent.add_child(body)
	body.position = point
	body.rotation_degrees.z = angle
	box_shape(body, size)
	if paint:
		block(body, size, paint)
	return body

static func mobile(parent: Node3D, point: Vector3, angle: float, weight: float, friction: float, bounce: float, active: bool) -> RigidBody3D:
	var body := RigidBody3D.new()
	body.axis_lock_linear_z = true
	body.axis_lock_angular_x = true
	body.axis_lock_angular_y = true
	body.physics_material_override = physics_material(friction, bounce)
	body.linear_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	body.angular_damp_mode = RigidBody3D.DAMP_MODE_REPLACE
	body.mass = weight
	body.collision_layer = LabConfig.BODY_LAYER
	body.collision_mask = LabConfig.SOLID_LAYER | LabConfig.BODY_LAYER | LabConfig.ROPE_LAYER
	body.continuous_cd = true
	body.freeze = not active
	parent.add_child(body)
	body.position = point
	body.rotation_degrees.z = angle
	return body

static func planar(point: Vector3) -> Vector2:
	return Vector2(point.x, point.y)
