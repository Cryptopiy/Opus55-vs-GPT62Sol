extends PanelContainer
## F10 dev menu: determinism test (P-02), load reference solution (F9 equivalent), box-stack test (P-28).

const Style := preload("res://scripts/ui/ui_style.gd")

var main: Node
var _out: Label
var _buttons: Array[Button] = []


func _ready() -> void:
	name = "DevMenu"
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 8)
	add_child(v)
	v.add_child(Style.label("Dev menu (F10)", 22, Style.ACCENT))
	_buttons.append(Style.button("Determinism test: solution x5", _on_determinism, 320))
	_buttons.append(Style.button("Load reference solution (F9)", _on_solution, 320))
	_buttons.append(Style.button("Stack test: 3 boxes, 10 s", _on_stack, 320))
	for b in _buttons:
		v.add_child(b)
	_out = Style.label("Results appear here.", 14, Style.TEXT_DIM)
	_out.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_out.custom_minimum_size = Vector2(320, 0)
	v.add_child(_out)
	# right edge, vertically centred; grows to the left when the result text gets longer
	grow_horizontal = Control.GROW_DIRECTION_BEGIN
	grow_vertical = Control.GROW_DIRECTION_BOTH
	set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT, Control.PRESET_MODE_MINSIZE, 12)
	visible = false
	main.get(&"dev_tools").report.connect(func(t: String) -> void: _out.text = t)


func _session() -> Node:
	return main.get(&"session")


func _set_busy(on: bool) -> void:
	for b in _buttons:
		b.disabled = on


func _on_determinism() -> void:
	if _session() == null:
		return
	_set_busy(true)
	_out.text = "Running the reference solution 5 times..."
	await main.get(&"dev_tools").run_determinism(_session())
	_set_busy(false)


func _on_solution() -> void:
	if _session():
		_session().load_solution()


func _on_stack() -> void:
	if _session() == null:
		return
	_set_busy(true)
	await main.get(&"dev_tools").run_stack_test(_session())
	_set_busy(false)
