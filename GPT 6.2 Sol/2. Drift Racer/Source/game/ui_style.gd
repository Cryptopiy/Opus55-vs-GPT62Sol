class_name PaddockStyle
extends RefCounted

const MINT := Color("#75f5cc")
const PAPER := Color("#f0f5ed")
const MUTED := Color("#a8bdb9")
const WARN := Color("#ffbd73")
const INK := Color("#0d1d22")
const BACK := Color(0.025, 0.060, 0.073, 0.91)
const RULE := Color(0.63, 0.83, 0.78, 0.21)

static func panel(color: Color, border: Color, radius := 4, width := 1) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = border
	style.set_border_width_all(width)
	style.set_corner_radius_all(radius)
	style.content_margin_left = 19.0
	style.content_margin_right = 19.0
	style.content_margin_top = 10.0
	style.content_margin_bottom = 10.0
	return style

static func font(weight: float, italic := false) -> FontVariation:
	var face := FontVariation.new()
	face.base_font = ThemeDB.fallback_font
	face.variation_embolden = weight
	if italic:
		face.variation_transform = Transform2D(Vector2(1.0, 0.0), Vector2(0.13, 1.0), Vector2.ZERO)
	return face

static func theme() -> Theme:
	var result := Theme.new()
	result.default_font_size = 22
	var font_color := Color("#e5f1ed")
	for kind: StringName in [&"Button", &"OptionButton"]:
		result.set_color(&"font_color", kind, font_color)
		result.set_color(&"font_hover_color", kind, MINT)
		result.set_color(&"font_pressed_color", kind, MINT)
		result.set_stylebox(&"normal", kind, panel(Color("#163038"), RULE))
		result.set_stylebox(&"hover", kind, panel(Color("#21474b"), MINT))
		result.set_stylebox(&"pressed", kind, panel(Color("#1b383b"), MINT))
		result.set_stylebox(&"focus", kind, panel(Color.TRANSPARENT, MINT, 4, 2))
	result.set_color(&"font_color", &"LineEdit", font_color)
	result.set_color(&"caret_color", &"LineEdit", MINT)
	result.set_color(&"selection_color", &"LineEdit", Color("#347c70"))
	result.set_stylebox(&"normal", &"LineEdit", panel(Color("#11272e"), RULE))
	result.set_stylebox(&"focus", &"LineEdit", panel(Color.TRANSPARENT, MINT, 4, 2))
	result.set_stylebox(&"panel", &"PopupMenu", panel(INK, RULE))
	result.set_stylebox(&"hover", &"PopupMenu", panel(Color("#25504c"), MINT))
	result.set_constant(&"v_separation", &"PopupMenu", 10)
	return result
