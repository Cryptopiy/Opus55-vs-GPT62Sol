class_name LabPalette
extends RefCounted

const DARK := Color("#111e30")
const PANEL := Color("#17283b")
const TILE := Color("#21364b")
const EDGE := Color("#385168")
const MUTED := Color("#a8bfd3")
const TEXT := Color("#eef4f8")
const AMBER := Color("#ffc77d")
const GREEN := Color("#7ee7c4")
const RED := Color("#ff9e8c")
var headline := SystemFont.new()
var reading := SystemFont.new()
var mono := SystemFont.new()

func _init() -> void:
	headline.font_names = PackedStringArray(["Bahnschrift", "Segoe UI"])
	headline.font_weight = 700
	reading.font_names = PackedStringArray(["Segoe UI", "Arial"])
	mono.font_names = PackedStringArray(["Cascadia Mono", "Consolas"])

func box(color: Color, radius := 10, edge := Color.TRANSPARENT, border := 0) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = edge
	style.set_border_width_all(border)
	style.set_corner_radius_all(radius)
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 6
	style.content_margin_bottom = 6
	return style

func label(value: String, size: int, color := TEXT, face := 0) -> Label:
	var node := Label.new()
	node.text = value
	node.mouse_filter = Control.MOUSE_FILTER_IGNORE
	node.add_theme_font_override("font", headline if face == 1 else (mono if face == 2 else reading))
	node.add_theme_font_size_override("font_size", size)
	node.add_theme_color_override("font_color", color)
	return node

func button(value: String, action: Callable, accent := false) -> Button:
	var node := Button.new()
	node.text = value
	node.focus_mode = Control.FOCUS_NONE
	node.custom_minimum_size.y = 42
	node.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	node.add_theme_font_override("font", headline)
	node.add_theme_font_size_override("font_size", 16)
	node.add_theme_color_override("font_color", DARK if accent else TEXT)
	node.add_theme_color_override("font_hover_color", DARK if accent else AMBER)
	node.add_theme_color_override("font_pressed_color", DARK if accent else GREEN)
	node.add_theme_color_override("font_disabled_color", MUTED.darkened(0.30))
	node.add_theme_stylebox_override("normal", box(AMBER if accent else TILE, 8, EDGE, 1))
	node.add_theme_stylebox_override("hover", box(AMBER.lightened(0.2) if accent else TILE.lightened(0.13), 8, AMBER, 1))
	node.add_theme_stylebox_override("pressed", box(AMBER.darkened(0.14) if accent else DARK, 8, GREEN, 1))
	node.add_theme_stylebox_override("disabled", box(DARK.lightened(0.04), 8, EDGE, 1))
	node.pressed.connect(action)
	return node
