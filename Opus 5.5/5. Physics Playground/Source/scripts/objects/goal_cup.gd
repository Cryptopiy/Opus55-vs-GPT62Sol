extends Node3D
## P-13 Goal hole: a cup (static, absorbent lining so balls do not bounce back out) with an Area3D sensor
## covering its interior. The level counts as complete when the ball centre stays inside for 1.0 s.
## The cup's interior centre is the level's "hole" coordinate; lips/backboard heights come from the level.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")

var hole := Vector2.ZERO
var sensor: Area3D
var _glow_mats: Array[StandardMaterial3D] = []
var _progress := 0.0
var _pulse := 0.0


static func parts(level: Dictionary) -> Array:
	## [centre, size] of floor, left wall, right wall (incl. backboard) in world space.
	var h: Vector2 = level["hole"]
	var lips: Vector2 = level["lips"]
	var back: float = level["back"]
	var inner := C.CUP_INNER
	# +2 cm keeps the cup floor from z-fighting with ground slabs it sits on (a ball rolls over it freely)
	var floor_top := h.y - inner.y * 0.5 + 0.02
	var w := C.CUP_WALL
	var right_h := maxf(lips.y, back)
	return [
		[Vector2(h.x, floor_top - C.CUP_FLOOR * 0.5), Vector2(inner.x + 2.0 * w, C.CUP_FLOOR)],
		[Vector2(h.x - inner.x * 0.5 - w * 0.5, floor_top + lips.x * 0.5 - C.CUP_FLOOR * 0.5), Vector2(w, lips.x + C.CUP_FLOOR)],
		[Vector2(h.x + inner.x * 0.5 + w * 0.5, floor_top + right_h * 0.5 - C.CUP_FLOOR * 0.5), Vector2(w, right_h + C.CUP_FLOOR)],
	]


## Cup walls and its interior are obstacles for placement.
static func footprint(level: Dictionary) -> Array:
	var out := []
	for p in parts(level):
		out.append(Fp.rect(p[0], p[1]))
	out.append(Fp.rect(level["hole"], C.CUP_INNER))
	return out


func build(level: Dictionary) -> void:
	hole = level["hole"]
	var body := Common.static_body(Transform3D.IDENTITY, 0.8, 1.0, true)
	body.name = "CupBody"
	add_child(body)
	var cup_mat := Mats.rounded(C.COL_CUP, 0.35)
	var i := 0
	for p in parts(level):
		var c: Vector2 = p[0]
		var s: Vector2 = p[1]
		var sz := Vector3(s.x, s.y, C.OBJECT_DEPTH * 1.1)
		ML.add_box_shape(body, sz, Vector3(c.x, c.y, 0))
		ML.add_box(body, sz, Vector3(c.x, c.y, 0), cup_mat)
		# glowing rim strip on top of each wall (bloom), and along the floor
		var glow := Mats.emissive_unique(C.COL_CUP_GLOW, C.CUP_RIM_GLOW)
		_glow_mats.append(glow)
		var strip_pos := Vector3(c.x, c.y + s.y * 0.5 + 0.02, 0) if i > 0 else Vector3(c.x, c.y + s.y * 0.5 + 0.01, C.OBJECT_DEPTH * 0.45)
		var strip_size := Vector3(s.x * 1.05, 0.05, C.OBJECT_DEPTH * 1.12) if i > 0 else Vector3(C.CUP_INNER.x, 0.03, 0.08)
		ML.add_box(body, strip_size, strip_pos, glow)
		i += 1
	# soft inner glow plate behind the cup so the target reads from far away
	var halo := ML.add_mesh(self, ML.quad(C.CUP_INNER * 1.25), Vector3(hole.x, hole.y, -0.6), Mats.emissive(C.COL_CUP_GLOW.darkened(0.2), 0.9))
	halo.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	sensor = Common.sensor(Vector3(C.CUP_INNER.x, C.CUP_INNER.y, C.OBJECT_DEPTH), Vector3(hole.x, hole.y, 0))
	sensor.name = "GoalSensor"
	add_child(sensor)


## True when the ball overlaps the sensor AND its centre lies inside the sensor box.
func contains_ball(ball: RigidBody3D) -> bool:
	if not sensor.overlaps_body(ball):
		return false
	var p := ball.global_position
	return absf(p.x - hole.x) <= C.CUP_INNER.x * 0.5 and absf(p.y - hole.y) <= C.CUP_INNER.y * 0.5


## 0..1 hold progress drives the rim brightness (readable "almost there" feedback).
func set_progress(f: float) -> void:
	_progress = clampf(f, 0.0, 1.0)


func _process(delta: float) -> void:
	_pulse = fposmod(_pulse + delta * 2.0, TAU)
	var e := C.CUP_RIM_GLOW * (1.0 + 0.15 * sin(_pulse) + 2.0 * _progress)
	for m in _glow_mats:
		m.emission_energy_multiplier = e
