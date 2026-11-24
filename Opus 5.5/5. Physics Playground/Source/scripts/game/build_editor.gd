extends Node3D
## Build-mode editor (P-15, P-16, P-18 input side): ghost preview with 0.25 m / 15 deg snapping that is green when
## valid and red when overlapping scenery, a forbidden zone or another object; LMB place, Q/E rotate 15 deg,
## Ctrl+wheel 5 deg; select (outline) + drag to move, Del removes (refund); Alt+drag handles (lever fulcrum,
## rope bob); Ctrl+Z / Ctrl+Y. Only active in build mode - objects are visual-only until Play.

signal message(text: String)

const C := preload("res://scripts/core/config.gd")
const Fp := preload("res://scripts/core/footprint.gd")
const Registry := preload("res://scripts/objects/registry.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")
const Lever := preload("res://scripts/objects/obj_lever.gd")
const Rope := preload("res://scripts/objects/obj_rope.gd")
const PlacementModel := preload("res://scripts/core/placement_model.gd")
const SH_GRID := preload("res://shaders/grid_local.gdshader")

var model: PlacementModel
var camera: Node3D
var level := {}
var obstacles: Array = []
var forbidden: Array = []
var bounds := Rect2()
var enabled := true

var tool := ""
var tool_rot := 0.0
var selected_id := -1

var _visual_root: Node3D
var _visuals := {}        # id -> Node3D
var _visual_recs := {}    # id -> record the visual was built from
var _ghost: Node3D
var _ghost_rec := {}
var _ghost_ok := false
var _hint_root: Node3D
var _handle_root: Node3D
var _grid: MeshInstance3D
var _band: MeshInstance3D
var _cursor := Vector2.ZERO
var _over_ui := false
var _drag := {}


func setup(p_model: PlacementModel, p_camera: Node3D, p_level: Dictionary, p_obstacles: Array, p_forbidden: Array) -> void:
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	model = p_model
	camera = p_camera
	level = p_level
	obstacles = p_obstacles
	forbidden = p_forbidden
	bounds = p_level["bounds"]
	_visual_root = _child("Placed")
	_hint_root = _child("Hints")
	_handle_root = _child("Handles")
	var gm := ShaderMaterial.new()
	gm.shader = SH_GRID
	_grid = ML.add_mesh(self, ML.quad(Vector2(7, 7)), Vector3(0, 0, -0.52), gm)
	_grid.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_grid.visible = false
	if level.has("rope_anchor_y"):
		var yr: Vector2 = level["rope_anchor_y"]
		_band = ML.add_mesh(self, ML.quad(Vector2(bounds.size.x, yr.y - yr.x)), Vector3(bounds.get_center().x, (yr.x + yr.y) * 0.5, -0.5), Mats.ghost(true))
		_band.transparency = 0.75
		_band.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		_band.visible = false
	model.changed.connect(_sync_visuals)


func _child(n: String) -> Node3D:
	var node := Node3D.new()
	node.name = n
	add_child(node)
	return node


func set_enabled(on: bool) -> void:
	enabled = on
	_visual_root.visible = on
	_hint_root.visible = on
	if not on:
		_drag.clear()
		_set_ghost_visible(false)
		_grid.visible = false
		if _band:
			_band.visible = false
	_refresh_handles()


# ------------------------------------------------------------------ validity
func is_valid(rec: Dictionary, ignore_id: int) -> bool:
	return validity_reason(rec, ignore_id) == ""


## Empty string when the record may be placed, otherwise a short reason for the HUD.
func validity_reason(rec: Dictionary, ignore_id: int) -> String:
	var fp := Registry.footprint(rec)
	if not Fp.inside_bounds(fp, bounds):
		return "Outside the level"
	if Fp.any_overlap(fp, forbidden, C.OVERLAP_EPS):
		return "Forbidden zone"
	if Fp.any_overlap(fp, obstacles, C.OVERLAP_EPS):
		return "Overlaps scenery"
	for other in model.records:
		if int(other["id"]) != ignore_id and Fp.any_overlap(fp, Registry.footprint(other), C.OVERLAP_EPS):
			return "Overlaps another object"
	if String(rec["type"]) == "rope" and level.has("rope_anchor_y"):
		var yr: Vector2 = level["rope_anchor_y"]
		if float(rec["y"]) < yr.x - 0.001 or float(rec["y"]) > yr.y + 0.001:
			return "Rope anchor must be between y = %d and %d" % [int(yr.x), int(yr.y)]
	return ""


