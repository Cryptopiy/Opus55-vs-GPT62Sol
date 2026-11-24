class_name CircuitScenery
extends Node3D

const C = preload("res://game/config.gd")
const G = preload("res://game/geometry.gd")
const GRASS = preload("res://shaders/grass.gdshader")
const INK = Color("#202d31")
const BONE = Color("#e9e9d7")
const MINT = Color("#58f5ce")

func build(track: RaceTrack) -> void:
	_make_sky()
	var lawn := ShaderMaterial.new()
	lawn.shader = GRASS
	var plane := PlaneMesh.new()
	plane.size = Vector2(6800.0, 6800.0)
	var level := track.ground_height - 1.25
	G.mesh(self, plane, lawn, Vector3.UP * level)
	_verges(track.data, level, lawn)
	var rng := RandomNumberGenerator.new()
	rng.seed = track.data.accepted_seed ^ 247735
	_trees(track.data, level, rng)
	_mountains(level, rng)
	_gantry(track.data)
	_grandstand(track.data, level)

func _make_sky() -> void:
	var sky := Sky.new()
	var atmosphere := ProceduralSkyMaterial.new()
	atmosphere.sky_top_color = Color("#527eaa")
	atmosphere.sky_horizon_color = Color("#e9ceae")
	atmosphere.ground_horizon_color = Color("#dac8ad")
	atmosphere.ground_bottom_color = Color("#4c6058")
	atmosphere.sun_angle_max = 22.0
	atmosphere.sun_curve = 0.13
	sky.sky_material = atmosphere
	var environment := Environment.new()
	environment.background_mode = Environment.BG_SKY
	environment.sky = sky
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("#bbcde8")
	environment.ambient_light_energy = 0.52
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.tonemap_exposure = 1.08
	environment.fog_enabled = true
	environment.fog_light_color = Color("#c6c8c3")
	environment.fog_density = 0.00078
	environment.fog_sky_affect = 0.15
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)
	var sunlight := DirectionalLight3D.new()
	sunlight.rotation_degrees = Vector3(-27.0, -113.0, 0.0)
	sunlight.light_color = Color("#ffe0b1")
	sunlight.light_energy = 1.53
	sunlight.shadow_enabled = true
	sunlight.directional_shadow_max_distance = C.SHADOW_DISTANCE
	sunlight.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	sunlight.shadow_bias = 0.07
	add_child(sunlight)

func _verges(data: TrackData, level: float, lawn: Material) -> void:
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for side: float in [-1.0, 1.0]:
		var edge := side * (C.ROAD_WIDTH * 0.5 + C.CURB_WIDTH)
		for i in data.points.size():
			var j := data.wrap_index(i + 1)
			var a := data.points[i] + data.right[i] * edge - Vector3.UP * 0.045
			var b := data.points[j] + data.right[j] * edge - Vector3.UP * 0.045
			var c := a + data.right[i] * side * 14.0
			var d := b + data.right[j] * side * 14.0
			c.y = level - 0.035
			d.y = level - 0.035
			for p: Vector3 in [a, b, c, c, b, d]:
				surface.set_normal(Vector3.UP)
				surface.add_vertex(p)
	G.mesh(self, surface.commit(), lawn, Vector3.ZERO)

