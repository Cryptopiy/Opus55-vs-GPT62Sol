extends RefCounted
## P-12 Fan: static housing blowing along its local +X. Force field = 4 x 2 m rectangle in front of the housing;
## a body inside gets 15 N per kg (15 N on a 1 kg object) falling off linearly to 0 at the far end.
## Visuals: spinning blades and particle streaks travelling through the field.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")


static func defaults() -> Dictionary:
	return {}


static func constrain(rec: Dictionary) -> void:
	rec["rot"] = fposmod(float(rec["rot"]) + 180.0, 360.0) - 180.0


static func footprint(rec: Dictionary) -> Array:
	return [Fp.rect(Common.origin(rec), C.FAN_HOUSING, Common.rot_rad(rec))]


## Returns the blade pivot (spun in simulation).
static func _add_meshes(parent: Node3D) -> Node3D:
	var hx := C.FAN_HOUSING.x * 0.5
	ML.add_box(parent, Vector3(C.FAN_HOUSING.x, C.FAN_HOUSING.y, C.OBJECT_DEPTH), Vector3.ZERO, Mats.rounded(C.COL_FAN, 0.5))
	var ring := ML.add_mesh(parent, ML.cylinder(0.56, 0.08, 32), Vector3(hx + 0.02, 0, 0), Mats.standard(C.COL_FAN.darkened(0.5), 0.5, 0.3))
	ring.rotation.z = PI * 0.5
	var pivot := Node3D.new()
	pivot.name = "Blades"
	pivot.position = Vector3(hx + 0.08, 0, 0)
	parent.add_child(pivot)
	var blade_mat := Mats.rounded(Color(0.92, 0.95, 0.93), 0.4)
	for i in 4:
		var arm := Node3D.new()
		arm.rotation.x = TAU * float(i) / 4.0
		pivot.add_child(arm)
		var b := ML.add_box(arm, Vector3(0.04, 0.42, 0.16), Vector3(0, 0.25, 0), blade_mat)
		b.rotation.y = deg_to_rad(25.0)
	ML.add_mesh(pivot, ML.sphere(0.09), Vector3.ZERO, Mats.standard(C.COL_FAN.darkened(0.3), 0.4, 0.4))
	return pivot


## Faint rectangle showing the 4 x 2 m force field in build mode.
static func _add_field_preview(parent: Node3D) -> void:
	var q := ML.add_mesh(parent, ML.quad(C.FAN_AREA), Vector3(C.FAN_HOUSING.x * 0.5 + C.FAN_AREA.x * 0.5, 0, -0.52), Mats.ghost(true))
	q.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	q.transparency = 0.6


static func build_visual(rec: Dictionary) -> Node3D:
	var root := Common.visual_root(rec)
	_add_meshes(root)
	_add_field_preview(root)
	return root


static func _make_streaks() -> GPUParticles3D:
	var p := GPUParticles3D.new()
	p.amount = C.FAN_PARTICLES
	p.lifetime = C.FAN_AREA.x / C.FAN_STREAK_SPEED
	p.local_coords = true
	p.fixed_fps = 60
	var pm := ParticleProcessMaterial.new()
	pm.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	pm.emission_box_extents = Vector3(0.05, C.FAN_AREA.y * 0.45, C.OBJECT_DEPTH * 0.4)
	pm.direction = Vector3(1, 0, 0)
	pm.spread = 3.0
	pm.initial_velocity_min = C.FAN_STREAK_SPEED * 0.85
	pm.initial_velocity_max = C.FAN_STREAK_SPEED * 1.15
	pm.gravity = Vector3.ZERO
	var ramp := Gradient.new()
	ramp.set_color(0, Color(1, 1, 1, 0.0))
	ramp.set_color(1, Color(1, 1, 1, 0.0))
	ramp.add_point(0.2, Color(1, 1, 1, 0.9))
	ramp.add_point(0.7, Color(1, 1, 1, 0.5))
	var tex := GradientTexture1D.new()
	tex.gradient = ramp
	pm.color_ramp = tex
	p.process_material = pm
	var quad := QuadMesh.new()
	quad.size = Vector2(0.7, 0.05)
	quad.material = Mats.streak()
	p.draw_pass_1 = quad
	p.visibility_aabb = AABB(Vector3(-0.5, -1.5, -1), Vector3(C.FAN_AREA.x + 1.0, 3.0, 2))
	p.position = Vector3(C.FAN_HOUSING.x * 0.5 + 0.1, 0, 0)
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return p


static func build_sim(rec: Dictionary, sim: Node) -> void:
	var body := Common.static_body(Common.xform(rec), 0.5)
	ML.add_box_shape(body, Vector3(C.FAN_HOUSING.x, C.FAN_HOUSING.y, C.OBJECT_DEPTH))
	var fan := FanSim.new()
	fan.sim = sim
	fan.blades = _add_meshes(body)
	fan.streaks = _make_streaks()
	body.add_child(fan.streaks)
	body.add_child(fan)
	sim.add_node(body)
	sim.add_ticker(fan)
	sim.fan_count += 1


class FanSim extends Node3D:
	var sim: Node
	var blades: Node3D
	var streaks: GPUParticles3D
	var _spin := 0.0

	## Force field: pure math on body centres (no Area3D signals) so the evaluation order is fixed (P-02).
	func sim_tick(s: Node) -> void:
		var xf := global_transform
		var inv := xf.affine_inverse()
		var face := C.FAN_HOUSING.x * 0.5
		var dir := xf.basis.x
		for b: RigidBody3D in s.dynamic_bodies:
			var lp := inv * b.global_position
			var d := lp.x - face
			if d < 0.0 or d > C.FAN_AREA.x or absf(lp.y) > C.FAN_AREA.y * 0.5:
				continue
			b.apply_central_force(dir * C.FAN_ACCEL * b.mass * (1.0 - d / C.FAN_AREA.x))
		_spin = fposmod(_spin + C.FAN_BLADE_SPEED * C.PHYS_DT, TAU)

	func _process(_delta: float) -> void:
		blades.rotation.x = _spin
		# streaks move with simulation time: frozen while paused, faster at x2
		streaks.speed_scale = 1.0 if sim.running else 0.0
