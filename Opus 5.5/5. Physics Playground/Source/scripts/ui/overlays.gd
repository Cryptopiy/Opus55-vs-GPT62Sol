extends RefCounted
## Modal / overlay panels built in code: results (P-13), tutorial (L1 first launch), pause menu (Esc).

const Style := preload("res://scripts/ui/ui_style.gd")
const Widgets := preload("res://scripts/ui/widgets.gd")


static func _centered_panel(root: Control, min_width: float) -> VBoxContainer:
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(center)
	var p := Style.modal_panel()
	p.custom_minimum_size = Vector2(min_width, 0)
	center.add_child(p)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 10)
	p.add_child(v)
	return v


static func results(stars: int, objects: int, par: int, time: float, par_time: float, best: int, has_next: bool,
		on_next: Callable, on_retry: Callable, on_levels: Callable) -> Control:
	var root := Control.new()
	root.name = "Results"
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(Style.dimmer(0.35))
	var v := _centered_panel(root, 420)
	var title := Style.label("Level complete!", 32, Style.GOOD)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(title)
	var star_box := CenterContainer.new()
	var sr := Widgets.StarRow.new(48.0)
	sr.set_filled(stars)
	star_box.add_child(sr)
	v.add_child(star_box)
	v.add_child(Style.label("Objects used: %d   (par %d)%s" % [objects, par, "  OK" if objects <= par else ""], 18, Style.GOOD if objects <= par else Style.TEXT))
	v.add_child(Style.label("Time: %.2f s   (par %.1f s)%s" % [time, par_time, "  OK" if time <= par_time + 0.0001 else ""], 18, Style.GOOD if time <= par_time + 0.0001 else Style.TEXT))
	v.add_child(Style.label("1 star: complete   2: <= par objects   3: and <= par time", 14, Style.TEXT_DIM))
	v.add_child(Style.label("Best: %d / 3 stars (saved)" % best, 16, Style.STAR))
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 10)
	if has_next:
		row.add_child(Style.button("Next level", on_next, 120))
	row.add_child(Style.button("Retry", on_retry, 100))
	row.add_child(Style.button("Level select", on_levels, 130))
	v.add_child(row)
	return root


static func tutorial(on_done: Callable) -> Control:
	var root := Control.new()
	root.name = "Tutorial"
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var v := _centered_panel(root, 560)
	v.add_child(Style.label("How to play", 28, Style.ACCENT))
	var lines := [
		"1. Pick a Plank in the bar below (click it or press 1).",
		"2. Move the mouse: the ghost snaps to a 0.25 m grid. Green = OK, red = blocked.",
		"3. Rotate with Q / E (15 deg) or Ctrl + mouse wheel (5 deg). Left-click to place.",
		"4. Click a placed object to select it, drag to move, Del to remove. Ctrl+Z / Ctrl+Y undo/redo.",
		"5. Press Space to play, Space again to pause, R to reset everything.",
		"Goal: get the ball into the glowing cup and keep it there for 1 second.",
		"Camera: middle mouse orbit, Shift+middle pan, wheel zoom, F re-frame.",
	]
	for line: String in lines:
		var l := Style.label(line, 17)
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.custom_minimum_size = Vector2(540, 0)
		v.add_child(l)
	var dismiss := func() -> void:
		on_done.call()
		root.queue_free()
	var b := Style.button("Got it", dismiss, 140)
	var box := CenterContainer.new()
	box.add_child(b)
	v.add_child(box)
	return root


static func pause_menu(on_resume: Callable, on_restart: Callable, on_quit: Callable) -> Control:
	var root := Control.new()
	root.name = "PauseMenu"
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(Style.dimmer(0.55))
	var v := _centered_panel(root, 300)
	var t := Style.label("Paused", 30)
	t.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(t)
	v.add_child(Style.button("Resume", on_resume, 260))
	v.add_child(Style.button("Restart level", on_restart, 260))
	v.add_child(Style.button("Quit game", on_quit, 260))
	var hint := Style.label("Esc closes this menu", 14, Style.TEXT_DIM)
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(hint)
	return root
