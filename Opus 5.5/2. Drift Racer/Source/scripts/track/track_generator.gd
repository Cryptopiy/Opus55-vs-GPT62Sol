class_name TrackGenerator
extends RefCounted
## Seeded circuit generator (R-01, R-02): 12 control points on a perturbed
## circle -> closed centripetal Catmull-Rom spline -> resampled every 2 m ->
## validated (no corridor self-overlap, radius >= 18 m, 1.2-2.0 km). Rejected
## layouts are regenerated from the next derived seed, so a seed always maps to
## the same track.


static func generate(user_seed: int) -> TrackData:
	for attempt in Config.MAX_TRACK_ATTEMPTS:
		var derived := derive_seed(user_seed, attempt)
		var data := _try_layout(derived)
		if data != null:
			data.seed_value = user_seed
			data.derived_seed = derived
			data.attempts = attempt + 1
			_finish(data)
			return data
	# Practically unreachable (validated over thousands of seeds); a plain circle
	# always passes every rule, so the game can never be left without a track.
	var fallback := _fallback_circle()
	fallback.seed_value = user_seed
	fallback.attempts = Config.MAX_TRACK_ATTEMPTS
	fallback.used_fallback = true
	_finish(fallback)
	return fallback


## Attempt 0 uses the seed itself; later attempts hash (seed, attempt).
static func derive_seed(user_seed: int, attempt: int) -> int:
	if attempt == 0:
		return user_seed
	return hash(Vector2i(user_seed, attempt)) & 0x7fffffff


static func _try_layout(derived_seed: int) -> TrackData:
	var rng := RandomNumberGenerator.new()
	rng.seed = derived_seed
	var base_radius := rng.randf_range(Config.CIRCLE_RADIUS_MIN, Config.CIRCLE_RADIUS_MAX)
	var n := Config.CONTROL_POINT_COUNT
	var radii := PackedFloat32Array()
	var heights := PackedFloat32Array()
	for i in n:
		radii.append(base_radius * (1.0 + rng.randf_range(-Config.RADIAL_NOISE, Config.RADIAL_NOISE)))
		heights.append(rng.randf_range(-Config.ELEVATION_AMPLITUDE, Config.ELEVATION_AMPLITUDE))
	# Smooth elevation around the loop so slopes stay gentle.
	for _pass in Config.ELEVATION_SMOOTH_PASSES:
		var smoothed := PackedFloat32Array()
		smoothed.resize(n)
		for i in n:
			smoothed[i] = 0.25 * heights[posmod(i - 1, n)] + 0.5 * heights[i] + 0.25 * heights[(i + 1) % n]
		heights = smoothed
	var controls := PackedVector3Array()
	for i in n:
		var ang := TAU * float(i) / float(n)
		controls.append(Vector3(cos(ang) * radii[i], heights[i], sin(ang) * radii[i]))
	var data := TrackData.new()
	data.control_points = controls
	data.points = _resample(_dense_spline(controls), Config.SAMPLE_SPACING)
	if data.points.size() < 16:
		return null
	data.finalize_geometry()
	if data.length < Config.TRACK_LENGTH_MIN or data.length > Config.TRACK_LENGTH_MAX:
		return null
	if data.min_radius < Config.MIN_CORNER_RADIUS:
		return null
	if _corridor_overlaps(data):
		return null
	return data


## Closed centripetal Catmull-Rom through the control points, densely sampled.
static func _dense_spline(controls: PackedVector3Array) -> PackedVector3Array:
	var out := PackedVector3Array()
	var n := controls.size()
	var steps := Config.SPLINE_DENSE_STEPS
	for seg in n:
		var p0 := controls[posmod(seg - 1, n)]
		var p1 := controls[seg]
		var p2 := controls[(seg + 1) % n]
		var p3 := controls[(seg + 2) % n]
		for s in steps:
			out.append(_catmull_rom(p0, p1, p2, p3, float(s) / float(steps)))
	return out


