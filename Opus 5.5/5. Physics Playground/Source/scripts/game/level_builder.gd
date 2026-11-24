extends RefCounted
## Builds a level's static scenery (fixed colliders), the goal cup, forbidden zones and decor from the level
## definition in config.gd, and provides the 2D obstacle footprints used by the placement editor.

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")
const GoalCup := preload("res://scripts/objects/goal_cup.gd")
const Ball := preload("res://scripts/objects/ball.gd")
const Machinery := preload("res://scripts/objects/machinery.gd")


static func _material_for(kind: String) -> Material:
	match kind:
		"floor":
			return Mats.rounded(C.COL_SCENERY_DARK, 0.85)
		"wall":
			return Mats.rounded(C.COL_SCENERY, 0.8)
		"post":
			return Mats.rounded(C.COL_SCENERY_DARK.darkened(0.15), 0.7)
	return Mats.rounded(C.COL_SCENERY.lightened(0.12), 0.75)


static func _depth_for(kind: String) -> float:
	match kind:
		"post":
			return 0.6
		"platform", "ramp":
			return C.OBJECT_DEPTH * 1.2
	return C.SCENERY_DEPTH


## Static scenery = one StaticBody3D with a box shape per element (created once per level session).
## Returns the goal cup node.
static func build_static(level: Dictionary, parent: Node3D) -> GoalCup:
	var body := Common.static_body(Transform3D.IDENTITY, C.SCENERY_FRICTION)
	body.name = "Scenery"
	parent.add_child(body)
	for st: Dictionary in level["statics"]:
		var c: Vector2 = st["c"]
		var s: Vector2 = st["s"]
		var kind: String = st.get("m", "wall")
		var rot: float = st.get("r", 0.0)
		var sz := Vector3(s.x, s.y, _depth_for(kind))
		ML.add_box_shape(body, Vector3(s.x, s.y, C.OBJECT_DEPTH), Vector3(c.x, c.y, 0), rot)
		ML.add_box(body, sz, Vector3(c.x, c.y, 0), _material_for(kind), rot)
	var cup := GoalCup.new()
	cup.name = "Goal"
	parent.add_child(cup)
	cup.build(level)
	return cup


## Forbidden zones (red hatched) and the back wall with its grid.
static func build_decor(level: Dictionary, parent: Node3D) -> void:
	for r: Rect2 in level["forbidden"]:
		var q := ML.add_mesh(parent, ML.quad(r.size), Vector3(r.get_center().x, r.get_center().y, -0.45), Mats.hatch())
		q.set_instance_shader_parameter("zone_size", r.size)
		q.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var b: Rect2 = level["bounds"]
	var wall_size := Vector2(b.size.x + 80.0, b.size.y + 60.0)
	var wall := ML.add_mesh(parent, ML.quad(wall_size), Vector3(b.get_center().x, b.get_center().y, -1.3), Mats.backdrop(b.position.y - 4.0, b.end.y + 6.0))
	wall.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	wall.name = "Backdrop"


## Ball start marker + L5 machinery in their build-mode pose (replaced by bodies while simulating).
static func build_preview(level: Dictionary, parent: Node3D) -> void:
	var ball := Ball.build_visual(level["ball"], level["ball_pos"])
	ball.name = "BallPreview"
	parent.add_child(ball)
	Machinery.build_visuals(level, parent)


## Everything a placed object may not overlap (besides forbidden zones and other placed objects).
static func obstacles(level: Dictionary) -> Array:
	var out := []
	for st: Dictionary in level["statics"]:
		out.append(Fp.rect(st["c"], st["s"], deg_to_rad(float(st.get("r", 0.0)))))
	out.append_array(GoalCup.footprint(level))
	out.append_array(Machinery.footprints(level))
	# the ball's start position (slightly enlarged so nothing is placed touching it)
	var ball_r: float = C.BALLS[level["ball"]]["radius"]
	out.append(Fp.circle(level["ball_pos"], ball_r + 0.05))
	return out


static func forbidden(level: Dictionary) -> Array:
	var out := []
	for r: Rect2 in level["forbidden"]:
		out.append(Fp.rect(r.get_center(), r.size))
	return out
