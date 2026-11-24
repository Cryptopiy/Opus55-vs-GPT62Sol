class_name LabStudio
extends Node3D

const C = preload("res://scripts/config.gd")
const G = preload("res://scripts/geometry.gd")
var labels: Node3D

func _ready() -> void:
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("#a0b7cd")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("#d8ecff")
	environment.ambient_light_energy = 0.78
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.ssao_enabled = true
	environment.ssao_radius = 1.25
	environment.ssao_intensity = 1.08
	environment.glow_enabled = true
	environment.glow_intensity = 0.42
	environment.glow_bloom = 0.09
	environment.glow_hdr_threshold = 1.05
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)

	var daylight := DirectionalLight3D.new()
	daylight.rotation_degrees = Vector3(-39, -31, -14)
	daylight.light_color = Color("#fff0db")
	daylight.light_energy = 1.42
	daylight.shadow_enabled = true
	daylight.directional_shadow_max_distance = 68
	daylight.light_angular_distance = 3.4
	add_child(daylight)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-8, 126, 0)
	fill.light_color = Color("#bddeff")
	fill.light_energy = 0.59
	add_child(fill)

	var quad := QuadMesh.new()
	quad.size = Vector2(160, 105)
	var paper := ShaderMaterial.new()
	paper.shader = preload("res://shaders/blueprint.gdshader")
	var backdrop := G.mesh(self, quad, paper, Vector3(0, 7, -4.7))
	backdrop.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	labels = Node3D.new()
	add_child(labels)

func decorate(level: Dictionary) -> void:
	for child in labels.get_children():
		labels.remove_child(child)
		child.queue_free()
	for zone in level.zones:
		var quad := QuadMesh.new()
		quad.size = zone.size
		var ink := ShaderMaterial.new()
		ink.shader = preload("res://shaders/hatch.gdshader")
		ink.set_shader_parameter("extent", zone.size)
		var mesh := G.mesh(labels, quad, ink, C.in_plane(zone.get_center(), -0.95))
		mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var launch: Vector2 = level.start
	var finish: Vector2 = level.goal
	_beacon(launch + Vector2(0, 1.04), "START", Color("#3f6279"))
	_beacon(finish + Vector2(0, 1.40), "RECEIVER", Color("#316c70"))
	if level.id == "L5":
		_beacon(C.SWITCH_POS + Vector2(0, 1.08), "5 kg", Color("#8a5935"))
	if level.id == "L2":
		_beacon(Vector2(-9, C.ROPE_ANCHOR_BAND.y + 0.12), "ANCHOR BAND", Color("#5e5782"))
		var ink := G.material(Color(0.34, 0.27, 0.53, 0.23))
		for x in [-11.4, -6.6, -1.8]:
			for y in [C.ROPE_ANCHOR_BAND.x, C.ROPE_ANCHOR_BAND.y]:
				var tick := G.block(labels, Vector3(0.45, 0.028, 0.014), ink, Vector3(x, y, 0.02))
				tick.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

func _beacon(point: Vector2, caption: String, color: Color) -> void:
	var badge := Label3D.new()
	badge.text = caption
	badge.font_size = 52
	badge.pixel_size = 0.0068
	badge.uppercase = true
	badge.modulate = color
	badge.outline_modulate = Color(0.87, 0.93, 0.98, 0.65)
	badge.outline_size = 4
	badge.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	badge.no_depth_test = true
	labels.add_child(badge)
	badge.position = C.in_plane(point, 0.0)
	var pin := G.sphere(labels, 0.044, G.material(color, 0.06), C.in_plane(point + Vector2(0, -0.32)))
	pin.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var tick := G.block(labels, Vector3(0.018, 0.25, 0.012), G.material(color), C.in_plane(point + Vector2(0, -0.50)))
	tick.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
