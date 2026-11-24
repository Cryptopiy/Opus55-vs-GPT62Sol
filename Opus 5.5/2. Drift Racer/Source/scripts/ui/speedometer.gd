class_name Speedometer
extends Control
## R-27: analog speed arc + needle with digital km/h, gear indicator and an
## RPM bar with a red line zone (7000-7500 rpm). Custom drawn every frame.

const MAX_KMH := 220.0
const ARC_START_DEG := 135.0
const ARC_SWEEP_DEG := 270.0
const DIAL_RADIUS := 118.0

var car: Car

var _font: Font
var _tick_labels: Array[String] = []
var _speed_text := "0"
var _shown_speed := -1
var _gear_text := "1"
var _shown_gear := 1


func _ready() -> void:
	custom_minimum_size = Vector2(290.0, 330.0)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_font = get_theme_default_font()
	for k in 12:
		_tick_labels.append(str(k * 20))


func _process(_delta: float) -> void:
	if car == null:
		return
	var kmh := roundi(car.speed_kmh)
	if kmh != _shown_speed:
		_shown_speed = kmh
		_speed_text = str(kmh)
	var gear := car.drivetrain.gear
	if gear != _shown_gear:
		_shown_gear = gear
		_gear_text = "R" if car.drivetrain.is_reverse() else str(gear)
	queue_redraw()


func _draw() -> void:
	if car == null:
		return
	var c := Vector2(size.x * 0.5, DIAL_RADIUS + 16.0)
	var r := DIAL_RADIUS
	draw_circle(c, r + 10.0, Color(0.03, 0.04, 0.06, 0.6))
	var a0 := deg_to_rad(ARC_START_DEG)
	var sweep := deg_to_rad(ARC_SWEEP_DEG)
	draw_arc(c, r - 6.0, a0, a0 + sweep, 72, Color(1.0, 1.0, 1.0, 0.12), 12.0, true)
	var f := clampf(car.speed_kmh / MAX_KMH, 0.0, 1.0)
	if f > 0.002:
		var col := UiStyle.ACCENT.lerp(Color(1.0, 0.2, 0.15), clampf((f - 0.6) / 0.4, 0.0, 1.0))
		draw_arc(c, r - 6.0, a0, a0 + sweep * f, 72, col, 12.0, true)
	for k in _tick_labels.size():
		var ang := a0 + sweep * float(k * 20) / MAX_KMH
		var dir := Vector2(cos(ang), sin(ang))
		draw_line(c + dir * (r - 22.0), c + dir * (r - 14.0), Color(1, 1, 1, 0.7), 2.0, true)
		var lp := c + dir * (r - 38.0) + Vector2(-20.0, 6.0)
		draw_string(_font, lp, _tick_labels[k], HORIZONTAL_ALIGNMENT_CENTER, 40.0, 14, Color(1, 1, 1, 0.65))
	var na := a0 + sweep * f
	var ndir := Vector2(cos(na), sin(na))
	draw_line(c - ndir * 12.0, c + ndir * (r - 18.0), Color(1.0, 0.25, 0.2), 4.0, true)
	draw_circle(c, 9.0, Color(0.15, 0.15, 0.18))
	draw_string(_font, c + Vector2(-80.0, 52.0), _speed_text, HORIZONTAL_ALIGNMENT_CENTER, 160.0, 44, UiStyle.TEXT)
	draw_string(_font, c + Vector2(-80.0, 74.0), "km/h", HORIZONTAL_ALIGNMENT_CENTER, 160.0, 16, UiStyle.DIM)
	draw_string(_font, c + Vector2(-40.0, -30.0), _gear_text, HORIZONTAL_ALIGNMENT_CENTER, 80.0, 40, UiStyle.ACCENT)
	_draw_rpm_bar(Rect2(18.0, c.y + r + 24.0, size.x - 36.0, 18.0))


func _draw_rpm_bar(rect: Rect2) -> void:
	var rpm := car.drivetrain.rpm
	draw_rect(rect.grow(3.0), Color(0.03, 0.04, 0.06, 0.6))
	draw_rect(rect, Color(1.0, 1.0, 1.0, 0.1))
	var red_x := rect.position.x + rect.size.x * (Config.RPM_UPSHIFT / Config.RPM_MAX)
	draw_rect(Rect2(red_x, rect.position.y, rect.end.x - red_x, rect.size.y), Color(0.8, 0.05, 0.05, 0.45))
	var f := clampf(rpm / Config.RPM_MAX, 0.0, 1.0)
	var fill := Color(0.35, 0.85, 1.0) if rpm < Config.RPM_UPSHIFT else Color(1.0, 0.2, 0.15)
	draw_rect(Rect2(rect.position, Vector2(rect.size.x * f, rect.size.y)), fill)
	for k in 8:
		var x := rect.position.x + rect.size.x * float(k * 1000) / Config.RPM_MAX
		draw_line(Vector2(x, rect.position.y), Vector2(x, rect.end.y), Color(0, 0, 0, 0.5), 1.0)
	draw_string(_font, Vector2(rect.position.x, rect.position.y - 4.0), "RPM x1000", HORIZONTAL_ALIGNMENT_LEFT, -1.0, 13, UiStyle.DIM)
