class_name GroundBuilder
extends RefCounted
## Rolling grass ground around the circuit (R-05). Heights follow the road
## elevation near the track (kept just below the road surface) and blend into
## low rolling hills further away. A large flat outer disc continues the
## ground to the horizon. Returns a height field used to place trees.

const GRASS_SHADER := preload("res://shaders/grass.gdshader")
const OUTER_RADIUS := 7000.0
const OUTER_SEGMENTS := 64
const EDGE_BLEND := 220.0                      # metres over which the grid edge sinks to the outer disc


class Field:
	extends RefCounted
	var x0 := 0.0
	var z0 := 0.0
	var cell := 1.0
	var nx := 0
	var nz := 0
	var outer_height := 0.0
	var heights := PackedFloat32Array()

	func height_at(x: float, z: float) -> float:
		var fx := (x - x0) / cell
		var fz := (z - z0) / cell
		if fx < 0.0 or fz < 0.0 or fx > float(nx - 1) or fz > float(nz - 1):
			return outer_height
		var ix := mini(int(fx), nx - 2)
		var iz := mini(int(fz), nz - 2)
		var tx := fx - float(ix)
		var tz := fz - float(iz)
		var h00 := heights[iz * nx + ix]
		var h10 := heights[iz * nx + ix + 1]
		var h01 := heights[(iz + 1) * nx + ix]
		var h11 := heights[(iz + 1) * nx + ix + 1]
		return lerpf(lerpf(h00, h10, tx), lerpf(h01, h11, tx), tz)


static func build(data: TrackData, parent: Node3D, rng: RandomNumberGenerator) -> Field:
	var field := Field.new()
	var b := data.bounds.grow(Config.GROUND_MARGIN)
	field.cell = Config.GROUND_CELL
	field.nx = int(ceilf(b.size.x / field.cell)) + 1
	field.nz = int(ceilf(b.size.z / field.cell)) + 1
	field.x0 = b.position.x
	field.z0 = b.position.z
	var total := field.nx * field.nz
	var avg_h := 0.0
	for p in data.points:
		avg_h += p.y
	avg_h /= float(data.count)
	field.outer_height = avg_h - Config.GROUND_ROLL_AMPLITUDE - 2.0

	# Splat the track into the grid: nearest centreline distance + its height.
	var best_d := PackedFloat32Array()
	best_d.resize(total)
	best_d.fill(1.0e12)
	var near_h := PackedFloat32Array()
	near_h.resize(total)
	near_h.fill(avg_h)
	var reach := int(ceilf(Config.GROUND_BLEND_END / field.cell)) + 1
	for i in range(0, data.count, 2):
		var p := data.points[i]
		var gx := roundi((p.x - field.x0) / field.cell)
		var gz := roundi((p.z - field.z0) / field.cell)
		for z in range(maxi(gz - reach, 0), mini(gz + reach + 1, field.nz)):
			var wz := field.z0 + float(z) * field.cell - p.z
			var row := z * field.nx
			for x in range(maxi(gx - reach, 0), mini(gx + reach + 1, field.nx)):
				var wx := field.x0 + float(x) * field.cell - p.x
				var d := wx * wx + wz * wz
				if d < best_d[row + x]:
					best_d[row + x] = d
					near_h[row + x] = p.y

	var noise := FastNoiseLite.new()
	noise.seed = rng.randi()
	noise.frequency = Config.GROUND_ROLL_FREQUENCY
	noise.fractal_octaves = 3
	field.heights.resize(total)
	for z in field.nz:
		for x in field.nx:
			var idx := z * field.nx + x
			var wx := field.x0 + float(x) * field.cell
			var wz := field.z0 + float(z) * field.cell
			var rolling := avg_h + noise.get_noise_2d(wx, wz) * Config.GROUND_ROLL_AMPLITUDE
			var d := sqrt(best_d[idx])
			var under := near_h[idx] - Config.GROUND_UNDER_ROAD
			var t := smoothstep(Config.GROUND_FLAT_RADIUS, Config.GROUND_BLEND_END, d)
			var h := lerpf(under, rolling, t)
			# Sink the grid edge onto the outer disc so there is no visible seam.
			var edge := minf(minf(float(x), float(field.nx - 1 - x)), minf(float(z), float(field.nz - 1 - z))) * field.cell
			h = lerpf(field.outer_height, h, smoothstep(0.0, EDGE_BLEND, edge))
			field.heights[idx] = h

	_build_mesh(field, parent)
	_build_collider(field, parent)
	_build_outer_disc(field, data, parent)
	return field


