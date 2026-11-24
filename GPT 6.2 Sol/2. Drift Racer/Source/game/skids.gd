class_name SkidPool
extends MultiMeshInstance3D

const C = preload("res://game/config.gd")
const INK = preload("res://shaders/skid.gdshader")

var last := PackedVector3Array()
var started := PackedByteArray()
var birth := PackedFloat32Array()
var cursor := 0
var clock := 0.0
var material: ShaderMaterial

func _ready() -> void:
	material = ShaderMaterial.new()
	material.shader = INK
	material.set_shader_parameter("mark_life", C.SKID_LIFE)
	var strip := PlaneMesh.new()
	strip.size = Vector2.ONE
	strip.material = material
	multimesh = MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.use_custom_data = true
	multimesh.mesh = strip
	multimesh.instance_count = C.SKID_LIMIT
	multimesh.visible_instance_count = 0
	multimesh.custom_aabb = AABB(Vector3(-500.0, -10.0, -500.0), Vector3(1000.0, 30.0, 1000.0))
	cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	last.resize(C.CAR_COUNT * 4)
	started.resize(C.CAR_COUNT * 4)
	birth.resize(C.SKID_LIMIT)
	birth.fill(-INF)

func drive(cars: Array[DriftCar], delta: float) -> void:
	clock += delta
	material.set_shader_parameter("race_clock", clock)
	for car in cars:
		for i in 4:
			var key := car.driver_id * 4 + i
			if not car.wheel_contact[i] or car.wheel_slip[i] < C.SKID_THRESHOLD or car.speed < 1.6:
				started[key] = 0
				continue
			var up := car.wheel_normals[i]
			var at := car.wheel_points[i] + up * 0.016
			var from := last[key]
			var travel := at - from
			var distance := travel.length()
			if not started[key] or distance > 4.0:
				started[key] = 1
				last[key] = at
			elif distance >= C.SKID_STEP:
				var lateral := up.cross(travel).normalized()
				multimesh.set_instance_transform(cursor,
					Transform3D(Basis(lateral * 0.245, up, travel * 1.08), from.lerp(at, 0.5)))
				multimesh.set_instance_custom_data(cursor, Color(clock, 0.45 + car.wheel_slip[i] * 0.43, 0.0, 1.0))
				birth[cursor] = clock
				multimesh.visible_instance_count = maxi(multimesh.visible_instance_count, cursor + 1)
				cursor = (cursor + 1) % C.SKID_LIMIT
				last[key] = at

func active_count() -> int:
	var active := 0
	for time in birth:
		if clock - time < C.SKID_LIFE:
			active += 1
	return active
