class_name LabConfig
extends RefCounted

const TICK_HZ := 120
const GRAVITY := 9.81
const KILL_Y := -15.0
const TIME_LIMIT := 25.0
const POSITION_SNAP := 0.25
const ROTATION_SNAP := 15.0
const FINE_ROTATION := 5.0
const HISTORY_LIMIT := 100
const HINT_DELAY := 60.0
const GOAL_HOLD := 1.0
const REPEAT_RUNS := 5
const REPEAT_TOLERANCE := 0.01
const STACK_DURATION := 10.0
const TIME_SCALES := [0.25, 1.0, 2.0]
const BUILD_LIMIT := Rect2(-14.0, -4.0, 28.0, 27.0)
const PLACE_MARGIN := 0.025
const SPAWN_SEATING := 0.045
const BODY_LAYER := 2
const SOLID_LAYER := 1
const ROPE_LAYER := 4
const TIP_IMPACT := 2.4
const SCREENSHOT_PATH := "res://Screenshots"

const CAMERA_YAW := 15.0
const CAMERA_PITCH := 7.0
const CAMERA_LIMIT := 45.0
const CAMERA_PADDING := 1.1
const FRAME_SAFE_WIDTH := 0.95
const FRAME_SAFE_HEIGHT := 0.76
const CAMERA_FOV := 35.0

const PLANK_SIZE := Vector3(3.0, 0.2, 1.0)
const PLANK_FRICTION := 0.5
const BOX_SIZE := Vector3.ONE
const BOX_S_MASS := 5.0
const BOX_H_MASS := 20.0
const BOX_FRICTION := 0.6
const LEVER_SIZE := Vector3(5.0, 0.2, 1.0)
const LEVER_LIMIT := 35.0
const LEVER_MASS := 10.0
const LEVER_DAMP := 0.5
const ROPE_MIN := 1.0
const ROPE_MAX := 6.0
const ROPE_SEGMENT := 0.25
const ROPE_MASS := 0.2
const ROPE_BOB_MASS := 4.0
const ROPE_RADIUS := 0.06
const ROPE_CABLE_DEPTH := -0.45
const ROPE_BOB := Vector3(0.74, 0.24, 0.8)
const ROPE_DEFAULT := Vector2(0, -2)
const ROPE_ANCHOR_BAND := Vector2(10, 14)
const SPRING_SPEED := 12.0
const SPRING_COOLDOWN := 0.3
const SPRING_SIZE := Vector3(1.2, 0.24, 1.0)
const CONVEYOR_SPEED := 2.0
const CONVEYOR_SIZE := Vector3(3, 0.32, 1)
const CLOTH_WIDTH := 2.0
const CLOTH_PATTERN_DEPTH := 2.0
const CLOTH_DEPTH := 1.0
const CLOTH_VERTICES := 10
const CLOTH_TARGET_SAG := 0.4
const CLOTH_STIFFNESS := 0.59
const FAN_SIZE := Vector3(0.48, 1.06, 0.92)
const FAN_REACH := 4.0
const FAN_WIDTH := 2.0
const FAN_FORCE := 15.0
const SWITCH_POS := Vector2(3.0, 0.1)
const SWITCH_MASS := 5.0
const SWITCH_HOLD := 0.5
const DOOR_X := 6.0
const DOOR_HEIGHT := 8.8
const DOOR_ANGLE := 90.0
const DOOR_DURATION := 1.0
const SHUTTLE_X := Vector2(-2.0, 2.0)
const SHUTTLE_Y := 5.0
const SHUTTLE_PERIOD := 6.0
const SHUTTLE_SIZE := Vector3(2.6, 0.25, 1.35)
const SWITCH_SIZE := Vector3(1.18, 0.20, 1.18)
const L4_SLOPE := -20.0
const PIT := Vector3(-2.0, 1.0, -4.0)
const CATCH_HEIGHT := 1.2
const CATCH_WIDTH := 1.65
const CATCH_DEPTH := 1.10