static func _build_mesh(field: Field, parent: Node3D) -> void:
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	verts.resize(field.nx * field.nz)
	normals.resize(field.nx * field.nz)
	for z in field.nz:
		for x in field.nx:
			var idx := z * field.nx + x
			verts[idx] = Vector3(field.x0 + float(x) * field.cell, field.heights[idx], field.z0 + float(z) * field.cell)
			var hl := field.heights[z * field.nx + maxi(x - 1, 0)]
			var hr := field.heights[z * field.nx + mini(x + 1, field.nx - 1)]
			var hd := field.heights[maxi(z - 1, 0) * field.nx + x]
			var hu := field.heights[mini(z + 1, field.nz - 1) * field.nx + x]
			normals[idx] = Vector3(hl - hr, 2.0 * field.cell, hd - hu).normalized()
	var indices := PackedInt32Array()
	indices.resize((field.nx - 1) * (field.nz - 1) * 6)
	var k := 0
	for z in field.nz - 1:
		for x in field.nx - 1:
			var a := z * field.nx + x
			var c := a + field.nx
			# Clockwise seen from above (Godot front face): a, a+1, c+1 / a, c+1, c.
			indices[k] = a
			indices[k + 1] = a + 1
			indices[k + 2] = c + 1
			indices[k + 3] = a
			indices[k + 4] = c + 1
			indices[k + 5] = c
			k += 6
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = verts
	arr[Mesh.ARRAY_NORMAL] = normals
	arr[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	mesh.surface_set_material(0, MeshArrays.shader_material(GRASS_SHADER, {}))
	var mi := MeshInstance3D.new()
	mi.name = "Ground"
	mi.mesh = mesh
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(mi)


## Height-field collider (safety net: the walls keep cars on the track).
static func _build_collider(field: Field, parent: Node3D) -> void:
	var shape := HeightMapShape3D.new()
	shape.map_width = field.nx
	shape.map_depth = field.nz
	var scaled := PackedFloat32Array()
	scaled.resize(field.heights.size())
	for i in field.heights.size():
		scaled[i] = field.heights[i] / field.cell
	shape.map_data = scaled
	var body := StaticBody3D.new()
	body.name = "GroundBody"
	body.collision_layer = Config.LAYER_ROAD
	body.collision_mask = 0
	var cs := CollisionShape3D.new()
	cs.shape = shape
	# Height maps are centred on their origin with 1 unit spacing; a uniform
	# scale of `cell` gives the grid spacing (heights were pre-divided).
	cs.scale = Vector3.ONE * field.cell
	cs.position = Vector3(field.x0 + float(field.nx - 1) * field.cell * 0.5, 0.0,
		field.z0 + float(field.nz - 1) * field.cell * 0.5)
	body.add_child(cs)
	parent.add_child(body)


static func _build_outer_disc(field: Field, data: TrackData, parent: Node3D) -> void:
	var arrays := MeshArrays.new()
	var centre := data.bounds.get_center()
	var c := arrays.add_vertex(Vector3(centre.x, field.outer_height - 0.05, centre.z), Vector3.UP, Vector2.ZERO)
	var rim := PackedInt32Array()
	for s in OUTER_SEGMENTS:
		var a := TAU * float(s) / float(OUTER_SEGMENTS)
		var p := Vector3(centre.x + cos(a) * OUTER_RADIUS, field.outer_height - 0.05, centre.z + sin(a) * OUTER_RADIUS)
		rim.append(arrays.add_vertex(p, Vector3.UP, Vector2.ZERO))
	for s in OUTER_SEGMENTS:
		arrays.add_tri_facing(c, rim[s], rim[(s + 1) % OUTER_SEGMENTS], Vector3.UP)
	var mesh := ArrayMesh.new()
	arrays.commit(mesh, MeshArrays.shader_material(GRASS_SHADER, {}))
	var mi := MeshInstance3D.new()
	mi.name = "OuterGround"
	mi.mesh = mesh
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(mi)
