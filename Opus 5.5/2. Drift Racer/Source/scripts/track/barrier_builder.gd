class_name BarrierBuilder
extends RefCounted
## Barrier walls along both road sides (R-04). Visuals alternate between
## concrete blocks and tyre stacks every BARRIER_STYLE_LENGTH metres; physics
## is one continuous, thick, closed trimesh prism per side (1.0 m tall, same
## inner face as the visuals) plus an invisible catch fence above it so a car
## can never leave the track area.

const CONCRETE_SHADER := preload("res://shaders/concrete.gdshader")
const TYRE_SEGMENTS := 10
const TYRE_VISIBILITY_END := 520.0


static func build(data: TrackData, parent: Node3D) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.name = "Barriers"
	body.collision_layer = Config.LAYER_BARRIER
	body.collision_mask = 0
	var pm := PhysicsMaterial.new()
	pm.friction = 0.2
	pm.bounce = 0.05
	body.physics_material_override = pm
	parent.add_child(body)

	var concrete := MeshArrays.new()
	var tyre_mesh := _tyre_stack_mesh()
	var tyre_mat := StandardMaterial3D.new()
	tyre_mat.vertex_color_use_as_albedo = true
	tyre_mat.roughness = 0.82
	for side_variant in [-1.0, 1.0]:
		var side: float = side_variant
		_add_collider(data, body, side)
		var style_shift := 0 if side < 0.0 else 1
		var run_start := 0
		var run_style := _style_at(data, 0, style_shift)
		for i in range(1, data.count + 1):
			var style := _style_at(data, i % data.count, style_shift) if i < data.count else -1
			if style != run_style:
				if run_style == 0:
					_concrete_run(concrete, data, side, run_start, i)
				else:
					_tyre_run(data, side, run_start, i, parent, tyre_mesh, tyre_mat)
				run_start = i
				run_style = style
	var mesh := ArrayMesh.new()
	concrete.commit(mesh, MeshArrays.shader_material(CONCRETE_SHADER, {
		"block_length": Config.CONCRETE_BLOCK_LENGTH,
		"wall_height": Config.WALL_HEIGHT,
	}))
	var mi := MeshInstance3D.new()
	mi.name = "ConcreteBarriers"
	mi.mesh = mesh
	parent.add_child(mi)
	return body


static func _style_at(data: TrackData, i: int, shift: int) -> int:
	return (int(floorf(data.distances[i] / Config.BARRIER_STYLE_LENGTH)) + shift) % 2


## Closed prism (inner face, top, outer face, bottom) + catch fence, per side.
static func _add_collider(data: TrackData, body: StaticBody3D, side: float) -> void:
	var wall := RoadBuilder.wall_line()
	var prism := MeshArrays.new()
	_append_prism(prism, data, side * wall, side * (wall + Config.WALL_COLLIDER_THICKNESS),
		-Config.WALL_BURY_DEPTH, Config.WALL_HEIGHT, side)
	var fence := MeshArrays.new()
	_append_prism(fence, data, side * wall, side * (wall + 0.3),
		Config.WALL_HEIGHT - 0.05, Config.CATCH_FENCE_HEIGHT, side)
	for arrays: MeshArrays in [prism, fence]:
		var shape := ConcavePolygonShape3D.new()
		shape.backface_collision = true
		shape.set_faces(arrays.to_faces())
		var cs := CollisionShape3D.new()
		cs.shape = shape
		body.add_child(cs)


static func _append_prism(arrays: MeshArrays, data: TrackData, lat_in: float, lat_out: float,
		y0: float, y1: float, side: float) -> void:
	var base := arrays.verts.size()
	var n := data.count
	for k in n + 1:
		var i := k % n
		var p := data.points[i]
		var r := data.rights[i]
		var inner := p + r * lat_in
		var outer := p + r * lat_out
		arrays.add_vertex(inner + Vector3.UP * y0, Vector3.UP, Vector2.ZERO)
		arrays.add_vertex(inner + Vector3.UP * y1, Vector3.UP, Vector2.ZERO)
		arrays.add_vertex(outer + Vector3.UP * y1, Vector3.UP, Vector2.ZERO)
		arrays.add_vertex(outer + Vector3.UP * y0, Vector3.UP, Vector2.ZERO)
	for k in n:
		var a := base + k * 4
		var b := a + 4
		var r := data.rights[k]
		arrays.add_quad_facing(a, a + 1, b + 1, b, -r * side)          # inner face -> track
		arrays.add_quad_facing(a + 1, a + 2, b + 2, b + 1, Vector3.UP)  # top
		arrays.add_quad_facing(a + 2, a + 3, b + 3, b + 2, r * side)    # outer face
		arrays.add_quad_facing(a + 3, a, b, b + 3, Vector3.DOWN)        # bottom


