class_name Hud
extends CanvasLayer
## In-race HUD (R-26, R-27): position, lap counter, total / current / best lap
## times, last 3 laps, countdown, WRONG WAY warning, drift score with combo,
## speedometer + RPM bar + gear, minimap. Texts refresh at 20 Hz.

var race: Race

var _pos_label: Label
var _lap_label: Label
var _assist_label: Label
var _camera_label: Label
var _total_label: Label
var _laptime_label: Label
var _best_label: Label
var _laps_list: Label
var _countdown: Label
var _wrong_way: Label
var _drift_label: Label
var _drift_event: Label
var _drift_total: Label
var _speedo: Speedometer
var _minimap: Minimap
var _accum := 0.0
var _clock := 0.0
var _go_timer := 0.0
var _countdown_tween: Tween


func _ready() -> void:
	layer = 5
	var root := Control.new()
	root.name = "HudRoot"
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.theme = UiStyle.theme()
	add_child(root)

	var left := UiStyle.panel()
	_anchor(left, Vector2(0, 0), Vector2(24, 20))
	var lbox := VBoxContainer.new()
	left.add_child(lbox)
	_pos_label = UiStyle.label("POS 1/4", 46)
	_lap_label = UiStyle.label("LAP 1/3", 32)
	_assist_label = UiStyle.label("", 16, UiStyle.DIM, 4)
	_camera_label = UiStyle.label("", 16, UiStyle.DIM, 4)
	for l in [_pos_label, _lap_label, _assist_label, _camera_label]:
		lbox.add_child(l)
	root.add_child(left)

	var right := UiStyle.panel()
	right.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_anchor(right, Vector2(1, 0), Vector2(-24, 20))
	var rbox := VBoxContainer.new()
	right.add_child(rbox)
	_total_label = UiStyle.label("TIME  --:--.---", 26)
	_laptime_label = UiStyle.label("LAP   --:--.---", 26)
	_best_label = UiStyle.label("BEST  --:--.---", 26, UiStyle.GOOD)
	_laps_list = UiStyle.label("", 18, UiStyle.DIM, 4)
	for l in [_total_label, _laptime_label, _best_label, UiStyle.label("LAST LAPS", 15, UiStyle.ACCENT, 4), _laps_list]:
		rbox.add_child(l)
	root.add_child(right)

	_countdown = _centered_label(150, UiStyle.ACCENT, 150.0)
	root.add_child(_countdown)
	_wrong_way = _centered_label(64, UiStyle.BAD, 330.0)
	_wrong_way.text = "WRONG WAY"
	root.add_child(_wrong_way)
	_drift_total = _centered_label(22, Color(1.0, 0.85, 0.3), 22.0)
	_drift_label = _centered_label(44, Color(1.0, 0.85, 0.3), 58.0)
	_drift_event = _centered_label(30, UiStyle.GOOD, 116.0)
	for l in [_drift_label, _drift_event, _drift_total]:
		root.add_child(l)

	_speedo = Speedometer.new()
	_speedo.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT)
	_speedo.offset_left = -310.0
	_speedo.offset_top = -350.0
	_speedo.offset_right = -20.0
	_speedo.offset_bottom = -20.0
	root.add_child(_speedo)
	_minimap = Minimap.new()
	_minimap.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	_minimap.offset_left = 24.0
	_minimap.offset_top = -Config.MINIMAP_SIZE - 24.0
	_minimap.offset_right = 24.0 + Config.MINIMAP_SIZE
	_minimap.offset_bottom = -24.0
	root.add_child(_minimap)
	visible = false


func _anchor(c: Control, anchor: Vector2, ofs: Vector2) -> void:
	c.anchor_left = anchor.x
	c.anchor_right = anchor.x
	c.anchor_top = anchor.y
	c.anchor_bottom = anchor.y
	c.offset_left = ofs.x
	c.offset_right = ofs.x
	c.offset_top = ofs.y
	c.offset_bottom = ofs.y


