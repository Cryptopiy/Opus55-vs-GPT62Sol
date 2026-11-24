class_name Minimap
extends Control
## R-27: corner minimap drawn from the track spline: track line, start line,
## a dot per car in its team colour and an arrow for the player.

const PAD := 16.0

var race: Race

var _line := PackedVector2Array()
var _scale := 1.0
var _origin := Vector2.ZERO
var _offset := Vector2.ZERO
var _arrow := PackedVector2Array([Vector2.ZERO, Vector2.ZERO, Vector2.ZERO])
var _outline := PackedVector2Array([Vector2.ZERO, Vector2.ZERO, Vector2.ZERO, Vector2.ZERO])
var _start_a := Vector2.ZERO
var _start_b := Vector2.ZERO


func _ready() -> void:
	custom_minimum_size = Vector2(Config.MINIMAP_SIZE, Config.MINIMAP_SIZE)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func bind(r: Race) -> void:
	race = r
	var t := r.track
	var b := t.bounds
	var span := maxf(b.size.x, b.size.z)
	_scale = (Config.MINIMAP_SIZE - PAD * 2.0) / maxf(span, 1.0)
	_origin = Vector2(b.position.x, b.position.z)
	# Centre the shorter axis.
	_offset = Vector2(PAD + (span - b.size.x) * 0.5 * _scale, PAD + (span - b.size.z) * 0.5 * _scale)
	_line.clear()
	for i in range(0, t.count, 2):
		_line.append(_to_map(t.points[i]))
	_line.append(_to_map(t.points[0]))
	var p0 := t.points[0]
	var r0 := t.rights[0] * (Config.ROAD_WIDTH * 0.5 + 6.0)
	_start_a = _to_map(p0 - r0)
	_start_b = _to_map(p0 + r0)


func _to_map(p: Vector3) -> Vector2:
	return Vector2(p.x - _origin.x, p.z - _origin.y) * _scale + _offset


func _process(_delta: float) -> void:
	if race != null:
		queue_redraw()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.03, 0.04, 0.06, 0.86))
	if race == null:
		return
	draw_polyline(_line, Color(0.0, 0.0, 0.0, 0.7), 8.0, true)
	draw_polyline(_line, Color(0.82, 0.84, 0.88), 4.0, true)
	draw_line(_start_a, _start_b, Color(1.0, 1.0, 1.0), 3.0)
	for car in race.cars:
		if car == race.player:
			continue
		draw_circle(_to_map(car.global_position), 6.5, Color(0, 0, 0, 0.8))
		draw_circle(_to_map(car.global_position), 5.0, car.paint_color)
	var pl := race.player
	var p := _to_map(pl.global_position)
	var fwd3 := -pl.global_transform.basis.z
	var fwd := Vector2(fwd3.x, fwd3.z)
	if fwd.length_squared() < 0.0001:
		fwd = Vector2.UP
	fwd = fwd.normalized()
	var side := Vector2(-fwd.y, fwd.x)
	_arrow[0] = p + fwd * 11.0
	_arrow[1] = p - fwd * 7.0 + side * 7.0
	_arrow[2] = p - fwd * 7.0 - side * 7.0
	draw_colored_polygon(_arrow, pl.paint_color)
	for k in 3:
		_outline[k] = _arrow[k]
	_outline[3] = _arrow[0]
	draw_polyline(_outline, Color(0, 0, 0, 0.9), 1.5, true)
