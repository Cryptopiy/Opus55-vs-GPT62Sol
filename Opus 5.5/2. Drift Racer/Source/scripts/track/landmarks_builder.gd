class_name LandmarksBuilder
extends RefCounted
## Start/finish arch with banner, grandstand along the start straight and the
## 12 distant hills on the horizon (R-05). Everything is primitive geometry.

const CHECKER_SHADER := preload("res://shaders/checker.gdshader")
const HILL_SHADER := preload("res://shaders/hill.gdshader")
const CROWD_SPACING := 0.75


## Side (-1 left / +1 right) of the start straight that faces away from the
## infield: the grandstand goes there.
static func outside_side(data: TrackData) -> float:
	var centre := data.bounds.get_center()
	return 1.0 if (data.points[0] - centre).dot(data.rights[0]) > 0.0 else -1.0


## Keep-out circles (x, z, radius) for trees around arch and grandstand.
static func keepouts(data: TrackData) -> PackedVector3Array:
	var side := outside_side(data)
	var p := data.points[0]
	var r := data.rights[0]
	var stand := p + r * side * (Config.GRANDSTAND_OFFSET + 8.0)
	var out := PackedVector3Array()
	out.append(Vector3(stand.x, stand.z, Config.GRANDSTAND_LENGTH * 0.5 + Config.GRANDSTAND_KEEPOUT))
	out.append(Vector3(p.x, p.z, Config.GRANDSTAND_KEEPOUT))
	return out


static func build_arch(data: TrackData, parent: Node3D) -> void:
	var p := data.points[0]
	var t := data.tangents[0]
	var flat_t := Vector3(t.x, 0.0, t.z).normalized()
	var arch := Node3D.new()
	arch.name = "StartArch"
	arch.transform = Transform3D(Basis.looking_at(flat_t, Vector3.UP), p)
	parent.add_child(arch)
	var pillar_mat := StandardMaterial3D.new()
	pillar_mat.albedo_color = Color(0.78, 0.78, 0.8)
	pillar_mat.metallic = 0.6
	pillar_mat.roughness = 0.35
	var off := Config.ARCH_PILLAR_OFFSET
	var h := Config.ARCH_HEIGHT
	for side in [-1.0, 1.0]:
		_box(arch, Vector3(0.9, h, 0.9), Vector3(off * side, h * 0.5 - 0.5, 0.0), pillar_mat)
	var beam_mat := MeshArrays.shader_material(CHECKER_SHADER, {"cell": 0.45})
	var beam_len := off * 2.0 + 0.9
	_box_uv_metres(arch, Vector3(beam_len, 1.0, 1.0), Vector3(0.0, h - 0.5, 0.0), beam_mat)
	var banner_mat := StandardMaterial3D.new()
	banner_mat.albedo_color = Color(0.05, 0.12, 0.35)
	banner_mat.roughness = 0.6
	_box(arch, Vector3(beam_len - 3.0, 1.5, 0.25), Vector3(0.0, h - 1.75, 0.0), banner_mat)
	for face in [-1.0, 1.0]:
		var label := Label3D.new()
		label.text = "START  /  FINISH"
		label.font_size = 160
		label.pixel_size = 0.0068
		label.outline_size = 18
		label.modulate = Color(1.0, 0.95, 0.85)
		label.outline_modulate = Color(0.0, 0.0, 0.0, 0.6)
		label.position = Vector3(0.0, h - 1.75, 0.14 * face)
		label.rotation = Vector3(0.0, 0.0 if face > 0.0 else PI, 0.0)
		label.shaded = false
		arch.add_child(label)


