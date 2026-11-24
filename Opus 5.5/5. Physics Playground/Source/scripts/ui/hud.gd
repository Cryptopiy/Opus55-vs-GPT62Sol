extends Control
## P-20 level HUD: level name + objective, Play/Pause, Reset, time-scale buttons, Hint, Levels; timer
## (simulation time), objects used / par, par time, live stars preview; message banner; inventory bar (P-15);
## results panel and tutorial overlay. Anchored containers keep it intact at any size / aspect (P-29).
## The results panel goes to the never-hidden screens layer (U-02): F8 can hide the HUD but not that screen.

const C := preload("res://scripts/core/config.gd")
const Style := preload("res://scripts/ui/ui_style.gd")
const Widgets := preload("res://scripts/ui/widgets.gd")
const InventoryBar := preload("res://scripts/ui/inventory_bar.gd")
const Overlays := preload("res://scripts/ui/overlays.gd")

var session: Node

var _play_btn: Button
var _speed_btns: Array[Button] = []
var _hint_btn: Button
var _mode_label: Label
var _time_label: Label
var _objects_label: Label
var _par_time_label: Label
var _stars: Widgets.StarRow
var _banner: PanelContainer
var _banner_label: Label
var _banner_timer := 0.0
var _inventory: InventoryBar
var _modal: Control
var _screens: Control


func setup(p_session: Node, screens_parent: Control) -> void:
	session = p_session
	_screens = screens_parent
	name = "HUD"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var lvl: Dictionary = session.level
	var top := HBoxContainer.new()
	top.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	top.offset_left = 12
	top.offset_right = -12
	top.offset_top = 10
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_theme_constant_override("separation", 12)
	add_child(top)
	# left: name + objective
	var left := Style.panel()
	left.custom_minimum_size = Vector2(380, 0)
	var lv := VBoxContainer.new()
	lv.mouse_filter = Control.MOUSE_FILTER_IGNORE
	left.add_child(lv)
	lv.add_child(Style.label("Level %d - %s" % [session.level_index + 1, lvl["name"]], 24))
	var obj := Style.label(lvl["objective"], 15, Style.TEXT_DIM)
	obj.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	obj.custom_minimum_size = Vector2(380, 0)
	lv.add_child(obj)
	top.add_child(left)
	top.add_child(_spacer())
	# centre: controls
	var mid := Style.panel()
	var mh := HBoxContainer.new()
	mh.add_theme_constant_override("separation", 6)
	mid.add_child(mh)
	_play_btn = Style.button("Play", _on_play, 96)
	_play_btn.tooltip_text = "Play / pause the simulation (Space)"
	mh.add_child(_play_btn)
	var reset_btn := Style.button("Reset", _on_reset, 80)
	reset_btn.tooltip_text = "Back to build mode, everything at its placed position (R)"
	mh.add_child(reset_btn)
	for i in C.TIME_SCALES.size():
		var s: float = C.TIME_SCALES[i]
		var b := Style.button("x%s" % ("0.25" if s < 1.0 else str(int(s))), _on_speed.bind(i), 58)
		b.toggle_mode = true
		b.tooltip_text = "Simulation speed ([ and ])"
		mh.add_child(b)
		_speed_btns.append(b)
	_hint_btn = Style.button("Hint", func() -> void: session.show_next_hint(), 70)
	mh.add_child(_hint_btn)
	var levels := Style.button("Levels", func() -> void: session.request_exit(), 80)
	levels.tooltip_text = "Back to the level select screen"
	mh.add_child(levels)
	_mode_label = Style.label("BUILD", 16, Style.ACCENT)
	_mode_label.custom_minimum_size = Vector2(96, 0)
	_mode_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_mode_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	mh.add_child(_mode_label)
	top.add_child(mid)
	top.add_child(_spacer())
	# right: timer / objects / stars
	var right := Style.panel()
	right.custom_minimum_size = Vector2(230, 0)
	var rv := VBoxContainer.new()
	rv.mouse_filter = Control.MOUSE_FILTER_IGNORE
	right.add_child(rv)
	_time_label = Style.label("Time 0.00 s", 20)
	rv.add_child(_time_label)
	_objects_label = Style.label("", 16, Style.TEXT_DIM)
	rv.add_child(_objects_label)
	_par_time_label = Style.label("Par time %.1f s" % float(lvl["par_time"]), 16, Style.TEXT_DIM)
	rv.add_child(_par_time_label)
	_stars = Widgets.StarRow.new(22.0)
	rv.add_child(_stars)
	top.add_child(right)
	# banner: a full-width centre container keeps the message centred at any width
	var banner_row := CenterContainer.new()
	banner_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(banner_row)
	banner_row.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	banner_row.offset_top = 150
	banner_row.offset_bottom = 200
	_banner = Style.panel()
	_banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_banner_label = Style.label("", 18)
	_banner_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_banner.add_child(_banner_label)
	_banner.visible = false
	banner_row.add_child(_banner)
	# inventory bar centred along the bottom edge
	var bottom := CenterContainer.new()
	bottom.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bottom)
	bottom.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	bottom.offset_top = -122
	bottom.offset_bottom = -10
	_inventory = InventoryBar.new()
	bottom.add_child(_inventory)
	_inventory.setup(session)
	if lvl.get("tutorial", false) and not Save.tutorial_done():
		var tut := Overlays.tutorial(func() -> void: Save.set_tutorial_done())
		add_child(tut)
	on_state_changed()


