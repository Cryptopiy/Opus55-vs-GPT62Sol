class_name CircuitGeometry
extends RefCounted

static func material(color: Color, roughness := 0.76, metal := 0.0) -> StandardMaterial3D:
	var result := StandardMaterial3D.new()
	result.albedo_color = color
	result.roughness = roughness
	result.metallic = metal
	return result

static func mesh(parent: Node3D, shape: Mesh, surface: Material, at: Vector3) -> MeshInstance3D:
	var instance := MeshInstance3D.new()
	instance.mesh = shape
	instance.position = at
	instance.material_override = surface
	parent.add_child(instance)
	return instance

static func box(parent: Node3D, size: Vector3, at: Vector3, surface: Material) -> MeshInstance3D:
	var shape := BoxMesh.new()
	shape.size = size
	return mesh(parent, shape, surface, at)

static func cylinder(parent: Node3D, radius: float, height: float, sides: int, surface: Material,
		at: Vector3) -> MeshInstance3D:
	var shape := CylinderMesh.new()
	shape.top_radius = radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = sides
	shape.rings = 0
	return mesh(parent, shape, surface, at)

static func sign_text(parent: Node3D, text: String, at: Vector3, size: float, color: Color) -> Label3D:
	var sign := Label3D.new()
	sign.text = text
	sign.position = at
	sign.font_size = 88
	sign.pixel_size = size / sign.font_size
	sign.modulate = color
	sign.outline_size = 0
	sign.no_depth_test = false
	parent.add_child(sign)
	return sign
