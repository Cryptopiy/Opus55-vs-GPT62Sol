extends RefCounted
## Placeable object types: display names, tooltips and the module script implementing each type.
## Every module provides: defaults(), constrain(rec), footprint(rec), build_visual(rec), build_sim(rec, sim).

const ORDER: Array[String] = ["plank", "box_s", "box_h", "lever", "rope", "spring", "conveyor", "cloth", "fan"]

const PLANK := preload("res://scripts/objects/obj_plank.gd")
const BOX := preload("res://scripts/objects/obj_box.gd")
const LEVER := preload("res://scripts/objects/obj_lever.gd")
const ROPE := preload("res://scripts/objects/obj_rope.gd")
const SPRING := preload("res://scripts/objects/obj_spring.gd")
const CONVEYOR := preload("res://scripts/objects/obj_conveyor.gd")
const CLOTH := preload("res://scripts/objects/obj_cloth.gd")
const FAN := preload("res://scripts/objects/obj_fan.gd")

const NAMES := {
	"plank": "Plank", "box_s": "Box S", "box_h": "Box H", "lever": "Lever", "rope": "Rope pendulum",
	"spring": "Spring pad", "conveyor": "Conveyor", "cloth": "Cloth hammock", "fan": "Fan",
}

const TIPS := {
	"plank": "Plank - static 3 x 0.2 m ramp, bridge or wall. Friction 0.5.",
	"box_s": "Box S - 1 m crate, 5 kg. Falls when the simulation starts.",
	"box_h": "Box H - 1 m steel box, 20 kg. A heavy counterweight; presses switches.",
	"lever": "Lever - 5 m seesaw on a hinge (+-35 deg). Alt+drag the pivot handle to move the fulcrum.",
	"rope": "Rope pendulum - click the anchor, drag to set length (1-6 m) and start angle. The cradle can carry a ball.",
	"spring": "Spring pad - launches touching bodies at 12 m/s along its up direction.",
	"conveyor": "Conveyor - 3 m belt moving things at 2 m/s along its arrow direction.",
	"cloth": "Cloth hammock - soft 2 m cloth pinned at 4 corners. Catches and slings balls.",
	"fan": "Fan - blows along its face: 4 x 2 m field, 15 N per kg fading with distance.",
}


static func module(type: String) -> GDScript:
	match type:
		"plank":
			return PLANK
		"box_s", "box_h":
			return BOX
		"lever":
			return LEVER
		"rope":
			return ROPE
		"spring":
			return SPRING
		"conveyor":
			return CONVEYOR
		"cloth":
			return CLOTH
		"fan":
			return FAN
	push_error("Unknown object type: %s" % type)
	return PLANK


static func type_index(type: String) -> int:
	return ORDER.find(type)


static func new_record(type: String, pos: Vector2, rot_deg: float) -> Dictionary:
	var rec := {"type": type, "x": pos.x, "y": pos.y, "rot": rot_deg}
	rec.merge(module(type).defaults())
	module(type).constrain(rec)
	return rec


static func footprint(rec: Dictionary) -> Array:
	return module(String(rec["type"])).footprint(rec)


static func build_visual(rec: Dictionary) -> Node3D:
	return module(String(rec["type"])).build_visual(rec)


static func has_handle(type: String) -> bool:
	return type == "lever" or type == "rope"
