class_name LabModal
extends Control

const C = preload("res://scripts/config.gd")
const P = preload("res://scripts/palette.gd")
var app: Node3D
var skin: LabPalette
var center: CenterContainer
var stack: VBoxContainer
var current := ""
var report: Label

func setup(owner_app: Node3D, palette: LabPalette) -> void:
	app = owner_app
	skin = palette
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var tint := ColorRect.new()
	tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	tint.color = Color(0.035, 0.064, 0.12, 0.55)
	add_child(tint)
	center = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	close()

func close() -> void:
	current = ""
	report = null
	hide()

func _sheet(view: String, name_text: String, detail: String, width: float) -> void:
	current = view
	for child in center.get_children():
		center.remove_child(child)
		child.queue_free()
	var panel := PanelContainer.new()
	panel.custom_minimum_size.x = width
	var paint := skin.box(P.PANEL, 20, P.EDGE, 1)
	paint.shadow_color = Color(0.02, 0.04, 0.08, 0.43)
	paint.shadow_size = 22
	paint.shadow_offset = Vector2(0, 8)
	panel.add_theme_stylebox_override("panel", paint)
	center.add_child(panel)
	var margin := MarginContainer.new()
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 22)
	panel.add_child(margin)
	stack = VBoxContainer.new()
	stack.add_theme_constant_override("separation", 15)
	margin.add_child(stack)
	stack.add_child(skin.label(name_text, 33, P.TEXT, 1))
	if not detail.is_empty():
		stack.add_child(skin.label(detail, 17, P.MUTED))
	show()

func pause_menu() -> void:
	_sheet("pause", "Paused", app.board.level.name, 440)
	stack.add_child(skin.button("Resume", app.close_overlay, true))
	stack.add_child(skin.button("Restart", app.reset_attempt))
	stack.add_child(skin.button("Quit", app.quit_game))

func result(success: bool, earned: int) -> void:
	var elapsed: float = app.board.elapsed
	_sheet("result", "Receiver secured" if success else app.board.outcome,
		"%s  /  %.2f s  /  %d parts" % [app.board.level.name, elapsed, app.placements.size()], 470)
	if success:
		stack.add_child(skin.label("★".repeat(earned) + "☆".repeat(3 - earned), 47, P.AMBER, 1))
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 9)
	stack.add_child(row)
	row.add_child(skin.button("Retry", app.reset_attempt, not success))
	row.add_child(skin.button("Levels", app.open_levels))
	if success and app.level_index < C.LEVELS.size() - 1:
		row.add_child(skin.button("Next level", app.choose_level.bind(app.level_index + 1), true))

func levels() -> void:
	_sheet("levels", "Select a level", "Open the next board by completing the current one.", 856)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	stack.add_child(row)
	for index in range(C.LEVELS.size()):
		var level: Dictionary = C.LEVELS[index]
		var open: bool = app.progress.unlocked(index)
		var medal: int = app.progress.best(index)
		var status := "★".repeat(medal) + "☆".repeat(3 - medal) if open else "LOCKED"
		var tile := skin.button("%s\n%s" % [level.name, status], app.choose_level.bind(index))
		tile.disabled = not open
		tile.custom_minimum_size = Vector2(146, 99)
		tile.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(tile)
	stack.add_child(skin.button("Back to board", app.close_overlay))

func developer() -> void:
	_sheet("dev", "Developer tests", app.board.level.name, 535)
	stack.add_child(skin.button("Determinism · 5 reference runs", app.start_determinism, true))
	stack.add_child(skin.button("Resting stack · 10 seconds", app.start_stack))
	report = skin.label(app.dev_report, 15, P.GREEN, 2)
	report.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	report.custom_minimum_size.x = 470
	stack.add_child(report)
	stack.add_child(skin.button("Back to board", app.close_overlay))

func update_report(value: String) -> void:
	if is_instance_valid(report):
		report.text = value
