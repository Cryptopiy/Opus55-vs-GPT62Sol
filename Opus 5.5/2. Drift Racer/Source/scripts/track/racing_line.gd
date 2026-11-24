class_name RacingLine
extends RefCounted
## AI racing line (R-21): the centreline shifted sideways by a precomputed
## per-sample offset (|offset| <= 4 m). The offsets come from an elastic-band
## relaxation (each point pulled toward the midpoint of its neighbours), which
## straightens the path: outside-inside-outside through corners. The target
## speed per sample is v = sqrt(a_lat * radius) followed by a backward braking
## pass so the AI starts braking early enough.


static func build(data: TrackData) -> void:
	var n := data.count
	var offsets := PackedFloat32Array()
	offsets.resize(n)
	offsets.fill(0.0)
	var limit := Config.RACING_LINE_MAX_OFFSET
	var current := PackedVector3Array()
	current.resize(n)
	for stencil_variant in Config.RACING_LINE_STENCILS:
		var k: int = stencil_variant
		for _iter in Config.RACING_LINE_ITERATIONS:
			for i in n:
				current[i] = data.points[i] + data.rights[i] * offsets[i]
			for i in n:
				var a := current[posmod(i - k, n)]
				var b := current[(i + k) % n]
				var mid := (a + b) * 0.5
				var desired := (mid - data.points[i]).dot(data.rights[i])
				offsets[i] = clampf(lerpf(offsets[i], desired, 0.5), -limit, limit)
	# Light smoothing removes the small ripples left by the coarse stencils.
	for _pass in 4:
		var smoothed := PackedFloat32Array()
		smoothed.resize(n)
		for i in n:
			smoothed[i] = (offsets[posmod(i - 2, n)] + offsets[posmod(i - 1, n)] + offsets[i]
				+ offsets[(i + 1) % n] + offsets[(i + 2) % n]) / 5.0
		offsets = smoothed
	data.racing_offsets = offsets
	var line := PackedVector3Array()
	line.resize(n)
	for i in n:
		line[i] = data.points[i] + data.rights[i] * offsets[i]
	data.racing_points = line
	data.target_speeds = _speed_profile(data, line)


static func _speed_profile(data: TrackData, line: PackedVector3Array) -> PackedFloat32Array:
	var n := data.count
	var v_max := Config.TOP_SPEED_KMH / 3.6
	var speeds := PackedFloat32Array()
	speeds.resize(n)
	for i in n:
		var k := absf(data.signed_curvature_at(line, i, Config.RACING_LINE_CURVE_STENCIL))
		var v := v_max
		if k > 0.000001:
			v = minf(v_max, sqrt(Config.AI_LAT_ACCEL / k))
		speeds[i] = v
	# Backward pass: v_i <= sqrt(v_{i+1}^2 + 2 a ds). Two laps settle the wrap.
	var two_a_ds := 2.0 * Config.AI_BRAKE_DECEL * data.spacing
	for _lap in 2:
		for step in n:
			var i := n - 1 - step
			var nxt := speeds[(i + 1) % n]
			speeds[i] = minf(speeds[i], sqrt(nxt * nxt + two_a_ds))
	return speeds
