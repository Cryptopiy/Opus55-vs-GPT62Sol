extends RefCounted
## P-04 Balls (steel / rubber / wood). The level decides which one is used and where it starts.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func spec(type: String) -> Dictionary:
	return C.BALLS[type]


static func footprint(type: String, pos: Vector2) -> Array:
	return [Fp.circle(pos, float(spec(type)["radius"]))]


static func _add_meshes(parent: Node3D, type: String) -> void:
	var s := spec(type)
	var r: float = s["radius"]
	var col: Color = s["color"]
	ML.add_mesh(parent, ML.sphere(r), Vector3.ZERO, Mats.standard(col, s["rough"], s["metallic"]))
	# a contrasting band around the ball (seen as a stripe from the camera) makes rolling and spin readable
	var band_col := col.lightened(0.55) if type != "wood" else col.darkened(0.35)
	ML.add_mesh(parent, ML.cylinder(r * 1.015, r * 0.28, 32), Vector3.ZERO, Mats.standard(band_col, 0.5, float(s["metallic"]) * 0.5))
	var dot := ML.add_mesh(parent, ML.sphere(r * 0.22), Vector3(0, r * 0.82, r * 0.42), Mats.standard(band_col, 0.5))
	dot.scale = Vector3(1, 1, 0.5)


static func build_visual(type: String, pos: Vector2) -> Node3D:
	var root := Node3D.new()
	root.position = Vector3(pos.x, pos.y, 0)
	_add_meshes(root, type)
	return root


static func build_sim(type: String, pos: Vector2, sim: Node) -> RigidBody3D:
	var s := spec(type)
	var body := Common.rigid_body(Transform3D(Basis(), Vector3(pos.x, pos.y, 0)), s["mass"], s["friction"], s["bounce"])
	# fast balls vs. 0.2 m planks: continuous collision detection prevents tunnelling
	body.continuous_cd = true
	ML.add_sphere_shape(body, s["radius"])
	_add_meshes(body, type)
	body.name = "Ball"
	sim.add_node(body)
	sim.add_dynamic(body, &"ball", s["radius"])
	return body