func _trees(data: TrackData, level: float, rng: RandomNumberGenerator) -> void:
	var trunk := CylinderMesh.new()
	trunk.height = 2.1
	trunk.top_radius = 0.12
	trunk.bottom_radius = 0.24
	trunk.radial_segments = 6
	trunk.rings = 0
	var shape := SurfaceTool.new()
	shape.begin(Mesh.PRIMITIVE_TRIANGLES)
	shape.append_from(trunk, 0, Transform3D(Basis.IDENTITY, Vector3.UP * 1.05))
	var stem_material := G.material(Color("#504238"))
	shape.set_material(stem_material)
	var tree := shape.commit()
	shape.clear()
	shape.begin(Mesh.PRIMITIVE_TRIANGLES)
	for tier in 3:
		var branch := CylinderMesh.new()
		branch.height = 2.55 - tier * 0.22
		branch.top_radius = 0.06
		branch.bottom_radius = 1.57 - tier * 0.34
		branch.radial_segments = 7
		branch.rings = 0
		var basis := Basis(Vector3.UP, tier * 0.22)
		shape.append_from(branch, 0, Transform3D(basis, Vector3.UP * (2.50 + 0.94 * tier)))
	var pines := G.material(Color("#345946"))
	pines.vertex_color_use_as_albedo = true
	shape.set_material(pines)
	shape.commit(tree)
	var groups := {}
	var total := 0
	while total < C.TREES:
		var candidate := Vector3(rng.randf_range(-425.0, 425.0), level - 0.08, rng.randf_range(-425.0, 425.0))
		if total < C.TREES / 4.0:
			var sample := rng.randi_range(32, data.points.size() - 32)
			candidate = data.points[sample] + data.right[sample] * rng.randf_range(30.0, 65.0) * (1.0 if total % 2 == 0 else -1.0)
			candidate.y = level - 0.08
		var near := false
		for i in range(0, data.points.size(), 4):
			if Vector2(candidate.x - data.points[i].x, candidate.z - data.points[i].z).length_squared() < 28.0 * 28.0:
				near = true
				break
		if near:
			continue
		var key := Vector2i(floori(candidate.x / C.TREE_CHUNK), floori(candidate.z / C.TREE_CHUNK))
		if not groups.has(key):
			groups[key] = []
		groups[key].append(candidate)
		total += 1
	for key: Vector2i in groups:
		var forest := MultiMesh.new()
		forest.transform_format = MultiMesh.TRANSFORM_3D
		forest.use_colors = true
		forest.mesh = tree
		forest.instance_count = groups[key].size()
		var local_origin := Vector3(key.x, 0.0, key.y) * C.TREE_CHUNK
		for i in forest.instance_count:
			var at: Vector3 = groups[key][i] - local_origin
			var size := rng.randf_range(0.84, 2.1)
			forest.set_instance_transform(i, Transform3D(
				Basis(Vector3.UP, rng.randf_range(0.0, TAU)).scaled(Vector3(size, size, size)), at))
			forest.set_instance_color(i, Color(rng.randf_range(0.72, 1.0), rng.randf_range(0.81, 1.0), 0.83))
		var cluster := MultiMeshInstance3D.new()
		cluster.position = local_origin
		cluster.multimesh = forest
		cluster.visibility_range_end = C.TREE_RANGE
		cluster.visibility_range_end_margin = 34.0
		cluster.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
		add_child(cluster)

func _mountains(level: float, rng: RandomNumberGenerator) -> void:
	var stone := G.material(Color.WHITE)
	stone.vertex_color_use_as_albedo = true
	stone.cull_mode = BaseMaterial3D.CULL_DISABLED
	for i in C.HILLS:
		var angle := TAU * i / C.HILLS + rng.randf_range(-0.09, 0.09)
		var distance := rng.randf_range(790.0, 1280.0)
		var height := rng.randf_range(118.0, 290.0)
		var width := rng.randf_range(160.0, 310.0)
		var center := Vector3(cos(angle) * distance, level - 3.0, sin(angle) * distance)
		var sides := 10
		var base := PackedVector3Array()
		var shoulders := PackedVector3Array()
		for j in sides:
			var heading := TAU * j / sides
			var radial := Vector3(cos(heading), 0.0, sin(heading))
			base.append(radial * width * rng.randf_range(0.76, 1.28))
			shoulders.append(radial * width * rng.randf_range(0.19, 0.47) + Vector3.UP * height * rng.randf_range(0.41, 0.79))
		var peak := Vector3(width * rng.randf_range(-0.21, 0.21), height, width * rng.randf_range(-0.21, 0.21))
		var rock := SurfaceTool.new()
		rock.begin(Mesh.PRIMITIVE_TRIANGLES)
		for j in sides:
			var k := (j + 1) % sides
			var gray := rng.randf_range(0.73, 1.06)
			var earth := Color("#535f60") * gray
			var ridge := Color("#84888b") * gray
			var summit := Color("#ddd6c8") * gray if height > 195.0 else ridge
			_facet(rock, base[j], base[k], shoulders[j], earth, earth, ridge)
			_facet(rock, shoulders[j], base[k], shoulders[k], ridge, earth, ridge)
			_facet(rock, shoulders[j], shoulders[k], peak, ridge, ridge, summit)
		var model := G.mesh(self, rock.commit(), stone, center)
		model.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

