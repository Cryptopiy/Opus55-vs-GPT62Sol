class_name UiVisibility
extends Node
## U-01..U-06: the single place that decides whether interface is drawn.
## F8 toggles it (default: visible). Mechanism: every top-level interface
## CanvasItem (HUD, F1 overlay, loading text and anything created later) is put
## on one dedicated canvas visibility layer; hiding the UI removes that layer
## from the root viewport's canvas cull mask, so those items and all their
## children stop drawing at once - including in F12 screenshots, which read
## the viewport image. Screens the player must use to go on (main menu, pause
## menu, results) are registered with keep_visible() and are never put on the
## layer, so the game can never get stuck. Nothing else changes: no pause, no
## input or game-state change; only drawing (and the mouse cursor while no
## such screen is open).

signal ui_visibility_changed(ui_visible: bool)

var ui_visible := true

var _layer_bit := 1 << (Config.UI_VISIBILITY_LAYER - 1)
var _must_stay: Array[CanvasLayer] = []
var _cursor_hidden := false


func _ready() -> void:
	name = "UiVisibility"
	process_mode = Node.PROCESS_MODE_ALWAYS
	# Every node that enters the tree later (new HUD texts, popups, overlays)
	# is classified here, so UI spawned while hidden is hidden too (U-04).
	get_tree().node_added.connect(_on_node_added)
	for node in get_tree().root.find_children("*", "CanvasItem", true, false):
		_classify(node as CanvasItem)
	_apply()


## Registers a screen that must stay drawn (menus, results). Call it before
## the layer enters the tree so its children are recognised as they arrive.
func keep_visible(layer: CanvasLayer) -> void:
	if not _must_stay.has(layer):
		_must_stay.append(layer)


func toggle() -> void:
	set_ui_visible(not ui_visible)


func set_ui_visible(value: bool) -> void:
	ui_visible = value
	_apply()
	ui_visibility_changed.emit(ui_visible)


## True when `item` actually draws: visible in the tree and neither it nor
## any CanvasItem parent is culled by its viewport's canvas cull mask.
static func is_drawn(item: CanvasItem) -> bool:
	if not item.is_inside_tree() or not item.is_visible_in_tree():
		return false
	var mask := item.get_viewport().canvas_cull_mask
	var node: Node = item
	while node is CanvasItem:
		if ((node as CanvasItem).visibility_layer & mask) == 0:
			return false
		node = node.get_parent()
	while node != null:
		if node is CanvasLayer and not (node as CanvasLayer).visible:
			return false
		node = node.get_parent()
	return true


func _input(event: InputEvent) -> void:
	# _input (not _unhandled_input) so focused menu widgets cannot swallow it.
	if event.is_action_pressed(InputSetup.TOGGLE_UI):
		toggle()
		get_viewport().set_input_as_handled()


func _process(_delta: float) -> void:
	_update_cursor()


func _apply() -> void:
	var vp := get_viewport()
	if ui_visible:
		vp.canvas_cull_mask = vp.canvas_cull_mask | _layer_bit
	else:
		vp.canvas_cull_mask = vp.canvas_cull_mask & ~_layer_bit
	_update_cursor()


func _on_node_added(node: Node) -> void:
	if node is CanvasItem:
		_classify(node as CanvasItem)


## Top-level canvas items (parent is not a CanvasItem) of every non-exempt
## layer go on the hideable layer; their children are culled with them.
func _classify(item: CanvasItem) -> void:
	if item.get_parent() is CanvasItem or _belongs_to_must_stay(item):
		return
	item.visibility_layer = _layer_bit


func _belongs_to_must_stay(node: Node) -> bool:
	var n := node.get_parent()
	while n != null:
		if n is CanvasLayer and _must_stay.has(n as CanvasLayer):
			return true
		n = n.get_parent()
	return false


## The OS cursor is hidden over the window while the UI is hidden and no
## must-use screen is open (clean footage); it returns with any menu.
func _update_cursor() -> void:
	var screen_open := false
	for layer in _must_stay:
		if layer.visible:
			screen_open = true
			break
	var hide_cursor := not ui_visible and not screen_open
	if hide_cursor != _cursor_hidden:
		_cursor_hidden = hide_cursor
		Input.mouse_mode = Input.MOUSE_MODE_HIDDEN if hide_cursor else Input.MOUSE_MODE_VISIBLE
