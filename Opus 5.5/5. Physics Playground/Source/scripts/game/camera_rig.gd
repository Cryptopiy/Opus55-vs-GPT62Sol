extends Node3D
## P-14 camera: perspective view of the play plane ~15 deg from the front, auto-framed to the level with 10%
## padding (inside the part of the screen not covered by the HUD). MMB orbit (yaw limited to +-45 deg),
## Shift+MMB pan, wheel zoom, F re-frames. Also: camera shake (P-25) and the success zoom (P-13).

const C := preload("res://scripts/core/config.gd")

var cam: Camera3D
## Normalised screen rect (0..1) the level should be framed into (HUD bars are excluded by the session).
var safe_rect := Rect2(0, 0.1, 1, 0.75)

var _bounds := Rect2(-10, -5, 20, 10)
var _yaw := 0.0
var _pitch := C.CAM_PITCH_DEG
var _target := Vector3.ZERO
var _dist := 30.0
var _cur_yaw := 0.0
var _cur_pitch := C.CAM_PITCH_DEG
var _cur_target := Vector3.ZERO
var _cur_dist := 30.0
var _orbiting := false
var _panning := false
var _shake := 0.0
var _shake_t := 0.0


func _ready() -> void:
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	cam = Camera3D.new()
	cam.fov = C.CAM_FOV
	cam.near = 0.1
	cam.far = 400.0
	cam.current = true
	add_child(cam)
	get_viewport().size_changed.connect(_on_resize)


func set_bounds(b: Rect2) -> void:
	_bounds = b


## F / level start: reset orbit + pan and fit the level bounds (optionally without smoothing).
func frame(instant: bool) -> void:
	_yaw = 0.0
	_pitch = C.CAM_PITCH_DEG
	var c := _bounds.get_center()
	_target = Vector3(c.x, c.y, 0)
	_dist = _fit_distance()
	# shift the target so the bounds centre lands in the middle of the safe rect, not the screen centre
	var off := _screen_offset_to_world(safe_rect.get_center() - Vector2(0.5, 0.5), _dist)
	_target -= off
	if instant:
		_snap()


func _snap() -> void:
	_cur_yaw = _yaw
	_cur_pitch = _pitch
	_cur_target = _target
	_cur_dist = _dist
	_apply(0.0)


func _on_resize() -> void:
	if not _orbiting and not _panning and absf(_yaw) < 0.001 and absf(_pitch - C.CAM_PITCH_DEG) < 0.001:
		frame(true)


func _basis_for(yaw_deg: float, pitch_deg: float) -> Basis:
	return Basis.from_euler(Vector3(deg_to_rad(pitch_deg), deg_to_rad(yaw_deg), 0.0), EULER_ORDER_YXZ)


## World offset at the target plane for a normalised screen offset (used to centre inside the safe rect).
func _screen_offset_to_world(n: Vector2, dist: float) -> Vector3:
	var vp := get_viewport().get_visible_rect().size
	var aspect := vp.x / maxf(vp.y, 1.0)
	var half_h := tan(deg_to_rad(C.CAM_FOV) * 0.5) * dist
	var half_w := half_h * aspect
	var b := _basis_for(_yaw, _pitch)
	return b.x * (n.x * 2.0 * half_w) - b.y * (n.y * 2.0 * half_h)


## Binary search for the distance at which all bounds corners fit inside the safe rect shrunk by 10% padding.
func _fit_distance() -> float:
	var vp := get_viewport().get_visible_rect().size
	var aspect := vp.x / maxf(vp.y, 1.0)
	var tan_v := tan(deg_to_rad(C.CAM_FOV) * 0.5)
	var tan_h := tan_v * aspect
	# usable half extents in NDC after HUD exclusion and 10% padding on every side
	var lim_x := safe_rect.size.x * (1.0 - 2.0 * C.CAM_PADDING)
	var lim_y := safe_rect.size.y * (1.0 - 2.0 * C.CAM_PADDING)
	var b := _basis_for(0.0, C.CAM_PITCH_DEG)
	var centre := Vector3(_bounds.get_center().x, _bounds.get_center().y, 0)
	var corners := [_bounds.position, Vector2(_bounds.end.x, _bounds.position.y), _bounds.end, Vector2(_bounds.position.x, _bounds.end.y)]
	var lo := C.CAM_MIN_DIST
	var hi := C.CAM_MAX_DIST * 2.0
	for _i in 40:
		var d := (lo + hi) * 0.5
		var eye := centre + b.z * d
		var ok := true
		for p: Vector2 in corners:
			var rel := Vector3(p.x, p.y, 0) - eye
			var z := -rel.dot(b.z)
			if z <= 0.01:
				ok = false
				break
			var nx := rel.dot(b.x) / (z * tan_h)
			var ny := rel.dot(b.y) / (z * tan_v)
			if absf(nx) > lim_x or absf(ny) > lim_y:
				ok = false
				break
		if ok:
			hi = d
		else:
			lo = d
	return hi