## Concrete visual from sample from_i to to_i (inclusive, may wrap).
static func _concrete_run(arrays: MeshArrays, data: TrackData, side: float, from_i: int, to_i: int) -> void:
	var wall := RoadBuilder.wall_line()
	var lat_in := side * wall
	var lat_out := side * (wall + Config.WALL_VISUAL_THICKNESS)
	var h := Config.WALL_HEIGHT
	var bury := Config.WALL_BURY_DEPTH
	var base := arrays.verts.size()
	var steps := to_i - from_i
	var along := 0.0
	var prev := Vector3.ZERO
	for k in steps + 1:
		var i := data.wrap_index(from_i + k)
		var p := data.points[i]
		var r := data.rights[i]
		var inner := p + r * lat_in
		var outer := p + r * lat_out
		if k > 0:
			along += inner.distance_to(prev)
		prev = inner
		var n_in := -r * side
		var n_out := r * side
		arrays.add_vertex(inner + Vector3.DOWN * bury, n_in, Vector2(-bury, along))
		arrays.add_vertex(inner + Vector3.UP * h, n_in, Vector2(h, along))
		arrays.add_vertex(inner + Vector3.UP * h, Vector3.UP, Vector2(h, along))
		arrays.add_vertex(outer + Vector3.UP * h, Vector3.UP, Vector2(h, along))
		arrays.add_vertex(outer + Vector3.UP * h, n_out, Vector2(h, along))
		arrays.add_vertex(outer + Vector3.DOWN * bury, n_out, Vector2(-bury, along))
	for k in steps:
		var a := base + k * 6
		var b := a + 6
		var r := data.rights[data.wrap_index(from_i + k)]
		arrays.add_quad_facing(a, a + 1, b + 1, b, -r * side)
		arrays.add_quad_facing(a + 2, a + 3, b + 3, b + 2, Vector3.UP)
		arrays.add_quad_facing(a + 4, a + 5, b + 5, b + 4, r * side)
	_cap(arrays, data, data.wrap_index(from_i), lat_in, lat_out, -1.0)
	_cap(arrays, data, data.wrap_index(to_i), lat_in, lat_out, 1.0)


static func _cap(arrays: MeshArrays, data: TrackData, i: int, lat_in: float, lat_out: float, dir: float) -> void:
	var p := data.points[i]
	var r := data.rights[i]
	var facing := data.tangents[i] * dir
	var h := Config.WALL_HEIGHT
	var bury := Config.WALL_BURY_DEPTH
	var a := arrays.add_vertex(p + r * lat_in + Vector3.DOWN * bury, facing, Vector2(-bury, 0.0))
	var b := arrays.add_vertex(p + r * lat_in + Vector3.UP * h, facing, Vector2(h, 0.0))
	var c := arrays.add_vertex(p + r * lat_out + Vector3.UP * h, facing, Vector2(h, 0.0))
	var d := arrays.add_vertex(p + r * lat_out + Vector3.DOWN * bury, facing, Vector2(-bury, 0.0))
	arrays.add_quad_facing(a, b, c, d, facing)


