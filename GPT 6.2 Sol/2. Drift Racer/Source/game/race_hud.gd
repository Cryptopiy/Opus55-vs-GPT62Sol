class_name RaceHUD
extends Control

const C = preload("res://game/config.gd")
const S = preload("res://game/ui_style.gd")

var race: RaceSession
var skids: SkidPool
var debug := false
var countersteer := true
var text_font := ThemeDB.fallback_font
var thick := S.font(0.63)
var numbers := S.font(1.06, true)
var map_path := PackedVector2Array()
var map_scale := 1.0
var map_center := Vector2.ZERO
var speed_ticks := PackedVector2Array()
var current_text := ""
var best_text := ""
var total_time := ""
var position_text := ""
var lap_text := ""
var lap_cards := PackedStringArray()
var speed_text := ""
var gear_text := ""
var drift_text := ""
var drift_total := ""
var combo := ""
var debug_text := ""
var countdown_text := ""

func setup(session: RaceSession, pool: SkidPool) -> void:
	race = session
	skids = pool
	var data := race.track.data
	var low := Vector2(INF, INF)
	var high := -low
	for p in data.points:
		low = low.min(Vector2(p.x, p.z))
		high = high.max(Vector2(p.x, p.z))
	map_center = (low + high) * 0.5
	map_scale = 148.0 / maxf(high.x - low.x, high.y - low.y)
	map_path.clear()
	for i in range(0, data.points.size(), 4):
		map_path.append(_map_point(data.points[i]))
	map_path.append(map_path[0])
	speed_ticks.clear()
	for i in 21:
		var angle := lerpf(deg_to_rad(146.0), deg_to_rad(394.0), i / 20.0)
		speed_ticks.append(Vector2.from_angle(angle))
	refresh()

func refresh() -> void:
	if not is_instance_valid(race):
		return
	var car := race.player
	lap_text = "%02d / %02d" % [mini(car.completed_laps + 1, C.LAPS), C.LAPS]
	position_text = "%d / %d" % [race.standings().find(car) + 1, C.CAR_COUNT]
	var end_time := car.finish_time if car.finish_time > 0.0 else race.elapsed
	total_time = stamp(end_time)
	var current_lap := float(car.lap_times[-1]) if car.finish_time > 0.0 else end_time - car.lap_start
	current_text = stamp(current_lap)
	best_text = stamp(car.best_lap) if car.best_lap > 0.0 else "—"
	lap_cards.clear()
	for i in car.lap_times.size():
		lap_cards.append("%02d    %s" % [i + 1, stamp(car.lap_times[i])])
	speed_text = "%03d" % roundi(car.speed * C.KMH)
	gear_text = "R" if car.reversing else str(car.gear)
	drift_text = "%04d" % int(race.drift.pending)
	drift_total = "%d BANKED" % int(race.drift.total)
	combo = "×%d" % race.drift.combo
	countdown_text = str(race.count) if race.count > 0 else "GO"
	if debug:
		var fps := Engine.get_frames_per_second()
		var active_bodies := 0
		for racer in race.cars:
			if not racer.freeze and not racer.sleeping and not get_tree().paused:
				active_bodies += 1
		debug_text = "%d FPS  ·  %.1f ms  ·  %d draws  ·  %d active bodies  ·  %d skid strips" % [
			fps, 1000.0 / maxf(1.0, fps), Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME),
			active_bodies, skids.active_count()]
	queue_redraw()

static func stamp(time: float) -> String:
	var millis := maxi(0, int(time * 1000.0))
	return "%02d:%02d.%03d" % [floori(millis / 60000.0), floori(millis / 1000.0) % 60, millis % 1000]

func _map_point(point: Vector3) -> Vector2:
	return (Vector2(point.x, point.z) - map_center) * map_scale

func _label(at: Vector2, value: String, size_px: int, color: Color, face: Font = null) -> void:
	draw_string(text_font if face == null else face, at, value, HORIZONTAL_ALIGNMENT_LEFT, -1, size_px, color)

