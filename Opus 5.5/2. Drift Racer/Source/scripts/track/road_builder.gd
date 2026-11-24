class_name RoadBuilder
extends RefCounted
## Road mesh and collision from the spline (R-03, R-04): 14 m asphalt with
## procedural lines, 0.3 m red/white curbs, gravel run-off up to the barrier
## line and one trimesh collider for the whole drivable surface.

const ASPHALT_SHADER := preload("res://shaders/asphalt.gdshader")
const CURB_SHADER := preload("res://shaders/curb.gdshader")
const RUNOFF_SHADER := preload("res://shaders/runoff.gdshader")


static func half_width() -> float:
	return Config.ROAD_WIDTH * 0.5


static func curb_outer() -> float:
	return half_width() + Config.CURB_WIDTH


## Lateral distance of the barrier's inner face from the centreline.
static func wall_line() -> float:
	return curb_outer() + Config.RUNOFF_WIDTH


static func build(data: TrackData, parent: Node3D) -> void:
	var half := half_width()
	var curb := curb_outer()
	var wall := wall_line()
	var mesh := ArrayMesh.new()

	var asphalt := MeshArrays.new()
	_append_strip(asphalt, data, -half, half, true)
	asphalt.commit(mesh, MeshArrays.shader_material(ASPHALT_SHADER, {
		"half_width": half,
		"center_dash": Config.CENTER_DASH_LENGTH,
		"center_gap": Config.CENTER_GAP_LENGTH,
		"center_line_width": Config.CENTER_LINE_WIDTH,
		"edge_line_width": Config.EDGE_LINE_WIDTH,
		"edge_line_inset": Config.EDGE_LINE_INSET,
		"start_line_depth": Config.START_LINE_DEPTH,
	}))

	var curbs := MeshArrays.new()
	_append_strip(curbs, data, -curb, -half, false)
	_append_strip(curbs, data, half, curb, false)
	curbs.commit(mesh, MeshArrays.shader_material(CURB_SHADER, {"stripe": Config.CURB_STRIPE_LENGTH}))

	var runoff := MeshArrays.new()
	_append_strip(runoff, data, -wall - 0.25, -curb, false)
	_append_strip(runoff, data, curb, wall + 0.25, false)
	runoff.commit(mesh, MeshArrays.shader_material(RUNOFF_SHADER, {}))

	var road := MeshInstance3D.new()
	road.name = "RoadMesh"
	road.mesh = mesh
	parent.add_child(road)

	# One wide collision strip covering asphalt, curbs and run-off (slightly
	# under the barriers so no gap exists at the wall foot).
	var col := MeshArrays.new()
	_append_strip(col, data, -wall - 0.6, wall + 0.6, false)
	var shape := ConcavePolygonShape3D.new()
	shape.backface_collision = true
	shape.set_faces(col.to_faces())
	var body := StaticBody3D.new()
	body.name = "RoadBody"
	body.collision_layer = Config.LAYER_ROAD
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	cs.shape = shape
	body.add_child(cs)
	parent.add_child(body)


## Appends a closed strip between lateral offsets lat_a < lat_b.
## centre_uv: UV = (lateral metres, centreline distance) for the asphalt shader;
## otherwise UV = (0..1 across, distance along this strip's own middle line).
static func _append_strip(arrays: MeshArrays, data: TrackData, lat_a: float, lat_b: float, centre_uv: bool) -> void:
	var n := data.count
	var base := arrays.verts.size()
	var edge_len := 0.0
	var prev_mid := Vector3.ZERO
	for k in n + 1:
		var i := k % n
		var p := data.points[i]
		var r := data.rights[i]
		var up := data.normals[i]
		var va := p + r * lat_a
		var vb := p + r * lat_b
		var mid := (va + vb) * 0.5
		if k > 0:
			edge_len += mid.distance_to(prev_mid)
		prev_mid = mid
		if centre_uv:
			var v := data.length if k == n else data.distances[i]
			arrays.add_vertex(va, up, Vector2(lat_a, v))
			arrays.add_vertex(vb, up, Vector2(lat_b, v))
		else:
			arrays.add_vertex(va, up, Vector2(0.0, edge_len))
			arrays.add_vertex(vb, up, Vector2(1.0, edge_len))
	for k in n:
		var a0 := base + k * 2
		arrays.add_quad_facing(a0, a0 + 2, a0 + 3, a0 + 1, data.normals[k])
