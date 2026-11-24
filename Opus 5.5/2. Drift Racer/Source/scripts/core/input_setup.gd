class_name InputSetup
extends RefCounted
## Builds the input map in code (no editor setup needed). Physical key codes
## keep WASD on the same keys for every keyboard layout. Gamepad bindings
## (left stick + triggers) are the OPTIONAL part of R-12.

const THROTTLE := &"throttle"
const BRAKE := &"brake"
const STEER_LEFT := &"steer_left"
const STEER_RIGHT := &"steer_right"
const HANDBRAKE := &"handbrake"
const RESPAWN := &"respawn"
const CAMERA := &"camera_toggle"
const ASSIST := &"assist_toggle"
const DEBUG := &"debug_overlay"
const SCREENSHOT := &"screenshot"
const PAUSE := &"pause"
const TOGGLE_UI := &"toggle_ui"

const AXIS_DEADZONE := 0.15


static func register() -> void:
	_keys(THROTTLE, [KEY_W, KEY_UP])
	_keys(BRAKE, [KEY_S, KEY_DOWN])
	_keys(STEER_LEFT, [KEY_A, KEY_LEFT])
	_keys(STEER_RIGHT, [KEY_D, KEY_RIGHT])
	_keys(HANDBRAKE, [KEY_SPACE])
	_keys(RESPAWN, [KEY_R])
	_keys(CAMERA, [KEY_C])
	_keys(ASSIST, [KEY_F3])
	_keys(DEBUG, [KEY_F1])
	_keys(SCREENSHOT, [KEY_F12])
	_keys(PAUSE, [KEY_ESCAPE])
	_keys(TOGGLE_UI, [KEY_F8])
	_axis(STEER_LEFT, JOY_AXIS_LEFT_X, -1.0)
	_axis(STEER_RIGHT, JOY_AXIS_LEFT_X, 1.0)
	_axis(THROTTLE, JOY_AXIS_TRIGGER_RIGHT, 1.0)
	_axis(BRAKE, JOY_AXIS_TRIGGER_LEFT, 1.0)
	_button(HANDBRAKE, JOY_BUTTON_A)
	_button(RESPAWN, JOY_BUTTON_Y)
	_button(CAMERA, JOY_BUTTON_BACK)
	_button(PAUSE, JOY_BUTTON_START)


static func _ensure(action: StringName) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action, AXIS_DEADZONE)


static func _keys(action: StringName, keys: Array) -> void:
	_ensure(action)
	for k: Key in keys:
		var ev := InputEventKey.new()
		ev.physical_keycode = k
		InputMap.action_add_event(action, ev)


static func _axis(action: StringName, axis: JoyAxis, value: float) -> void:
	_ensure(action)
	var ev := InputEventJoypadMotion.new()
	ev.axis = axis
	ev.axis_value = value
	InputMap.action_add_event(action, ev)


static func _button(action: StringName, button: JoyButton) -> void:
	_ensure(action)
	var ev := InputEventJoypadButton.new()
	ev.button_index = button
	InputMap.action_add_event(action, ev)
