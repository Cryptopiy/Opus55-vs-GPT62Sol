class_name MeshArrays
extends RefCounted
## Small helper that accumulates vertex data for one ArrayMesh surface.
## Quads are emitted with Godot's front-face winding (clockwise as seen from
## the visible side), chosen automatically from the desired facing normal.

var verts := PackedVector3Array()
var normals := PackedVector3Array()
var uvs := PackedVector2Array()
var colors := PackedColorArray()
var indices := PackedInt32Array()
var use_colors := false


func add_vertex(v: Vector3, n: Vector3, uv: Vector2) -> int:
	verts.append(v)
	normals.append(n)
	uvs.append(uv)
	if use_colors:
		colors.append(Color.WHITE)
	return verts.size() - 1


func add_vertex_color(v: Vector3, n: Vector3, uv: Vector2, c: Color) -> int:
	verts.append(v)
	normals.append(n)
	uvs.append(uv)
	colors.append(c)
	return verts.size() - 1


## Adds the quad a-b-c-d (any cyclic order) so that it faces `facing`.
func add_quad_facing(a: int, b: int, c: int, d: int, facing: Vector3) -> void:
	var pa := verts[a]
	var front := (verts[c] - pa).cross(verts[b] - pa)
	if front.dot(facing) >= 0.0:
		indices.append_array(PackedInt32Array([a, b, c, a, c, d]))
	else:
		indices.append_array(PackedInt32Array([a, c, b, a, d, c]))


## Adds the triangle a-b-c so that it faces `facing`.
func add_tri_facing(a: int, b: int, c: int, facing: Vector3) -> void:
	var pa := verts[a]
	var front := (verts[c] - pa).cross(verts[b] - pa)
	if front.dot(facing) >= 0.0:
		indices.append_array(PackedInt32Array([a, b, c]))
	else:
		indices.append_array(PackedInt32Array([a, c, b]))


func is_empty() -> bool:
	return verts.is_empty()


func commit(mesh: ArrayMesh, material: Material) -> void:
	if verts.is_empty():
		return
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = verts
	arr[Mesh.ARRAY_NORMAL] = normals
	arr[Mesh.ARRAY_TEX_UV] = uvs
	if use_colors:
		arr[Mesh.ARRAY_COLOR] = colors
	arr[Mesh.ARRAY_INDEX] = indices
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	mesh.surface_set_material(mesh.get_surface_count() - 1, material)


## Triangle soup (for ConcavePolygonShape3D) built from the indexed data.
func to_faces() -> PackedVector3Array:
	var faces := PackedVector3Array()
	faces.resize(indices.size())
	for i in indices.size():
		faces[i] = verts[indices[i]]
	return faces


static func shader_material(shader: Shader, params: Dictionary) -> ShaderMaterial:
	var mat := ShaderMaterial.new()
	mat.shader = shader
	for key: String in params:
		mat.set_shader_parameter(key, params[key])
	return mat
