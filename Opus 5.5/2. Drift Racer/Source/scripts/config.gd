class_name Config
extends RefCounted
## Central configuration. Every gameplay / tuning number lives here, tagged
## with the requirement ID it comes from. No other script hard-codes these.

# ------------------------------------------------------------------ general
const PHYSICS_TICKS := 120                      # R-07..R-13: physics tick (Hz)
const GRAVITY := 9.8                            # m/s^2 (matches project default)

# ------------------------------------------------------------- collision layers
const LAYER_ROAD := 1                           # road + ground (wheel rays hit this)
const LAYER_BARRIER := 2                        # barrier walls / catch fence
const LAYER_CAR := 4                            # car bodies
const LAYER_CHECKPOINT := 8                     # checkpoint gates

# --------------------------------------------------------- track generation
const DEFAULT_SEED := 2024                      # R-01
const SEED_MIN := 0
const SEED_MAX := 99999999
const CONTROL_POINT_COUNT := 12                 # R-02
const CIRCLE_RADIUS_MIN := 220.0                # R-02 (m)
const CIRCLE_RADIUS_MAX := 320.0                # R-02 (m)
const RADIAL_NOISE := 0.25                      # R-02 (+-25 %)
const ELEVATION_AMPLITUDE := 6.0                # R-02 (+-6 m)
const ELEVATION_SMOOTH_PASSES := 2              # neighbour-average passes on elevation
const SAMPLE_SPACING := 2.0                     # R-02 (m)
const SPLINE_DENSE_STEPS := 400                 # dense steps per spline segment before resampling
const MIN_CORNER_RADIUS := 18.0                 # R-02 (m)
const CURVATURE_STENCIL := 3                    # samples each side used to measure a radius
const TRACK_LENGTH_MIN := 1200.0                # R-02 (m)
const TRACK_LENGTH_MAX := 2000.0                # R-02 (m)
const MAX_TRACK_ATTEMPTS := 2000                # derived-seed retries before the safe fallback
const CORRIDOR_CLEARANCE := 26.0                # min distance between distant parts of the centreline (m)
const CORRIDOR_NEIGHBOUR_SKIP := 70.0           # along-track distance treated as "same part" (m)
const START_STRAIGHT_HALF_WINDOW := 70.0        # straightness window used to place the start line (m)
const FALLBACK_RADIUS := 260.0                  # plain circle used only if every attempt fails

# --------------------------------------------------------------- road (R-03)
const ROAD_WIDTH := 14.0
const CURB_WIDTH := 0.3
const CURB_STRIPE_LENGTH := 3.0
const CENTER_DASH_LENGTH := 3.0
const CENTER_GAP_LENGTH := 3.0
const CENTER_LINE_WIDTH := 0.15
const EDGE_LINE_WIDTH := 0.2
const EDGE_LINE_INSET := 0.3                    # edge line centre distance from asphalt edge
const START_LINE_DEPTH := 1.6                   # checkered start/finish band along the track
const RUNOFF_WIDTH := 1.2                       # strip between curb and barrier

# ----------------------------------------------------------- barriers (R-04)
const WALL_HEIGHT := 1.0
const WALL_VISUAL_THICKNESS := 0.8
const WALL_COLLIDER_THICKNESS := 1.6
const WALL_BURY_DEPTH := 0.6                    # walls extend below the road to hide the ground seam
const CATCH_FENCE_HEIGHT := 4.0                 # invisible collider above the 1 m wall (safety)
const BARRIER_STYLE_LENGTH := 32.0              # alternating concrete / tyre-stack sections (m)
const TYRE_RADIUS := 0.3
const TYRES_PER_STACK := 3
const TYRE_STACK_SPACING := 0.62
const CONCRETE_BLOCK_LENGTH := 4.0