func _unhandled_input(event: InputEvent) -> void:
	var mb := event as InputEventMouseButton
	if mb:
		if mb.button_index == MOUSE_BUTTON_MIDDLE:
			_panning = mb.pressed and mb.shift_pressed
			_orbiting = mb.pressed and not mb.shift_pressed
			get_viewport().set_input_as_handled()
		elif mb.pressed and not mb.ctrl_pressed and (mb.button_index == MOUSE_BUTTON_WHEEL_UP or mb.button_index == MOUSE_BUTTON_WHEEL_DOWN):
			var f := C.CAM_ZOOM_STEP if mb.button_index == MOUSE_BUTTON_WHEEL_UP else 1.0 / C.CAM_ZOOM_STEP
			_dist = clampf(_dist * f, C.CAM_MIN_DIST, C.CAM_MAX_DIST)
			get_viewport().set_input_as_handled()
		return
	var mm := event as InputEventMouseMotion
	if mm and (_orbiting or _panning):
		if _orbiting:
			_yaw = clampf(_yaw - mm.relative.x * C.CAM_ORBIT_SPEED, -C.CAM_ORBIT_YAW_LIMIT, C.CAM_ORBIT_YAW_LIMIT)
			_pitch = clampf(_pitch - mm.relative.y * C.CAM_ORBIT_SPEED, C.CAM_ORBIT_PITCH_MIN, C.CAM_ORBIT_PITCH_MAX)
		else:
			var vp_h := get_viewport().get_visible_rect().size.y
			var world_per_px := 2.0 * tan(deg_to_rad(C.CAM_FOV) * 0.5) * _dist / maxf(vp_h, 1.0)
			var b := _basis_for(_yaw, _pitch)
			_target += (-b.x * mm.relative.x + b.y * mm.relative.y) * world_per_px
		get_viewport().set_input_as_handled()


func shake(amount: float) -> void:
	_shake = clampf(maxf(_shake, amount), 0.0, C.SHAKE_MAX)


## P-13 success: slight zoom toward the goal.
func success_zoom(focus: Vector2) -> void:
	_dist *= C.SUCCESS_ZOOM
	_target = _target.lerp(Vector3(focus.x, focus.y, 0), 0.3)


func _process(delta: float) -> void:
	# camera motion uses real time so it stays responsive at x0.25 / x2 simulation speed
	var real := delta / maxf(Engine.time_scale, 0.001)
	_apply(real)


func _apply(real_delta: float) -> void:
	var k := 1.0 - exp(-C.CAM_SMOOTH * real_delta) if real_delta > 0.0 else 1.0
	_cur_yaw = lerpf(_cur_yaw, _yaw, k)
	_cur_pitch = lerpf(_cur_pitch, _pitch, k)
	_cur_target = _cur_target.lerp(_target, k)
	_cur_dist = lerpf(_cur_dist, _dist, k)
	var b := _basis_for(_cur_yaw, _cur_pitch)
	var pos := _cur_target + b.z * _cur_dist
	if _shake > 0.0001:
		_shake_t += real_delta
		var off := Vector3(sin(_shake_t * 61.0) + 0.5 * sin(_shake_t * 137.0), cos(_shake_t * 53.0) + 0.5 * sin(_shake_t * 97.0), 0.0)
		pos += b * off * _shake
		_shake = maxf(0.0, _shake - C.SHAKE_DECAY * _shake * real_delta - 0.01 * real_delta)
	cam.global_transform = Transform3D(b, pos)


## Mouse position projected onto the play plane z = 0.
func screen_to_plane(screen_pos: Vector2) -> Vector2:
	var o := cam.project_ray_origin(screen_pos)
	var n := cam.project_ray_normal(screen_pos)
	if absf(n.z) < 0.00001:
		return Vector2(o.x, o.y)
	var t := -o.z / n.z
	var p := o + n * t
	return Vector2(p.x, p.y)
