extends RefCounted
## Vector-drawn widgets (no image assets): star rows, padlock and the inventory icons of every object type.

const C := preload("res://scripts/core/config.gd")
const Style := preload("res://scripts/ui/ui_style.gd")


class StarRow extends Control:
	var count := 3
	var filled := 0
	var star_size := 22.0

	func _init(p_size: float = 22.0) -> void:
		star_size = p_size
		custom_minimum_size = Vector2(star_size * 3.0 + 8.0, star_size)
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func set_filled(n: int) -> void:
		if n != filled:
			filled = n
			queue_redraw()

	func _draw() -> void:
		for i in count:
			var c := Vector2(star_size * 0.5 + i * (star_size + 4.0), star_size * 0.5)
			var pts := PackedVector2Array()
			for k in 10:
				var r := star_size * (0.5 if k % 2 == 0 else 0.22)
				var a := -PI * 0.5 + k * PI / 5.0
				pts.append(c + Vector2(cos(a), sin(a)) * r)
			if i < filled:
				draw_colored_polygon(pts, Style.STAR)
			else:
				draw_colored_polygon(pts, Color(1, 1, 1, 0.12))
			pts.append(pts[0])
			draw_polyline(pts, Style.STAR if i < filled else Color(1, 1, 1, 0.45), 1.5, true)


class Padlock extends Control:
	func _init() -> void:
		custom_minimum_size = Vector2(40, 48)
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var col := Color(0.75, 0.8, 0.9)
		draw_arc(Vector2(20, 20), 11.0, PI, TAU, 24, col, 4.0, true)
		draw_line(Vector2(9, 20), Vector2(9, 24), col, 4.0)
		draw_line(Vector2(31, 20), Vector2(31, 24), col, 4.0)
		draw_rect(Rect2(4, 24, 32, 22), col)
		draw_circle(Vector2(20, 33), 3.5, Color(0.1, 0.12, 0.18))


## Inventory icon: a tiny 2D drawing of the object in its scene colours.
class ObjectIcon extends Control:
	var type := ""

	func _init(p_type: String) -> void:
		type = p_type
		custom_minimum_size = Vector2(56, 40)
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var s := size
		var c := s * 0.5
		match type:
			"plank":
				_rot_rect(c, Vector2(48, 8), -0.25, C.COL_PLANK)
			"box_s":
				draw_rect(Rect2(c - Vector2(15, 15), Vector2(30, 30)), C.COL_BOX_S)
				draw_line(c + Vector2(-13, 13), c + Vector2(13, -13), C.COL_BOX_S.darkened(0.3), 3.0)
			"box_h":
				draw_rect(Rect2(c - Vector2(15, 15), Vector2(30, 30)), C.COL_BOX_H)
				draw_rect(Rect2(c + Vector2(-15, -9), Vector2(30, 5)), C.COL_BOX_H.darkened(0.45))
				draw_rect(Rect2(c + Vector2(-15, 5), Vector2(30, 5)), C.COL_BOX_H.darkened(0.45))
			"lever":
				draw_colored_polygon(PackedVector2Array([c + Vector2(0, 2), c + Vector2(-9, 16), c + Vector2(9, 16)]), C.COL_LEVER_STAND.lightened(0.3))
				_rot_rect(c + Vector2(0, -1), Vector2(50, 6), 0.22, C.COL_LEVER)
			"rope":
				draw_rect(Rect2(c + Vector2(-8, -18), Vector2(16, 5)), C.COL_BOB.lightened(0.3))
				draw_line(c + Vector2(0, -13), c + Vector2(12, 8), C.COL_ROPE, 3.0)
				draw_rect(Rect2(c + Vector2(4, 8), Vector2(18, 4)), C.COL_BOB.lightened(0.4))
				draw_rect(Rect2(c + Vector2(4, 2), Vector2(3, 7)), C.COL_BOB.lightened(0.4))
				draw_rect(Rect2(c + Vector2(19, 2), Vector2(3, 7)), C.COL_BOB.lightened(0.4))
			"spring":
				draw_rect(Rect2(c + Vector2(-18, 10), Vector2(36, 6)), C.COL_SPRING.darkened(0.5))
				for k in 3:
					draw_line(c + Vector2(-10, 9 - k * 6), c + Vector2(10, 6 - k * 6), Color(0.8, 0.82, 0.86), 2.5)
				draw_rect(Rect2(c + Vector2(-18, -14), Vector2(36, 6)), C.COL_SPRING)
			"conveyor":
				draw_rect(Rect2(c + Vector2(-24, -6), Vector2(48, 12)), C.COL_CONVEYOR.lightened(0.15))
				for k in 4:
					var x := -18.0 + k * 11.0
					draw_polyline(PackedVector2Array([c + Vector2(x, -4), c + Vector2(x + 5, 0), c + Vector2(x, 4)]), C.COL_CONVEYOR_STRIPE, 2.0)
			"cloth":
				draw_rect(Rect2(c + Vector2(-24, -10), Vector2(4, 16)), C.COL_CLOTH.darkened(0.45))
				draw_rect(Rect2(c + Vector2(20, -10), Vector2(4, 16)), C.COL_CLOTH.darkened(0.45))
				var pts := PackedVector2Array()
				for k in 11:
					var u := float(k) / 10.0
					pts.append(c + Vector2(-20 + 40 * u, -8 + 14 * sin(u * PI)))
				draw_polyline(pts, C.COL_CLOTH, 3.0, true)
			"fan":
				draw_rect(Rect2(c + Vector2(-22, -14), Vector2(10, 28)), C.COL_FAN)
				draw_line(c + Vector2(-10, -10), c + Vector2(-10, 10), Color(0.92, 0.95, 0.93), 3.0)
				for k in 3:
					draw_line(c + Vector2(-4, -8 + k * 8), c + Vector2(20, -8 + k * 8), Color(0.8, 0.92, 1.0, 0.8), 1.5)

	func _rot_rect(center: Vector2, sz: Vector2, ang: float, col: Color) -> void:
		var h := sz * 0.5
		var pts := PackedVector2Array([Vector2(-h.x, -h.y), Vector2(h.x, -h.y), Vector2(h.x, h.y), Vector2(-h.x, h.y)])
		for i in pts.size():
			pts[i] = center + pts[i].rotated(ang)
		draw_colored_polygon(pts, col)
