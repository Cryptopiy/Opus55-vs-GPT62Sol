extends Node
## U-01..U-06: the single place that decides whether interface is drawn (F8 toggles it, default visible).
## All hideable interface lives below registered root Controls (HUD layer: HUD, inventory bar, banners,
## tutorial, tooltips' owners; debug layer: F1 overlay, F10 dev menu). Only the roots' visibility is flipped, so
## everything created below them later (a new HUD after Restart or a level change, a new banner or popup) is
## hidden too until F8 is pressed again. Screens the player needs to continue (level select, pause menu,
## results) live under a separate root that is never registered here, so the game can never get stuck.
## Gameplay is untouched: no pausing, no input blocking, no state change - only CanvasItem visibility.

signal changed(ui_visible: bool)

var ui_visible := true
var _roots: Array[Control] = []


func register_root(root: Control) -> void:
	_roots.append(root)
	root.visible = ui_visible


func toggle() -> void:
	set_ui_visible(not ui_visible)


func set_ui_visible(on: bool) -> void:
	ui_visible = on
	for r in _roots:
		if is_instance_valid(r):
			# hiding a root propagates NOTIFICATION_VISIBILITY_CHANGED to every descendant, which also cancels
			# any tooltip that one of them is showing
			r.visible = on
	changed.emit(on)


## Every Control below the registered roots that is currently drawn (used by the automated check).
func drawn_controls() -> Array[Control]:
	var out: Array[Control] = []
	for r in _roots:
		if not is_instance_valid(r):
			continue
		for n in r.find_children("*", "Control", true, false):
			var c := n as Control
			if c.is_visible_in_tree():
				out.append(c)
	return out