func _panel(rect: Rect2, accent := false) -> void:
	draw_rect(rect, S.BACK)
	draw_rect(rect, S.RULE, false, 1.0)
	if accent:
		draw_rect(Rect2(rect.position, Vector2(52.0, 3.0)), S.MINT)

func _draw() -> void:
	if not is_instance_valid(race):
		return
	_timing()
	_map()
	_instruments()
	_drift()
	var car := race.player
	if race.count >= 0 and race.elapsed < 0.85:
		var center := Vector2(size.x * 0.5, size.y * 0.34)
		_panel(Rect2(center - Vector2(102.0, 81.0), Vector2(204.0, 151.0)), true)
		draw_string(numbers, center - Vector2(100.0, -38.0), countdown_text,
			HORIZONTAL_ALIGNMENT_CENTER, 200.0, 94, S.MINT)
	elif car.wrong_way > C.WRONG_WAY_TIME and car.finish_time == 0.0:
		_alert("WRONG WAY", S.WARN)
	elif car.finish_time > 0.0 and not race.complete:
		_alert("FINISHED  /  WAITING FOR FIELD", S.MINT)
	if debug:
		var debug_box := Rect2(Vector2(size.x * 0.5 - 345.0, 22.0), Vector2(690.0, 41.0))
		draw_rect(debug_box, S.BACK)
		draw_string(text_font, debug_box.position + Vector2(0.0, 28.0), debug_text,
			HORIZONTAL_ALIGNMENT_CENTER, debug_box.size.x, 18, S.PAPER)

func _alert(message: String, color: Color) -> void:
	var alert := Rect2(Vector2(size.x * 0.5 - 244.0, 91.0), Vector2(488.0, 49.0))
	draw_rect(alert, S.BACK)
	draw_rect(Rect2(alert.position, Vector2(3.0, alert.size.y)), color)
	draw_string(thick, alert.position + Vector2(0.0, 32.0), message,
		HORIZONTAL_ALIGNMENT_CENTER, alert.size.x, 22, color)

func _timing() -> void:
	var origin := Vector2(44.0, 40.0)
	var panel_height := 215.0 + lap_cards.size() * 26.0
	_panel(Rect2(origin, Vector2(340.0, panel_height)), true)
	_label(origin + Vector2(20.0, 32.0), "POSITION", 16, S.MUTED, thick)
	_label(origin + Vector2(183.0, 32.0), "LAP", 16, S.MUTED, thick)
	_label(origin + Vector2(20.0, 72.0), position_text, 36, S.PAPER, numbers)
	_label(origin + Vector2(183.0, 72.0), lap_text, 36, S.PAPER, numbers)
	draw_line(origin + Vector2(20.0, 91.0), origin + Vector2(320.0, 91.0), S.RULE, 1.0)
	_label(origin + Vector2(20.0, 121.0), "CURRENT", 15, S.MUTED, thick)
	_label(origin + Vector2(136.0, 123.0), current_text, 25, S.MINT, thick)
	_label(origin + Vector2(20.0, 161.0), "BEST", 15, S.MUTED, thick)
	_label(origin + Vector2(136.0, 162.0), best_text, 23, S.PAPER)
	_label(origin + Vector2(20.0, 194.0), "TOTAL", 15, S.MUTED, thick)
	_label(origin + Vector2(136.0, 195.0), total_time, 23, S.PAPER)
	for i in lap_cards.size():
		_label(origin + Vector2(20.0, 222.0 + 26.0 * i), lap_cards[i], 18, S.MUTED)

