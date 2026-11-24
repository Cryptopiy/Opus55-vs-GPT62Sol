class_name UiStyle
extends RefCounted
## Shared look of all menus and the HUD, built in code (theme, panels, labels).

const ACCENT := Color(1.0, 0.55, 0.1)
const PANEL_BG := Color(0.05, 0.06, 0.09, 0.72)
const TEXT := Color(0.96, 0.97, 1.0)
const DIM := Color(0.7, 0.74, 0.8)
const GOOD := Color(0.45, 1.0, 0.55)
const BAD := Color(1.0, 0.3, 0.25)

static var _theme: Theme


static func clear_cache() -> void:
	_theme = null


static func theme() -> Theme:
	if _theme != null:
		return _theme
	var t := Theme.new()
	t.default_font_size = 22
	t.set_stylebox("normal", "Button", _box(Color(0.12, 0.14, 0.2, 0.92), Color(0.3, 0.34, 0.45), 2))
	t.set_stylebox("hover", "Button", _box(Color(0.18, 0.2, 0.28, 0.95), ACCENT, 2))
	t.set_stylebox("pressed", "Button", _box(Color(0.3, 0.18, 0.08, 0.95), ACCENT, 2))
	t.set_stylebox("focus", "Button", _box(Color(0.0, 0.0, 0.0, 0.0), ACCENT, 2))
	t.set_stylebox("disabled", "Button", _box(Color(0.1, 0.1, 0.12, 0.8), Color(0.2, 0.2, 0.25), 1))
	t.set_color("font_color", "Button", TEXT)
	t.set_color("font_hover_color", "Button", Color(1.0, 0.85, 0.6))
	t.set_color("font_pressed_color", "Button", ACCENT)
	t.set_color("font_focus_color", "Button", TEXT)
	for type_name in ["OptionButton"]:
		t.set_stylebox("normal", type_name, _box(Color(0.12, 0.14, 0.2, 0.92), Color(0.3, 0.34, 0.45), 2))
		t.set_stylebox("hover", type_name, _box(Color(0.18, 0.2, 0.28, 0.95), ACCENT, 2))
		t.set_stylebox("pressed", type_name, _box(Color(0.3, 0.18, 0.08, 0.95), ACCENT, 2))
		t.set_stylebox("focus", type_name, _box(Color(0.0, 0.0, 0.0, 0.0), ACCENT, 2))
	t.set_stylebox("normal", "LineEdit", _box(Color(0.03, 0.04, 0.06, 0.95), Color(0.3, 0.34, 0.45), 2))
	t.set_stylebox("focus", "LineEdit", _box(Color(0.0, 0.0, 0.0, 0.0), ACCENT, 2))
	t.set_color("font_color", "LineEdit", TEXT)
	t.set_color("font_color", "Label", TEXT)
	t.set_stylebox("panel", "PanelContainer", panel_box())
	_theme = t
	return t


static func _box(bg: Color, border: Color, border_w: int) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg
	sb.border_color = border
	sb.set_border_width_all(border_w)
	sb.set_corner_radius_all(8)
	sb.content_margin_left = 14.0
	sb.content_margin_right = 14.0
	sb.content_margin_top = 8.0
	sb.content_margin_bottom = 8.0
	return sb


static func panel_box() -> StyleBoxFlat:
	var sb := _box(PANEL_BG, Color(1.0, 1.0, 1.0, 0.08), 1)
	sb.set_corner_radius_all(12)
	sb.content_margin_left = 18.0
	sb.content_margin_right = 18.0
	sb.content_margin_top = 12.0
	sb.content_margin_bottom = 12.0
	return sb


static func label(text: String, size: int, color: Color = TEXT, outline: int = 6) -> Label:
	var l := Label.new()
	l.text = text
	var ls := LabelSettings.new()
	ls.font_size = size
	ls.font_color = color
	ls.outline_size = outline
	ls.outline_color = Color(0.0, 0.0, 0.0, 0.75)
	l.label_settings = ls
	return l


static func set_label_color(l: Label, color: Color) -> void:
	l.label_settings.font_color = color


static func panel() -> PanelContainer:
	var p := PanelContainer.new()
	p.add_theme_stylebox_override("panel", panel_box())
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return p


static func button(text: String, min_width: float = 320.0) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(min_width, 54.0)
	b.add_theme_font_size_override("font_size", 24)
	return b