static func build_grandstand(data: TrackData, parent: Node3D, rng: RandomNumberGenerator) -> void:
	var side := outside_side(data)
	var p := data.points[0]
	var t := data.tangents[0]
	var flat_t := Vector3(t.x, 0.0, t.z).normalized()
	var out := Vector3(data.rights[0].x, 0.0, data.rights[0].z).normalized() * side
	var stand := Node3D.new()
	stand.name = "Grandstand"
	# Local frame: X along the straight, +Z away from the track, origin on the
	# ground. X is flipped with the side so the basis stays right-handed.
	var basis := Basis(flat_t * side, Vector3.UP, out)
	stand.transform = Transform3D(basis, p + out * Config.GRANDSTAND_OFFSET + Vector3.DOWN * 0.4)
	parent.add_child(stand)
	var concrete := StandardMaterial3D.new()
	concrete.albedo_color = Color(0.62, 0.62, 0.64)
	concrete.roughness = 0.85
	var seat := StandardMaterial3D.new()
	seat.albedo_color = Color(0.1, 0.25, 0.55)
	seat.roughness = 0.6
	var roof := StandardMaterial3D.new()
	roof.albedo_color = Color(0.85, 0.86, 0.9)
	roof.metallic = 0.5
	roof.roughness = 0.4
	var length := Config.GRANDSTAND_LENGTH
	var tiers := Config.GRANDSTAND_TIERS
	var step_h := 0.9
	var step_d := 1.6
	var crowd := []
	for k in tiers:
		var top := 0.8 + step_h * float(k + 1)
		_box(stand, Vector3(length, top, step_d), Vector3(0.0, top * 0.5, 1.0 + step_d * (float(k) + 0.5)), concrete)
		_box(stand, Vector3(length, 0.12, step_d * 0.55), Vector3(0.0, top + 0.06, 1.0 + step_d * (float(k) + 0.35)), seat)
		var x := -length * 0.5 + 0.6
		while x < length * 0.5 - 0.6:
			if rng.randf() < 0.82:
				crowd.append(Transform3D(Basis.IDENTITY, Vector3(x + rng.randf_range(-0.15, 0.15),
					top + 0.12 + 0.45, 1.0 + step_d * (float(k) + 0.45))))
			x += CROWD_SPACING
	var back := 1.0 + step_d * float(tiers)
	var roof_y := 0.8 + step_h * float(tiers) + 4.0
	_box(stand, Vector3(length, roof_y, 0.4), Vector3(0.0, roof_y * 0.5, back + 0.2), concrete)
	_box(stand, Vector3(length + 2.0, 0.3, back + 2.5), Vector3(0.0, roof_y, back * 0.5 - 0.2), roof)
	var pillar_x := -length * 0.5
	while pillar_x <= length * 0.5 + 0.01:
		_box(stand, Vector3(0.35, roof_y, 0.35), Vector3(pillar_x, roof_y * 0.5, 0.4), roof)
		pillar_x += 12.0
	_add_crowd(stand, crowd, rng)


static func _add_crowd(stand: Node3D, crowd: Array, rng: RandomNumberGenerator) -> void:
	var body := BoxMesh.new()
	body.size = Vector3(0.45, 0.9, 0.35)
	var mat := StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.roughness = 0.8
	body.material = mat
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = body
	mm.instance_count = crowd.size()
	for i in crowd.size():
		var xf: Transform3D = crowd[i]
		mm.set_instance_transform(i, xf)
		mm.set_instance_color(i, Color.from_hsv(rng.randf(), rng.randf_range(0.4, 0.9), rng.randf_range(0.5, 1.0)))
	var mmi := MultiMeshInstance3D.new()
	mmi.name = "Crowd"
	mmi.multimesh = mm
	stand.add_child(mmi)


## 12 distant hills / mountains spread around the horizon.
static func build_hills(data: TrackData, parent: Node3D, rng: RandomNumberGenerator, base_height: float) -> void:
	var centre := data.bounds.get_center()
	for i in Config.HILL_COUNT:
		var ang := TAU * (float(i) + rng.randf_range(-0.3, 0.3)) / float(Config.HILL_COUNT)
		var dist := rng.randf_range(Config.HILL_DISTANCE_MIN, Config.HILL_DISTANCE_MAX)
		var radius := rng.randf_range(Config.HILL_RADIUS_MIN, Config.HILL_RADIUS_MAX)
		var height := rng.randf_range(Config.HILL_HEIGHT_MIN, Config.HILL_HEIGHT_MAX)
		var mesh := _hill_mesh(radius, height, rng.randf() * 100.0)
		var mi := MeshInstance3D.new()
		mi.name = "Hill%d" % i
		mi.mesh = mesh
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.material_override = MeshArrays.shader_material(HILL_SHADER, {
			"peak_height": height, "snow_line": 0.78 if height > 230.0 else 2.0,
		})
		mi.position = Vector3(centre.x + cos(ang) * dist, base_height - 2.0, centre.z + sin(ang) * dist)
		parent.add_child(mi)


