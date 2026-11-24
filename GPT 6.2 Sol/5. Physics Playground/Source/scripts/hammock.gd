class_name LabHammock
extends Node3D

const C = preload("res://scripts/config.gd")
const G = preload("res://scripts/geometry.gd")
var cloth: SoftBody3D

func assemble(board: LabBoard, spec: Dictionary) -> void:
	position = C.in_plane(spec.position)
	rotation_degrees.z = spec.rotation
	var paint := G.material(Color("#b6ddea"))
	paint.cull_mode = BaseMaterial3D.CULL_DISABLED
	var grid := PlaneMesh.new()
	grid.size = Vector2(C.CLOTH_WIDTH, C.CLOTH_PATTERN_DEPTH)
	grid.subdivide_width = C.CLOTH_VERTICES - 2
	grid.subdivide_depth = C.CLOTH_VERTICES - 2
	var arrays := grid.get_mesh_arrays()
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	for index in range(vertices.size()):
		var vertex := vertices[index]
		vertex.z *= C.CLOTH_DEPTH / C.CLOTH_PATTERN_DEPTH
		vertex.y = -0.10 * (1.0 - vertex.x * vertex.x) * (1.0 - 4.0 * vertex.z * vertex.z)
		vertices[index] = vertex
	arrays[Mesh.ARRAY_VERTEX] = vertices
	var fabric := ArrayMesh.new()
	fabric.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	if board.active:
		cloth = SoftBody3D.new()
		cloth.mesh = fabric
		cloth.material_override = paint
		cloth.collision_layer = C.SOLID_LAYER
		cloth.collision_mask = C.BODY_LAYER
		cloth.total_mass = 0.42
		cloth.linear_stiffness = C.CLOTH_STIFFNESS
		cloth.simulation_precision = 12
		cloth.damping_coefficient = 0.09
		add_child(cloth)
		for index in range(vertices.size()):
			var v := vertices[index]
			if absf(v.x) >= 0.999 and absf(v.z) >= 0.499:
				cloth.set_point_pinned(index, true)
	else:
		G.mesh(self, fabric, paint)
	for x in [-C.CLOTH_WIDTH * 0.5, C.CLOTH_WIDTH * 0.5]:
		for z in [-C.CLOTH_DEPTH * 0.5, C.CLOTH_DEPTH * 0.5]:
			G.sphere(self, 0.105, G.material(Color("#477f9d"), 0.26), Vector3(x, 0, z))


