extends RefCounted
## Central configuration: EVERY tunable number of the game lives here (spec numbers are marked with their ID).
## Other scripts reference it with `const C := preload("res://scripts/core/config.gd")`.

# ---------------------------------------------------------------- physics (P-01, P-02, P-03)
const PHYS_HZ := 120                       # P-01 physics tick
const PHYS_DT := 1.0 / 120.0
const GRAVITY := 9.81                      # P-01 (also set in project.godot physics/3d/default_gravity)
const KILL_Y := -15.0                      # P-03 kill plane
const SIM_TIMEOUT := 25.0                  # P-03 "Too slow"
const GOAL_HOLD_TIME := 1.0                # P-13 ball centre inside sensor continuously
const TIME_SCALES: Array[float] = [0.25, 1.0, 2.0]   # P-17
const DEFAULT_TIME_SCALE_INDEX := 1
const TURBO_SCALE := 8.0                   # dev/test fast-forward (still 1/120 s per step)
const TURBO_MAX_STEPS_PER_FRAME := 16
const NORMAL_MAX_STEPS_PER_FRAME := 8
const DETERMINISM_RUNS := 5                # P-02
const DETERMINISM_TOLERANCE := 0.01        # P-02 1 cm
const RETIRE_CALM_TIME := 0.6             # build-mode quiet time before old physics worlds are freed
const ESCAPE_LIMIT := 500.0                # P-28 bodies beyond this are frozen (never reached in play)
const SCENERY_FRICTION := 0.6
const SCENERY_DEPTH := 1.4                 # visual/collision depth of static scenery slabs (Z)
const OBJECT_DEPTH := 1.0                  # all placeable objects are 1 m deep (Z)

# collision layers (bit values)
const LAYER_STATIC := 1
const LAYER_DYNAMIC := 2
const LAYER_ROPE := 4
const LAYER_SENSOR := 8

# ---------------------------------------------------------------- balls (P-04)
const BALLS := {
	"steel": {"radius": 0.25, "mass": 1.0, "bounce": 0.1, "friction": 0.4, "color": Color(0.52, 0.6, 0.7), "metallic": 0.8, "rough": 0.25, "name": "Steel ball"},
	"rubber": {"radius": 0.25, "mass": 0.4, "bounce": 0.9, "friction": 0.8, "color": Color(0.86, 0.24, 0.52), "metallic": 0.0, "rough": 0.7, "name": "Rubber ball"},
	"wood": {"radius": 0.3, "mass": 0.6, "bounce": 0.3, "friction": 0.6, "color": Color(0.8, 0.62, 0.38), "metallic": 0.0, "rough": 0.8, "name": "Wood ball"},
}