static func _hill_mesh(radius: float, height: float, phase: float) -> ArrayMesh:
	var a := MeshArrays.new()
	var rings := 10
	var segs := 28
	var top := a.add_vertex(Vector3(0.0, height, 0.0), Vector3.UP, Vector2.ZERO)
	var ring_ids := []
	for r in range(1, rings + 1):
		var f := float(r) / float(rings)
		var ids := PackedInt32Array()
		for s in segs:
			var th := TAU * float(s) / float(segs)
			var wobble := 1.0 + 0.18 * sin(th * 3.0 + phase) + 0.1 * sin(th * 7.0 + phase * 1.7)
			var rr := radius * f * wobble
			# Rounded peak falling off to the base, with ridged noise.
			var hh := height * pow(1.0 - f, 1.6) * (1.0 + 0.12 * sin(th * 5.0 + phase + f * 4.0))
			var p := Vector3(cos(th) * rr, hh, sin(th) * rr)
			ids.append(a.add_vertex(p, Vector3.UP, Vector2.ZERO))
		ring_ids.append(ids)
	var first: PackedInt32Array = ring_ids[0]
	for s in segs:
		a.add_tri_facing(top, first[s], first[(s + 1) % segs], Vector3.UP)
	for r in rings - 1:
		var inner: PackedInt32Array = ring_ids[r]
		var outer: PackedInt32Array = ring_ids[r + 1]
		for s in segs:
			var s1 := (s + 1) % segs
			a.add_quad_facing(inner[s], outer[s], outer[s1], inner[s1], Vector3.UP)
	_recompute_normals(a)
	var mesh := ArrayMesh.new()
	a.commit(mesh, null)
	return mesh


static func _recompute_normals(a: MeshArrays) -> void:
	var acc := PackedVector3Array()
	acc.resize(a.verts.size())
	acc.fill(Vector3.ZERO)
	for i in range(0, a.indices.size(), 3):
		var i0 := a.indices[i]
		var i1 := a.indices[i + 1]
		var i2 := a.indices[i + 2]
		# Godot front faces are clockwise, so the outward normal is (c-a) x (b-a).
		var n := (a.verts[i2] - a.verts[i0]).cross(a.verts[i1] - a.verts[i0])
		acc[i0] += n
		acc[i1] += n
		acc[i2] += n
	for i in acc.size():
		a.normals[i] = acc[i].normalized()


static func _box(parent: Node3D, size: Vector3, pos: Vector3, mat: Material) -> MeshInstance3D:
	var mesh := BoxMesh.new()
	mesh.size = size
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	parent.add_child(mi)
	return mi


## Box whose UVs are in metres on every face (for the checkered beam).
static func _box_uv_metres(parent: Node3D, size: Vector3, pos: Vector3, mat: Material) -> void:
	var a := MeshArrays.new()
	var half := size * 0.5
	var axes := [Vector3.RIGHT, Vector3.UP, Vector3.BACK]
	for ax: Vector3 in axes:
		var u: Vector3 = axes[(axes.find(ax) + 1) % 3]
		var v: Vector3 = axes[(axes.find(ax) + 2) % 3]
		for sgn in [-1.0, 1.0]:
			var n: Vector3 = ax * sgn
			var c := n * absf(half.dot(ax))
			var hu := absf(half.dot(u))
			var hv := absf(half.dot(v))
			var ids := PackedInt32Array()
			for corner in [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, 1)]:
				var cv: Vector2 = corner
				var p := c + u * hu * cv.x + v * hv * cv.y
				ids.append(a.add_vertex(p, n, Vector2(p.dot(u), p.dot(v))))
			a.add_quad_facing(ids[0], ids[1], ids[2], ids[3], n)
	var mesh := ArrayMesh.new()
	a.commit(mesh, mat)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	parent.add_child(mi)