## Tyre stacks every TYRE_STACK_SPACING along the barrier line (one
## MultiMeshInstance3D per section so distant sections are range-culled).
static func _tyre_run(data: TrackData, side: float, from_i: int, to_i: int, parent: Node3D,
		mesh: Mesh, material: Material) -> void:
	var lat := side * (RoadBuilder.wall_line() + Config.TYRE_RADIUS)
	var xforms: Array[Transform3D] = []
	var carry := 0.0
	for k in to_i - from_i:
		var i0 := data.wrap_index(from_i + k)
		var i1 := data.wrap_index(i0 + 1)
		var a := data.points[i0] + data.rights[i0] * lat
		var b := data.points[i1] + data.rights[i1] * lat
		var seg := a.distance_to(b)
		var d := carry
		while d < seg:
			var pos := a.lerp(b, d / seg)
			var yaw := float(xforms.size()) * 1.7
			xforms.append(Transform3D(Basis(Vector3.UP, yaw), pos))
			d += Config.TYRE_STACK_SPACING
		carry = d - seg
	if xforms.is_empty():
		return
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = mesh
	mm.instance_count = xforms.size()
	for j in xforms.size():
		mm.set_instance_transform(j, xforms[j])
		var white := ((j >> 1) & 1) == 1   # pairs of black / white stacks
		mm.set_instance_color(j, Color(0.86, 0.86, 0.84) if white else Color(0.07, 0.07, 0.075))
	var mmi := MultiMeshInstance3D.new()
	mmi.name = "TyreStacks"
	mmi.multimesh = mm
	mmi.material_override = material
	mmi.visibility_range_end = TYRE_VISIBILITY_END
	mmi.visibility_range_end_margin = 30.0
	mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	parent.add_child(mmi)


## Three stacked low-poly tyres (outer wall, tread top ring, dark hole).
static func _tyre_stack_mesh() -> ArrayMesh:
	var arrays := MeshArrays.new()
	arrays.use_colors = true
	var r_out := Config.TYRE_RADIUS
	var r_in := Config.TYRE_RADIUS * 0.48
	var tyre_h := Config.WALL_HEIGHT / float(Config.TYRES_PER_STACK)
	var rubber := Color(1.0, 1.0, 1.0)
	var hole := Color(0.25, 0.25, 0.25)
	for t in Config.TYRES_PER_STACK:
		var y0 := float(t) * tyre_h
		var y1 := y0 + tyre_h - 0.025
		for s in TYRE_SEGMENTS:
			var a0 := TAU * float(s) / float(TYRE_SEGMENTS)
			var a1 := TAU * float(s + 1) / float(TYRE_SEGMENTS)
			var d0 := Vector3(cos(a0), 0.0, sin(a0))
			var d1 := Vector3(cos(a1), 0.0, sin(a1))
			var dm := ((d0 + d1) * 0.5).normalized()
			# outer wall
			var o0 := arrays.add_vertex_color(d0 * r_out + Vector3.UP * y0, d0, Vector2.ZERO, rubber)
			var o1 := arrays.add_vertex_color(d0 * r_out + Vector3.UP * y1, d0, Vector2.ZERO, rubber)
			var o2 := arrays.add_vertex_color(d1 * r_out + Vector3.UP * y1, d1, Vector2.ZERO, rubber)
			var o3 := arrays.add_vertex_color(d1 * r_out + Vector3.UP * y0, d1, Vector2.ZERO, rubber)
			arrays.add_quad_facing(o0, o1, o2, o3, dm)
			# top ring
			var t0 := arrays.add_vertex_color(d0 * r_out + Vector3.UP * y1, Vector3.UP, Vector2.ZERO, rubber)
			var t1 := arrays.add_vertex_color(d1 * r_out + Vector3.UP * y1, Vector3.UP, Vector2.ZERO, rubber)
			var t2 := arrays.add_vertex_color(d1 * r_in + Vector3.UP * y1, Vector3.UP, Vector2.ZERO, rubber)
			var t3 := arrays.add_vertex_color(d0 * r_in + Vector3.UP * y1, Vector3.UP, Vector2.ZERO, rubber)
			arrays.add_quad_facing(t0, t1, t2, t3, Vector3.UP)
			# dark inner hole wall
			var h0 := arrays.add_vertex_color(d0 * r_in + Vector3.UP * y1, -d0, Vector2.ZERO, hole)
			var h1 := arrays.add_vertex_color(d1 * r_in + Vector3.UP * y1, -d1, Vector2.ZERO, hole)
			var h2 := arrays.add_vertex_color(d1 * r_in + Vector3.UP * (y0 + 0.04), -d1, Vector2.ZERO, hole)
			var h3 := arrays.add_vertex_color(d0 * r_in + Vector3.UP * (y0 + 0.04), -d0, Vector2.ZERO, hole)
			arrays.add_quad_facing(h0, h1, h2, h3, -dm)
	var mesh := ArrayMesh.new()
	arrays.commit(mesh, null)
	return mesh