static func snap(p: Vector2) -> Vector2:
	return p.snapped(Vector2(C.SNAP_POS, C.SNAP_POS))


# ------------------------------------------------------------------ tools
func select_tool(type: String) -> void:
	if not enabled:
		return
	tool = "" if tool == type else type
	if tool != "":
		select(-1)
		if tool == "lever" or tool == "rope":
			tool_rot = 0.0
	_ghost_rec = {}
	_update_ghost()
	if _band:
		_band.visible = tool == "rope"


func select(id: int) -> void:
	selected_id = id
	for vid: int in _visuals:
		ML.set_overlay(_visuals[vid], Mats.outline() if vid == selected_id else null)
	_refresh_handles()


func _refresh_handles() -> void:
	for c in _handle_root.get_children():
		c.queue_free()
	if not enabled or selected_id < 0:
		return
	var rec := model.get_record(selected_id)
	if rec.is_empty() or not Registry.has_handle(String(rec["type"])):
		return
	var pts: Array[Vector2] = Lever.handles(rec) if rec["type"] == "lever" else Rope.handles(rec)
	for p in pts:
		var h := ML.add_mesh(_handle_root, ML.sphere(0.16), Vector3(p.x, p.y, 0.62), Mats.emissive(C.COL_OUTLINE, 2.0))
		h.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func show_hints(recs: Array) -> void:
	for c in _hint_root.get_children():
		c.queue_free()
	for rec: Dictionary in recs:
		var v := Registry.build_visual(rec)
		ML.override_all(v, Mats.hint())
		ML.set_shadows(v, false)
		_hint_root.add_child(v)


# ------------------------------------------------------------------ visuals
func _sync_visuals() -> void:
	var seen := {}
	for rec in model.records:
		var id := int(rec["id"])
		seen[id] = true
		if not _visuals.has(id) or _visual_recs[id] != rec:
			_rebuild_visual(id, rec)
	for id: int in _visuals.keys():
		if not seen.has(id):
			(_visuals[id] as Node3D).queue_free()
			_visuals.erase(id)
			_visual_recs.erase(id)
	if selected_id >= 0 and not seen.has(selected_id):
		selected_id = -1
	select(selected_id)
	_ghost_rec = {}
	_update_ghost()


func _rebuild_visual(id: int, rec: Dictionary, tint: Material = null) -> void:
	if _visuals.has(id):
		(_visuals[id] as Node3D).queue_free()
	var v := Registry.build_visual(rec)
	if tint:
		ML.override_all(v, tint)
	_visual_root.add_child(v)
	_visuals[id] = v
	_visual_recs[id] = rec.duplicate(true)


func _set_ghost_visible(on: bool) -> void:
	if _ghost:
		_ghost.visible = on


func _update_ghost() -> void:
	if not enabled or tool == "" or _over_ui:
		_set_ghost_visible(false)
		_grid.visible = not _drag.is_empty() and enabled
		return
	var rec: Dictionary
	if _drag.get("mode", "") == "rope":
		rec = _drag["rec"]
	else:
		rec = Registry.new_record(tool, snap(_cursor), tool_rot)
	_grid.visible = true
	_grid.position = Vector3(float(rec["x"]), float(rec["y"]), -0.52)
	if rec == _ghost_rec and _ghost:
		_ghost.visible = true
		return
	_ghost_rec = rec
	_ghost_ok = model.remaining(tool) > 0 and is_valid(rec, -1)
	if _ghost:
		_ghost.queue_free()
	_ghost = Registry.build_visual(rec)
	ML.override_all(_ghost, Mats.ghost(_ghost_ok))
	ML.set_shadows(_ghost, false)
	add_child(_ghost)