func _on_play() -> void:
	session.toggle_play()
	on_state_changed()


func _on_reset() -> void:
	session.reset()
	on_state_changed()


func _on_speed(i: int) -> void:
	session.set_time_scale_index(i)
	on_state_changed()


func _spacer() -> Control:
	var s := Control.new()
	s.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	s.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return s


## Part of the screen the camera frames the level into (between the top bar and the inventory bar).
func safe_rect() -> Rect2:
	return Rect2(0.0, 0.14, 1.0, 0.71)


func flash(text: String) -> void:
	show_banner(text, false)
	_banner_timer = 2.6


func show_banner(text: String, sticky: bool) -> void:
	_banner_label.text = text
	_banner_label.add_theme_color_override("font_color", Style.BAD if sticky else Style.TEXT)
	_banner.visible = true
	_banner_timer = -1.0 if sticky else 2.6


func on_state_changed() -> void:
	var st: int = session.state
	var names := ["BUILD", "RUNNING", "PAUSED", "COMPLETE", "FAILED"]
	var cols := [Style.ACCENT, Style.GOOD, Style.STAR, Style.GOOD, Style.BAD]
	_mode_label.text = names[st]
	_mode_label.add_theme_color_override("font_color", cols[st])
	_play_btn.text = "Pause" if st == 1 else "Play"
	_play_btn.disabled = st >= 3
	for i in _speed_btns.size():
		_speed_btns[i].set_pressed_no_signal(i == session.time_scale_index)
	if st == 0:
		_banner_timer = 0.0
		_banner.visible = false
		_close_modal()
	_inventory.refresh()


func show_results(stars: int, objects: int, time: float) -> void:
	_close_modal()
	var lvl: Dictionary = session.level
	var has_next: bool = session.level_index + 1 < C.LEVELS.size()
	_modal = Overlays.results(stars, objects, int(lvl["par"]), time, float(lvl["par_time"]), Save.best_stars(String(lvl["id"])), has_next,
		func() -> void: session.request_next(),
		_on_reset,
		func() -> void: session.request_exit())
	_screens.add_child(_modal)


func _close_modal() -> void:
	if is_instance_valid(_modal):
		_modal.queue_free()
	_modal = null


func _exit_tree() -> void:
	# the results panel lives on the screens layer, not below the HUD: free it with the HUD
	_close_modal()


func _process(delta: float) -> void:
	var real := delta / maxf(Engine.time_scale, 0.001)
	var st: int = session.state
	var t: float = session.last_time if st == 3 else (session.sim.sim_time() if session.sim else 0.0)
	_time_label.text = "Time %.2f s" % t
	var lvl: Dictionary = session.level
	var used: int = session.model.count()
	_objects_label.text = "Objects %d / par %d" % [used, int(lvl["par"])]
	_objects_label.add_theme_color_override("font_color", Style.TEXT_DIM if used <= int(lvl["par"]) else Style.BAD)
	_par_time_label.add_theme_color_override("font_color", Style.TEXT_DIM if t <= float(lvl["par_time"]) else Style.BAD)
	_stars.set_filled(session.preview_stars())
	var avail: bool = session.hint_available()
	_hint_btn.disabled = not avail
	var left: float = maxf(0.0, C.HINT_DELAY - session.level_time)
	_hint_btn.tooltip_text = "Show one object of the reference solution" if avail else ("Hint unlocks in %d s" % int(ceil(left)) if left > 0.0 else "All hints shown")
	if _banner_timer > 0.0:
		_banner_timer -= real
		if _banner_timer <= 0.0:
			_banner.visible = false
