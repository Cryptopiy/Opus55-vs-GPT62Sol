class_name CircuitInterface
extends CanvasLayer

const C = preload("res://game/config.gd")
const S = preload("res://game/ui_style.gd")

var game: Node3D
var hud := RaceHUD.new()
var shell := Control.new()
var gameplay_overlay := Control.new()
var world_overlay := Node3D.new()
var menu: Control
var pause_menu: Control
var results_menu: Control
var results_rows: VBoxContainer
var results_drift: Label
var results_seed: Label
var difficulty: OptionButton
var seed_field: LineEdit
var start_button: Button
var seed_error: Label
var course_length: Label
var debug_back: PanelContainer
var debug_label: Label
var strong := S.font(0.56)
var title := S.font(1.10, true)
var _last := -1
var _refresh_in := 0.0

func setup(owner_game: Node3D) -> void:
	game = owner_game
	process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(shell)
	shell.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shell.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shell.theme = S.theme()
	_build_menu()
	_build_pause()
	_build_results()
	# Mount transient gameplay UI here so it inherits visibility even after the track is rebuilt.
	shell.add_child(gameplay_overlay)
	gameplay_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	gameplay_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(world_overlay)
	gameplay_overlay.add_child(hud)
	hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	debug_back = PanelContainer.new()
	debug_back.add_theme_stylebox_override("panel", S.panel(S.BACK, S.RULE, 2))
	debug_back.mouse_filter = Control.MOUSE_FILTER_IGNORE
	gameplay_overlay.add_child(debug_back)
	debug_back.position = Vector2(788.0, 31.0)
	debug_label = _label("", 18, S.PAPER)
	debug_back.add_child(debug_label)
	world_changed()
	_update_screen()

func world_changed() -> void:
	hud.setup(game.race, game.skids)
	if is_instance_valid(seed_field):
		seed_field.text = str(game.race.seed_value)
		difficulty.select(game.race.difficulty)
		course_length.text = "%.2f KM  /  %d LAPS" % [game.race.track.data.length / 1000.0, C.LAPS]
		seed_error.text = ""

func toggle_debug() -> void:
	hud.debug = not hud.debug
	_refresh_in = 0.0

func toggle_gameplay_ui() -> void:
	gameplay_overlay.visible = not gameplay_overlay.visible
	world_overlay.visible = gameplay_overlay.visible

func _process(delta: float) -> void:
	if _last != game.screen:
		_update_screen()
	_refresh_in -= delta
	if _refresh_in > 0.0:
		return
	_refresh_in = 0.065
	hud.countersteer = game.countersteer
	if hud.visible or hud.debug:
		hud.refresh()
	debug_back.visible = hud.debug and not hud.visible
	if debug_back.visible:
		debug_label.text = hud.debug_text

func _update_screen() -> void:
	_last = game.screen
	menu.visible = _last == game.Screen.MENU
	pause_menu.visible = _last == game.Screen.PAUSE
	results_menu.visible = _last == game.Screen.RESULTS
	hud.visible = _last == game.Screen.RACE
	if results_menu.visible:
		_fill_results()
	Input.mouse_mode = Input.MOUSE_MODE_HIDDEN if hud.visible else Input.MOUSE_MODE_VISIBLE

func _base() -> Control:
	var layer := Control.new()
	shell.add_child(layer)
	layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return layer