func _map() -> void:
	var origin := Vector2(size.x - 282.0, 40.0)
	_panel(Rect2(origin, Vector2(238.0, 222.0)))
	_label(origin + Vector2(16.0, 30.0), "CIRCUIT", 16, S.MINT, thick)
	var center := origin + Vector2(119.0, 119.0)
	draw_set_transform(center)
	draw_polyline(map_path, Color("#152d31"), 9.0, true)
	draw_polyline(map_path, Color("#7b9a94"), 4.0, true)
	for car in race.cars:
		if car == race.player:
			continue
		draw_circle(_map_point(car.global_position), 7.5, S.INK)
		draw_circle(_map_point(car.global_position), 5.5, car.paint)
	var player := race.player
	var at := _map_point(player.global_position)
	var nose := Vector2(-player.global_basis.z.x, -player.global_basis.z.z).normalized()
	var side := Vector2(-nose.y, nose.x)
	var points := PackedVector2Array([at + nose * 11.0, at - nose * 7.0 + side * 6.0,
		at - nose * 3.0, at - nose * 7.0 - side * 6.0])
	draw_colored_polygon(points, player.paint)
	draw_set_transform(Vector2.ZERO)

func _instruments() -> void:
	var origin := size - Vector2(415.0, 302.0)
	_panel(Rect2(origin, Vector2(371.0, 266.0)), true)
	var center := origin + Vector2(125.0, 134.0)
	var start := deg_to_rad(146.0)
	var finish := deg_to_rad(394.0)
	var car := race.player
	draw_arc(center, 96.0, start, finish, 36, Color("#355157"), 6.0, true)
	var needle := lerpf(start, finish, clampf(car.speed / C.TOP_SPEED, 0.0, 1.0))
	if needle > start + 0.002:
		draw_arc(center, 96.0, start, needle, 36, S.MINT, 6.0, true)
	for i in speed_ticks.size():
		var dir := speed_ticks[i]
		draw_line(center + dir * (81.0 if i % 5 == 0 else 86.0), center + dir * 90.0, S.MUTED, 1.4, true)
	draw_string(numbers, center - Vector2(84.0, -17.0), speed_text,
		HORIZONTAL_ALIGNMENT_CENTER, 168.0, 57, S.PAPER)
	draw_string(thick, center - Vector2(60.0, -44.0), "KM/H",
		HORIZONTAL_ALIGNMENT_CENTER, 120.0, 15, S.MUTED)
	draw_rect(Rect2(origin + Vector2(247.0, 73.0), Vector2(99.0, 103.0)), Color("#142e35"))
	draw_string(numbers, origin + Vector2(251.0, 132.0), gear_text,
		HORIZONTAL_ALIGNMENT_CENTER, 85.0, 61, S.MINT)
	_label(origin + Vector2(269.0, 158.0), "GEAR", 16, S.MUTED, thick)
	var rpm_width := 279.0
	var bar := origin + Vector2(68.0, 224.0)
	var rpm_fraction := clampf(car.rpm / C.MAX_RPM, 0.0, 1.0)
	var red := C.UPSHIFT / C.MAX_RPM
	_label(origin + Vector2(20.0, 236.0), "RPM", 15, S.MUTED, thick)
	draw_rect(Rect2(bar, Vector2(rpm_width, 10.0)), Color("#2c494e"))
	draw_rect(Rect2(bar + Vector2(rpm_width * red, 0.0), Vector2(rpm_width * (1.0 - red), 10.0)), S.WARN)
	draw_rect(Rect2(bar, Vector2(rpm_width * minf(red, rpm_fraction), 10.0)), S.MINT)

func _drift() -> void:
	var origin := Vector2(44.0, size.y - 156.0)
	_panel(Rect2(origin, Vector2(340.0, 120.0)), race.drift.active)
	_label(origin + Vector2(20.0, 32.0), "DRIFT SCORE", 16, S.MUTED, thick)
	_label(origin + Vector2(20.0, 78.0), drift_text, 43, S.PAPER, numbers)
	_label(origin + Vector2(249.0, 76.0), combo, 38, S.MINT, numbers)
	_label(origin + Vector2(21.0, 103.0), drift_total, 16, S.MUTED, thick)
	_label(origin + Vector2(41.0, -14.0), "COUNTER-STEER " + ("ON" if countersteer else "OFF"), 15, S.PAPER, thick)