# ---------------------------------------------------------------- placeable objects (P-05 .. P-12)
const PLANK_SIZE := Vector2(3.0, 0.2)      # P-05 3.0 x 0.2 x 1.0
const PLANK_FRICTION := 0.5
const BOX_SIZE := 1.0                      # P-06 cube 1 m
const BOX_S_MASS := 5.0
const BOX_H_MASS := 20.0
const BOX_FRICTION := 0.6
const LEVER_LENGTH := 5.0                  # P-07
const LEVER_THICKNESS := 0.2
const LEVER_MASS := 10.0
const LEVER_LIMIT_DEG := 35.0
const LEVER_ANGULAR_DAMP := 0.5
const LEVER_FRICTION := 0.5
const LEVER_FULCRUM_STEP := 0.5
const LEVER_FULCRUM_MAX := 2.0             # fulcrum may sit up to 2 m from the plank centre
const LEVER_STAND_HEIGHT := 0.55           # visual/static triangular stand below the pivot
const ROPE_SEGMENT := 0.25                 # P-08
const ROPE_SEGMENT_MASS := 0.2
const ROPE_RADIUS := 0.05
const ROPE_MIN := 1.0
const ROPE_MAX := 6.0
const ROPE_DEFAULT := 3.0
const ROPE_MAX_ANGLE := 90.0
const ROPE_BOB_MASS := 4.0
const ROPE_BASKET_SIZE := Vector2(0.9, 0.12)   # cradle floor (inner width 0.66 m)
const ROPE_BASKET_LIP := Vector2(0.12, 0.32)   # cradle side lips
const ROPE_BASKET_HANG := 0.55             # bail point height above the cradle floor centre
const ROPE_FRICTION := 0.6
const ROPE_CREAK_MIN_SPEED := 1.2
const ROPE_CREAK_COOLDOWN := 0.45
const ROPE_TUBE_SIDES := 8
const ROPE_TUBE_RINGS_PER_SEGMENT := 3
const SPRING_SIZE := Vector2(1.0, 0.3)     # pad footprint (base + plate)
const SPRING_LAUNCH_SPEED := 12.0          # P-09
const SPRING_COOLDOWN := 0.3               # P-09
const SPRING_TRIGGER_HEIGHT := 0.18        # sensor slab above the plate
const SPRING_MAX_APPROACH := 1.0           # bodies already leaving faster than this are not re-launched
const SPRING_ANIM_TIME := 0.3
const CONVEYOR_SIZE := Vector2(3.0, 0.3)   # P-10 3 m long
const CONVEYOR_SPEED := 2.0                # P-10 2 m/s along local X
const CONVEYOR_FRICTION := 0.9
const CONVEYOR_STRIPE_SPACING := 0.5
const CLOTH_SIZE := Vector2(2.0, 1.0)      # P-11 2 m span (X) x 1 m depth (Z), see README decisions
const CLOTH_VERTS := 10                    # P-11 10 x 10 vertices
const CLOTH_DIP := 0.32                    # pre-shaped slack of the rest mesh
const CLOTH_STIFFNESS := 0.8
const CLOTH_DAMPING := 2.0
const CLOTH_PRECISION := 20
const CLOTH_MASS := 0.5
const CLOTH_POST_SIZE := Vector2(0.16, 0.5)
const FAN_HOUSING := Vector2(0.5, 1.3)
const FAN_AREA := Vector2(4.0, 2.0)        # P-12 4 x 2 m in front of the fan
const FAN_ACCEL := 15.0                    # P-12 15 N on a 1 kg object (= 15 m/s^2 field, linear falloff)
const FAN_PARTICLES := 48
const FAN_STREAK_SPEED := 6.0
const FAN_BLADE_SPEED := 14.0              # rad/s visual spin

# ---------------------------------------------------------------- goal (P-13)
const CUP_INNER := Vector2(1.2, 1.0)       # sensor = cup interior, centred on the level's hole point
const CUP_WALL := 0.15
const CUP_FLOOR := 0.15
const CUP_RIM_GLOW := 3.5
const SUCCESS_ZOOM := 0.88                 # camera distance factor on success
const RESULTS_DELAY := 1.1

# ---------------------------------------------------------------- editor (P-15 .. P-19)
const SNAP_POS := 0.25
const SNAP_ROT := 15.0
const FINE_ROT := 5.0
const UNDO_LIMIT := 100                    # P-18 requires >= 50
const HINT_DELAY := 60.0                   # P-19
const OVERLAP_EPS := 0.02                  # touching surfaces are allowed
const DRAG_START_PIXELS := 4.0
const ROPE_DRAG_MIN := 0.6

# ---------------------------------------------------------------- camera (P-14, P-25)
const CAM_FOV := 40.0
const CAM_PITCH_DEG := -15.0               # looks at the play plane ~15 deg from the front
const CAM_PADDING := 0.10                  # auto-frame padding 10%
const CAM_ORBIT_YAW_LIMIT := 45.0
const CAM_ORBIT_PITCH_MIN := -55.0
const CAM_ORBIT_PITCH_MAX := 20.0
const CAM_ORBIT_SPEED := 0.25              # deg per pixel
const CAM_ZOOM_STEP := 0.9
const CAM_MIN_DIST := 6.0
const CAM_MAX_DIST := 80.0
const CAM_SMOOTH := 12.0
const SHAKE_HEAVY_MIN_DV := 3.0            # Box H impact speed change that shakes the camera
const SHAKE_PER_DV := 0.035
const SHAKE_MAX := 0.35
const SHAKE_DECAY := 6.0

# ---------------------------------------------------------------- vfx / audio (P-25, P-26)
const DUST_MIN_DV := 3.0
const DUST_POOL := 12
const DUST_PARTICLES := 14
const TRAIL_POINTS := 36
const TRAIL_MIN_STEP := 0.05
const TRAIL_WIDTH := 0.22
const CONFETTI_COUNT := 160
const IMPACT_MIN_DV := 0.8
const IMPACT_FULL_DV := 9.0
const IMPACT_COOLDOWN := 0.06
const AUDIO_RATE := 22050
const AUDIO_POOL := 16

# ---------------------------------------------------------------- performance / debug
const DEBUG_REFRESH := 0.25
const SCREENSHOT_DIR := "Screenshots"

