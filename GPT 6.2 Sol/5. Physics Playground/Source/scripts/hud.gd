class_name LabHud
extends CanvasLayer

const C = preload("res://scripts/config.gd")
const P = preload("res://scripts/palette.gd")
var app: Node3D
var skin := LabPalette.new()
var normal_slot := skin.box(P.TILE, 8, P.EDGE, 1)
var selected_slot := skin.box(P.TILE.lightened(0.16), 8, P.AMBER, 2)
var root: Control
var frame: VBoxContainer
var title: Label
var objective: Label
var clock_label: Label
var part_value: Label
var stars: Label
var inventory: HBoxContainer
var play_button: Button
var reset_button: Button
var hint_button: Button
var mode_label: Label
var controls: Label
var debug_panel: PanelContainer
var debug_value: Label
var tutorial: PanelContainer
var rates: Array[Button] = []
var item_buttons: Dictionary = {}
var item_count: Dictionary = {}
var modal: LabModal
var time_budget := 0.0

func setup(owner_app: Node3D) -> void:
	app = owner_app
	root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_margins(margin, 24, 20)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(margin)
	frame = VBoxContainer.new()
	frame.add_theme_constant_override("separation", 10)
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(frame)
	_header()
	var air := Control.new()
	air.mouse_filter = Control.MOUSE_FILTER_IGNORE
	air.size_flags_vertical = Control.SIZE_EXPAND_FILL
	frame.add_child(air)
	_footer()

	debug_panel = PanelContainer.new()
	debug_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT, Control.PRESET_MODE_KEEP_SIZE)
	debug_panel.offset_left = -315
	debug_panel.offset_right = -24
	debug_panel.offset_top = 138
	debug_panel.add_theme_stylebox_override("panel", skin.box(P.DARK, 8, P.EDGE, 1))
	debug_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(debug_panel)
	debug_value = skin.label("", 14, P.GREEN, 2)
	debug_panel.add_child(debug_value)
	debug_panel.hide()
	_tutorial()
	modal = LabModal.new()
	# Blocking screens stay accessible when the gameplay HUD is hidden.
	add_child(modal)
	modal.setup(app, skin)

func _header() -> void:
	var top := PanelContainer.new()
	top.add_theme_stylebox_override("panel", skin.box(P.DARK, 15, P.EDGE, 1))
	frame.add_child(top)
	var margin := MarginContainer.new()
	_margins(margin, 10, 7)
	top.add_child(margin)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 16)
	margin.add_child(row)
	var brand := LabGlyph.new()
	brand.setup("brand", P.AMBER, Vector2(46, 45))
	brand.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(brand)
	var identity := _column(0)
	row.add_child(identity)
	identity.add_child(skin.label("CONTRAPTION LAB", 18, P.AMBER, 1))
	_divider(row)
	var intro := _column(2)
	row.add_child(intro)
	intro.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title = skin.label("", 29, P.TEXT, 1)
	intro.add_child(title)
	objective = skin.label("", 15, P.MUTED)
	objective.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	intro.add_child(objective)
	clock_label = _metric(row, "TIME", "00.00")
	part_value = _metric(row, "PARTS / PAR", "0 / 2")
	stars = _metric(row, "STARS", "★★★")
	_divider(row)
	row.add_child(skin.button("Levels", app.open_levels))

func _footer() -> void:
	var tray := PanelContainer.new()
	tray.add_theme_stylebox_override("panel", skin.box(P.DARK, 15, P.EDGE, 1))
	frame.add_child(tray)
	var margin := MarginContainer.new()
	_margins(margin, 9, 5)
	tray.add_child(margin)
	var column := _column(8)
	margin.add_child(column)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 7)
	column.add_child(row)
	mode_label = skin.label("BUILD", 13, P.AMBER, 1)
	mode_label.custom_minimum_size.x = 56
	row.add_child(mode_label)
	_divider(row)
	inventory = HBoxContainer.new()
	inventory.add_theme_constant_override("separation", 7)
	inventory.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(inventory)
	hint_button = skin.button("Hint", app.show_hint)
	hint_button.custom_minimum_size.x = 75
	row.add_child(hint_button)
	reset_button = skin.button("↻  Reset", app.reset_attempt)
	row.add_child(reset_button)
	play_button = skin.button("▶  Simulate", app.toggle_play, true)
	play_button.custom_minimum_size.x = 134
	row.add_child(play_button)
	var after := HBoxContainer.new()
	after.add_theme_constant_override("separation", 5)
	column.add_child(after)
	controls = skin.label("", 14, P.MUTED)
	controls.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	controls.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	after.add_child(controls)
	for index in range(C.TIME_SCALES.size()):
		var name := ["¼×", "1×", "2×"][index] as String
		var button := skin.button(name, app.set_rate.bind(index))
		button.custom_minimum_size = Vector2(49, 26)
		button.add_theme_font_size_override("font_size", 13)
		button.tooltip_text = "Simulation speed"
		rates.append(button)
		after.add_child(button)

func _tutorial() -> void:
	tutorial = PanelContainer.new()
	tutorial.add_theme_stylebox_override("panel", skin.box(P.PANEL, 14, P.EDGE, 1))
	tutorial.position = Vector2(24, 138)
	tutorial.custom_minimum_size.x = 298
	root.add_child(tutorial)
	var margin := MarginContainer.new()
	_margins(margin, 9, 6)
	tutorial.add_child(margin)
	var column := _column(11)
	margin.add_child(column)
	column.add_child(skin.label("Make the first roll", 24, P.TEXT, 1))
	column.add_child(skin.label("1  Select a plank from the parts tray.\n2  Use Q or E to rotate it.\n3  Click to place it, then press Space.", 16, P.MUTED))
	column.add_child(skin.button("Got it", app.dismiss_tutorial))
	tutorial.hide()

