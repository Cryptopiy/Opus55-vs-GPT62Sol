extends RefCounted
## P-08 Rope pendulum: static anchor -> chain of 0.25 m capsule segments (0.2 kg) joined by PinJoint3D ->
## 4 kg bob shaped as a cradle (a ball can ride in it). Visual: one smooth skinned tube following the segments.
## Record: x/y = anchor, "length" = rope length (1-6 m, 0.25 steps), rot = rope angle from straight down (CCW).

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")

const ANCHOR_SIZE := Vector3(0.4, 0.25, 0.6)
const BASKET_HANG := 0.8   # cradle floor centre below the bail point: keeps a riding ball clear of the rope


static func defaults() -> Dictionary:
	return {"length": C.ROPE_DEFAULT}


static func constrain(rec: Dictionary) -> void:
	rec["length"] = clampf(snappedf(float(rec.get("length", C.ROPE_DEFAULT)), C.ROPE_SEGMENT), C.ROPE_MIN, C.ROPE_MAX)
	rec["rot"] = clampf(float(rec["rot"]), -C.ROPE_MAX_ANGLE, C.ROPE_MAX_ANGLE)


static func direction(rec: Dictionary) -> Vector2:
	var t := Common.rot_rad(rec)
	return Vector2(sin(t), -cos(t))


static func bail_point(rec: Dictionary) -> Vector2:
	return Common.origin(rec) + direction(rec) * float(rec["length"])


static func footprint(rec: Dictionary) -> Array:
	var a := Common.origin(rec)
	var b := bail_point(rec)
	return [
		Fp.rect(a, Vector2(ANCHOR_SIZE.x, ANCHOR_SIZE.y)),
		Fp.segment(a + direction(rec) * 0.15, b, 0.12),
		Fp.rect(b + Vector2(0, -BASKET_HANG + 0.17), Vector2(C.ROPE_BASKET_SIZE.x, 0.46)),
	]


## Editor handle (Alt+drag) on the bail point: dragging it changes length and angle.
static func handles(rec: Dictionary) -> Array[Vector2]:
	return [bail_point(rec)]


static func drag_bob(rec: Dictionary, p: Vector2) -> Dictionary:
	var out := rec.duplicate(true)
	var v := p - Common.origin(rec)
	out["length"] = v.length()
	out["rot"] = snappedf(rad_to_deg(atan2(v.x, -v.y)), C.SNAP_ROT)
	constrain(out)
	return out


static func _add_anchor_meshes(parent: Node3D) -> void:
	ML.add_box(parent, ANCHOR_SIZE, Vector3.ZERO, Mats.rounded(C.COL_BOB.darkened(0.2), 0.5, 0.5))
	var ring := ML.add_mesh(parent, ML.cylinder(0.07, 0.12, 12), Vector3(0, -0.12, 0), Mats.standard(C.COL_BOB, 0.4, 0.7))
	ring.rotation.x = PI * 0.5