# ---------------------------------------------------------------- colours (P-24)
const COL_BG_TOP := Color(0.74, 0.81, 0.9)
const COL_BG_BOTTOM := Color(0.9, 0.93, 0.97)
const COL_GRID := Color(0.55, 0.62, 0.74)
const COL_SCENERY := Color(0.6, 0.66, 0.75)
const COL_SCENERY_DARK := Color(0.43, 0.48, 0.57)
const COL_PLANK := Color(0.9, 0.56, 0.24)
const COL_BOX_S := Color(0.6, 0.4, 0.22)
const COL_BOX_H := Color(0.46, 0.48, 0.52)
const COL_LEVER := Color(0.86, 0.2, 0.2)
const COL_LEVER_STAND := Color(0.35, 0.33, 0.36)
const COL_ROPE := Color(0.93, 0.86, 0.66)
const COL_BOB := Color(0.3, 0.33, 0.4)
const COL_SPRING := Color(0.98, 0.8, 0.16)
const COL_CONVEYOR := Color(0.16, 0.17, 0.2)
const COL_CONVEYOR_STRIPE := Color(0.95, 0.75, 0.2)
const COL_CLOTH := Color(0.84, 0.92, 1.0)
const COL_FAN := Color(0.28, 0.74, 0.4)
const COL_CUP := Color(0.92, 0.95, 1.0)
const COL_CUP_GLOW := Color(0.2, 0.85, 1.0)
const COL_SWITCH_OFF := Color(1.0, 0.45, 0.15)
const COL_SWITCH_ON := Color(0.3, 1.0, 0.45)
const COL_DOOR := Color(0.35, 0.38, 0.46)
const COL_MOVER := Color(0.25, 0.62, 0.66)
const COL_FORBIDDEN := Color(0.95, 0.2, 0.2)
const COL_GHOST_OK := Color(0.25, 0.95, 0.45, 0.45)
const COL_GHOST_BAD := Color(1.0, 0.2, 0.2, 0.45)
const COL_HINT := Color(0.35, 0.6, 1.0, 0.4)
const COL_OUTLINE := Color(1.0, 0.95, 0.4)

# ---------------------------------------------------------------- L5 machinery
const MOVER_SIZE := Vector2(3.0, 0.3)
const MOVER_X_RANGE := Vector2(-2.0, 2.0)  # L5 moving platform
const MOVER_Y := 5.0
const MOVER_PERIOD := 6.0
const DOOR_SIZE := Vector2(0.3, 3.0)
const DOOR_OPEN_DEG := 90.0
const DOOR_OPEN_TIME := 1.0
const SWITCH_SIZE := Vector2(1.4, 0.1)
const SWITCH_MIN_MASS := 5.0
const SWITCH_HOLD := 0.5
const SWITCH_PRESS_DEPTH := 0.06

