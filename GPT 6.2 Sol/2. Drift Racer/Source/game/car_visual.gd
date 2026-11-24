class_name TouringBody
extends Node3D

const C = preload("res://game/config.gd")
const G = preload("res://game/geometry.gd")

var car: DriftCar
var wheels: Array[Node3D] = []
var hubs: Array[Node3D] = []
var brake_lights: Array[OmniLight3D] = []
var brake_material: StandardMaterial3D
var reverse_material: StandardMaterial3D
var reverse_light: OmniLight3D
var _lamp_state := Vector2i(-1, -1)

func build(owner_car: DriftCar) -> void:
	car = owner_car
	var shell := G.material(car.paint, 0.34, 0.22)
	shell.clearcoat_enabled = true
	shell.clearcoat = 0.45
	shell.clearcoat_roughness = 0.35
	var dark := G.material(Color("#182527"), 0.54, 0.35)
	var glass := G.material(Color("#203546"), 0.14, 0.53)
	var tire := G.material(Color("#171c20"), 0.93)
	var alloy := G.material(Color("#afb4b0"), 0.28, 0.72)
	var light := _glowing(Color("#eaf9ef"), 2.00)
	brake_material = _glowing(Color("#fa252b"), 0.35)
	reverse_material = _glowing(Color("#d9e9e2"), 0.0)
	_body(shell, dark, glass)
	for side: float in [-1.0, 1.0]:
		G.box(self, Vector3(0.48, 0.13, 0.06), Vector3(side * 0.58, 0.14, -2.00), light)
		G.box(self, Vector3(0.50, 0.13, 0.07), Vector3(side * 0.59, 0.161, 2.045), brake_material)
		G.box(self, Vector3(0.11, 0.04, 0.071), Vector3(side * 0.30, 0.15, 2.05), reverse_material)
		G.box(self, Vector3(0.12, 0.053, 0.84), Vector3(side * 0.86, -0.07, 0.0), alloy)
		var mirror := G.box(self, Vector3(0.24, 0.095, 0.16), Vector3(side * 0.79, 0.48, -0.63), shell)
		mirror.rotation.y = side * 0.14
		var beam := SpotLight3D.new()
		beam.light_color = Color("#e6f9ff")
		beam.light_energy = 1.85
		beam.spot_range = 24.0
		beam.spot_angle = 28.0
		beam.spot_angle_attenuation = 1.6
		beam.position = Vector3(side * 0.59, 0.13, -2.09)
		beam.rotation_degrees.x = -5.0
		add_child(beam)
		var lamp := _omni(Vector3(side * 0.59, 0.13, 2.15), Color("#ff3441"))
		brake_lights.append(lamp)
	reverse_light = _omni(Vector3(0.0, 0.14, 2.17), Color("#e7f9f3"))
	for i in 4:
		var spindle := Node3D.new()
		add_child(spindle)
		wheels.append(spindle)
		var hub := Node3D.new()
		spindle.add_child(hub)
		hubs.append(hub)
		var side := -1.0 if i % 2 == 0 else 1.0
		var rubber := G.cylinder(hub, C.WHEEL_RADIUS, 0.29, 18, tire, Vector3.ZERO)
		rubber.rotation.z = PI * 0.5
		var rim := G.cylinder(hub, 0.23, 0.30, 15, alloy, Vector3.ZERO)
		rim.rotation.z = PI * 0.5
		var inset := G.cylinder(hub, 0.186, 0.305, 12, dark, Vector3.ZERO)
		inset.rotation.z = PI * 0.5
		for j in 5:
			var spoke := G.box(hub, Vector3(0.014, 0.35, 0.043), Vector3(side * 0.161, 0.0, 0.0), alloy)
			spoke.rotation.x = TAU * j / 10.0
		var cap := G.cylinder(hub, 0.073, 0.030, 10, alloy, Vector3(side * 0.169, 0.0, 0.0))
		cap.rotation.z = PI * 0.5
	update_body()

func update_body() -> void:
	for i in wheels.size():
		wheels[i].position = car.mounts[i] - Vector3.UP * car.wheel_length[i]
		wheels[i].rotation.y = -car.steering if i < 2 else 0.0
		hubs[i].rotation.x = car.wheel_spin[i]
	var lamps := Vector2i(int((car.brake > 0.12 and not car.reversing) or car.handbrake), int(car.reversing))
	if lamps == _lamp_state:
		return
	_lamp_state = lamps
	brake_material.emission_energy_multiplier = 3.4 if lamps.x else 0.35
	reverse_material.emission_energy_multiplier = 2.8 if lamps.y else 0.0
	for lamp in brake_lights:
		lamp.visible = lamps.x != 0
	reverse_light.visible = lamps.y != 0

