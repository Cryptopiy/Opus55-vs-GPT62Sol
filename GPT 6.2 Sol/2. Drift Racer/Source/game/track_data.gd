class_name TrackData
extends RefCounted

const C = preload("res://game/config.gd")

var points := PackedVector3Array()
var forward := PackedVector3Array()
var right := PackedVector3Array()
var radii := PackedFloat32Array()
var curvature := PackedFloat32Array()
var length := 0.0
var step := C.SAMPLE_STEP
var accepted_seed := C.DEFAULT_SEED
var _arc := PackedFloat32Array()

func generate(requested_seed: int) -> void:
	var rng := RandomNumberGenerator.new()
	accepted_seed = requested_seed
	while true:
		rng.seed = accepted_seed
		var dense := _spline(rng)
		if _resample(dense) and _validate():
			_resample(dense, _start_offset())
			break
		# A bounded arithmetic step avoids overflow for a signed 64-bit menu seed.
		accepted_seed = 0 if accepted_seed == 9223372036854775807 else accepted_seed + 1
	_measure()

func _spline(rng: RandomNumberGenerator) -> PackedVector3Array:
	var controls := PackedVector3Array()
	var heights := PackedFloat32Array()
	var base_radius := rng.randf_range(C.BASE_RADIUS.x, C.BASE_RADIUS.y)
	for i in C.CONTROL_POINTS:
		var angle := TAU * i / C.CONTROL_POINTS
		var radius := base_radius * (1.0 + rng.randf_range(-C.RADIAL_NOISE, C.RADIAL_NOISE))
		heights.append(rng.randf_range(-C.ELEVATION, C.ELEVATION))
		controls.append(Vector3(cos(angle) * radius, 0.0, sin(angle) * radius))
	for i in C.CONTROL_POINTS:
		var point := controls[i]
		point.y = (heights[posmod(i - 1, C.CONTROL_POINTS)] + heights[i] * 2.0
			+ heights[(i + 1) % C.CONTROL_POINTS]) * 0.25
		controls[i] = point
	var dense := PackedVector3Array()
	var subdivisions := 96
	for i in C.CONTROL_POINTS:
		var a := controls[posmod(i - 1, C.CONTROL_POINTS)]
		var b := controls[i]
		var c := controls[(i + 1) % C.CONTROL_POINTS]
		var d := controls[(i + 2) % C.CONTROL_POINTS]
		for j in subdivisions:
			var t := float(j) / subdivisions
			dense.append(0.5 * ((2.0 * b) + (-a + c) * t
				+ (2.0 * a - 5.0 * b + 4.0 * c - d) * t * t
				+ (-a + 3.0 * b - 3.0 * c + d) * t * t * t))
	dense.append(dense[0])
	return dense

func _resample(dense: PackedVector3Array, start := 0.0) -> bool:
	_arc = PackedFloat32Array([0.0])
	for i in range(1, dense.size()):
		_arc.append(_arc[i - 1] + dense[i - 1].distance_to(dense[i]))
	length = _arc[-1]
	if length < C.LENGTH_RANGE.x or length > C.LENGTH_RANGE.y:
		return false
	var count := ceili(length / C.SAMPLE_STEP)
	step = C.SAMPLE_STEP
	points.resize(count)
	var cursor := 0
	for i in count:
		var distance := fposmod(start + i * step, length)
		if distance < _arc[cursor]:
			cursor = 0
		while _arc[cursor + 1] < distance:
			cursor += 1
		points[i] = dense[cursor].lerp(dense[cursor + 1],
			inverse_lerp(_arc[cursor], _arc[cursor + 1], distance))
	return true

func _curvature_at(index: int, stride: int) -> float:
	var a := points[wrap_index(index - stride)]
	var b := points[index]
	var c := points[wrap_index(index + stride)]
	var ab := Vector2(b.x - a.x, b.z - a.z)
	var bc := Vector2(c.x - b.x, c.z - b.z)
	return 2.0 * ab.cross(bc) / maxf(0.0001, ab.length() * bc.length() * (ab + bc).length())

func _validate() -> bool:
	for i in points.size():
		if absf(_curvature_at(i, 1)) > 1.0 / C.MIN_CORNER:
			return false
	# A clearance test rejects both crossings and overlaps between distant road strips.
	var clearance := C.ROAD_WIDTH + C.CURB_WIDTH * 2.0 + C.BARRIER_WIDTH * 2.0 + step
	var clearance_squared := clearance * clearance
	var nearby := ceili(clearance * 1.65 / step)
	for i in points.size():
		var a := Vector2(points[i].x, points[i].z)
		for j in range(i + nearby, mini(i + points.size() - nearby + 1, points.size())):
			if a.distance_squared_to(Vector2(points[j].x, points[j].z)) < clearance_squared:
				return false
	return true

func _start_offset() -> float:
	# Keep the grid and grandstand along the straightest stretch of the candidate.
	var best := INF
	var start := 0
	for i in points.size():
		var bend := 0.0
		for offset in range(-12, 13, 3):
			bend += absf(_curvature_at(wrap_index(i + offset), 3))
		if bend < best:
			best = bend
			start = i
	return start * step

func _measure() -> void:
	forward.resize(points.size())
	right.resize(points.size())
	curvature.resize(points.size())
	radii.resize(points.size())
	for i in points.size():
		forward[i] = (points[wrap_index(i + 1)] - points[wrap_index(i - 1)]).normalized()
		right[i] = forward[i].cross(Vector3.UP).normalized()
		curvature[i] = _curvature_at(i, 3)
		radii[i] = 1.0 / maxf(absf(curvature[i]), 0.00001)

func wrap_index(index: int) -> int:
	return posmod(index, points.size())

func index_at(distance: float) -> int:
	return wrap_index(floori(fposmod(distance, length) / step))

func point_at(distance: float) -> Vector3:
	var along := fposmod(distance, length)
	var i := index_at(along)
	var span := minf(step, length - i * step)
	return points[i].lerp(points[wrap_index(i + 1)], (along - i * step) / span)

func frame_at(distance: float, lateral: float, height: float) -> Transform3D:
	var i := index_at(distance)
	var up := right[i].cross(forward[i])
	return Transform3D(Basis(right[i], up, -forward[i]), point_at(distance) + right[i] * lateral + up * height)

func nearest(position: Vector3, hint: int = -1) -> int:
	var best := INF
	var result := 0
	var first := 0 if hint < 0 else hint - 22
	var count := points.size() if hint < 0 else 45
	for i in range(first, first + count):
		var index := wrap_index(i)
		var separation := position.distance_squared_to(points[index])
		if separation < best:
			best = separation
			result = index
	return result

func progress(position: Vector3, index: int) -> float:
	return fposmod(index * step + (position - points[index]).dot(forward[index]), length)