# ---------------------------------------------------------------- levels (section 4)
# statics: c = centre, s = size, r = rotation deg (CCW), m = material key.
# hole: centre of the cup interior / sensor; lips = wall heights above the cup floor (left, right); back = backboard.
const LEVELS := [
	{
		"id": "L1", "name": "First Roll", "par": 2, "par_time": 6.0, "tutorial": true,
		"objective": "Build a path so the steel ball rolls into the glowing cup.",
		"ball": "steel", "ball_pos": Vector2(-8, 6),
		"hole": Vector2(8, 0.5), "lips": Vector2(0.12, 1.0), "back": 1.6,
		"inventory": [["plank", 3]],
		"statics": [
			{"c": Vector2(-7, -2.25), "s": Vector2(10, 4.5), "m": "floor"},
			{"c": Vector2(6.5, -2.25), "s": Vector2(11, 4.5), "m": "floor"},
			{"c": Vector2(-0.5, -4.25), "s": Vector2(3, 0.5), "m": "floor"},
			{"c": Vector2(-8, 5.5), "s": Vector2(3, 0.5), "r": -12.0, "m": "platform"},
			{"c": Vector2(-8, 2.75), "s": Vector2(0.4, 5.0), "m": "post"},
		],
		"forbidden": [],
		"bounds": Rect2(-13, -5, 26, 13),
	},
	{
		"id": "L2", "name": "Swing Time", "par": 3, "par_time": 10.0,
		"objective": "Get the rubber ball over the wall and into the cup on the ledge.",
		"ball": "rubber", "ball_pos": Vector2(-9, 8),
		"hole": Vector2(9, 8), "lips": Vector2(0.45, 1.0), "back": 2.5,
		"inventory": [["rope", 1], ["plank", 2], ["spring", 1]],
		"rope_anchor_y": Vector2(10, 14),
		"statics": [
			{"c": Vector2(0, -0.5), "s": Vector2(26, 1), "m": "floor"},
			{"c": Vector2(-9.25, 7.5), "s": Vector2(2.0, 0.5), "m": "platform"},
			{"c": Vector2(-9.25, 3.6), "s": Vector2(0.4, 7.3), "m": "post"},
			{"c": Vector2(0, 3), "s": Vector2(0.6, 6), "m": "wall"},
			{"c": Vector2(10, 3.75), "s": Vector2(5, 7.5), "m": "wall"},
		],
		"forbidden": [Rect2(-1.5, 6, 3, 9)],
		"bounds": Rect2(-14.5, -1, 27.5, 16),
	},
	{
		"id": "L3", "name": "Counterweight", "par": 4, "par_time": 12.0,
		"objective": "Launch the wooden ball into the high cup. Heavy boxes make good counterweights.",
		"ball": "wood", "ball_pos": Vector2(-7, 1),
		"hole": Vector2(9, 9), "lips": Vector2(0.6, 1.0), "back": 3.0,
		"inventory": [["lever", 1], ["box_h", 2], ["box_s", 1], ["plank", 1]],
		"statics": [
			{"c": Vector2(0, -0.5), "s": Vector2(26, 1), "m": "floor"},
			{"c": Vector2(9, 4.2), "s": Vector2(1.8, 8.4), "m": "wall"},
		],
		"forbidden": [Rect2(4.5, 0, 3.5, 13)],
		"bounds": Rect2(-13, -1, 26, 15),
	},
	{
		"id": "L4", "name": "Hammock", "par": 3, "par_time": 10.0,
		"objective": "Sling or bounce the steel ball over the wall into the cup.",
		"ball": "steel", "ball_pos": Vector2(-9, 10),
		"hole": Vector2(10, 2), "lips": Vector2(0.6, 1.0), "back": 2.5,
		"inventory": [["cloth", 1], ["fan", 1], ["spring", 1], ["plank", 2]],
		"statics": [
			{"c": Vector2(0, -0.5), "s": Vector2(26, 1), "m": "floor"},
			{"c": Vector2(-7.0, 8.79), "s": Vector2(5.6, 0.4), "r": -20.0, "m": "ramp"},
			{"c": Vector2(-9.75, 10.1), "s": Vector2(0.3, 0.8), "r": -20.0, "m": "post"},
			{"c": Vector2(-7.0, 4.4), "s": Vector2(0.4, 8.6), "m": "post"},
			{"c": Vector2(5, 1.5), "s": Vector2(0.6, 3), "m": "wall"},
			{"c": Vector2(9.75, 0.75), "s": Vector2(4.5, 1.5), "m": "platform"},
		],
		"forbidden": [Rect2(4.0, 3.0, 2.0, 11.0), Rect2(5.3, 0, 7, 6)],
		"bounds": Rect2(-13, -1, 26, 14),
	},
	{
		"id": "L5", "name": "The Machine", "par": 6, "par_time": 20.0,
		"objective": "Hold the switch down with something heavy to open the door, then deliver the rubber ball to the cup.",
		"ball": "rubber", "ball_pos": Vector2(-10, 11),
		"hole": Vector2(10, 1), "lips": Vector2(0.3, 1.0), "back": 2.0,
		"inventory": [["plank", 2], ["rope", 1], ["lever", 1], ["box_s", 2], ["conveyor", 1], ["spring", 1], ["fan", 1]],
		"statics": [
			{"c": Vector2(0, -0.5), "s": Vector2(26, 1), "m": "floor"},
			{"c": Vector2(-10, 10.5), "s": Vector2(3.6, 0.5), "r": -8.0, "m": "platform"},
			{"c": Vector2(-5.5, 7.25), "s": Vector2(3.0, 0.5), "m": "platform"},
			{"c": Vector2(3.5, 3.0), "s": Vector2(2.4, 0.4), "m": "platform"},
			{"c": Vector2(6.0, 6.25), "s": Vector2(0.6, 6.5), "m": "wall"},
			{"c": Vector2(10.0, 0.25), "s": Vector2(1.8, 0.5), "m": "platform"},
		],
		"mover": true, "door": Vector2(6.0, 3.0), "switch": Vector2(3, 0.1),
		"forbidden": [Rect2(6.3, 0, 5.7, 9.5)],
		"bounds": Rect2(-13, -1, 26, 15),
	},
]