# ------------------------------------------------------- surroundings (R-05)
const GROUND_MARGIN := 900.0                    # ground extends this far beyond the track bounds
const GROUND_CELL := 6.0                        # ground grid spacing (m)
const GROUND_UNDER_ROAD := 0.45                 # ground sits this far below the road near the track
const GROUND_FLAT_RADIUS := 13.0                # distance from centreline kept below road level
const GROUND_BLEND_END := 70.0                  # distance where the ground reaches its rolling height
const GROUND_ROLL_AMPLITUDE := 4.0
const GROUND_ROLL_FREQUENCY := 0.0045
const TREE_COUNT := 720                         # R-05 requires >= 400
const TREE_MIN_TRACK_DISTANCE := 22.0
const TREE_MAX_TRACK_DISTANCE := 430.0
const TREE_MIN_SPACING := 5.0
const TREE_CHUNK_SIZE := 140.0
const TREE_LOD0_END := 240.0                    # R-31 visibility range / LOD switch
const TREE_LOD1_END := 1600.0
const TREE_LOD_MARGIN := 20.0
const TREE_SCALE_MIN := 0.8
const TREE_SCALE_MAX := 1.6
const HILL_COUNT := 12                          # R-05
const HILL_DISTANCE_MIN := 1700.0
const HILL_DISTANCE_MAX := 2500.0
const HILL_RADIUS_MIN := 260.0
const HILL_RADIUS_MAX := 560.0
const HILL_HEIGHT_MIN := 90.0
const HILL_HEIGHT_MAX := 340.0
const ARCH_HEIGHT := 7.5
const ARCH_PILLAR_OFFSET := 11.2
const GRANDSTAND_LENGTH := 72.0
const GRANDSTAND_OFFSET := 13.5                 # distance of the stand's front from the centreline
const GRANDSTAND_TIERS := 6
const GRANDSTAND_KEEPOUT := 30.0                # trees are kept this far from stand / arch
const SUN_ELEVATION_DEG := 36.0
const SUN_AZIMUTH_DEG := 140.0
const FOG_DENSITY := 0.00045
const SHADOW_DISTANCE := 150.0                  # R-31
const CAMERA_FAR := 5200.0

# --------------------------------------------------------- checkpoints (R-06)
const CHECKPOINT_COUNT := 8
const CHECKPOINT_HEIGHT := 8.0
const CHECKPOINT_DEPTH := 2.0

# ---------------------------------------------------------- car body (R-07)
const CAR_MASS := 1200.0
const COM_DROP := 0.3
const WHEELBASE := 2.6
const TRACK_WIDTH := 1.6
const WHEEL_RADIUS := 0.34
const WHEEL_WIDTH := 0.26
const CHASSIS_SIZE := Vector3(1.8, 0.6, 4.3)     # collision box (body origin = suspension top)
const CHASSIS_OFFSET_Y := 0.05
const CABIN_SIZE := Vector3(1.35, 0.4, 2.0)
const CABIN_OFFSET := Vector3(0.0, 0.55, 0.2)
const CAR_INERTIA := Vector3(1750.0, 2050.0, 600.0)   # pitch, yaw, roll (kg m^2)
const ANGULAR_DAMP := 0.9                       # tuned (drift_sweep): no spin-outs, drifts still sustainable
const BODY_FRICTION := 0.25                     # car body vs walls / cars
const BODY_BOUNCE := 0.05

# -------------------------------------------------------- suspension (R-08)
const MOUNT_HEIGHT := 0.0                       # suspension top, relative to body origin
const SUSPENSION_REST := 0.35
const SPRING_K := 30000.0
const DAMPER_C := 2500.0
const BUMP_STOP_START := 0.29                   # compression where the bump stop engages
const BUMP_STOP_K := 160000.0
const ANTI_ROLL_K := 9000.0                     # N per m of left/right compression difference

# ---------------------------------------------------------- drivetrain (R-09)
const MAX_ENGINE_FORCE := 9000.0                # total rear-wheel force at peak torque in 1st
const TOP_SPEED_KMH := 190.0
const GEAR_RATIOS := [3.40, 2.35, 1.80, 1.47, 1.24, 1.07]
const FINAL_DRIVE := 4.73                       # 6th gear hits 7500 rpm at ~190 km/h
const REVERSE_RATIO := 3.2
const RPM_IDLE := 1000.0
const RPM_MAX := 7500.0
const RPM_UPSHIFT := 7000.0
const RPM_DOWNSHIFT := 3500.0
const RPM_LIMITER_SOFT := 7350.0                # force fades from here to RPM_MAX
const SHIFT_TIME := 0.18                        # torque cut while shifting (s)
const LAUNCH_RPM := 3800.0                      # simulated clutch slip at pull-away
const RPM_RISE_RATE := 14000.0                  # rpm/s
const RPM_FALL_RATE := 9000.0
const TORQUE_CURVE_RPM := [1000.0, 2000.0, 3000.0, 4500.0, 5500.0, 6500.0, 7500.0]
const TORQUE_CURVE_VALUE := [0.55, 0.72, 0.86, 1.0, 0.98, 0.9, 0.78]
const ENGINE_BRAKE_FORCE := 900.0
const REVERSE_FORCE := 4800.0
const REVERSE_MAX_SPEED_KMH := 30.0
const BRAKE_FORCE := 12000.0
const BRAKE_BIAS_FRONT := 0.62
const STOP_SPEED := 1.0                         # m/s: "stationary" for reverse engagement
const HOLD_BRAKE_FORCE := 2600.0                # keeps a stopped car from creeping on slopes
const AERO_DRAG := 0.42                         # F = AERO_DRAG * v^2
const ROLLING_RESISTANCE := 0.015               # fraction of wheel load

