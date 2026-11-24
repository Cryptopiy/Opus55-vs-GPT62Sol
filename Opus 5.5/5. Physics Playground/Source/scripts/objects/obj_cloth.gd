extends RefCounted
## P-11 Cloth hammock: SoftBody3D grid of 10 x 10 vertices spanning 2 m (X) x 1 m depth (Z), pinned at its
## 4 corners to static anchor bars that move/rotate with the placement. The rest mesh carries a slight dip
## (built-in slack) which, with the tuned stiffness, gives ~0.4 m sag under a 1 kg ball without jitter.
## The ball cannot leave the play plane: every dynamic body is axis-locked to z = 0 (P-01).
## Record: x/y = middle of the line between the anchor bars, rot = hammock orientation.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func defaults() -> Dictionary:
	return {}


static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 180.0, 360.0) - 180.0


static func footprint(rec: Dictionary) -> Array:
	var o := Common.origin(rec)
	var r := Common.rot_rad(rec)
	var half := C.CLOTH_SIZE.x * 0.5
	var shapes := [Fp.local_rect(o, r, Vector2(0, -C.CLOTH_DIP * 0.5), Vector2(C.CLOTH_SIZE.x, C.CLOTH_DIP + 0.1))]
	for side: float in [-1.0, 1.0]:
		shapes.append(Fp.local_rect(o, r, Vector2(side * (half + C.CLOTH_POST_SIZE.x * 0.5), -C.CLOTH_POST_SIZE.y * 0.5 + 0.08), C.CLOTH_POST_SIZE))
	return shapes


static func _add_post_meshes(parent: Node3D) -> void:
	var mat := Mats.rounded(C.COL_CLOTH.darkened(0.45), 0.5, 0.3)
	var half := C.CLOTH_SIZE.x * 0.5
	for side: float in [-1.0, 1.0]:
		var x := side * (half + C.CLOTH_POST_SIZE.x * 0.5)
		# crossbar holding the two corners of this side, plus a short post below it
		ML.add_box(parent, Vector3(C.CLOTH_POST_SIZE.x, 0.16, C.CLOTH_SIZE.y + 0.12), Vector3(x, 0, 0), mat)
		ML.add_box(parent, Vector3(C.CLOTH_POST_SIZE.x * 0.6, C.CLOTH_POST_SIZE.y, 0.12), Vector3(x, -C.CLOTH_POST_SIZE.y * 0.5 + 0.08, 0), mat)


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_post_meshes(root)
	var cloth := ML.add_mesh(root, ML.cloth_grid(C.CLOTH_SIZE, C.CLOTH_VERTS, C.CLOTH_DIP), Vector3.ZERO, Mats.double_sided(C.COL_CLOTH, 0.85))
	cloth.name = "Cloth"
	return root


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var xf := Common.xform(rec)
	var posts := Common.static_body(xf, 0.6)
	var half := C.CLOTH_SIZE.x * 0.5
	for side: float in [-1.0, 1.0]:
		ML.add_box_shape(posts, Vector3(C.CLOTH_POST_SIZE.x, 0.16, C.CLOTH_SIZE.y + 0.12), Vector3(side * (half + C.CLOTH_POST_SIZE.x * 0.5), 0, 0))
	_add_post_meshes(posts)
	sim.add_node(posts)
	# The soft body is built directly in world space as a top-level node with an identity transform: SoftBody3D
	# otherwise resets its own transform to identity (deferred) once it renders, which happens at a frame-time
	# dependent moment and made windowed runs non-deterministic.
	var mesh := ML.cloth_grid(C.CLOTH_SIZE, C.CLOTH_VERTS, C.CLOTH_DIP, xf)
	var sb := SoftBody3D.new()
	sb.mesh = mesh
	sb.top_level = true
	sb.transform = Transform3D.IDENTITY
	sb.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	sb.visible = false
	sb.total_mass = C.CLOTH_MASS
	sb.linear_stiffness = C.CLOTH_STIFFNESS
	sb.damping_coefficient = C.CLOTH_DAMPING
	sb.simulation_precision = C.CLOTH_PRECISION
	sb.collision_layer = C.LAYER_DYNAMIC
	sb.collision_mask = C.LAYER_STATIC | C.LAYER_DYNAMIC
	sb.material_override = Mats.double_sided(C.COL_CLOTH, 0.85)
	sim.add_node(sb)
	# pin the 4 corners of the row-major grid
	var n := C.CLOTH_VERTS
	for k: int in [0, n - 1, n * (n - 1), n * n - 1]:
		sb.set_point_pinned(k, true)
	# SoftBody3D updates its render mesh from the physics body before every drawn frame (RenderingServer
	# frame_pre_draw) - a frame-rate dependent call that made windowed runs diverge (headless never draws).
	# That hook is removed: ClothView samples the points once per physics tick (same moment every run) and
	# only builds the render mesh per frame.
	sb.set_process_internal(false)
	for conn: Dictionary in RenderingServer.frame_pre_draw.get_connections():
		var cb: Callable = conn["callable"]
		if cb.get_object() == sb:
			RenderingServer.frame_pre_draw.disconnect(cb)
	sim.cloths.append(sb)
	var view := ClothView.new()
	view.body = sb
	view.indices = mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX]
	sim.add_node(view)
	sim.add_ticker(view)


## Draws the simulated cloth: samples the 10 x 10 point positions every physics tick, rebuilds one
## double-sided ImmediateMesh per rendered frame from those samples.
class ClothView extends MeshInstance3D:
	var body: SoftBody3D
	var indices := PackedInt32Array()
	var _pts := PackedVector3Array()
	var _imm := ImmediateMesh.new()

	func _ready() -> void:
		physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
		top_level = true
		mesh = _imm
		material_override = Mats.double_sided(C.COL_CLOTH, 0.85)
		_pts.resize(C.CLOTH_VERTS * C.CLOTH_VERTS)
		_sample()
		_rebuild()

	func sim_tick(_sim: Node) -> void:
		_sample()

	func _process(_delta: float) -> void:
		_rebuild()

	func _sample() -> void:
		var rid := body.get_physics_rid()
		for i in _pts.size():
			_pts[i] = PhysicsServer3D.soft_body_get_point_global_position(rid, i)

	func _rebuild() -> void:
		_imm.clear_surfaces()
		_imm.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
		for t in range(0, indices.size(), 3):
			var a := _pts[indices[t]]
			var b := _pts[indices[t + 1]]
			var c := _pts[indices[t + 2]]
			var nrm := (c - a).cross(b - a).normalized()
			for p in [a, b, c]:
				_imm.surface_set_normal(nrm)
				_imm.surface_add_vertex(p)
		_imm.surface_end()
