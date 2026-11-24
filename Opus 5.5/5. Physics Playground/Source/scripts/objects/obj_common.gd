extends RefCounted
## Helpers shared by all object modules: record <-> transform, body factories with the play-plane locks (P-01).
## A "record" is a plain Dictionary describing one placed object:
##   {id:int, type:String, x:float, y:float, rot:float(deg, CCW)} + type params (rope "length", lever "fulcrum").

const C := preload("res://scripts/core/config.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func origin(rec: Dictionary) -> Vector2:
	return Vector2(float(rec["x"]), float(rec["y"]))


static func rot_rad(rec: Dictionary) -> float:
	return deg_to_rad(float(rec["rot"]))


static func xform(rec: Dictionary) -> Transform3D:
	return Transform3D(Basis(Vector3.BACK, rot_rad(rec)), Vector3(float(rec["x"]), float(rec["y"]), 0.0))


static func xform_at(p: Vector2, deg: float) -> Transform3D:
	return Transform3D(Basis(Vector3.BACK, deg_to_rad(deg)), Vector3(p.x, p.y, 0.0))


static func visual_root(rec: Dictionary) -> Node3D:
	var n := Node3D.new()
	n.transform = xform(rec)
	return n


## Static collider on the static layer.
static func static_body(xf: Transform3D, friction: float, bounce: float = 0.0, absorbent: bool = false) -> StaticBody3D:
	var b := StaticBody3D.new()
	b.transform = xf
	b.physics_material_override = Mats.phys(friction, bounce, absorbent)
	b.collision_layer = C.LAYER_STATIC
	b.collision_mask = C.LAYER_DYNAMIC | C.LAYER_ROPE
	return b


## Dynamic body locked to the play plane z = 0 (P-01: linear Z and angular X/Y locked).
static func rigid_body(xf: Transform3D, mass: float, friction: float, bounce: float = 0.0) -> RigidBody3D:
	var b := RigidBody3D.new()
	b.transform = xf
	b.mass = mass
	b.physics_material_override = Mats.phys(friction, bounce)
	lock_to_plane(b)
	b.collision_layer = C.LAYER_DYNAMIC
	b.collision_mask = C.LAYER_STATIC | C.LAYER_DYNAMIC | C.LAYER_ROPE
	return b


static func lock_to_plane(b: RigidBody3D) -> void:
	b.axis_lock_linear_z = true
	b.axis_lock_angular_x = true
	b.axis_lock_angular_y = true


## Sensor volume that only reports dynamic bodies.
static func sensor(sz: Vector3, pos: Vector3) -> Area3D:
	var a := Area3D.new()
	a.collision_layer = C.LAYER_SENSOR
	a.collision_mask = C.LAYER_DYNAMIC
	a.monitorable = false
	a.position = pos
	var cs := CollisionShape3D.new()
	var sh := BoxShape3D.new()
	sh.size = sz
	cs.shape = sh
	a.add_child(cs)
	return a


## Joint helper: joints must be in the tree and positioned before node paths are set, because Godot computes
## the local anchor frames from the joint's global transform at the moment both bodies are assigned.
static func connect_joint(j: Joint3D, parent: Node, at: Vector3, a: PhysicsBody3D, b: PhysicsBody3D) -> void:
	parent.add_child(j)
	j.global_position = at
	j.node_a = j.get_path_to(a)
	j.node_b = j.get_path_to(b)


static func copy_record(rec: Dictionary) -> Dictionary:
	return rec.duplicate(true)


## Canonical sort key so the same set of placements always builds bodies in the same order (P-02).
static func sort_key(rec: Dictionary, type_index: int) -> String:
	return "%02d|%09.3f|%09.3f|%07.2f|%06.2f|%06.2f" % [type_index, float(rec["x"]) + 1000.0, float(rec["y"]) + 1000.0,
		float(rec["rot"]) + 1000.0, float(rec.get("length", 0.0)), float(rec.get("fulcrum", 0.0)) + 10.0]