# --------------------------------------------------------- tyres (R-10)
const TYRE_MU := 1.25
const TYRE_PEAK_SLIP_DEG := 8.0
const TYRE_FALLOFF_END_DEG := 25.0
const TYRE_SLIDE_RATIO := 0.7
const TYRE_LOAD_SENSITIVITY := 0.12
const TYRE_NOMINAL_LOAD := 2943.0               # CAR_MASS * GRAVITY / 4
const TYRE_LOW_SPEED := 3.0                     # slip-angle denominator floor (m/s)
const WHEELSPIN_LONG_RATIO := 0.9               # kinetic share of grip while spinning / locked
const HANDBRAKE_GRIP := 0.4                     # R-10
const SPIN_SLIP_SCALE := 12.0                   # visual wheelspin slip (m/s) at 100 % excess torque

# ------------------------------------------------------------ steering (R-11)
const STEER_MAX_LOW_DEG := 32.0
const STEER_MAX_HIGH_DEG := 8.0
const STEER_SPEED_REF_KMH := 190.0
const STEER_RATE := 4.0                         # rad/s
const ASSIST_GAIN := 0.9                         # tuned with the drift_sweep autotest
const ASSIST_START_DEG := 3.0
const ASSIST_FULL_DEG := 8.0
const ASSIST_DRIFT_INPUT_SCALE := 0.5           # driver input share while sliding (tuned, drift_sweep)
const ASSIST_MIN_SPEED := 4.0                   # m/s

# ---------------------------------------------------------- stability (R-13)
const FLIP_UP_DOT := 0.3
const FLIP_RESPAWN_TIME := 2.0
const KILL_DEPTH := 25.0                        # below the lowest road point -> respawn
const SPAWN_HEIGHT := 0.72                      # body origin above the road on (re)spawn
const SPAWN_CLEARANCE := 5.5                    # min distance to other cars when respawning
const SPAWN_LATERAL_SLOTS := [0.0, -3.5, 3.5, -5.5, 5.5]

# ------------------------------------------------------------- drift (R-14)
const DRIFT_MIN_SLIP_DEG := 15.0
const DRIFT_MAX_SLIP_DEG := 80.0
const DRIFT_MIN_SPEED_KMH := 40.0
const DRIFT_SPEED_DIVISOR := 10.0
const DRIFT_SLIP_FACTOR_REF_DEG := 30.0         # slip factor = slip / 30 deg
const DRIFT_SLIP_FACTOR_MIN := 0.5
const DRIFT_SLIP_FACTOR_MAX := 2.5
const DRIFT_COMBO_MAX := 5
const DRIFT_COMBO_STEP_TIME := 2.0
const DRIFT_BANK_DELAY := 1.0
const DRIFT_MIN_DURATION := 1.0

# ------------------------------------------------------------- effects
const SKID_MAX_SEGMENTS := 600                  # R-15
const SKID_FADE_TIME := 20.0                    # R-15
const SKID_SEGMENT_LENGTH := 0.6
const SKID_WIDTH := 0.24
const SKID_SLIP_THRESHOLD := 3.0                # wheel sliding speed (m/s)
const SMOKE_AMOUNT := 96                        # R-16 per rear wheel
const SMOKE_LIFETIME := 2.6
const SMOKE_SLIP_MIN := 3.0
const SMOKE_SLIP_FULL := 12.0
const SPARK_AMOUNT := 90                        # R-17
const SPARK_LIFETIME := 0.55
const SPARK_MIN_SPEED_KMH := 20.0               # R-17
const IMPACT_MIN_SPEED := 2.5                   # normal speed (m/s) that counts as a hit
const IMPACT_BURST_TIME := 0.18

# ------------------------------------------------------------- camera (R-18)
const CHASE_DISTANCE := 6.0
const CHASE_HEIGHT := 2.2
const CHASE_LOOK_HEIGHT := 0.9
const CHASE_FOV_MIN := 60.0
const CHASE_FOV_MAX := 85.0
const CHASE_YAW_RATE := 5.5                     # follow lag (1/s)
const CHASE_HEIGHT_RATE := 7.0
const CHASE_DRIFT_BLEND := 0.45                 # 0 = heading, 1 = velocity direction
const HOOD_OFFSET := Vector3(0.0, 1.08, -0.35)
const HOOD_LOOK_DISTANCE := 20.0

# ------------------------------------------------------------- lights (R-19)
const HEADLIGHT_RANGE := 45.0
const HEADLIGHT_ANGLE_DEG := 32.0
const HEADLIGHT_ENERGY := 3.5
const TAIL_LIGHT_IDLE_EMISSION := 0.6
const BRAKE_LIGHT_EMISSION := 5.0
const BRAKE_OMNI_ENERGY := 1.6
const BRAKE_OMNI_RANGE := 3.5
const REVERSE_LIGHT_EMISSION := 4.0
const REVERSE_OMNI_ENERGY := 1.2

