class_name PauseMenu
extends CanvasLayer
## R-29 / common rules: Esc pauses the race; Resume, Restart, Menu, Quit.

signal resume_requested
signal restart_requested
signal menu_requested
signal quit_requested

var _resume: Button


func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = UiStyle.theme()
	add_child(root)
	var dim := ColorRect.new()
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim.color = Color(0.0, 0.0, 0.0, 0.55)
	root.add_child(dim)
	var panel := PanelContainer.new()
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	root.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 14)
	panel.add_child(box)
	var title := UiStyle.label("PAUSED", 56, UiStyle.ACCENT, 10)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	_resume = UiStyle.button("RESUME")
	_resume.pressed.connect(func() -> void: resume_requested.emit())
	box.add_child(_resume)
	var restart := UiStyle.button("RESTART")
	restart.pressed.connect(func() -> void: restart_requested.emit())
	box.add_child(restart)
	var menu := UiStyle.button("MAIN MENU")
	menu.pressed.connect(func() -> void: menu_requested.emit())
	box.add_child(menu)
	var quit := UiStyle.button("QUIT GAME")
	quit.pressed.connect(func() -> void: quit_requested.emit())
	box.add_child(quit)
	visible = false


func open() -> void:
	visible = true
	_resume.grab_focus()
