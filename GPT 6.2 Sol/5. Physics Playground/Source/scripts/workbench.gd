class_name LabWorkbench
extends Node3D

const C = preload("res://scripts/config.gd")
const G = preload("res://scripts/geometry.gd")
var app: Node3D
var camera: LabViewRig
var selected := -1
var tool := ""
var angle := 0.0
var cursor := Vector2.ZERO
var start_drag := Vector2.ZERO
var hold_rope := false
var moving := false
var rope_anchor := Vector2.ZERO
var drag_offset := Vector2.ZERO
var draft: Dictionary = {}
var can_commit := false
var ghost: Node3D
var hint_overlay: Node3D
var hint: Node3D
var outline: MeshInstance3D
var ghost_body: MeshInstance3D
var ghost_bob: MeshInstance3D
var ghost_cord: MeshInstance3D
var shown_type := ""
var hint_index := 0
var green := G.material(Color(0.15, 0.92, 0.61, 0.53))
var red := G.material(Color(1.0, 0.26, 0.20, 0.60))
var amber := G.material(Color(1.0, 0.77, 0.30, 0.40))
var white := G.material(Color("#f6ce7d"), 0.03, 0.5)

func setup(owner_app: Node3D, rig: LabViewRig) -> void:
	app = owner_app
	camera = rig
	ghost = Node3D.new()
	add_child(ghost)
	hint_overlay = Node3D.new()
	add_child(hint_overlay)
	hint = Node3D.new()
	hint_overlay.add_child(hint)
	outline = G.mesh(self, ImmediateMesh.new(), white)
	outline.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ghost.hide()
	hint.hide()
	outline.hide()

func clear() -> void:
	tool = ""
	selected = -1
	moving = false
	hold_rope = false
	ghost.hide()
	outline.hide()

func new_level() -> void:
	clear()
	hint_index = 0
	hint.hide()

func choose(kind: String) -> void:
	if app.mode != "build":
		return
	if tool == kind or app.remaining(kind) <= 0:
		clear()
	else:
		clear()
		tool = kind
		angle = 0.0
		update_cursor(get_viewport().get_mouse_position())
	app.refresh_ui()

func cycle_hint() -> void:
	var references := LabBlueprint.read_reference(app.board.level)
	if references.is_empty():
		return
	if hint_index >= references.size():
		hint_index = 0
	var spec: Dictionary = references[hint_index]
	hint_index += 1
	for child in hint.get_children():
		hint.remove_child(child)
		child.queue_free()
	var size: Vector3 = C.KIT[spec.type][3]
	G.block(hint, size + Vector3.ONE * 0.02, amber)
	hint.position = C.in_plane(spec.position)
	hint.rotation_degrees.z = 0.0 if spec.type == "rope" else spec.rotation
	if spec.type == "rope":
		var line := G.block(hint, Vector3.ONE, amber)
		var bob := G.block(hint, C.ROPE_BOB, amber)
		_pose_rope(spec, line, bob)
	hint.visible = app.mode == "build"

func handle(event: InputEvent) -> bool:
	if app.mode != "build":
		return false
	if event is InputEventMouseMotion:
		if selected >= 0 and event.button_mask & MOUSE_BUTTON_MASK_LEFT:
			moving = moving or event.position.distance_to(start_drag) > 4.0
		update_cursor(event.position)
		return moving or hold_rope
	if event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_Q:
				spin(-C.ROTATION_SNAP)
				return true
			KEY_E:
				spin(C.ROTATION_SNAP)
				return true
			KEY_DELETE:
				if selected >= 0:
					var updated: Array = app.placements.duplicate()
					updated.remove_at(selected)
					clear()
					app.commit(updated, "place")
					return true
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
			clear()
			app.refresh_ui()
			return true
		if event.pressed and event.ctrl_pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			spin(C.FINE_ROTATION if event.button_index == MOUSE_BUTTON_WHEEL_UP else -C.FINE_ROTATION)
			return true
		if event.button_index == MOUSE_BUTTON_LEFT:
			update_cursor(event.position)
			if event.pressed:
				_begin(event.position)
			else:
				_release()
			return true
	return false

func _begin(pixel: Vector2) -> void:
	if tool == "rope" and LabBlueprint.valid_anchor(draft, app.board.level, app.placements):
		rope_anchor = draft.position
		hold_rope = true
		update_cursor(pixel)
		return
	if not tool.is_empty() and can_commit:
		_place()
		return
	var next := -1
	for index in range(app.placements.size() - 1, -1, -1):
		if LabBlueprint.hit(cursor, app.placements[index]):
			next = index
			break
	clear()
	selected = next
	if selected >= 0:
		var spec: Dictionary = app.placements[selected]
		start_drag = pixel
		drag_offset = Vector2(spec.position) - cursor
		_show_outline(spec)
	app.refresh_ui()