# ----------------------------------------------------------------- AI (R-20..R-24)
const AI_LAT_ACCEL := 9.0                       # R-21
const RACING_LINE_MAX_OFFSET := 4.0             # R-21
const RACING_LINE_ITERATIONS := 120
const RACING_LINE_STENCILS := [24, 12, 6]       # elastic-band neighbour distances (samples)
const RACING_LINE_CURVE_STENCIL := 5
const AI_BRAKE_DECEL := 6.5                     # braking pass of the speed profile (m/s^2)
const AI_LOOKAHEAD_BASE := 7.0                  # m
const AI_LOOKAHEAD_PER_SPEED := 0.55            # s (look-ahead grows with speed)
const AI_SPEED_LOOKAHEAD_TIME := 0.45           # s
const AI_THROTTLE_GAIN := 0.35                  # per m/s of speed error
const AI_BRAKE_GAIN := 0.25
const DIFFICULTY_NAMES := ["Easy", "Normal", "Hard"]
const DIFFICULTY_SPEED := [0.85, 0.95, 1.0]     # R-22
const DEFAULT_DIFFICULTY := 1
const RUBBER_BAND_DISTANCE := 80.0              # R-22
const RUBBER_BAND_FACTOR := 0.05                # R-22
const AI_LANE_OFFSET_MAX := 1.5
const AI_LANE_PERIOD_MIN := 14.0
const AI_LANE_PERIOD_MAX := 26.0
const AI_MAX_TOTAL_OFFSET := 5.4
const AI_AVOID_DISTANCE := 6.0                  # R-23 gap ahead (bumper to bumper)
const AI_AVOID_LANE_HALF_WIDTH := 2.2
const AI_AVOID_SHIFT := 3.2
const AI_AVOID_RATE := 3.0                      # avoidance offset change (m/s)
const AI_CLOSING_BRAKE_SPEED := 3.0             # closing speed that triggers braking (m/s)
const AI_STUCK_SPEED_KMH := 5.0                 # R-23
const AI_STUCK_TIME := 3.0                      # R-23
const AI_COOLDOWN_SPEED_SCALE := 0.7            # pace after crossing the finish line

# ---------------------------------------------------------------- race (R-25, R-26)
const LAP_COUNT := 3
const CAR_COUNT := 4
const COUNTDOWN_FROM := 3
const COUNTDOWN_STEP := 1.0
const GO_DISPLAY_TIME := 1.0
const GRID_FIRST_ROW := 7.0                     # distance of pole slot behind the line (m)
const GRID_ROW_SPACING := 9.0
const GRID_STAGGER := 4.5
const GRID_COLUMN_OFFSET := 3.2
const PLAYER_GRID_SLOT := 3
const WRONG_WAY_DOT := -0.5
const WRONG_WAY_TIME := 2.0
const CAR_COLORS := [Color(1.0, 0.45, 0.0), Color(0.85, 0.06, 0.08), Color(0.08, 0.3, 0.95), Color(0.1, 0.72, 0.2)]
const CAR_NAMES := ["YOU", "Red AI", "Blue AI", "Green AI"]

# --------------------------------------------------------------- audio (R-30)
const ENGINE_MIX_RATE := 22050.0
const ENGINE_BUFFER_SEC := 0.12
const ENGINE_CHUNK := 256
const ENGINE_CYLINDERS := 6
const ENGINE_TABLE_SIZE := 1024
const ENGINE_HARMONICS := 16
const ENGINE_VOLUME_DB := -6.0
const AI_ENGINE_VOLUME_DB := 2.0
const AI_AUDIO_RANGE := 150.0
const AUDIO_UNIT_SIZE := 9.0
const SFX_MIX_RATE := 22050
const SQUEAL_VOLUME_DB := -8.0
const WIND_VOLUME_DB := -14.0
const SCRAPE_VOLUME_DB := -6.0
const THUD_VOLUME_DB := -3.0
const BEEP_VOLUME_DB := -6.0
const BEEP_FREQ := 880.0
const BEEP_GO_FREQ := 1760.0

# ------------------------------------------------------------------ UI (R-27..R-29)
const MINIMAP_SIZE := 260.0
const LAP_LIST_COUNT := 3
const HUD_UPDATE_INTERVAL := 0.05
const DEBUG_UPDATE_INTERVAL := 0.25
const SCREENSHOT_DIR := "Screenshots"
const UI_VISIBILITY_LAYER := 20                # canvas visibility layer of hideable UI (U-02); F8 culls it


## Rear-axle speed (m/s) that corresponds to a given engine rpm in a gear.
static func speed_for_rpm(rpm: float, gear_ratio: float) -> float:
	return rpm * TAU / 60.0 * WHEEL_RADIUS / (gear_ratio * FINAL_DRIVE)