## Cradle (bob): floor + two lips, origin at the bail point where the rope attaches.
static func _add_basket_meshes(parent: Node3D) -> void:
	var mat := Mats.rounded(C.COL_BOB, 0.45, 0.4)
	var fs := C.ROPE_BASKET_SIZE
	var ls := C.ROPE_BASKET_LIP
	ML.add_box(parent, Vector3(fs.x, fs.y, C.OBJECT_DEPTH * 0.8), Vector3(0, -BASKET_HANG, 0), mat)
	for side: float in [-1.0, 1.0]:
		ML.add_box(parent, Vector3(ls.x, ls.y, C.OBJECT_DEPTH * 0.8), Vector3(side * (fs.x - ls.x) * 0.5, -BASKET_HANG + fs.y * 0.5 + ls.y * 0.5, 0), mat)
		# bail rods from the lip tops up to the rope ring (visual only)
		var top := Vector3(side * (fs.x - ls.x) * 0.5, -BASKET_HANG + fs.y * 0.5 + ls.y, 0)
		var mid := top * 0.5
		var rod := ML.add_mesh(parent, ML.cylinder(0.025, top.length(), 8), mid, Mats.standard(C.COL_BOB.lightened(0.3), 0.4, 0.8))
		rod.rotation.z = atan2(top.x, -top.y) + PI
	ML.add_mesh(parent, ML.sphere(0.07), Vector3.ZERO, Mats.standard(C.COL_BOB.lightened(0.3), 0.4, 0.8))


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Node3D.new()
	var a := Common.origin(rec)
	root.position = Vector3(a.x, a.y, 0)
	_add_anchor_meshes(root)
	var dir := direction(rec)
	var length := float(rec["length"])
	var tube := ML.add_mesh(root, ML.cylinder(C.ROPE_RADIUS, length, 10), Vector3(dir.x, dir.y, 0) * length * 0.5, Mats.standard(C.COL_ROPE, 0.9))
	tube.rotation.z = Common.rot_rad(rec)
	var basket := Node3D.new()
	basket.position = Vector3(dir.x, dir.y, 0) * length
	root.add_child(basket)
	_add_basket_meshes(basket)
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var anchor := Vector3(float(rec["x"]), float(rec["y"]), 0)
	var theta := Common.rot_rad(rec)
	var dir := Vector3(sin(theta), -cos(theta), 0)
	var n := int(round(float(rec["length"]) / C.ROPE_SEGMENT))
	var anchor_body := Common.static_body(Transform3D(Basis(), anchor), C.ROPE_FRICTION)
	ML.add_box_shape(anchor_body, ANCHOR_SIZE)
	_add_anchor_meshes(anchor_body)
	sim.add_node(anchor_body)
	var segs: Array[RigidBody3D] = []
	var seg_basis := Basis(Vector3.BACK, theta)
	for i in n:
		var s := RigidBody3D.new()
		var cs := CollisionShape3D.new()
		var cap := CapsuleShape3D.new()
		cap.radius = C.ROPE_RADIUS
		cap.height = C.ROPE_SEGMENT
		cs.shape = cap
		s.add_child(cs)
		s.mass = C.ROPE_SEGMENT_MASS
		s.physics_material_override = Mats.phys(C.ROPE_FRICTION, 0.0)
		Common.lock_to_plane(s)
		# rope segments never collide with each other (no self-tangling jitter), but do hit the world
		s.collision_layer = C.LAYER_ROPE
		s.collision_mask = C.LAYER_STATIC | C.LAYER_DYNAMIC
		s.transform = Transform3D(seg_basis, anchor + dir * (C.ROPE_SEGMENT * (i + 0.5)))
		sim.add_node(s)
		segs.append(s)
	var bail := anchor + dir * (C.ROPE_SEGMENT * n)
	var basket := Common.rigid_body(Transform3D(Basis(), bail), C.ROPE_BOB_MASS, C.ROPE_FRICTION)
	var fs := C.ROPE_BASKET_SIZE
	var ls := C.ROPE_BASKET_LIP
	ML.add_box_shape(basket, Vector3(fs.x, fs.y, C.OBJECT_DEPTH * 0.8), Vector3(0, -BASKET_HANG, 0))
	for side: float in [-1.0, 1.0]:
		ML.add_box_shape(basket, Vector3(ls.x, ls.y, C.OBJECT_DEPTH * 0.8), Vector3(side * (fs.x - ls.x) * 0.5, -BASKET_HANG + fs.y * 0.5 + ls.y * 0.5, 0))
	_add_basket_meshes(basket)
	sim.add_node(basket)
	sim.add_dynamic(basket, &"bob", fs.x * 0.5)
	var prev: PhysicsBody3D = anchor_body
	for i in n + 1:
		var nxt: PhysicsBody3D = segs[i] if i < n else basket
		Common.connect_joint(PinJoint3D.new(), sim, anchor + dir * (C.ROPE_SEGMENT * i), prev, nxt)
		prev = nxt
	var tube := RopeTube.new()
	sim.add_node(tube)
	tube.setup(segs, anchor, dir)
	var ticker := RopeSim.new()
	ticker.anchor = anchor
	ticker.basket = basket
	ticker.segs = segs
	sim.add_node(ticker)
	sim.add_ticker(ticker)


