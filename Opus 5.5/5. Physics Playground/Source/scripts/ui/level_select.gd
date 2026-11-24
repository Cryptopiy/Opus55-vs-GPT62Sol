extends Control
## P-21 level select: 5 tiles with name, best stars (0-3) and lock state (level N+1 unlocks after N).

signal level_chosen(index: int)

const C := preload("res://scripts/core/config.gd")
const Style := preload("res://scripts/ui/ui_style.gd")
const Widgets := preload("res://scripts/ui/widgets.gd")


func _ready() -> void:
	name = "LevelSelect"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0.12, 0.16, 0.24)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 26)
	center.add_child(v)
	var title := Style.label("CONTRAPTION LAB", 56, Color(0.9, 0.95, 1.0))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(title)
	var sub := Style.label("Place planks, levers, ropes, springs, conveyors, cloth and fans to deliver the ball into the cup.", 18, Style.TEXT_DIM)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(sub)
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 18)
	v.add_child(row)
	for i in C.LEVELS.size():
		row.add_child(_tile(i))
	var foot := Style.label("Progress is saved automatically.   F1 debug overlay  -  F12 screenshot  -  Esc menu", 15, Style.TEXT_DIM)
	foot.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(foot)


func _tile(i: int) -> Control:
	var lvl: Dictionary = C.LEVELS[i]
	var unlocked := Save.is_unlocked(i)
	var b := Button.new()
	b.name = "Tile%d" % (i + 1)
	b.focus_mode = Control.FOCUS_NONE
	b.custom_minimum_size = Vector2(210, 230)
	b.disabled = not unlocked
	b.tooltip_text = ("Par: %d objects, %.0f s" % [int(lvl["par"]), float(lvl["par_time"])]) if unlocked else "Complete the previous level to unlock"
	b.pressed.connect(func() -> void: level_chosen.emit(i))
	var v := VBoxContainer.new()
	v.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	v.offset_top = 16
	v.offset_bottom = -16
	v.alignment = BoxContainer.ALIGNMENT_CENTER
	v.add_theme_constant_override("separation", 12)
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	b.add_child(v)
	var num := Style.label("Level %d" % (i + 1), 16, Style.ACCENT)
	num.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(num)
	var nm := Style.label(lvl["name"], 24)
	nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(nm)
	var mid := CenterContainer.new()
	mid.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if unlocked:
		var stars := Widgets.StarRow.new(30.0)
		stars.set_filled(Save.best_stars(String(lvl["id"])))
		mid.add_child(stars)
	else:
		mid.add_child(Widgets.Padlock.new())
	v.add_child(mid)
	var state := Style.label("Locked" if not unlocked else ("Completed" if Save.is_completed(String(lvl["id"])) else "New"), 15, Style.TEXT_DIM)
	state.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(state)
	return b
