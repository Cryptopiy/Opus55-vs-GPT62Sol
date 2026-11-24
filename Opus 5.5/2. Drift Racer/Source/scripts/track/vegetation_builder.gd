class_name VegetationBuilder
extends RefCounted
## Low-poly trees (R-05, R-31): >= 400 trees placed from the track seed,
## grouped into spatial chunks of MultiMeshInstance3D. Each chunk has a
## detailed LOD0 (near, casts shadows) and a cheap LOD1 (far) switched with
## visibility ranges, so distant trees cost almost nothing.

const TREE_SHADER := preload("res://shaders/tree.gdshader")
const TRUNK_COLOR := Color(0.32, 0.21, 0.12)
const SPECIES_PINE := 0
const SPECIES_ROUND := 1


## keepouts: Vector3(x, z, radius) circles where no tree may grow.
static func build(data: TrackData, ground: GroundBuilder.Field, parent: Node3D,
		rng: RandomNumberGenerator, keepouts: PackedVector3Array) -> int:
	var material := ShaderMaterial.new()
	material.shader = TREE_SHADER
	var meshes := [
		[_pine_mesh(true), _pine_mesh(false)],
		[_round_mesh(true), _round_mesh(false)],
	]
	var chunks := {}                       # Vector3i(cx, cz, species) -> Array[Transform3D, Color...]
	var taken := {}                        # Vector2i -> true (min spacing grid)
	var placed := 0
	var region := data.bounds.grow(Config.TREE_MAX_TRACK_DISTANCE * 0.7)
	var attempts := 0
	while placed < Config.TREE_COUNT and attempts < Config.TREE_COUNT * 40:
		attempts += 1
		var x := 0.0
		var z := 0.0
		if attempts % 2 == 0:
			# Half the candidates line the circuit at a short distance.
			var i := rng.randi_range(0, data.count - 1)
			var side := -1.0 if rng.randf() < 0.5 else 1.0
			var off := rng.randf_range(Config.TREE_MIN_TRACK_DISTANCE, 90.0) * side
			var p := data.points[i] + data.rights[i] * off
			x = p.x
			z = p.z
		else:
			x = rng.randf_range(region.position.x, region.end.x)
			z = rng.randf_range(region.position.z, region.end.z)
		if data.distance_to_centerline(x, z, Config.TREE_MIN_TRACK_DISTANCE) < Config.TREE_MIN_TRACK_DISTANCE:
			continue
		if _in_keepout(x, z, keepouts):
			continue
		var key := Vector2i(int(floorf(x / Config.TREE_MIN_SPACING)), int(floorf(z / Config.TREE_MIN_SPACING)))
		if taken.has(key):
			continue
		taken[key] = true
		var species := SPECIES_PINE if rng.randf() < 0.6 else SPECIES_ROUND
		var s := rng.randf_range(Config.TREE_SCALE_MIN, Config.TREE_SCALE_MAX)
		var basis := Basis(Vector3.UP, rng.randf() * TAU).scaled(Vector3(s, s * rng.randf_range(0.9, 1.15), s))
		var pos := Vector3(x, ground.height_at(x, z) - 0.1, z)
		var tint := _tint(species, rng)
		var ckey := Vector3i(int(floorf(x / Config.TREE_CHUNK_SIZE)), int(floorf(z / Config.TREE_CHUNK_SIZE)), species)
		var list: Array = chunks.get(ckey, [])
		list.append(Transform3D(basis, pos))
		list.append(tint)
		chunks[ckey] = list
		placed += 1
	for ckey: Vector3i in chunks:
		var list: Array = chunks[ckey]
		var pair: Array = meshes[ckey.z]
		_add_chunk(parent, list, pair[0], material, 0.0, Config.TREE_LOD0_END, true)
		_add_chunk(parent, list, pair[1], material, Config.TREE_LOD0_END, Config.TREE_LOD1_END, false)
	return placed


static func _in_keepout(x: float, z: float, keepouts: PackedVector3Array) -> bool:
	for k in keepouts:
		if (x - k.x) * (x - k.x) + (z - k.y) * (z - k.y) < k.z * k.z:
			return true
	return false


static func _tint(species: int, rng: RandomNumberGenerator) -> Color:
	if species == SPECIES_PINE:
		return Color(rng.randf_range(0.16, 0.24), rng.randf_range(0.3, 0.42), rng.randf_range(0.12, 0.2))
	return Color(rng.randf_range(0.28, 0.42), rng.randf_range(0.42, 0.55), rng.randf_range(0.12, 0.2))


static func _add_chunk(parent: Node3D, list: Array, mesh: Mesh, material: Material,
		range_begin: float, range_end: float, shadows: bool) -> void:
	var count := list.size() >> 1
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = mesh
	mm.instance_count = count
	for j in count:
		var xf: Transform3D = list[j * 2]
		var tint: Color = list[j * 2 + 1]
		mm.set_instance_transform(j, xf)
		mm.set_instance_custom_data(j, tint)
	var mmi := MultiMeshInstance3D.new()
	mmi.name = "Trees"
	mmi.multimesh = mm
	mmi.material_override = material
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadows else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mmi.visibility_range_begin = range_begin
	mmi.visibility_range_begin_margin = Config.TREE_LOD_MARGIN if range_begin > 0.0 else 0.0
	mmi.visibility_range_end = range_end
	mmi.visibility_range_end_margin = Config.TREE_LOD_MARGIN
	mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	parent.add_child(mmi)