## Smooth tube skinned to one bone per segment; vertex weights blend between neighbouring segment centres,
## so the tube bends smoothly at every joint. Bone poses are copied from the (interpolated) segment transforms.
class RopeTube extends Node3D:
	var segs: Array[RigidBody3D] = []
	var skel: Skeleton3D

	func setup(segments: Array[RigidBody3D], anchor: Vector3, dir: Vector3) -> void:
		physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
		segs = segments
		skel = Skeleton3D.new()
		add_child(skel)
		var n := segs.size()
		for i in n:
			skel.add_bone("seg%d" % i)
			skel.set_bone_rest(i, segs[i].transform)
		var sides := C.ROPE_TUBE_SIDES
		var rings := n * C.ROPE_TUBE_RINGS_PER_SEGMENT + 1
		var length := C.ROPE_SEGMENT * n
		var p1 := Vector3(-dir.y, dir.x, 0)
		var verts := PackedVector3Array()
		var norms := PackedVector3Array()
		var bones := PackedInt32Array()
		var weights := PackedFloat32Array()
		var idx := PackedInt32Array()
		for k in rings:
			var s := length * float(k) / float(rings - 1)
			var f := s / C.ROPE_SEGMENT - 0.5
			var i0 := clampi(floori(f), 0, n - 1)
			var i1 := mini(i0 + 1, n - 1)
			var w1 := clampf(f - float(i0), 0.0, 1.0)
			var centre := anchor + dir * s
			for j in sides:
				var a := TAU * float(j) / float(sides)
				var off := p1 * cos(a) + Vector3.BACK * sin(a)
				verts.append(centre + off * C.ROPE_RADIUS * 1.15)
				norms.append(off)
				bones.append_array(PackedInt32Array([i0, i1, 0, 0]))
				weights.append_array(PackedFloat32Array([1.0 - w1, w1, 0.0, 0.0]))
		for k in rings - 1:
			for j in sides:
				var a0 := k * sides + j
				var b0 := k * sides + (j + 1) % sides
				idx.append_array(PackedInt32Array([a0, a0 + sides, b0, b0, a0 + sides, b0 + sides]))
		var arr := []
		arr.resize(Mesh.ARRAY_MAX)
		arr[Mesh.ARRAY_VERTEX] = verts
		arr[Mesh.ARRAY_NORMAL] = norms
		arr[Mesh.ARRAY_BONES] = bones
		arr[Mesh.ARRAY_WEIGHTS] = weights
		arr[Mesh.ARRAY_INDEX] = idx
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
		var mi := MeshInstance3D.new()
		mi.mesh = mesh
		mi.material_override = Mats.double_sided(C.COL_ROPE, 0.9)
		# skinned vertices move far from the rest pose: use a generous culling box
		mi.custom_aabb = AABB(Vector3(-60, -40, -5), Vector3(120, 80, 10))
		skel.add_child(mi)
		mi.skeleton = NodePath("..")
		mi.skin = skel.create_skin_from_rest_transforms()

	func _process(_delta: float) -> void:
		for i in segs.size():
			skel.set_bone_pose(i, segs[i].get_global_transform_interpolated())


## Per-tick rope logic: creak sound at swing reversals and stretch measurement (P-08 evidence in F1/F10).
class RopeSim extends Node:
	var anchor := Vector3.ZERO
	var basket: RigidBody3D
	var segs: Array[RigidBody3D] = []
	var max_stretch := 0.0
	var _last_sign := 0.0
	var _peak := 0.0
	var _cooldown := 0.0

	func sim_tick(_sim: Node) -> void:
		var rel := basket.global_position - anchor
		var tangent := Vector3(-rel.y, rel.x, 0).normalized()
		var v := basket.linear_velocity.dot(tangent)
		_peak = maxf(_peak, absf(v))
		_cooldown -= C.PHYS_DT
		var sgn := signf(v)
		if sgn != 0.0 and _last_sign != 0.0 and sgn != _last_sign:
			if _peak > C.ROPE_CREAK_MIN_SPEED and _cooldown <= 0.0:
				Sfx.play(&"creak", clampf(-14.0 + _peak * 1.5, -14.0, -4.0), 0.9 + 0.05 * _peak)
				_cooldown = C.ROPE_CREAK_COOLDOWN
			_peak = 0.0
		if sgn != 0.0:
			_last_sign = sgn
		max_stretch = maxf(max_stretch, measure_stretch())

	## Sum of the gaps opened at every pin joint, relative to the rest length.
	func measure_stretch() -> float:
		var gaps := 0.0
		var prev_end := anchor
		var half := C.ROPE_SEGMENT * 0.5
		for s in segs:
			var top := s.global_transform * Vector3(0, half, 0)
			var bottom := s.global_transform * Vector3(0, -half, 0)
			gaps += prev_end.distance_to(top)
			prev_end = bottom
		gaps += prev_end.distance_to(basket.global_position)
		return gaps / (C.ROPE_SEGMENT * segs.size())