func _process(_delta: float) -> void:
	var over := get_viewport().gui_get_hovered_control() != null
	if over != _over_ui:
		_over_ui = over
		_update_ghost()


# ------------------------------------------------------------------ input
func _unhandled_input(event: InputEvent) -> void:
	if not enabled:
		return
	var mm := event as InputEventMouseMotion
	if mm:
		_cursor = camera.call(&"screen_to_plane", mm.position)
		if _drag.is_empty():
			_update_ghost()
		else:
			_drag_update(mm.position)
		return
	var mb := event as InputEventMouseButton
	if mb:
		_cursor = camera.call(&"screen_to_plane", mb.position)
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				_lmb_down(mb)
			else:
				_lmb_up()
			get_viewport().set_input_as_handled()
		elif mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			_cancel()
			get_viewport().set_input_as_handled()
		elif mb.pressed and mb.ctrl_pressed and (mb.button_index == MOUSE_BUTTON_WHEEL_UP or mb.button_index == MOUSE_BUTTON_WHEEL_DOWN):
			rotate_by(C.FINE_ROT if mb.button_index == MOUSE_BUTTON_WHEEL_UP else -C.FINE_ROT)
			get_viewport().set_input_as_handled()
		return
	var k := event as InputEventKey
	if k and k.pressed:
		_key(k)


func _key(k: InputEventKey) -> void:
	if not _drag.is_empty():
		return
	var handled := true
	match k.keycode:
		KEY_Q:
			rotate_by(C.SNAP_ROT)
		KEY_E:
			rotate_by(-C.SNAP_ROT)
		KEY_DELETE, KEY_BACKSPACE:
			delete_selected()
		KEY_Z:
			if k.ctrl_pressed and not k.echo:
				if k.shift_pressed:
					do_redo()
				else:
					do_undo()
			else:
				handled = false
		KEY_Y:
			if k.ctrl_pressed and not k.echo:
				do_redo()
			else:
				handled = false
		_:
			handled = false
	if not handled and not k.echo and k.keycode >= KEY_1 and k.keycode <= KEY_9 and not k.ctrl_pressed:
		var i := int(k.keycode - KEY_1)
		if i < model.order.size():
			select_tool(model.order[i])
			handled = true
	if handled:
		get_viewport().set_input_as_handled()


func _lmb_down(mb: InputEventMouseButton) -> void:
	if tool != "":
		if tool == "rope":
			var anchor := snap(_cursor)
			var rec := Registry.new_record("rope", anchor, tool_rot)
			_drag = {"mode": "rope", "rec": rec, "anchor": anchor}
			_ghost_rec = {}
			_update_ghost()
		else:
			try_place(Registry.new_record(tool, snap(_cursor), tool_rot))
		return
	if mb.alt_pressed and selected_id >= 0:
		var rec := model.get_record(selected_id)
		if Registry.has_handle(String(rec["type"])):
			var pts: Array[Vector2] = Lever.handles(rec) if rec["type"] == "lever" else Rope.handles(rec)
			for p in pts:
				if p.distance_to(_cursor) < 0.45:
					_drag = {"mode": "handle", "id": selected_id, "start": rec.duplicate(true), "rec": rec.duplicate(true)}
					return
	var hit := pick(_cursor)
	select(hit)
	if hit >= 0:
		var rec := model.get_record(hit)
		_drag = {"mode": "move", "id": hit, "start": rec.duplicate(true), "rec": rec.duplicate(true),
			"grab": _cursor - Vector2(float(rec["x"]), float(rec["y"])), "press": mb.position, "moved": false}