const BALLS := {
	"steel": {"radius": 0.25, "mass": 1.0, "bounce": 0.1, "friction": 0.4, "color": Color("#7898bc")},
	"rubber": {"radius": 0.25, "mass": 0.4, "bounce": 0.9, "friction": 0.8, "color": Color("#fb744d")},
	"wood": {"radius": 0.3, "mass": 0.6, "bounce": 0.3, "friction": 0.6, "color": Color("#dba05d")},
}
const KIT := {
	"plank": ["Plank", "Rigid ramp or bridge", Color("#dda25e"), PLANK_SIZE, "▰"],
	"box_s": ["Box S", "5 kg dynamic crate", Color("#a87954"), BOX_SIZE, "▪"],
	"box_h": ["Box H", "20 kg counterweight", Color("#68788d"), BOX_SIZE, "■"],
	"lever": ["Lever", "5 m hinged seesaw · ±35°", Color("#d76156"), LEVER_SIZE, "⤧"],
	"rope": ["Rope", "Drag from anchor to bob · 1–6 m", Color("#9e7ae2"), Vector3(0.34, 0.34, 0.45), "⤓"],
	"spring": ["Spring", "Contact launcher · 12 m/s", Color("#f0bc4f"), SPRING_SIZE, "⇈"],
	"conveyor": ["Conveyor", "Belt travels along local +X · 2 m/s", Color("#384c64"), CONVEYOR_SIZE, "⇥"],
	"cloth": ["Cloth", "Pinned hammock · catches and slings", Color("#90cce8"), Vector3(CLOTH_WIDTH, 0.12, CLOTH_DEPTH), "⌣"],
	"fan": ["Fan", "Air pushes along local +X", Color("#51ae8d"), FAN_SIZE, "✣"],
}
const LEVELS := [
	{
		"id": "L1", "name": "First Roll", "objective": "Guide a steel ball over the pit and into the receiver.",
		"ball": "steel", "start": Vector2(-8, 6), "goal": Vector2(8, 0.5),
		"par": 2, "par_time": 6.0, "kit": {"plank": 3},
		"frame": Rect2(-12.5, -4.4, 25, 14.0), "zones": [],
		"fixtures": [
			[Vector2(-7, -0.25), Vector2(10, 0.5), 0.0],
			[Vector2(6.5, -0.25), Vector2(11, 0.5), 0.0],
			[Vector2(-0.5, -4.25), Vector2(3, 0.5), 0.0],
			[Vector2(-2.18, -2), Vector2(0.36, 4), 0.0],
			[Vector2(1.18, -2), Vector2(0.36, 4), 0.0],
			[Vector2(-8, 5.5), Vector2(3, 0.24), -13.0],
		],
	},
	{
		"id": "L2", "name": "Swing Time", "objective": "Send the rubber ball over the six-metre divider.",
		"ball": "rubber", "start": Vector2(-9, 8), "goal": Vector2(9, 8),
		"par": 3, "par_time": 10.0, "kit": {"rope": 1, "plank": 2, "spring": 1},
		"frame": Rect2(-12, -1, 24, 16), "zones": [Rect2(-0.5, 0, 1, 6)],
		"fixtures": [
			[Vector2(-9, 7.62), Vector2(2, 0.22), -22.0],
			[Vector2(0, 3), Vector2(0.62, 6), 0.0],
			[Vector2(9.7, 7.32), Vector2(4.2, 0.52), 0.0],
		],
	},
	{
		"id": "L3", "name": "Counterweight", "objective": "Drop a heavy box onto a seesaw to launch the wood ball.",
		"ball": "wood", "start": Vector2(-7, 1), "goal": Vector2(9, 9),
		"par": 4, "par_time": 12.0, "kit": {"lever": 1, "box_h": 2, "box_s": 1, "plank": 1},
		"frame": Rect2(-13, -1.0, 26, 21), "zones": [],
		"fixtures": [[Vector2(0, -0.30), Vector2(25, 0.60), 0.0]],
	},
	{
		"id": "L4", "name": "Hammock", "objective": "Use a bounce or sling to clear the wall before the receiver.",
		"ball": "steel", "start": Vector2(-9, 10), "goal": Vector2(10, 2),
		"par": 3, "par_time": 10.0, "kit": {"cloth": 1, "fan": 1, "spring": 1, "plank": 2},
		"frame": Rect2(-13, -1, 26, 14), "zones": [Rect2(4.65, 0, 0.7, 3)],
		"fixtures": [
			[Vector2(-10.2, 9.56), Vector2(2.12, 0.26), 0.0],
			[Vector2(-6.14, 8.46), Vector2(6.55, 0.22), L4_SLOPE],
			[Vector2(0, -0.3), Vector2(25, 0.6), 0.0],
			[Vector2(5, 1.5), Vector2(0.54, 3), 0.0],
			[Vector2(10, 1.32), Vector2(4.5, 0.48), 0.0],
		],
	},
	{
		"id": "L5", "name": "The Machine", "objective": "Hold the pressure plate with a crate to open the gate.",
		"ball": "rubber", "start": Vector2(-10, 11), "goal": Vector2(10, 1),
		"par": 6, "par_time": 20.0,
		"kit": {"plank": 2, "rope": 1, "lever": 1, "box_s": 2, "conveyor": 1, "spring": 1, "fan": 1},
		"frame": Rect2(-13, -1, 26, 15), "zones": [Rect2(5.72, 0, 0.56, DOOR_HEIGHT)],
		"fixtures": [
			[Vector2(-8.9, 10.12), Vector2(4.3, 0.26), -16.0],
			[Vector2(-4.95, 7.7), Vector2(3.7, 0.28), -16.0],
			[Vector2(2.18, 2.45), Vector2(4.0, 0.24), -13.0],
			[Vector2(0, -0.30), Vector2(25, 0.60), 0.0],
			[Vector2(10, 0.38), Vector2(2.0, 0.76), 0.0],
		],
	},
]

static func placement(kind: String, position: Vector2, angle: float, values: Dictionary = {}) -> Dictionary:
	return {"type": kind, "position": position, "rotation": angle, "parameters": values}

static func in_plane(point: Vector2, depth := 0.0) -> Vector3:
	return Vector3(point.x, point.y, depth)

static func stars_for(level: Dictionary, used: int, elapsed: float) -> int:
	if used > level.par:
		return 1
	return 3 if elapsed <= level.par_time else 2
