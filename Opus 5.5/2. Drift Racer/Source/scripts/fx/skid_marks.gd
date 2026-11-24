class_name SkidMarks
extends MultiMeshInstance3D
## Pooled tyre-mark strips (R-15): a fixed ring buffer of 600 quad segments in
## one MultiMesh (one draw call, no allocations at runtime). Each segment
## stores its birth time; the shader fades it out over 20 s. When the pool is
## full the oldest segment is reused.

const SKID_SHADER := preload("res://shaders/skid.gdshader")
const UNUSED_BIRTH := -1000.0

var _head := 0
var _time := 0.0
var _births := PackedFloat32Array()
var _material: ShaderMaterial


func setup(track: TrackData) -> void:
	name = "SkidMarks"
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var quad := MeshArrays.new()
	# Unit quad in the XZ plane: X = across (0..1 in UV.x), Z = along.
	var a := quad.add_vertex(Vector3(-0.5, 0.0, -0.5), Vector3.UP, Vector2(0.0, 0.0))
	var b := quad.add_vertex(Vector3(0.5, 0.0, -0.5), Vector3.UP, Vector2(1.0, 0.0))
	var c := quad.add_vertex(Vector3(0.5, 0.0, 0.5), Vector3.UP, Vector2(1.0, 1.0))
	var d := quad.add_vertex(Vector3(-0.5, 0.0, 0.5), Vector3.UP, Vector2(0.0, 1.0))
	quad.add_quad_facing(a, b, c, d, Vector3.UP)
	var mesh := ArrayMesh.new()
	_material = MeshArrays.shader_material(SKID_SHADER, {"fade_time": Config.SKID_FADE_TIME, "now": 0.0})
	quad.commit(mesh, _material)
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_custom_data = true
	mm.mesh = mesh
	mm.instance_count = Config.SKID_MAX_SEGMENTS
	_births.resize(Config.SKID_MAX_SEGMENTS)
	var hidden := Transform3D(Basis().scaled(Vector3.ZERO), Vector3.ZERO)
	for i in Config.SKID_MAX_SEGMENTS:
		mm.set_instance_transform(i, hidden)
		mm.set_instance_custom_data(i, Color(UNUSED_BIRTH, 0.0, 0.0, 0.0))
		_births[i] = UNUSED_BIRTH
	# Fixed bounds (whole circuit) so moving segments never trigger AABB work.
	mm.custom_aabb = track.bounds.grow(20.0)
	multimesh = mm


## Adds one strip segment from a to b lying on a surface with normal n.
func add_segment(a: Vector3, b: Vector3, n: Vector3, strength: float) -> void:
	var along := b - a
	var length := along.length()
	if length < 0.01:
		return
	var side := along.cross(n).normalized() * Config.SKID_WIDTH
	# Slight overlap (5 %) hides seams between consecutive segments.
	var seg_basis := Basis(side, n, along * 1.05)
	var origin := (a + b) * 0.5 + n * 0.025
	multimesh.set_instance_transform(_head, Transform3D(seg_basis, origin))
	multimesh.set_instance_custom_data(_head, Color(_time, strength, 0.0, 0.0))
	_births[_head] = _time
	_head = (_head + 1) % Config.SKID_MAX_SEGMENTS


func _physics_process(delta: float) -> void:
	_time += delta
	_material.set_shader_parameter(&"now", _time)


## Segments still visible (younger than the fade time) - for the F1 overlay.
func active_count() -> int:
	var n := 0
	var oldest := _time - Config.SKID_FADE_TIME
	for t in _births:
		if t > oldest:
			n += 1
	return n
