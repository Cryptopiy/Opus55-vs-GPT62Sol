class_name LabBlueprint
extends RefCounted

const C = preload("res://scripts/config.gd")
var undo_steps: Array[Array] = []
var redo_steps: Array[Array] = []

func clear() -> void:
	undo_steps.clear()
	redo_steps.clear()

func record(before: Array) -> void:
	undo_steps.append(before.duplicate(true))
	if undo_steps.size() > C.HISTORY_LIMIT:
		undo_steps.pop_front()
	redo_steps.clear()

func undo(current: Array) -> Array:
	if undo_steps.is_empty():
		return current
	redo_steps.append(current.duplicate(true))
	return undo_steps.pop_back()

func redo(current: Array) -> Array:
	if redo_steps.is_empty():
		return current
	undo_steps.append(current.duplicate(true))
	return redo_steps.pop_back()

static func read_reference(level: Dictionary) -> Array:
	var raw := FileAccess.get_file_as_string("res://levels/solutions/%s.json" % level.id)
	var source: Variant = JSON.parse_string(raw)
	var result := []
	if not source is Array:
		return result
	for entry in source:
		var vector: Array = entry.position
		var values: Dictionary = entry.parameters
		if values.has("rope_vector"):
			values.rope_vector = Vector2(values.rope_vector[0], values.rope_vector[1])
		result.append(C.placement(entry.type, Vector2(vector[0], vector[1]), float(entry.rotation), values))
	return result

static func hit(point: Vector2, spec: Dictionary) -> bool:
	var center: Vector2 = spec.position
	var dims: Vector3 = C.KIT[spec.type][3]
	var local := (point - center).rotated(deg_to_rad(-spec.rotation)).abs()
	if local.x <= dims.x * 0.5 + 0.2 and local.y <= dims.y * 0.5 + 0.2:
		return true
	if spec.type != "rope":
		return false
	var rope := LabRope.vector_for(spec)
	var projected := Geometry2D.get_closest_point_to_segment(point, center, center + rope)
	return projected.distance_to(point) < 0.30

static func valid(spec: Dictionary, level: Dictionary, layout: Array, ignored := -1) -> bool:
	if spec.type == "lever" and absf(spec.rotation) > C.LEVER_LIMIT:
		return false
	var shapes := _rects(spec)
	return _valid_shapes(spec, level, layout, ignored, shapes)

static func valid_anchor(spec: Dictionary, level: Dictionary, layout: Array) -> bool:
	return _valid_shapes(spec, level, layout, -1, [_rects(spec)[0]])

static func _valid_shapes(spec: Dictionary, level: Dictionary, layout: Array, ignored: int, shapes: Array) -> bool:
	if spec.type == "rope" and level.id == "L2":
		if spec.position.y < C.ROPE_ANCHOR_BAND.x or spec.position.y > C.ROPE_ANCHOR_BAND.y:
			return false
	for shape in shapes:
		var center: Vector2 = shape[0]
		var half: Vector2 = shape[1] * 0.5
		var angle: float = shape[2]
		var edge := (Vector2.RIGHT.rotated(angle) * half.x).abs() + (Vector2.DOWN.rotated(angle) * half.y).abs()
		if not C.BUILD_LIMIT.encloses(Rect2(center - edge, edge * 2.0)):
			return false
		for fixture in level.fixtures:
			if _overlap(shape, [fixture[0], fixture[1], deg_to_rad(fixture[2])]):
				return false
		if level.id == "L5":
			var shuttle := Vector2(C.SHUTTLE_X.x, C.SHUTTLE_Y)
			var size := Vector2(C.SHUTTLE_SIZE.x, C.SHUTTLE_SIZE.y)
			var plate := Vector2(C.SWITCH_SIZE.x, C.SWITCH_SIZE.y)
			if _overlap(shape, [shuttle, size, 0.0]) or _overlap(shape, [C.SWITCH_POS, plate, 0.0]):
				return false
		for zone: Rect2 in level.zones:
			if _overlap(shape, [zone.get_center(), zone.size, 0.0]):
				return false
		var goal: Vector2 = level.goal
		if _overlap(shape, [goal, Vector2(1.97, 1.18), 0.0]):
			return false
		var spawn: Vector2 = level.start
		var local := (spawn - center).rotated(-angle).abs()
		var clearance := (local - half).max(Vector2.ZERO)
		# A surface may seat the ball by a few centimeters at the start of the attempt.
		if clearance.length() < float(C.BALLS[level.ball].radius) - C.SPAWN_SEATING:
			return false
		for index in range(layout.size()):
			if index != ignored:
				for other in _rects(layout[index]):
					if _overlap(shape, other):
						return false
	return true

static func _rects(spec: Dictionary) -> Array:
	var dims: Vector3 = C.KIT[spec.type][3]
	if spec.type != "rope":
		return [[spec.position, Vector2(dims.x, dims.y), deg_to_rad(spec.rotation)]]
	var anchor: Vector2 = spec.position
	var rope := LabRope.vector_for(spec)
	var bob := anchor + rope + rope.normalized() * C.ROPE_BOB.y * 0.6
	return [
		[anchor, Vector2(dims.x, dims.y), 0.0],
		[anchor + rope * 0.5, Vector2(rope.length(), C.ROPE_RADIUS * 2.0), rope.angle()],
		[bob + Vector2(0, 0.07), Vector2(C.ROPE_BOB.x, C.ROPE_BOB.y + 0.18), 0.0],
	]

static func _overlap(a: Array, b: Array) -> bool:
	var av := Vector2.RIGHT.rotated(a[2])
	var bv := Vector2.RIGHT.rotated(b[2])
	var ay := av.orthogonal()
	var by := bv.orthogonal()
	var diff: Vector2 = b[0] - a[0]
	# A separating axis lets nearly touching pieces share a snap line.
	for axis in [av, ay, bv, by]:
		var span_a: float = (absf(axis.dot(av)) * a[1].x + absf(axis.dot(ay)) * a[1].y) * 0.5
		var span_b: float = (absf(axis.dot(bv)) * b[1].x + absf(axis.dot(by)) * b[1].y) * 0.5
		if absf(diff.dot(axis)) >= span_a + span_b - C.PLACE_MARGIN:
			return false
	return true