func _margins(node: MarginContainer, x: int, y: int) -> void:
	node.add_theme_constant_override("margin_left", x)
	node.add_theme_constant_override("margin_right", x)
	node.add_theme_constant_override("margin_top", y)
	node.add_theme_constant_override("margin_bottom", y)

func _column(gap: int) -> VBoxContainer:
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", gap)
	return column

func _divider(parent: HBoxContainer) -> void:
	var divider := ColorRect.new()
	divider.color = P.EDGE
	divider.custom_minimum_size.x = 1
	divider.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(divider)

func _metric(parent: HBoxContainer, caption: String, value: String) -> Label:
	var column := _column(1)
	column.custom_minimum_size.x = 92
	parent.add_child(column)
	column.add_child(skin.label(caption, 12, P.MUTED, 2))
	var label := skin.label(value, 24, P.TEXT, 1)
	column.add_child(label)
	return label

func build_inventory(level: Dictionary) -> void:
	for child in inventory.get_children():
		inventory.remove_child(child)
		child.queue_free()
	item_buttons.clear()
	item_count.clear()
	var sequence := 0
	for kind in C.KIT:
		sequence += 1
		if not level.kit.has(kind):
			continue
		var item: Array = C.KIT[kind]
		var tile := skin.button("", app.workbench.choose.bind(kind))
		tile.custom_minimum_size = Vector2(104, 54)
		tile.tooltip_text = "%s\n%s" % [item[0], item[1]]
		item_buttons[kind] = tile
		inventory.add_child(tile)
		var row := HBoxContainer.new()
		row.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_theme_constant_override("separation", 5)
		tile.add_child(row)
		row.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		row.offset_left = 7
		row.offset_right = -6
		var icon := LabGlyph.new()
		icon.setup(kind, item[2], Vector2(29, 31))
		row.add_child(icon)
		var column := _column(1)
		column.mouse_filter = Control.MOUSE_FILTER_IGNORE
		column.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(column)
		column.add_child(skin.label(item[0], 14, P.TEXT, 1))
		var count := skin.label("%d  ·  ×0" % sequence, 11, P.MUTED, 2)
		column.add_child(count)
		item_count[kind] = [count, sequence]
	title.text = level.name
	objective.text = level.objective

func refresh() -> void:
	if not is_instance_valid(app.board):
		return
	var board: LabBoard = app.board
	var in_build: bool = app.mode == "build"
	var remaining_seconds := maxi(0, ceili(C.HINT_DELAY - app.level_age))
	clock_label.text = "%05.2f" % board.elapsed
	part_value.text = "%d / %d" % [app.placements.size(), board.level.par]
	var stars_count := C.stars_for(board.level, app.placements.size(), board.elapsed)
	stars.text = "★".repeat(stars_count) + "☆".repeat(3 - stars_count)
	stars.add_theme_color_override("font_color", P.AMBER)
	mode_label.text = {"build": "BUILD", "run": "RUN", "paused": "HOLD", "result": "RESULT", "test": "TEST"}.get(app.mode, "BUILD")
	mode_label.add_theme_color_override("font_color", P.GREEN if app.mode == "run" else P.AMBER)
	play_button.text = {"build": "▶  Simulate", "run": "Ⅱ  Pause", "paused": "▶  Resume", "result": "↻  Try again", "test": "Testing…"}.get(app.mode, "Simulate")
	play_button.disabled = app.mode == "test"
	reset_button.disabled = app.mode == "test"
	hint_button.disabled = remaining_seconds > 0 or not in_build
	hint_button.text = "Hint %ds" % remaining_seconds if remaining_seconds > 0 else "Hint"
	for kind in item_buttons:
		var amount: int = app.remaining(kind)
		var tile: Button = item_buttons[kind]
		var current: bool = app.workbench.tool == kind
		tile.disabled = not in_build or amount == 0
		tile.add_theme_stylebox_override("normal", selected_slot if current else normal_slot)
		var count: Label = item_count[kind][0]
		count.text = "%d  ·  ×%d" % [item_count[kind][1], amount]
	for i in range(rates.size()):
		rates[i].add_theme_color_override("font_color", P.AMBER if app.rate == i else P.MUTED)
	if in_build:
		controls.text = "Drag Move   ·   Q / E Rotate   ·   Del Remove   ·   Ctrl+Z / Y History" if app.workbench.selected >= 0 else "LMB Place / select   ·   Q / E Rotate   ·   MMB Orbit   ·   F Reframe"
	else:
		controls.text = "Space Play / pause   ·   R Reset   ·   [ / ] Speed   ·   Esc Menu"
	if debug_panel.visible:
		var fps := Engine.get_frames_per_second()
		var draws := int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
		var physics := Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0
		debug_value.text = "%d FPS     %.2f ms / frame\n%d draws   %d active bodies\nPhysics %.2f ms   FX %d" % [
			fps, 1000.0 / maxf(1, fps), draws, board.live_bodies(), physics, app.effects.active_particles()]

func _process(delta: float) -> void:
	time_budget += delta
	if time_budget >= 0.12 and is_instance_valid(app):
		time_budget = 0.0
		refresh()