func _glowing(color: Color, energy: float) -> StandardMaterial3D:
	var lamp := G.material(color * 0.65, 0.31)
	lamp.emission_enabled = true
	lamp.emission = color
	lamp.emission_energy_multiplier = energy
	return lamp

func _omni(at: Vector3, color: Color) -> OmniLight3D:
	var lamp := OmniLight3D.new()
	lamp.position = at
	lamp.light_color = color
	lamp.light_energy = 1.22
	lamp.omni_range = 3.5
	add_child(lamp)
	return lamp

func _body(shell: Material, dark: Material, glass: Material) -> void:
	var profile := [
		Vector4(-2.05, 0.77, -0.14, 0.23), Vector4(-1.54, 0.95, -0.16, 0.30),
		Vector4(-0.64, 0.91, -0.16, 0.31), Vector4(0.90, 0.92, -0.16, 0.32),
		Vector4(1.56, 0.99, -0.17, 0.32), Vector4(2.04, 0.85, -0.11, 0.27)
	]
	G.mesh(self, _loft(profile, 0.14), shell, Vector3.ZERO)
	var canopy := [
		Vector4(-0.96, 0.69, 0.292, 0.326), Vector4(-0.43, 0.71, 0.305, 0.708),
		Vector4(0.57, 0.69, 0.308, 0.718), Vector4(1.24, 0.67, 0.306, 0.339)
	]
	G.mesh(self, _loft(canopy, 0.19), glass, Vector3.ZERO)
	G.box(self, Vector3(1.02, 0.055, 1.02), Vector3(0.0, 0.718, 0.08), shell)
	G.box(self, Vector3(0.31, 0.015, 0.93), Vector3(0.0, 0.751, 0.08), dark)
	G.box(self, Vector3(1.57, 0.11, 0.24), Vector3(0.0, -0.129, -1.98), dark)
	G.box(self, Vector3(1.54, 0.11, 0.17), Vector3(0.0, -0.134, 2.02), dark)
	G.box(self, Vector3(0.72, 0.13, 0.073), Vector3(0.0, 0.008, -2.052), dark)
	G.box(self, Vector3(0.71, 0.15, 0.072), Vector3(0.0, 0.045, 2.045), dark)
	for i in 6:
		G.box(self, Vector3(0.046, 0.082, 0.036), Vector3((i - 2.5) * 0.102, 0.008, -2.100), shell)
	for side: float in [-1.0, 1.0]:
		G.box(self, Vector3(0.11, 0.28, 0.20), Vector3(side * 0.57, 0.45, 1.69), dark)
		G.box(self, Vector3(0.10, 0.14, 0.46), Vector3(side * 0.94, 0.592, 1.71), shell)
	G.box(self, Vector3(1.91, 0.07, 0.43), Vector3(0.0, 0.59, 1.72), dark)

func _loft(sections: Array, shoulder: float) -> Mesh:
	var rings: Array[PackedVector3Array] = []
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	for s: Vector4 in sections:
		var width := s.y
		var low := s.z
		var high := s.w
		var z := s.x
		rings.append(PackedVector3Array([
			Vector3(-width + 0.11, low, z), Vector3(-width, lerpf(low, high, 0.40), z),
			Vector3(-width + shoulder, high, z), Vector3(width - shoulder, high, z),
			Vector3(width, lerpf(low, high, 0.40), z), Vector3(width - 0.11, low, z)
		]))
	for i in range(1, rings.size()):
		for j in 6:
			var k := (j + 1) % 6
			_face(surface, rings[i-1][j], rings[i][j], rings[i][k])
			_face(surface, rings[i-1][j], rings[i][k], rings[i-1][k])
	for j in range(1, 5):
		_face(surface, rings[0][0], rings[0][j], rings[0][j + 1])
		_face(surface, rings[-1][0], rings[-1][j + 1], rings[-1][j])
	return surface.commit()

func _face(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a).normalized()
	for p: Vector3 in [a, c, b]:
		surface.set_normal(normal)
		surface.add_vertex(p)