func _release() -> void:
	if hold_rope:
		hold_rope = false
		if can_commit:
			_place()
		else:
			update_cursor(get_viewport().get_mouse_position())
	elif moving:
		moving = false
		if can_commit and draft.position != app.placements[selected].position:
			var updated: Array = app.placements.duplicate()
			updated[selected] = draft
			app.commit(updated, "place")
		ghost.hide()
		if selected >= 0:
			_show_outline(app.placements[selected])

func _place() -> void:
	var next: Array = app.placements.duplicate()
	next.append(draft)
	var kind := tool
	app.commit(next, "place")
	if app.remaining(kind) <= 0:
		clear()
	else:
		update_cursor(get_viewport().get_mouse_position())

func spin(amount: float) -> void:
	if not tool.is_empty():
		angle = wrapf(angle + amount, -180.0, 180.0)
		update_cursor(get_viewport().get_mouse_position())
		app.audio.play("rotate", 1.0)
	elif selected >= 0:
		var spec: Dictionary = app.placements[selected].duplicate(true)
		spec.rotation = wrapf(spec.rotation + amount, -180.0, 180.0)
		if LabBlueprint.valid(spec, app.board.level, app.placements, selected):
			var next: Array = app.placements.duplicate()
			next[selected] = spec
			app.commit(next, "rotate")
			_show_outline(spec)

func update_cursor(pixel: Vector2) -> void:
	cursor = camera.projected(pixel)
	var snap := Vector2.ONE * C.POSITION_SNAP
	if moving and selected >= 0:
		draft = app.placements[selected].duplicate(true)
		draft.position = (cursor + drag_offset).snapped(snap)
	elif not tool.is_empty():
		var point := rope_anchor if hold_rope else cursor.snapped(snap)
		var param := {}
		if tool == "rope":
			param.rope_vector = (cursor - rope_anchor).snapped(snap).rotated(deg_to_rad(-angle)) if hold_rope else C.ROPE_DEFAULT
		draft = C.placement(tool, point, angle, param)
	else:
		return
	can_commit = LabBlueprint.valid(draft, app.board.level, app.placements, selected if moving else -1)
	_show_ghost()

func _show_ghost() -> void:
	if shown_type != draft.type:
		shown_type = draft.type
		for child in ghost.get_children():
			ghost.remove_child(child)
			child.queue_free()
		ghost_body = G.block(ghost, C.KIT[shown_type][3], green)
		ghost_body.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		if shown_type == "rope":
			ghost_cord = G.block(ghost, Vector3.ONE, green)
			ghost_bob = G.block(ghost, C.ROPE_BOB, green)
			ghost_cord.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			ghost_bob.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ghost.position = C.in_plane(draft.position, 0.03)
	ghost.rotation_degrees.z = 0.0 if shown_type == "rope" else draft.rotation
	ghost_body.material_override = green if can_commit else red
	if shown_type == "rope":
		ghost_cord.material_override = ghost_body.material_override
		ghost_bob.material_override = ghost_body.material_override
		_pose_rope(draft, ghost_cord, ghost_bob)
	ghost.show()

func _pose_rope(spec: Dictionary, cord: MeshInstance3D, bob: MeshInstance3D) -> void:
	var rope := LabRope.vector_for(spec)
	cord.scale = Vector3(rope.length(), C.ROPE_RADIUS * 2.0, C.ROPE_RADIUS * 2.0)
	cord.position = C.in_plane(rope * 0.5)
	cord.rotation.z = rope.angle()
	bob.position = C.in_plane(rope + rope.normalized() * C.ROPE_BOB.y * 0.6)

func _show_outline(spec: Dictionary) -> void:
	var half: Vector3 = Vector3(C.KIT[spec.type][3]) * 0.5 + Vector3.ONE * 0.035
	var lines: ImmediateMesh = outline.mesh
	lines.clear_surfaces()
	lines.surface_begin(Mesh.PRIMITIVE_LINES)
	var corners := [
		Vector3(-half.x, -half.y, half.z), Vector3(half.x, -half.y, half.z),
		Vector3(half.x, half.y, half.z), Vector3(-half.x, half.y, half.z),
	]
	for index in range(corners.size()):
		lines.surface_add_vertex(corners[index])
		lines.surface_add_vertex(corners[(index + 1) % corners.size()])
	lines.surface_end()
	outline.position = C.in_plane(spec.position)
	outline.rotation_degrees.z = spec.rotation
	outline.show()
