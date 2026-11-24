class_name LabViewRig
extends Camera3D

const C = preload("res://scripts/config.gd")
var center := Vector3.ZERO
var frame_bounds := Rect2()
var yaw := C.CAMERA_YAW
var pitch := C.CAMERA_PITCH
var distance := 38.0
var zoom_factor := 1.0
var kick := 0.0
var phase := 0.0
var celebration := 0.0
var win_target := false
var orbiting := false

func _ready() -> void:
	fov = C.CAMERA_FOV
	current = true
	far = 120
	get_viewport().size_changed.connect(_fit)

func frame(bounds: Rect2) -> void:
	frame_bounds = bounds
	yaw = C.CAMERA_YAW
	pitch = C.CAMERA_PITCH
	zoom_factor = 1.0
	center = C.in_plane(frame_bounds.get_center())
	win_target = false
	celebration = 0
	_fit()

func _fit() -> void:
	var screen := get_viewport().get_visible_rect().size
	var ratio := maxf(0.5, screen.x / maxf(screen.y, 1.0))
	var half_tan := tan(deg_to_rad(fov * 0.5))
	var x_scale := half_tan * ratio * C.FRAME_SAFE_WIDTH
	var y_scale := half_tan * C.FRAME_SAFE_HEIGHT
	var angle := deg_to_rad(C.CAMERA_YAW)
	var tilt := deg_to_rad(C.CAMERA_PITCH)
	var outward := Vector3(sin(angle) * cos(tilt), sin(tilt), cos(angle) * cos(tilt))
	var right := Vector3(cos(angle), 0.0, -sin(angle))
	var up := outward.cross(right)
	var half := frame_bounds.size * 0.5
	distance = 8.0
	# Fit the closest edges in perspective and reserve room for both toolbars.
	for x in [-half.x, half.x]:
		for y in [-half.y, half.y]:
			for z in [-0.9, 0.9]:
				var point := Vector3(x, y, z)
				var scale_needed := maxf(absf(point.dot(right)) / x_scale, absf(point.dot(up)) / y_scale)
				distance = maxf(distance, scale_needed * C.CAMERA_PADDING + point.dot(outward))
	_refresh()

func _process(delta: float) -> void:
	if kick > 0.001 or win_target or celebration > 0.001:
		phase += delta * 58.0
		kick = move_toward(kick, 0.0, delta * 0.30)
		celebration = move_toward(celebration, 0.075 if win_target else 0.0, delta * 0.15)
		_refresh()

func _refresh() -> void:
	var horizontal := deg_to_rad(yaw)
	var vertical := deg_to_rad(pitch)
	var outward := Vector3(sin(horizontal) * cos(vertical), sin(vertical), cos(horizontal) * cos(vertical))
	var shake := Vector3(sin(phase) * kick, cos(phase * 1.19) * kick * 0.65, 0)
	position = center + outward * distance * zoom_factor * (1.0 - celebration) + shake
	if position.distance_squared_to(center) > 0.01:
		look_at(center + shake)

func pointer(event: InputEvent) -> bool:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_MIDDLE:
			orbiting = event.pressed
			return true
		if event.pressed and not event.ctrl_pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			zoom_factor = clampf(zoom_factor * (0.89 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.12), 0.49, 2.0)
			_refresh()
			return true
	if event is InputEventMouseMotion and orbiting:
		if event.shift_pressed:
			var scale_factor := distance * zoom_factor / maxf(get_viewport().get_visible_rect().size.y, 1.0) * 0.73
			center -= (basis.x * event.relative.x - basis.y * event.relative.y) * scale_factor
			center.z = 0
		else:
			yaw = clampf(yaw - event.relative.x * 0.23, -C.CAMERA_LIMIT, C.CAMERA_LIMIT)
			pitch = clampf(pitch + event.relative.y * 0.20, -6.0, 35.0)
		_refresh()
		return true
	return false

func projected(pixel: Vector2) -> Vector2:
	var ray := project_ray_normal(pixel)
	if absf(ray.z) < 0.0001:
		return Vector2(1000, 1000)
	var origin := project_ray_origin(pixel)
	return LabGeometry.planar(origin - ray * origin.z / ray.z)
