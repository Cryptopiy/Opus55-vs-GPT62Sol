extends Node
## Entry point (main.tscn): everything is built in code. Screens: level select <-> level session.
## Global keys: F1 debug overlay, F12 screenshot, Esc pause menu, F10 dev menu, F8 hide/show all UI.
## Layers: "UI" (HUD) and "TopUI" (F1/F10) are hideable via UIVisibility; "Screens" (level select, pause menu,
## results) is never hidden.

const C := preload("res://scripts/core/config.gd")
const Style := preload("res://scripts/ui/ui_style.gd")
const Overlays := preload("res://scripts/ui/overlays.gd")
const LevelSelect := preload("res://scripts/ui/level_select.gd")
const LevelSession := preload("res://scripts/game/level_session.gd")
const DebugOverlay := preload("res://scripts/ui/debug_overlay.gd")
const DevMenu := preload("res://scripts/ui/dev_menu.gd")
const DevTools := preload("res://scripts/game/dev_tools.gd")
const UIVisibility := preload("res://scripts/ui/ui_visibility.gd")

var session: LevelSession
var dev_tools: DevTools
var ui_root: Control
var screens_root: Control
var ui_visibility: UIVisibility
var _level_select: Control
var _pause_menu: Control
var _debug: DebugOverlay
var _dev_menu: DevMenu


func _ready() -> void:
	get_tree().auto_accept_quit = false
	DisplayServer.window_set_title("Contraption Lab")
	get_window().min_size = Vector2i(960, 540)
	var ui := CanvasLayer.new()
	ui.name = "UI"
	add_child(ui)
	ui_root = Control.new()
	ui_root.name = "UIRoot"
	ui_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui_root.theme = Style.make_theme()
	ui.add_child(ui_root)
	var top := CanvasLayer.new()
	top.name = "TopUI"
	top.layer = 10
	add_child(top)
	var top_root := Control.new()
	top_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	top_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_root.theme = ui_root.theme
	top.add_child(top_root)
	var screens := CanvasLayer.new()
	screens.name = "Screens"
	screens.layer = 20
	add_child(screens)
	screens_root = Control.new()
	screens_root.name = "ScreensRoot"
	screens_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screens_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	screens_root.theme = ui_root.theme
	screens.add_child(screens_root)
	ui_visibility = UIVisibility.new()
	ui_visibility.name = "UIVisibility"
	add_child(ui_visibility)
	ui_visibility.register_root(ui_root)
	ui_visibility.register_root(top_root)
	dev_tools = DevTools.new()
	dev_tools.name = "DevTools"
	add_child(dev_tools)
	_debug = DebugOverlay.new()
	_debug.main = self
	top_root.add_child(_debug)
	_dev_menu = DevMenu.new()
	_dev_menu.main = self
	top_root.add_child(_dev_menu)
	show_level_select()
	var mode := _arg_value("--autotest")
	if mode != "":
		var script_path := "res://tests/autotest.gd"
		if mode == "screens":
			script_path = "res://tests/screens.gd"
		elif mode == "play":
			script_path = "res://tests/playbot.gd"
		var at: Node = load(script_path).new()
		at.name = "AutoTest"
		add_child(at)


static func _arg_value(key: String) -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with(key + "="):
			return a.substr(key.length() + 1)
	return ""


func show_level_select() -> void:
	_close_pause_menu()
	if session:
		session.queue_free()
		remove_child(session)
		session = null
	_dev_menu.visible = false
	_level_select = LevelSelect.new()
	screens_root.add_child(_level_select)
	_level_select.level_chosen.connect(func(i: int) -> void: open_level.call_deferred(i))


func open_level(index: int) -> LevelSession:
	_close_pause_menu()
	if is_instance_valid(_level_select):
		_level_select.queue_free()
	if session:
		session.queue_free()
		remove_child(session)
	session = LevelSession.new()
	add_child(session)
	session.setup(index, ui_root, screens_root)
	session.exit_requested.connect(func() -> void: show_level_select.call_deferred())
	session.next_requested.connect(func(i: int) -> void: open_level.call_deferred(i))
	return session


func _input(event: InputEvent) -> void:
	var k := event as InputEventKey
	if not k or not k.pressed or k.echo:
		return
	match k.keycode:
		KEY_F1:
			_debug.visible = not _debug.visible
		KEY_F8:
			ui_visibility.toggle()
		KEY_F12:
			take_screenshot()
		KEY_F10:
			if session:
				_dev_menu.visible = not _dev_menu.visible
		KEY_ESCAPE:
			if _pause_menu:
				_close_pause_menu()
			elif session:
				_open_pause_menu()
		_:
			return
	get_viewport().set_input_as_handled()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		quit_game()


## Stops all sounds and gives the audio thread a moment to release them, then quits (clean exit, no leaks).
func quit_game(code: int = 0) -> void:
	Sfx.stop_all()
	PhysicsServer3D.set_active(false)
	await get_tree().create_timer(0.2, true, false, true).timeout
	get_tree().quit(code)


func _open_pause_menu() -> void:
	_pause_menu = Overlays.pause_menu(_close_pause_menu, _on_restart, func() -> void: quit_game())
	screens_root.add_child(_pause_menu)
	session.set_menu_open(true)


func _close_pause_menu() -> void:
	if _pause_menu:
		_pause_menu.queue_free()
		_pause_menu = null
		if session:
			session.set_menu_open(false)


func _on_restart() -> void:
	_close_pause_menu()
	if session:
		session.restart_level()


## F12: PNG of the current frame into <project>/Screenshots/ with a timestamp in the name.
func take_screenshot() -> String:
	await RenderingServer.frame_post_draw
	var tex := get_viewport().get_texture()
	if tex == null:
		return ""
	var img := tex.get_image()
	if img == null:
		return ""
	var dir := ProjectSettings.globalize_path("res://").path_join(C.SCREENSHOT_DIR)
	DirAccess.make_dir_recursive_absolute(dir)
	var stamp := Time.get_datetime_string_from_system().replace(":", "-").replace("T", "_")
	var path := dir.path_join("screenshot_%s_%03d.png" % [stamp, Time.get_ticks_msec() % 1000])
	var err := img.save_png(path)
	if session and err == OK:
		session.hud.flash("Screenshot saved: Screenshots/" + path.get_file())
	print("Screenshot: ", path, " (", error_string(err), ")")
	return path
