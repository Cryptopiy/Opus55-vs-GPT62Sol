extends RefCounted
## UI theme built in code (no external assets) + small helpers for building panels and buttons.

const PANEL_BG := Color(0.1, 0.13, 0.2, 0.86)
const PANEL_BORDER := Color(0.45, 0.6, 0.85, 0.35)
const ACCENT := Color(0.32, 0.72, 1.0)
const TEXT := Color(0.94, 0.96, 1.0)
const TEXT_DIM := Color(0.7, 0.76, 0.86)
const GOOD := Color(0.4, 0.95, 0.55)
const BAD := Color(1.0, 0.45, 0.4)
const STAR := Color(1.0, 0.82, 0.25)


static func _box(bg: Color, border: Color, radius: int, pad: float) -> StyleBoxFlat:
	var sb := StyleBoxFlat.new()
	sb.bg_color = bg
	sb.border_color = border
	sb.set_border_width_all(1)
	sb.set_corner_radius_all(radius)
	sb.content_margin_left = pad
	sb.content_margin_right = pad
	sb.content_margin_top = pad * 0.6
	sb.content_margin_bottom = pad * 0.6
	return sb


static func make_theme() -> Theme:
	var t := Theme.new()
	t.default_font_size = 18
	t.set_stylebox("panel", "Panel", _box(PANEL_BG, PANEL_BORDER, 12, 12))
	t.set_stylebox("panel", "PanelContainer", _box(PANEL_BG, PANEL_BORDER, 12, 12))
	t.set_stylebox("normal", "Button", _box(Color(0.18, 0.23, 0.33), Color(0.4, 0.5, 0.7, 0.5), 8, 12))
	t.set_stylebox("hover", "Button", _box(Color(0.24, 0.31, 0.45), ACCENT, 8, 12))
	t.set_stylebox("pressed", "Button", _box(Color(0.16, 0.42, 0.66), ACCENT, 8, 12))
	t.set_stylebox("hover_pressed", "Button", _box(Color(0.2, 0.48, 0.74), ACCENT, 8, 12))
	t.set_stylebox("disabled", "Button", _box(Color(0.14, 0.16, 0.2, 0.8), Color(0.3, 0.3, 0.35, 0.4), 8, 12))
	t.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	t.set_color("font_color", "Button", TEXT)
	t.set_color("font_hover_color", "Button", Color.WHITE)
	t.set_color("font_pressed_color", "Button", Color.WHITE)
	t.set_color("font_disabled_color", "Button", Color(0.5, 0.52, 0.58))
	t.set_color("font_color", "Label", TEXT)
	t.set_color("font_shadow_color", "Label", Color(0, 0, 0, 0.45))
	t.set_constant("shadow_offset_x", "Label", 1)
	t.set_constant("shadow_offset_y", "Label", 1)
	t.set_stylebox("panel", "TooltipPanel", _box(Color(0.06, 0.08, 0.12, 0.96), ACCENT, 6, 8))
	t.set_color("font_color", "TooltipLabel", TEXT)
	t.set_font_size("font_size", "TooltipLabel", 16)
	return t


static func label(text: String, size: int = 18, color: Color = TEXT) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


static func button(text: String, callback: Callable, min_width: float = 0.0) -> Button:
	var b := Button.new()
	b.text = text
	# no keyboard focus: Space / Enter must reach the game hotkeys, not re-trigger the last clicked button
	b.focus_mode = Control.FOCUS_NONE
	b.custom_minimum_size = Vector2(min_width, 38)
	b.pressed.connect(callback)
	return b


static func panel() -> PanelContainer:
	var p := PanelContainer.new()
	p.mouse_filter = Control.MOUSE_FILTER_STOP
	return p


## Opaque panel for modal windows (results, pause menu, tutorial) so text behind never shows through.
static func modal_panel() -> PanelContainer:
	var p := panel()
	p.add_theme_stylebox_override("panel", _box(Color(PANEL_BG.r, PANEL_BG.g, PANEL_BG.b, 0.98), ACCENT.darkened(0.3), 14, 18))
	return p


## Full-screen dim layer that blocks clicks to the game behind a modal panel.
static func dimmer(alpha: float = 0.55) -> ColorRect:
	var d := ColorRect.new()
	d.color = Color(0.02, 0.03, 0.06, alpha)
	d.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	d.mouse_filter = Control.MOUSE_FILTER_STOP
	return d
