class_name ChaseCamera
extends Camera3D
## R-18: chase camera 6 m behind / 2.2 m above the car with smoothed (lagging)
## yaw and height. Its look direction blends the car heading with the travel
## direction, so during a drift the car's angle stays visible. FOV widens from
## 60 deg at standstill to 85 deg at top speed. `C` switches to a hood camera.
## Runs in _process on the car's interpolated transform (smooth at any FPS).

enum Mode { CHASE, HOOD }

var target: Car
var mode := Mode.CHASE

var _dir := Vector3.FORWARD
var _height := 0.0
var _snap := true


func _ready() -> void:
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	near = 0.08
	far = Config.CAMERA_FAR
	fov = Config.CHASE_FOV_MIN
	doppler_tracking = Camera3D.DOPPLER_TRACKING_IDLE_STEP
	current = true


func set_target(car: Car) -> void:
	if target != null and target.respawned.is_connected(snap):
		target.respawned.disconnect(snap)
	target = car
	target.respawned.connect(snap)
	snap()


func snap() -> void:
	_snap = true


func toggle_mode() -> void:
	mode = Mode.HOOD if mode == Mode.CHASE else Mode.CHASE
	snap()


func _process(delta: float) -> void:
	if target == null:
		return
	var xf := target.get_global_transform_interpolated()
	var t := clampf(target.speed_kmh / Config.TOP_SPEED_KMH, 0.0, 1.0)
	fov = lerpf(Config.CHASE_FOV_MIN, Config.CHASE_FOV_MAX, t)
	if mode == Mode.HOOD:
		var eye := xf * Config.HOOD_OFFSET
		var ahead := xf * (Config.HOOD_OFFSET + Vector3(0.0, -0.15, -Config.HOOD_LOOK_DISTANCE))
		look_at_from_position(eye, ahead, xf.basis.y)
		_snap = true
		return
	var heading := -xf.basis.z
	heading.y = 0.0
	if heading.length_squared() < 0.0001:
		heading = _dir
	heading = heading.normalized()
	var desired := heading
	var vel := target.linear_velocity
	vel.y = 0.0
	if vel.length() > 3.0 and target.forward_speed > 0.0:
		var vdir := vel.normalized()
		if vdir.dot(heading) > -0.2:
			desired = heading.slerp(vdir, Config.CHASE_DRIFT_BLEND).normalized()
	var car_pos := xf.origin
	if _snap:
		_dir = desired
		_height = car_pos.y + Config.CHASE_HEIGHT
		_snap = false
	else:
		_dir = _dir.slerp(desired, 1.0 - exp(-Config.CHASE_YAW_RATE * delta)).normalized()
		_height = lerpf(_height, car_pos.y + Config.CHASE_HEIGHT, 1.0 - exp(-Config.CHASE_HEIGHT_RATE * delta))
	var cam_pos := car_pos - _dir * Config.CHASE_DISTANCE
	cam_pos.y = _height
	look_at_from_position(cam_pos, car_pos + Vector3.UP * Config.CHASE_LOOK_HEIGHT, Vector3.UP)
