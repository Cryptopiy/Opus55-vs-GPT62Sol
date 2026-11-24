class_name LabGlyph
extends Control

var kind := ""
var ink := Color.WHITE

func setup(value: String, tint: Color, extent: Vector2) -> void:
	kind = value
	ink = tint
	custom_minimum_size = extent
	mouse_filter = Control.MOUSE_FILTER_IGNORE

func _draw() -> void:
	var span := minf(size.x, size.y)
	draw_set_transform(size * 0.5, 0, Vector2.ONE * span / 42.0)
	var dark := ink.darkened(0.42)
	match kind:
		"brand":
			draw_rect(Rect2(-19, -19, 38, 38), ink)
			draw_line(Vector2(-12, 10), Vector2(12, 10), LabPalette.DARK, 2, true)
			draw_line(Vector2(-12, 10), Vector2(10, -5), LabPalette.DARK, 3, true)
			draw_circle(Vector2(-6, -5), 5, LabPalette.DARK, true, -1, true)
			draw_circle(Vector2(11, 4), 2, LabPalette.DARK, true, -1, true)
		"plank":
			draw_colored_polygon(PackedVector2Array([Vector2(-18, 7), Vector2(15, -10), Vector2(19, -4), Vector2(-14, 13)]), ink)
			draw_line(Vector2(-12, 8), Vector2(14, -6), dark, 1.6, true)
		"box_s", "box_h":
			draw_rect(Rect2(-12, -12, 24, 24), ink)
			draw_rect(Rect2(-8, -8, 16, 16), dark, false, 2)
			draw_line(Vector2(-8, 8), Vector2(8, -8), dark, 3, true)
		"lever":
			draw_colored_polygon(PackedVector2Array([Vector2(-7, 13), Vector2(0, 0), Vector2(7, 13)]), dark)
			draw_line(Vector2(-18, 5), Vector2(18, -6), ink, 6, true)
			draw_circle(Vector2.ZERO, 2.6, Color("#f2d8c7"), true, -1, true)
		"rope":
			draw_line(Vector2(0, -12), Vector2(3, 8), ink, 2.5, true)
			draw_circle(Vector2(0, -12), 3.5, ink, true, -1, true)
			draw_circle(Vector2(3, 10), 6, ink, true, -1, true)
		"spring":
			draw_polyline(PackedVector2Array([Vector2(-8, 9), Vector2(9, 5), Vector2(-9, 0), Vector2(9, -4), Vector2(-8, -9)]), ink, 2.8, true)
			draw_line(Vector2(-13, 11), Vector2(13, 11), dark, 3, true)
			draw_line(Vector2(-13, -11), Vector2(13, -11), ink, 3, true)
		"conveyor":
			draw_circle(Vector2(-11, 0), 6, ink.lightened(0.36), true, -1, true)
			draw_circle(Vector2(11, 0), 6, ink.lightened(0.36), true, -1, true)
			draw_rect(Rect2(-11, -6, 22, 12), ink.lightened(0.36))
			for x in range(-8, 12, 5):
				draw_line(Vector2(x, -5), Vector2(x - 3, 5), dark, 1.5, true)
		"cloth":
			draw_arc(Vector2(0, -8), 14, 0.2, PI - 0.2, 16, ink, 3.5, true)
			draw_circle(Vector2(-14, -5), 3.3, ink, true, -1, true)
			draw_circle(Vector2(14, -5), 3.3, ink, true, -1, true)
		"fan":
			draw_circle(Vector2.ZERO, 13, ink, false, 2, true)
			for index in range(3):
				var turn := index * TAU / 3.0
				var tip := Vector2(2, -9).rotated(turn)
				draw_line(Vector2.ZERO, tip, ink, 5, true)
			draw_circle(Vector2.ZERO, 2.8, Color("#d3f9e5"), true, -1, true)