func _build_menu() -> void:
	menu = _base()
	var tint := _shade(Color(0.009, 0.03, 0.041, 0.98), Color(0.017, 0.06, 0.063, 0.9), Color(0.024, 0.060, 0.06, 0.02))
	menu.add_child(tint)
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var stack := VBoxContainer.new()
	menu.add_child(stack)
	stack.set_anchors_and_offsets_preset(Control.PRESET_CENTER_LEFT)
	stack.grow_vertical = Control.GROW_DIRECTION_BOTH
	stack.position.x = 102.0
	stack.custom_minimum_size.x = 552.0
	stack.add_theme_constant_override("separation", 0)
	var marker := _label("T I M B E R L I N E   C I R C U I T", 19, S.MINT, strong)
	stack.add_child(marker)
	_space(stack, 12)
	var logo := _label("DRIFT\nRACER", 116, S.PAPER, title)
	logo.add_theme_constant_override("line_spacing", -29)
	stack.add_child(logo)
	_space(stack, 19)
	course_length = _label("", 20, S.MUTED, strong)
	stack.add_child(course_length)
	_space(stack, 54)
	var options := HBoxContainer.new()
	options.add_theme_constant_override("separation", 18)
	stack.add_child(options)
	var seed := VBoxContainer.new()
	seed.add_theme_constant_override("separation", 8)
	seed.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var skill := VBoxContainer.new()
	skill.add_theme_constant_override("separation", 8)
	skill.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	options.add_child(seed)
	options.add_child(skill)
	seed.add_child(_label("TRACK SEED", 16, S.MUTED, strong))
	skill.add_child(_label("DIFFICULTY", 16, S.MUTED, strong))
	seed_field = LineEdit.new()
	seed_field.max_length = 20
	seed_field.custom_minimum_size = Vector2(207.0, 61.0)
	seed_field.text_submitted.connect(func(_value: String) -> void: _start())
	seed.add_child(seed_field)
	difficulty = OptionButton.new()
	for name_text: String in ["Easy", "Normal", "Hard"]:
		difficulty.add_item(name_text)
	difficulty.select(1)
	difficulty.custom_minimum_size = Vector2(207.0, 61.0)
	skill.add_child(difficulty)
	_space(stack, 16)
	start_button = _button("Start Race    ›", _start, true)
	stack.add_child(start_button)
	seed_error = _label("", 17, S.WARN)
	stack.add_child(seed_error)
	var quit_button := _button("Quit", game.quit_game)
	quit_button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	quit_button.custom_minimum_size.x = 139.0
	stack.add_child(quit_button)
	var guide := _label("W A S D  Drive     SPACE  Handbrake     C  Camera     R  Reset", 18, S.PAPER, strong)
	menu.add_child(guide)
	guide.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	guide.position += Vector2(102.0, -42.0)

func _build_pause() -> void:
	pause_menu = _base()
	var stack := _modal(pause_menu, 470.0)
	stack.add_child(_label("TIMBERLINE CIRCUIT", 17, S.MINT, strong))
	stack.add_child(_label("PAUSED", 70, S.PAPER, title))
	_space(stack, 7)
	stack.add_child(_button("Resume", game.resume_race, true))
	stack.add_child(_button("Restart", game.restart_race))
	stack.add_child(_button("Menu", func() -> void: game.go_to_menu(game.race.seed_value)))
	stack.add_child(_button("Quit", game.quit_game))

func _build_results() -> void:
	results_menu = _base()
	var stack := _modal(results_menu, 934.0)
	results_seed = _label("", 17, S.MINT, strong)
	stack.add_child(results_seed)
	stack.add_child(_label("RACE RESULTS", 76, S.PAPER, title))
	_space(stack, 7)
	stack.add_child(_result_row("POS", "DRIVER", "TOTAL TIME", "BEST LAP", S.MUTED, true))
	results_rows = VBoxContainer.new()
	results_rows.add_theme_constant_override("separation", 5)
	stack.add_child(results_rows)
	results_drift = _label("", 21, S.MINT, strong)
	stack.add_child(results_drift)
	_space(stack, 18)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 14)
	stack.add_child(actions)
	_add_action(actions, _button("Restart", game.restart_race, true))
	_add_action(actions, _button("New seed", _new_seed))
	_add_action(actions, _button("Menu", func() -> void: game.go_to_menu(game.race.seed_value)))

func _fill_results() -> void:
	for child in results_rows.get_children():
		results_rows.remove_child(child)
		child.queue_free()
	var order: Array[DriftCar] = game.race.standings()
	for index in order.size():
		var car := order[index]
		results_rows.add_child(_result_row("%02d" % (index + 1), C.DRIVER_NAMES[car.driver_id],
			RaceHUD.stamp(car.finish_time), RaceHUD.stamp(car.best_lap), car.paint, false))
	results_seed.text = "TIMBERLINE CIRCUIT  /  SEED %d" % game.race.seed_value
	results_drift.text = "DRIFT SCORE   %d" % int(game.race.drift.total)

