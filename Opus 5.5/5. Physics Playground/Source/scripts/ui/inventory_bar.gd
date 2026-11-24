extends PanelContainer
## P-15 inventory bar (bottom centre): icon, name, remaining count and hotkey 1-9 per item; tooltip per type.
## Clicking a slot (or its hotkey) picks the item: the editor then shows a ghost following the cursor.

const Style := preload("res://scripts/ui/ui_style.gd")
const Widgets := preload("res://scripts/ui/widgets.gd")
const Registry := preload("res://scripts/objects/registry.gd")

var session: Node
var _slots: Array[Button] = []
var _counts: Array[Label] = []
var _types: Array[String] = []


func setup(p_session: Node) -> void:
	session = p_session
	name = "Inventory"
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	add_child(row)
	var order: Array[String] = session.model.order
	for i in order.size():
		var type := order[i]
		var b := Button.new()
		b.toggle_mode = true
		b.focus_mode = Control.FOCUS_NONE
		b.custom_minimum_size = Vector2(118, 96)
		b.tooltip_text = "%s\nHotkey %d" % [Registry.TIPS[type], i + 1]
		b.pressed.connect(_on_slot.bind(type))
		var v := VBoxContainer.new()
		v.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		v.mouse_filter = Control.MOUSE_FILTER_IGNORE
		v.add_theme_constant_override("separation", 0)
		b.add_child(v)
		var top := HBoxContainer.new()
		top.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var key := Style.label(" %d" % (i + 1), 14, Style.ACCENT)
		top.add_child(key)
		var icon := Widgets.ObjectIcon.new(type)
		icon.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		top.add_child(icon)
		v.add_child(top)
		var nm := Style.label(Registry.NAMES[type], 14)
		nm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		v.add_child(nm)
		var cnt := Style.label("", 14, Style.TEXT_DIM)
		cnt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		v.add_child(cnt)
		row.add_child(b)
		_slots.append(b)
		_counts.append(cnt)
		_types.append(type)
	session.model.changed.connect(refresh)
	refresh()


func _on_slot(type: String) -> void:
	session.editor.select_tool(type)
	refresh()


func refresh() -> void:
	var building: bool = session.state == 0
	for i in _slots.size():
		var type := _types[i]
		var left: int = session.model.remaining(type)
		var total: int = session.model.inventory[type]
		_counts[i].text = "%d / %d left" % [left, total]
		_slots[i].set_pressed_no_signal(session.editor.tool == type)
		_slots[i].disabled = not building or (left <= 0 and session.editor.tool != type)


func _process(_delta: float) -> void:
	# the editor can change the tool via hotkeys; keep the highlighted slot in sync
	for i in _slots.size():
		var want: bool = session.editor.tool == _types[i]
		if _slots[i].button_pressed != want:
			_slots[i].set_pressed_no_signal(want)
