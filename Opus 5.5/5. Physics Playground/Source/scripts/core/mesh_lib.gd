extends RefCounted
## Procedural meshes (all geometry is generated: primitives + small custom ArrayMeshes) and helpers that
## attach mesh instances / collision shapes. Meshes are cached by size so identical parts share one resource.

const Mats := preload("res://scripts/core/materials.gd")

static var _cache := {}


static func box(sz: Vector3) -> BoxMesh:
	var key := "box_%.3f_%.3f_%.3f" % [sz.x, sz.y, sz.z]
	if _cache.has(key):
		return _cache[key]
	var m := BoxMesh.new()
	m.size = sz
	_cache[key] = m
	return m


static func sphere(radius: float) -> SphereMesh:
	var key := "sphere_%.3f" % radius
	if _cache.has(key):
		return _cache[key]
	var m := SphereMesh.new()
	m.radius = radius
	m.height = radius * 2.0
	m.radial_segments = 32
	m.rings = 16
	_cache[key] = m
	return m


static func cylinder(radius: float, height: float, sides: int = 24) -> CylinderMesh:
	var key := "cyl_%.3f_%.3f_%d" % [radius, height, sides]
	if _cache.has(key):
		return _cache[key]
	var m := CylinderMesh.new()
	m.top_radius = radius
	m.bottom_radius = radius
	m.height = height
	m.radial_segments = sides
	_cache[key] = m
	return m


static func quad(sz: Vector2) -> QuadMesh:
	var key := "quad_%.3f_%.3f" % [sz.x, sz.y]
	if _cache.has(key):
		return _cache[key]
	var m := QuadMesh.new()
	m.size = sz
	_cache[key] = m
	return m


## Triangular prism, apex up at local y = 0, base at y = -height, extruded along Z (lever stand, wedges).
static func prism(width: float, height: float, depth: float) -> ArrayMesh:
	var key := "prism_%.3f_%.3f_%.3f" % [width, height, depth]
	if _cache.has(key):
		return _cache[key]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var hz := depth * 0.5
	var a := Vector2(0, 0)
	var b := Vector2(-width * 0.5, -height)
	var c := Vector2(width * 0.5, -height)
	# front / back triangles
	_tri(st, Vector3(a.x, a.y, hz), Vector3(b.x, b.y, hz), Vector3(c.x, c.y, hz), Vector3.BACK)
	_tri(st, Vector3(a.x, a.y, -hz), Vector3(c.x, c.y, -hz), Vector3(b.x, b.y, -hz), Vector3.FORWARD)
	# three side quads
	for e in [[a, b], [b, c], [c, a]]:
		var p: Vector2 = e[0]
		var q: Vector2 = e[1]
		var d := q - p
		var n := Vector3(d.y, -d.x, 0).normalized()
		_tri(st, Vector3(p.x, p.y, hz), Vector3(p.x, p.y, -hz), Vector3(q.x, q.y, -hz), n)
		_tri(st, Vector3(p.x, p.y, hz), Vector3(q.x, q.y, -hz), Vector3(q.x, q.y, hz), n)
	var m := st.commit()
	_cache[key] = m
	return m


static func _tri(st: SurfaceTool, p0: Vector3, p1: Vector3, p2: Vector3, n: Vector3) -> void:
	# Godot uses clockwise winding for front faces
	st.set_normal(n)
	st.add_vertex(p0)
	st.set_normal(n)
	st.add_vertex(p2)
	st.set_normal(n)
	st.add_vertex(p1)


## Cloth rest mesh: n x n grid over size (X span, Z depth) with a parabolic dip along X (built-in slack),
## optionally baked into world space with `xf` (row-major: corner vertex indices are 0, n-1, n*(n-1), n*n-1).
static func cloth_grid(sz: Vector2, n: int, dip: float, xf: Transform3D = Transform3D.IDENTITY) -> ArrayMesh:
	var verts := PackedVector3Array()
	var norms := PackedVector3Array()
	var uvs := PackedVector2Array()
	var idx := PackedInt32Array()
	for j in n:
		for i in n:
			var u := float(i) / float(n - 1)
			var v := float(j) / float(n - 1)
			var x := (u - 0.5) * sz.x
			var xn := x / (sz.x * 0.5)
			verts.append(xf * Vector3(x, -dip * (1.0 - xn * xn), (v - 0.5) * sz.y))
			norms.append(xf.basis * Vector3.UP)
			uvs.append(Vector2(u, v))
	for j in n - 1:
		for i in n - 1:
			var a := j * n + i
			idx.append_array(PackedInt32Array([a, a + 1, a + n, a + 1, a + n + 1, a + n]))
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = verts
	arr[Mesh.ARRAY_NORMAL] = norms
	arr[Mesh.ARRAY_TEX_UV] = uvs
	arr[Mesh.ARRAY_INDEX] = idx
	var m := ArrayMesh.new()
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return m


## Adds a box mesh instance (rounded material gets its half extents as an instance uniform).
static func add_box(parent: Node3D, sz: Vector3, pos: Vector3, mat: Material, rot_z_deg: float = 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = box(sz)
	mi.material_override = mat
	mi.position = pos
	mi.rotation.z = deg_to_rad(rot_z_deg)
	if Mats.uses_extents(mat):
		mi.set_instance_shader_parameter("half_extents", sz * 0.5)
	parent.add_child(mi)
	return mi


static func add_mesh(parent: Node3D, mesh: Mesh, pos: Vector3, mat: Material) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	parent.add_child(mi)
	return mi


static func add_box_shape(body: Node3D, sz: Vector3, pos: Vector3 = Vector3.ZERO, rot_z_deg: float = 0.0) -> CollisionShape3D:
	var cs := CollisionShape3D.new()
	var sh := BoxShape3D.new()
	sh.size = sz
	cs.shape = sh
	cs.position = pos
	cs.rotation.z = deg_to_rad(rot_z_deg)
	body.add_child(cs)
	return cs


static func add_sphere_shape(body: Node3D, radius: float, pos: Vector3 = Vector3.ZERO) -> CollisionShape3D:
	var cs := CollisionShape3D.new()
	var sh := SphereShape3D.new()
	sh.radius = radius
	cs.shape = sh
	cs.position = pos
	body.add_child(cs)
	return cs


## Convex prism collision (apex up at y = 0) matching prism().
static func add_prism_shape(body: Node3D, width: float, height: float, depth: float) -> CollisionShape3D:
	var cs := CollisionShape3D.new()
	var sh := ConvexPolygonShape3D.new()
	var hz := depth * 0.5
	sh.points = PackedVector3Array([
		Vector3(0, 0, hz), Vector3(-width * 0.5, -height, hz), Vector3(width * 0.5, -height, hz),
		Vector3(0, 0, -hz), Vector3(-width * 0.5, -height, -hz), Vector3(width * 0.5, -height, -hz)])
	cs.shape = sh
	body.add_child(cs)
	return cs


## Sets cast_shadow / layers on every MeshInstance3D below `root` (ghosts and hints must not cast shadows).
static func set_shadows(root: Node, enabled: bool) -> void:
	for n in root.find_children("*", "GeometryInstance3D", true, false):
		(n as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if enabled else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## Replaces every material below `root` with `mat` (ghost / hint look).
static func override_all(root: Node, mat: Material) -> void:
	for n in root.find_children("*", "GeometryInstance3D", true, false):
		(n as GeometryInstance3D).material_override = mat


static func set_overlay(root: Node, mat: Material) -> void:
	for n in root.find_children("*", "GeometryInstance3D", true, false):
		(n as GeometryInstance3D).material_overlay = mat
