class_name ResultsScreen
extends CanvasLayer
## R-28 results: final standings with total time, best lap and drift score
## per car, the player's drift total, and Restart / New seed / Menu buttons.
## Cars still on their last lap are listed live until they cross the line.

signal restart_requested
signal new_seed_requested
signal menu_requested

const COLUMNS := ["POS", "DRIVER", "TOTAL TIME", "BEST LAP", "DRIFT"]

var _race: Race
var _cells: Array[Label] = []
var _swatches: Array[ColorRect] = []
var _status: Label
var _info: Label
var _drift_total: Label
var _restart: Button
var _accum := 0.0


func _ready() -> void:
	layer = 15
	process_mode = Node.PROCESS_MODE_ALWAYS
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = UiStyle.theme()
	add_child(root)
	var dim := ColorRect.new()
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.color = Color(0.0, 0.0, 0.0, 0.45)
	root.add_child(dim)
	var panel := PanelContainer.new()
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	root.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 12)
	panel.add_child(box)
	var title := UiStyle.label("RACE RESULTS", 54, UiStyle.ACCENT, 10)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	_info = UiStyle.label("", 18, UiStyle.DIM, 4)
	_info.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(_info)
	var grid := GridContainer.new()
	grid.columns = COLUMNS.size() + 1
	grid.add_theme_constant_override("h_separation", 28)
	grid.add_theme_constant_override("v_separation", 8)
	box.add_child(grid)
	grid.add_child(Control.new())
	for c in COLUMNS:
		grid.add_child(UiStyle.label(c, 18, UiStyle.ACCENT, 4))
	for _row in Config.CAR_COUNT:
		var sw := ColorRect.new()
		sw.custom_minimum_size = Vector2(14.0, 26.0)
		grid.add_child(sw)
		_swatches.append(sw)
		for _col in COLUMNS.size():
			var cell := UiStyle.label("", 24, UiStyle.TEXT, 5)
			grid.add_child(cell)
			_cells.append(cell)
	_drift_total = UiStyle.label("", 26, Color(1.0, 0.85, 0.3), 6)
	_drift_total.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(_drift_total)
	_status = UiStyle.label("", 18, UiStyle.DIM, 4)
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(_status)
	var buttons := HBoxContainer.new()
	buttons.alignment = BoxContainer.ALIGNMENT_CENTER
	buttons.add_theme_constant_override("separation", 14)
	box.add_child(buttons)
	_restart = UiStyle.button("RESTART", 220.0)
	_restart.pressed.connect(func() -> void: restart_requested.emit())
	buttons.add_child(_restart)
	var new_seed := UiStyle.button("NEW SEED", 220.0)
	new_seed.pressed.connect(func() -> void: new_seed_requested.emit())
	buttons.add_child(new_seed)
	var menu := UiStyle.button("MENU", 220.0)
	menu.pressed.connect(func() -> void: menu_requested.emit())
	buttons.add_child(menu)
	visible = false


func open(race: Race) -> void:
	_race = race
	_info.text = "Seed %d   |   %s   |   %d laps   |   track %.2f km" % [
		race.seed_value, Config.DIFFICULTY_NAMES[race.difficulty], Config.LAP_COUNT, race.track.length / 1000.0]
	_refresh()
	visible = true
	_restart.grab_focus()


func close() -> void:
	visible = false
	_race = null


func _process(delta: float) -> void:
	if not visible or _race == null:
		return
	_accum += delta
	if _accum > 0.25:
		_accum = 0.0
		_refresh()


func _refresh() -> void:
	var m := _race.manager
	for row in m.order.size():
		var e := m.order[row]
		var base := row * COLUMNS.size()
		_swatches[row].color = e.car.paint_color
		_cells[base].text = "P%d" % (row + 1)
		_cells[base + 1].text = e.car.display_name
		_cells[base + 2].text = Race.format_time(e.finish_time) if e.finished else "lap %d/%d ..." % [m.current_lap(e.car), Config.LAP_COUNT]
		_cells[base + 3].text = Race.format_time(e.best_lap)
		_cells[base + 4].text = str(int(e.drift.total))
		var col := UiStyle.ACCENT if e.car.is_player else UiStyle.TEXT
		for k in COLUMNS.size():
			UiStyle.set_label_color(_cells[base + k], col)
	var pe := m.entry_of(_race.player)
	_drift_total.text = "Your drift score total: %d" % int(pe.drift.total)
	var waiting := m.cars.size() - m.finish_count
	_status.text = "Final standings" if waiting == 0 else "Waiting for %d car(s) to finish..." % waiting
