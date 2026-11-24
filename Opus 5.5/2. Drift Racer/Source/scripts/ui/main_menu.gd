class_name MainMenu
extends CanvasLayer
## R-28 main menu: Start Race, track seed (typed number or -/+ buttons, default
## 2024), difficulty (Easy / Normal / Hard) and Quit.

signal start_requested(seed_value: int, difficulty: int)
signal quit_requested

var _seed := Config.DEFAULT_SEED
var _seed_edit: LineEdit
var _difficulty: OptionButton
var _start: Button


func _ready() -> void:
	layer = 10
	process_mode = Node.PROCESS_MODE_ALWAYS
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = UiStyle.theme()
	add_child(root)
	var bg := TextureRect.new()
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.texture = _background_texture()
	root.add_child(bg)
	for i in 3:
		var stripe := ColorRect.new()
		stripe.color = Color(1.0, 0.55, 0.1, 0.10 - 0.025 * float(i))
		stripe.position = Vector2(-400.0, 620.0 + 70.0 * float(i))
		stripe.size = Vector2(3200.0, 26.0)
		stripe.rotation = deg_to_rad(-14.0)
		stripe.mouse_filter = Control.MOUSE_FILTER_IGNORE
		root.add_child(stripe)

	var column := VBoxContainer.new()
	column.set_anchors_preset(Control.PRESET_CENTER)
	column.grow_horizontal = Control.GROW_DIRECTION_BOTH
	column.grow_vertical = Control.GROW_DIRECTION_BOTH
	column.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_theme_constant_override("separation", 18)
	root.add_child(column)
	var title := UiStyle.label("DRIFT RACER", 96, UiStyle.ACCENT, 14)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(title)
	var sub := UiStyle.label("procedural circuit  |  raycast-suspension physics  |  3 AI rivals  |  3 laps", 20, UiStyle.DIM, 4)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(sub)

	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	column.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 14)
	panel.add_child(box)
	_start = UiStyle.button("START RACE", 460.0)
	_start.pressed.connect(_on_start)
	box.add_child(_start)

	var seed_row := HBoxContainer.new()
	seed_row.add_theme_constant_override("separation", 8)
	var seed_label := UiStyle.label("TRACK SEED", 20, UiStyle.DIM, 4)
	seed_label.custom_minimum_size = Vector2(150.0, 0.0)
	seed_row.add_child(seed_label)
	var minus := UiStyle.button("-", 54.0)
	minus.pressed.connect(_step_seed.bind(-1))
	seed_row.add_child(minus)
	_seed_edit = LineEdit.new()
	_seed_edit.custom_minimum_size = Vector2(170.0, 50.0)
	_seed_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
	_seed_edit.max_length = 8
	_seed_edit.add_theme_font_size_override("font_size", 24)
	_seed_edit.text_submitted.connect(_on_seed_submitted)
	_seed_edit.focus_exited.connect(_commit_seed_text)
	seed_row.add_child(_seed_edit)
	var plus := UiStyle.button("+", 54.0)
	plus.pressed.connect(_step_seed.bind(1))
	seed_row.add_child(plus)
	box.add_child(seed_row)

	var diff_row := HBoxContainer.new()
	diff_row.add_theme_constant_override("separation", 8)
	var diff_label := UiStyle.label("DIFFICULTY", 20, UiStyle.DIM, 4)
	diff_label.custom_minimum_size = Vector2(150.0, 0.0)
	diff_row.add_child(diff_label)
	_difficulty = OptionButton.new()
	for d in Config.DIFFICULTY_NAMES.size():
		var pct: float = Config.DIFFICULTY_SPEED[d]
		_difficulty.add_item("%s  (%d%% pace)" % [Config.DIFFICULTY_NAMES[d], roundi(pct * 100.0)], d)
	_difficulty.custom_minimum_size = Vector2(302.0, 50.0)
	_difficulty.add_theme_font_size_override("font_size", 22)
	diff_row.add_child(_difficulty)
	box.add_child(diff_row)

	var quit := UiStyle.button("QUIT", 460.0)
	quit.pressed.connect(func() -> void: quit_requested.emit())
	box.add_child(quit)

	var help := UiStyle.label("W throttle   S brake / reverse   A D steer   SPACE handbrake   R respawn   C camera\n"
		+ "F3 counter-steer assist   ESC pause   F1 debug overlay   F12 screenshot", 17, UiStyle.DIM, 4)
	help.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(help)
	visible = false


func open(seed_value: int, difficulty: int) -> void:
	_set_seed(seed_value)
	_difficulty.select(difficulty)
	visible = true
	_start.grab_focus()


func _background_texture() -> GradientTexture2D:
	var g := Gradient.new()
	g.set_color(0, Color(0.03, 0.05, 0.1))
	g.set_color(1, Color(0.12, 0.05, 0.12))
	g.add_point(0.55, Color(0.05, 0.09, 0.18))
	var tex := GradientTexture2D.new()
	tex.gradient = g
	tex.fill_from = Vector2(0.0, 0.0)
	tex.fill_to = Vector2(1.0, 1.0)
	tex.width = 256
	tex.height = 256
	return tex


func _set_seed(value: int) -> void:
	_seed = clampi(value, Config.SEED_MIN, Config.SEED_MAX)
	_seed_edit.text = str(_seed)


func _step_seed(step: int) -> void:
	_commit_seed_text()
	_set_seed(_seed + step)


func _on_seed_submitted(_text: String) -> void:
	_commit_seed_text()
	_start.grab_focus()


## Accepts only digits; anything else restores the last valid seed.
func _commit_seed_text() -> void:
	var t := _seed_edit.text.strip_edges()
	if t.is_valid_int():
		_set_seed(t.to_int())
	else:
		_set_seed(_seed)


func _on_start() -> void:
	_commit_seed_text()
	start_requested.emit(_seed, _difficulty.get_selected_id())
