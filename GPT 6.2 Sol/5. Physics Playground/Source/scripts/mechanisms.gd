class_name LabMechanisms
extends Node3D

const C = preload("res://scripts/config.gd")
const G = preload("res://scripts/geometry.gd")

var board: LabBoard
var springs: Array[Dictionary] = []
var fans: Array[Dictionary] = []
var belts: Array[Dictionary] = []
var ropes: Array[LabRope] = []

func build(spec: Dictionary) -> Node3D:
	var kind: String = spec.type
	var position_at := C.in_plane(spec.position)
	var angle: float = spec.rotation
	var kit: Array = C.KIT[kind]
	var paint := G.material(kit[2], 0.26 if kind in ["box_h", "conveyor", "lever"] else 0.06)
	var size: Vector3 = kit[3]
	match kind:
		"rope":
			var rope := LabRope.new()
			add_child(rope)
			rope.assemble(board, spec)
			ropes.append(rope)
			return rope
		"cloth":
			var hammock := LabHammock.new()
			add_child(hammock)
			hammock.assemble(board, spec)
			return hammock
		"box_s", "box_h":
			var mass := C.BOX_H_MASS if kind == "box_h" else C.BOX_S_MASS
			var crate := G.mobile(self, position_at, angle, mass, C.BOX_FRICTION, 0, board.active)
			G.box_shape(crate, size)
			_piece_surface(crate, kind, size, paint)
			board.bodies.append(crate)
			if kind == "box_h" and board.active:
				crate.contact_monitor = true
				crate.max_contacts_reported = 3
				crate.body_entered.connect(_heavy_contact.bind(crate))
			return crate
		"lever":
			return _lever(position_at, angle, paint)
		_:
			var friction := C.PLANK_FRICTION if kind == "plank" else 0.78
			var base := G.solid(self, size, position_at, angle, friction, null)
			_piece_surface(base, kind, size, paint)
			match kind:
				"spring":
					var cap := G.block(base, Vector3(size.x * 0.94, 0.078, size.z * 0.96),
						G.material(Color("#fff1aa")), Vector3(0, size.y * 0.37, 0))
					var loads: Array[RigidBody3D] = []
					springs.append({"node": base, "cap": cap, "last": {}, "loads": loads, "visual": 0.0})
				"conveyor":
					base.constant_linear_velocity = base.basis.x * C.CONVEYOR_SPEED
					var tread := ShaderMaterial.new()
					tread.shader = preload("res://shaders/tread.gdshader")
					var belt := PlaneMesh.new()
					belt.size = Vector2(size.x - 0.055, size.z - 0.032)
					tread.set_shader_parameter("metres", belt.size.x)
					G.mesh(base, belt, tread, Vector3(0, size.y * 0.5 + 0.005, 0))
					for offset in [-0.44, 0.44]:
						G.block(base, Vector3(size.x * 0.96, 0.038, 0.049),
							G.material(Color("#6e8da5")), Vector3(0, size.y * 0.5, offset))
					belts.append({"tread": tread})
				"fan":
					var blades := Node3D.new()
					base.add_child(blades)
					blades.position.z = size.z * 0.53
					var blade_paint := G.material(Color("#b4edc8"), 0.14)
					for index in range(3):
						var blade := G.block(blades, Vector3(0.11, 0.24, 0.06), blade_paint)
						var radians := index * TAU / 3.0
						blade.position = Vector3(-sin(radians), cos(radians), 0) * 0.137
						blade.rotation.z = radians + 0.23
					var bars: Array[MeshInstance3D] = []
					var streak_paint := G.material(Color(0.39, 0.98, 0.80, 0.57))
					for index in range(9):
						var bar := G.block(base, Vector3(0.34, 0.025, 0.026), streak_paint)
						bar.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
						bar.hide()
						bars.append(bar)
					fans.append({"node": base, "blades": blades, "streaks": bars})
			return base

func _lever(point: Vector3, angle: float, paint: Material) -> Node3D:
	var pivot := StaticBody3D.new()
	pivot.collision_layer = 0
	pivot.collision_mask = 0
	add_child(pivot)
	pivot.position = point
	var bearing := G.cylinder(pivot, 0.33, 1.13, G.material(Color("#523e61"), 0.28))
	bearing.rotation.x = PI * 0.5
	var beam := G.mobile(self, point, angle, C.LEVER_MASS, 0.69, 0, board.active)
	beam.angular_damp = C.LEVER_DAMP
	G.box_shape(beam, C.LEVER_SIZE)
	_piece_surface(beam, "lever", C.LEVER_SIZE, paint)
	board.bodies.append(beam)
	if board.active:
		var joint := HingeJoint3D.new()
		add_child(joint)
		joint.position = point
		joint.set_flag(HingeJoint3D.FLAG_USE_LIMIT, true)
		# Joint zero is world horizontal, independent of the beam's starting angle.
		joint.set_param(HingeJoint3D.PARAM_LIMIT_LOWER, deg_to_rad(angle - C.LEVER_LIMIT))
		joint.set_param(HingeJoint3D.PARAM_LIMIT_UPPER, deg_to_rad(angle + C.LEVER_LIMIT))
		joint.node_a = joint.get_path_to(pivot)
		joint.node_b = joint.get_path_to(beam)
	return beam