static func _pine_mesh(detailed: bool) -> ArrayMesh:
	var a := MeshArrays.new()
	a.use_colors = true
	_cylinder(a, 0.22, 2.4, 6 if detailed else 4)
	if detailed:
		_cone(a, 1.4, 4.6, 2.1, 8)
		_cone(a, 3.0, 6.2, 1.65, 8)
		_cone(a, 4.6, 7.8, 1.15, 8)
	else:
		_cone(a, 1.4, 7.8, 2.0, 6)
	var mesh := ArrayMesh.new()
	a.commit(mesh, null)
	return mesh


static func _round_mesh(detailed: bool) -> ArrayMesh:
	var a := MeshArrays.new()
	a.use_colors = true
	_cylinder(a, 0.2, 2.6, 6 if detailed else 4)
	if detailed:
		_blob(a, Vector3(0.0, 3.9, 0.0), Vector3(2.3, 2.1, 2.3), 10, 6)
	else:
		_blob(a, Vector3(0.0, 3.9, 0.0), Vector3(2.2, 2.0, 2.2), 6, 4)
	var mesh := ArrayMesh.new()
	a.commit(mesh, null)
	return mesh


static func _cylinder(a: MeshArrays, radius: float, height: float, sides: int) -> void:
	for s in sides:
		var a0 := TAU * float(s) / float(sides)
		var a1 := TAU * float(s + 1) / float(sides)
		var d0 := Vector3(cos(a0), 0.0, sin(a0))
		var d1 := Vector3(cos(a1), 0.0, sin(a1))
		var v0 := a.add_vertex_color(d0 * radius, d0, Vector2.ZERO, TRUNK_COLOR * 0.7)
		var v1 := a.add_vertex_color(d0 * radius * 0.8 + Vector3.UP * height, d0, Vector2.ZERO, TRUNK_COLOR)
		var v2 := a.add_vertex_color(d1 * radius * 0.8 + Vector3.UP * height, d1, Vector2.ZERO, TRUNK_COLOR)
		var v3 := a.add_vertex_color(d1 * radius, d1, Vector2.ZERO, TRUNK_COLOR * 0.7)
		a.add_quad_facing(v0, v1, v2, v3, (d0 + d1).normalized())


## Foliage cone with darker base (fake ambient occlusion) and a closed bottom.
static func _cone(a: MeshArrays, y0: float, y1: float, radius: float, sides: int) -> void:
	var h := y1 - y0
	var dark := Color(0.55, 0.55, 0.55)
	var centre := a.add_vertex_color(Vector3.UP * y0, Vector3.DOWN, Vector2(1.0, 0.0), dark * 0.6)
	for s in sides:
		var a0 := TAU * float(s) / float(sides)
		var a1 := TAU * float(s + 1) / float(sides)
		var d0 := Vector3(cos(a0), 0.0, sin(a0))
		var d1 := Vector3(cos(a1), 0.0, sin(a1))
		var n0 := Vector3(d0.x * h, radius, d0.z * h).normalized()
		var n1 := Vector3(d1.x * h, radius, d1.z * h).normalized()
		var nm := (n0 + n1).normalized()
		var b0 := a.add_vertex_color(d0 * radius + Vector3.UP * y0, n0, Vector2(1.0, 0.0), dark)
		var b1 := a.add_vertex_color(d1 * radius + Vector3.UP * y0, n1, Vector2(1.0, 0.0), dark)
		var tip := a.add_vertex_color(Vector3.UP * y1, nm, Vector2(1.0, 0.0), Color.WHITE)
		a.add_tri_facing(b0, tip, b1, (d0 + d1).normalized())
		var u0 := a.add_vertex_color(d0 * radius + Vector3.UP * y0, Vector3.DOWN, Vector2(1.0, 0.0), dark * 0.6)
		var u1 := a.add_vertex_color(d1 * radius + Vector3.UP * y0, Vector3.DOWN, Vector2(1.0, 0.0), dark * 0.6)
		a.add_tri_facing(centre, u0, u1, Vector3.DOWN)


## Slightly lumpy ellipsoid crown (deterministic lumps, no RNG needed).
static func _blob(a: MeshArrays, centre: Vector3, radii: Vector3, segments: int, rings: int) -> void:
	var base := a.verts.size()
	for r in rings + 1:
		var v := float(r) / float(rings)
		var phi := PI * v
		for s in segments + 1:
			var theta := TAU * float(s) / float(segments)
			var dir := Vector3(sin(phi) * cos(theta), cos(phi), sin(phi) * sin(theta))
			var lump := 1.0 + 0.09 * sin(theta * 3.0 + phi * 5.0) + 0.06 * cos(theta * 5.0 - phi * 3.0)
			var p := centre + dir * radii * lump
			var shade := lerpf(1.0, 0.5, v)
			a.add_vertex_color(p, dir, Vector2(1.0, 0.0), Color(shade, shade, shade))
	for r in rings:
		for s in segments:
			var i0 := base + r * (segments + 1) + s
			var i1 := i0 + segments + 1
			var mid := (a.verts[i0] + a.verts[i1 + 1]) * 0.5 - centre
			a.add_quad_facing(i0, i0 + 1, i1 + 1, i1, mid.normalized())