## Centripetal (alpha = 0.5) Catmull-Rom, Barry-Goldman form: no cusps/loops
## inside a segment even with uneven control spacing.
static func _catmull_rom(p0: Vector3, p1: Vector3, p2: Vector3, p3: Vector3, u: float) -> Vector3:
	var t0 := 0.0
	var t1 := t0 + maxf(sqrt(p0.distance_to(p1)), 0.0001)
	var t2 := t1 + maxf(sqrt(p1.distance_to(p2)), 0.0001)
	var t3 := t2 + maxf(sqrt(p2.distance_to(p3)), 0.0001)
	var t := lerpf(t1, t2, u)
	var a1 := p0 * ((t1 - t) / (t1 - t0)) + p1 * ((t - t0) / (t1 - t0))
	var a2 := p1 * ((t2 - t) / (t2 - t1)) + p2 * ((t - t1) / (t2 - t1))
	var a3 := p2 * ((t3 - t) / (t3 - t2)) + p3 * ((t - t2) / (t3 - t2))
	var b1 := a1 * ((t2 - t) / (t2 - t0)) + a2 * ((t - t0) / (t2 - t0))
	var b2 := a2 * ((t3 - t) / (t3 - t1)) + a3 * ((t - t1) / (t3 - t1))
	return b1 * ((t2 - t) / (t2 - t1)) + b2 * ((t - t1) / (t2 - t1))


## Resamples a closed polyline at equal arc-length steps (~`step` m).
static func _resample(dense: PackedVector3Array, step: float) -> PackedVector3Array:
	var n := dense.size()
	var cumulative := PackedFloat32Array()
	cumulative.resize(n + 1)
	var acc := 0.0
	for i in n:
		cumulative[i] = acc
		acc += dense[i].distance_to(dense[(i + 1) % n])
	cumulative[n] = acc
	var count := maxi(roundi(acc / step), 3)
	var spacing := acc / float(count)
	var out := PackedVector3Array()
	var j := 0
	for k in count:
		var target := float(k) * spacing
		while j < n - 1 and cumulative[j + 1] < target:
			j += 1
		var seg_len := cumulative[j + 1] - cumulative[j]
		var f := 0.0 if seg_len <= 0.0 else (target - cumulative[j]) / seg_len
		out.append(dense[j].lerp(dense[(j + 1) % n], clampf(f, 0.0, 1.0)))
	return out


## True when two parts of the circuit that are far apart along the track come
## closer than the corridor clearance (covers centreline self-intersection and
## overlapping road/barrier corridors).
static func _corridor_overlaps(data: TrackData) -> bool:
	var cell := Config.CORRIDOR_CLEARANCE
	var grid := {}
	for i in data.count:
		var p := data.points[i]
		var key := Vector2i(int(floorf(p.x / cell)), int(floorf(p.z / cell)))
		var ids: PackedInt32Array = grid.get(key, PackedInt32Array())
		ids.append(i)
		grid[key] = ids
	var clearance_sq := Config.CORRIDOR_CLEARANCE * Config.CORRIDOR_CLEARANCE
	var skip := int(ceilf(Config.CORRIDOR_NEIGHBOUR_SKIP / data.spacing))
	for i in data.count:
		var p := data.points[i]
		var cx := int(floorf(p.x / cell))
		var cz := int(floorf(p.z / cell))
		for gx in range(cx - 1, cx + 2):
			for gz in range(cz - 1, cz + 2):
				var key := Vector2i(gx, gz)
				if not grid.has(key):
					continue
				var ids: PackedInt32Array = grid[key]
				for j in ids:
					var along := absi(j - i)
					along = mini(along, data.count - along)
					if along <= skip:
						continue
					var q := data.points[j]
					if (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < clearance_sq:
						return true
	return false


## Rotates the samples so index 0 sits in the middle of the straightest
## stretch (start/finish straight), then derives racing line and checkpoints.
static func _finish(data: TrackData) -> void:
	var half := int(ceilf(Config.START_STRAIGHT_HALF_WINDOW / data.spacing))
	var best_index := 0
	var best_cost := INF
	for i in data.count:
		var cost := 0.0
		for k in range(-half, half + 1, 2):
			cost += absf(data.curvature[data.wrap_index(i + k)])
		if cost < best_cost:
			best_cost = cost
			best_index = i
	if best_index != 0:
		var rotated := PackedVector3Array()
		rotated.resize(data.count)
		for i in data.count:
			rotated[i] = data.points[data.wrap_index(i + best_index)]
		data.points = rotated
		data.finalize_geometry()
	data.checkpoint_indices.clear()
	for c in Config.CHECKPOINT_COUNT:
		data.checkpoint_indices.append(roundi(float(c) * float(data.count) / float(Config.CHECKPOINT_COUNT)) % data.count)
	RacingLine.build(data)


static func _fallback_circle() -> TrackData:
	var data := TrackData.new()
	var n := maxi(roundi(TAU * Config.FALLBACK_RADIUS / Config.SAMPLE_SPACING), 3)
	for i in n:
		var ang := TAU * float(i) / float(n)
		data.points.append(Vector3(cos(ang), 0.0, sin(ang)) * Config.FALLBACK_RADIUS)
	data.finalize_geometry()
	return data