func _drag_update(screen: Vector2) -> void:
	var mode: String = _drag["mode"]
	if mode == "rope":
		var anchor: Vector2 = _drag["anchor"]
		var v := _cursor - anchor
		var rope_rec := Registry.new_record("rope", anchor, tool_rot)
		if v.length() >= C.ROPE_DRAG_MIN:
			rope_rec["length"] = v.length()
			rope_rec["rot"] = snappedf(rad_to_deg(atan2(v.x, -v.y)), C.SNAP_ROT)
			Rope.constrain(rope_rec)
		_drag["rec"] = rope_rec
		_update_ghost()
		return
	var id: int = _drag["id"]
	var rec: Dictionary
	if mode == "move":
		if not _drag["moved"] and screen.distance_to(_drag["press"]) < C.DRAG_START_PIXELS:
			return
		_drag["moved"] = true
		var p := snap(_cursor - Vector2(_drag["grab"]))
		rec = (_drag["start"] as Dictionary).duplicate(true)
		rec["x"] = p.x
		rec["y"] = p.y
	else:
		var start: Dictionary = _drag["start"]
		rec = Lever.drag_fulcrum(start, _cursor) if start["type"] == "lever" else Rope.drag_bob(start, _cursor)
	if rec == _drag["rec"]:
		return
	_drag["rec"] = rec
	var ok := is_valid(rec, id)
	_rebuild_visual(id, rec, null if ok else Mats.ghost(false))
	ML.set_overlay(_visuals[id], Mats.outline())
	_grid.visible = true
	_grid.position = Vector3(float(rec["x"]), float(rec["y"]), -0.52)


func _lmb_up() -> void:
	if _drag.is_empty():
		return
	var d := _drag
	_drag = {}
	_grid.visible = false
	var mode: String = d["mode"]
	var rec: Dictionary = d["rec"]
	if mode == "rope":
		try_place(rec)
		return
	var id: int = d["id"]
	if mode == "move" and not d["moved"]:
		return
	var reason := validity_reason(rec, id)
	if reason == "":
		model.modify(id, rec)
		Sfx.play(&"click", -6.0, 1.1)
	else:
		message.emit("Can't move here: " + reason)
		_rebuild_visual(id, model.get_record(id))
		select(id)
	_refresh_handles()


func _cancel() -> void:
	if not _drag.is_empty():
		var d := _drag
		_drag = {}
		if d.has("id"):
			_rebuild_visual(int(d["id"]), model.get_record(int(d["id"])))
			select(int(d["id"]))
		_update_ghost()
		return
	if tool != "":
		select_tool(tool)
	else:
		select(-1)


func try_place(rec: Dictionary) -> bool:
	var type: String = rec["type"]
	if model.remaining(type) <= 0:
		message.emit("No %s left" % Registry.NAMES[type])
		return false
	var reason := validity_reason(rec, -1)
	if reason != "":
		message.emit("Can't place here: " + reason)
		return false
	model.place(rec)
	Sfx.play(&"click", -3.0, 1.0)
	if model.remaining(type) <= 0 and tool == type:
		select_tool(type)  # out of stock: drop the tool (select_tool toggles it off)
	return true


func rotate_by(deg: float) -> void:
	if tool != "":
		tool_rot += deg
		var probe := Registry.new_record(tool, Vector2.ZERO, tool_rot)
		tool_rot = float(probe["rot"])
		_ghost_rec = {}
		_update_ghost()
		Sfx.play(&"tick", -8.0, 1.0)
	elif selected_id >= 0:
		var rec := model.get_record(selected_id).duplicate(true)
		rec["rot"] = float(rec["rot"]) + deg
		Registry.module(String(rec["type"])).constrain(rec)
		var reason := validity_reason(rec, selected_id)
		if reason == "":
			model.modify(selected_id, rec)
			Sfx.play(&"tick", -8.0, 1.0)
		else:
			message.emit("Can't rotate: " + reason)


func delete_selected() -> void:
	if selected_id < 0:
		return
	model.delete(selected_id)
	selected_id = -1
	Sfx.play(&"click", -4.0, 0.7)


func do_undo() -> void:
	if model.undo():
		Sfx.play(&"tick", -6.0, 0.8)


func do_redo() -> void:
	if model.redo():
		Sfx.play(&"tick", -6.0, 1.2)


## Top-most placed object under point p (last placed wins), -1 if none.
func pick(p: Vector2) -> int:
	for i in range(model.records.size() - 1, -1, -1):
		var rec := model.records[i]
		if Fp.contains_point(Registry.footprint(rec), p):
			return int(rec["id"])
	return -1