func _centered_label(font_size: int, color: Color, y: float, from_bottom: bool = false) -> Label:
	var l := UiStyle.label("", font_size, color, 10)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.anchor_left = 0.0
	l.anchor_right = 1.0
	l.anchor_top = 1.0 if from_bottom else 0.0
	l.anchor_bottom = l.anchor_top
	l.offset_top = y
	l.offset_bottom = y + float(font_size) * 1.4
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	l.visible = false
	return l


func bind(r: Race) -> void:
	race = r
	_speedo.car = r.player
	_minimap.bind(r)
	r.manager.countdown_tick.connect(_on_countdown_tick)
	_countdown.visible = false
	_wrong_way.visible = false
	_refresh_texts()
	visible = true


func unbind() -> void:
	race = null
	_speedo.car = null
	_minimap.race = null
	visible = false


func _on_countdown_tick(value: int) -> void:
	_countdown.visible = true
	_countdown.text = str(value) if value > 0 else "GO!"
	UiStyle.set_label_color(_countdown, UiStyle.ACCENT if value > 0 else UiStyle.GOOD)
	_countdown.pivot_offset = _countdown.size * 0.5
	if _countdown_tween != null:
		_countdown_tween.kill()
	_countdown_tween = create_tween()
	_countdown.scale = Vector2(1.6, 1.6)
	_countdown_tween.tween_property(_countdown, "scale", Vector2.ONE, 0.25)
	_go_timer = Config.GO_DISPLAY_TIME if value == 0 else 0.0


func _process(delta: float) -> void:
	if race == null:
		return
	_clock += delta
	if _go_timer > 0.0:
		_go_timer -= delta
		if _go_timer <= 0.0:
			_countdown.visible = false
	var wrong := race.manager.is_wrong_way(race.player)
	_wrong_way.visible = wrong and fmod(_clock, 0.8) < 0.5
	_accum += delta
	if _accum >= Config.HUD_UPDATE_INTERVAL:
		_accum = 0.0
		_refresh_texts()


func _refresh_texts() -> void:
	var m := race.manager
	var p := race.player
	var e := m.entry_of(p)
	_pos_label.text = "POS %d/%d" % [e.position, race.cars.size()]
	_lap_label.text = "LAP %d/%d" % [m.current_lap(p), Config.LAP_COUNT]
	_assist_label.text = "Counter-steer assist: %s  [F3]" % ("ON" if p.assist_enabled else "OFF")
	_camera_label.text = "Camera: %s  [C]" % ("HOOD" if race.camera.mode == ChaseCamera.Mode.HOOD else "CHASE")
	_total_label.text = "TIME  " + Race.format_time(m.total_time(p))
	_laptime_label.text = "LAP   " + Race.format_time(m.current_lap_time(p))
	_best_label.text = "BEST  " + Race.format_time(e.best_lap)
	var lines := PackedStringArray()
	var n := e.lap_times.size()
	for k in range(maxi(0, n - Config.LAP_LIST_COUNT), n):
		lines.append("L%d  %s" % [k + 1, Race.format_time(e.lap_times[k])])
	_laps_list.text = "\n".join(lines) if lines.size() > 0 else "-"
	var d := e.drift
	_drift_label.visible = d.chain_active and d.pending >= 1.0
	if _drift_label.visible:
		_drift_label.text = "DRIFT  %d   x%d" % [int(d.pending), d.combo]
	_drift_event.visible = d.event_age < 1.6 and d.last_event != DriftScorer.Event.NONE
	if _drift_event.visible:
		if d.last_event == DriftScorer.Event.BANKED:
			_drift_event.text = "+%d  BANKED" % int(d.last_event_value)
			UiStyle.set_label_color(_drift_event, UiStyle.GOOD)
		else:
			_drift_event.text = "DRIFT LOST  (-%d)" % int(d.last_event_value)
			UiStyle.set_label_color(_drift_event, UiStyle.BAD)
	_drift_total.visible = true
	_drift_total.text = "DRIFT SCORE  %d" % int(d.total)