func _result_row(position_text: String, driver: String, total: String, best: String,
		color: Color, header: bool) -> PanelContainer:
	var inside := HBoxContainer.new()
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", S.panel(S.INK if header else Color("#163039"), S.RULE, 2))
	panel.add_child(inside)
	var font_size := 15 if header else 24
	for i in 4:
		var entry := _label([position_text, driver, total, best][i], font_size, color if i < 2 else S.PAPER, strong)
		entry.custom_minimum_size.x = [82.0, 275.0, 245.0, 200.0][i]
		inside.add_child(entry)
	return panel

func _modal(layer: Control, width: float) -> VBoxContainer:
	var tint := ColorRect.new()
	tint.color = Color(0.01, 0.027, 0.038, 0.69)
	layer.add_child(tint)
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var centered := CenterContainer.new()
	layer.add_child(centered)
	centered.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var panel := PanelContainer.new()
	centered.add_child(panel)
	var style := S.panel(S.BACK, S.RULE, 5)
	style.border_width_top = 3
	style.border_color = S.MINT
	style.content_margin_left = 33.0
	style.content_margin_right = 33.0
	style.content_margin_top = 30.0
	style.content_margin_bottom = 34.0
	panel.add_theme_stylebox_override("panel", style)
	var stack := VBoxContainer.new()
	stack.custom_minimum_size.x = width
	stack.add_theme_constant_override("separation", 12)
	panel.add_child(stack)
	return stack

func _start() -> void:
	var input := seed_field.text.strip_edges()
	if not input.is_valid_int():
		seed_error.text = "Enter a whole number."
		seed_field.grab_focus()
		return
	seed_error.text = ""
	game.start_race(int(input), difficulty.selected)

func _new_seed() -> void:
	var last_seed: int = game.race.seed_value
	game.go_to_menu(0 if last_seed == 9223372036854775807 else last_seed + 1)
	seed_field.grab_focus.call_deferred()

func _shade(dark: Color, middle: Color, transparent: Color) -> TextureRect:
	var ramp := Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.35, 0.77])
	ramp.colors = PackedColorArray([dark, middle, transparent])
	var texture := GradientTexture2D.new()
	texture.width = 1024
	texture.height = 4
	texture.gradient = ramp
	texture.fill_to = Vector2(1.0, 0.0)
	var veil := TextureRect.new()
	veil.texture = texture
	veil.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return veil

func _label(words: String, size_px: int, color: Color, face: Font = null) -> Label:
	var text := Label.new()
	text.text = words
	text.add_theme_font_size_override("font_size", size_px)
	text.add_theme_color_override("font_color", color)
	if face != null:
		text.add_theme_font_override("font", face)
	return text

func _button(words: String, action: Callable, primary := false) -> Button:
	var button := Button.new()
	button.text = words
	button.custom_minimum_size.y = 62.0
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.add_theme_font_override("font", strong)
	button.pressed.connect(action)
	if primary:
		for mode: StringName in [&"font_color", &"font_hover_color", &"font_focus_color", &"font_pressed_color"]:
			button.add_theme_color_override(mode, S.INK)
		button.add_theme_stylebox_override("normal", S.panel(S.MINT, S.MINT))
		button.add_theme_stylebox_override("hover", S.panel(Color("#a1ffe4"), S.MINT))
		button.add_theme_stylebox_override("pressed", S.panel(Color("#68dbb8"), S.MINT))
		button.custom_minimum_size.y = 66.0
	return button

func _add_action(row: HBoxContainer, button: Button) -> void:
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(button)

func _space(box: VBoxContainer, height: float) -> void:
	var gap := Control.new()
	gap.custom_minimum_size.y = height
	gap.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(gap)