func _piece_surface(parent: Node3D, kind: String, size: Vector3, paint: Material) -> void:
	G.rounded(parent, size, paint, minf(size.y * 0.22, 0.074))
	var highlight := G.material(Color("#f2dcc0") if kind == "plank" else Color("#cdd9df"))
	if kind == "plank":
		for offset in [-0.39, 0.39]:
			G.block(parent, Vector3(size.x * 0.91, 0.012, 0.027), highlight, Vector3(0, size.y * 0.5, offset))
	elif kind in ["box_s", "box_h"]:
		for offset in [-0.37, 0.37]:
			G.block(parent, Vector3(0.10, 0.91, 0.022), highlight, Vector3(offset, 0, size.z * 0.51))
		G.block(parent, Vector3(0.48, 0.10, 0.024), highlight, Vector3(0, 0, size.z * 0.51))
	elif kind == "lever":
		for offset in [-2.12, 2.12]:
			G.block(parent, Vector3(0.17, 0.15, 0.028), highlight, Vector3(offset, 0, size.z * 0.51))

func _heavy_contact(_other: Node, crate: RigidBody3D) -> void:
	if crate.linear_velocity.length() > C.TIP_IMPACT:
		board.feedback.emit("heavy", crate.position, crate.linear_velocity.length())

func step(delta: float) -> void:
	for spring in springs:
		_spring_step(spring, delta)
	for fan in fans:
		_fan_step(fan, delta)
	for belt in belts:
		belt.tread.set_shader_parameter("travel", fposmod(board.elapsed * C.CONVEYOR_SPEED, 10.0))
	for rope in ropes:
		rope.step()


func arm_contacts() -> void:
	if not board.active or springs.is_empty():
		return
	for body in board.bodies:
		if body == board.ball or body.collision_layer != C.BODY_LAYER:
			continue
		body.contact_monitor = true
		body.max_contacts_reported = maxi(body.max_contacts_reported, 4)
		body.body_entered.connect(_spring_entered.bind(body))
		body.body_exited.connect(_spring_exited.bind(body))

func _spring_entered(other: Node, load_body: RigidBody3D) -> void:
	for spring in springs:
		if spring.node == other:
			spring.loads.append(load_body)

func _spring_exited(other: Node, load_body: RigidBody3D) -> void:
	for spring in springs:
		if spring.node == other:
			spring.loads.erase(load_body)

func _spring_step(spring: Dictionary, delta: float) -> void:
	var pad: StaticBody3D = spring.node
	var half := C.SPRING_SIZE * 0.5
	var ball := board.ball
	var local := pad.to_local(ball.global_position)
	var radius: float = board.ballspec.radius
	var normal_speed := ball.linear_velocity.dot(pad.basis.y)
	var clearance := local.y - half.y - radius
	# Sweep the sphere's contact over the next step so a fast ball does not miss the pad.
	if absf(local.x) <= half.x + radius * 0.48 and clearance <= maxf(0.045, -normal_speed * delta) and clearance >= -0.14:
		_launch(spring, ball)
	# Solid loads can touch with a corner or an off-center end, so use their actual contacts.
	for load_body: RigidBody3D in spring.loads:
		if not load_body.freeze and pad.to_local(load_body.global_position).y >= 0.0:
			_launch(spring, load_body)
	spring.visual = maxf(0.0, float(spring.visual) - delta)
	spring.cap.position.y = C.SPRING_SIZE.y * 0.37 - 0.1 * float(spring.visual) / C.SPRING_COOLDOWN

func _launch(spring: Dictionary, body: RigidBody3D) -> void:
	var cooldowns: Dictionary = spring.last
	var key := body.get_instance_id()
	if board.elapsed < float(cooldowns.get(key, 0.0)):
		return
	var pad: StaticBody3D = spring.node
	body.apply_central_impulse(pad.basis.y * C.SPRING_SPEED * body.mass)
	cooldowns[key] = board.elapsed + C.SPRING_COOLDOWN
	spring.visual = C.SPRING_COOLDOWN
	board.feedback.emit("spring", pad.position, 1.0)

func _fan_step(fan: Dictionary, delta: float) -> void:
	var emitter: StaticBody3D = fan.node
	fan.blades.rotation.z -= delta * 17.0
	var bars: Array[MeshInstance3D] = fan.streaks
	for index in range(bars.size()):
		var fraction := fposmod(board.elapsed * 1.17 + index / 9.0, 1.0)
		var streak := bars[index]
		streak.show()
		streak.position = Vector3(C.FAN_SIZE.x * 0.5 + C.FAN_REACH * fraction,
			(index % 5 - 2) * 0.27 + 0.1 * sin(fraction * TAU), 0.52 + (index % 3) * 0.10)
		streak.scale.x = maxf(0.08, sin(fraction * PI))
	for body in board.bodies:
		if body.freeze:
			continue
		var local := emitter.to_local(body.global_position)
		var axial := local.x - C.FAN_SIZE.x * 0.5
		if axial >= 0.0 and axial < C.FAN_REACH and absf(local.y) <= C.FAN_WIDTH * 0.5:
			body.apply_central_force(emitter.basis.x * C.FAN_FORCE * (1.0 - axial / C.FAN_REACH))