func _facet(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3,
		first: Color, second: Color, third: Color) -> void:
	var normal := (c - a).cross(b - a).normalized()
	surface.set_normal(normal)
	surface.set_color(first)
	surface.add_vertex(a)
	surface.set_color(second)
	surface.add_vertex(b)
	surface.set_color(third)
	surface.add_vertex(c)

func _gantry(data: TrackData) -> void:
	var arch := Node3D.new()
	arch.name = "Timberline start finish"
	arch.transform = data.frame_at(0.0, 0.0, 0.0)
	add_child(arch)
	var steel := G.material(INK, 0.49, 0.40)
	var accent := G.material(MINT, 0.55)
	var concrete := G.material(Color("#9baba7"))
	for side: float in [-1.0, 1.0]:
		var edge := side * (C.ROAD_WIDTH * 0.5 + 0.93)
		G.box(arch, Vector3(0.88, 8.9, 1.38), Vector3(edge, 1.30, 0.0), steel)
		G.box(arch, Vector3(1.40, 1.18, 1.83), Vector3(edge, 0.62, 0.0), concrete)
		G.box(arch, Vector3(0.30, 3.15, 0.10), Vector3(edge, 3.01, 0.72), accent)
	G.box(arch, Vector3(16.15, 1.44, 1.22), Vector3(0.0, 5.71, 0.0), steel)
	G.box(arch, Vector3(16.23, 0.12, 1.26), Vector3(0.0, 6.46, 0.0), accent)
	G.sign_text(arch, "T I M B E R L I N E    /    S T A R T   ·   F I N I S H",
		Vector3(0.0, 5.73, 0.627), 0.59, BONE)
	var back := G.sign_text(arch, "T I M B E R L I N E", Vector3(0.0, 5.73, -0.627), 0.73, BONE)
	back.rotation.y = PI
	for i in 6:
		var chip := G.material(BONE if i % 2 == 0 else INK)
		for side: float in [-1.0, 1.0]:
			G.box(arch, Vector3(0.40, 0.39, 0.08), Vector3(side * (6.08 + 0.42 * (i % 3)),
				5.52 + floorf(i / 3.0) * 0.39, 0.64), chip)

func _grandstand(data: TrackData, level: float) -> void:
	var stand := Node3D.new()
	stand.name = "Main grandstand"
	stand.transform = data.frame_at(8.0, -18.2, 0.0)
	add_child(stand)
	var steel := G.material(INK, 0.48, 0.32)
	var seat := G.material(Color("#3a827b"))
	var light_seat := G.material(Color("#79ac99"))
	var trim := G.material(BONE)
	var base := stand.position.y - level
	G.box(stand, Vector3(9.6, base + 1.5, 42.0), Vector3(0.0, (1.5 - base) * 0.5, 0.0), steel)
	for step_index in 6:
		var height := 1.5 + step_index * 0.56
		G.box(stand, Vector3(1.44, height, 40.0), Vector3(3.75 - step_index * 1.43, height * 0.5, 0.0), steel)
		for section in 4:
			G.box(stand, Vector3(0.68, 0.28, 8.92),
				Vector3(3.68 - step_index * 1.43, height + 0.19, -14.35 + section * 9.57),
				light_seat if (step_index + section) % 3 == 0 else seat)
	for offset: float in [-19.5, -9.8, 0.0, 9.8, 19.5]:
		G.box(stand, Vector3(0.18, 5.7, 0.24), Vector3(-4.25, 3.04, offset), steel)
	G.box(stand, Vector3(9.5, 0.36, 42.2), Vector3(-0.3, 5.8, 0.0), steel)
	G.box(stand, Vector3(0.19, 0.16, 42.2), Vector3(4.48, 5.78, 0.0), trim)
	var marker := G.sign_text(stand, "T I M B E R L I N E    /    G R A N D S T A N D",
		Vector3(4.62, 5.78, 0.0), 0.27, BONE)
	marker.rotation.y = PI * 0.5
