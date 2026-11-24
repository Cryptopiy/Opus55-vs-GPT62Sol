class_name AutoTestChecks
extends RefCounted
## Pure-logic self-checks used by the autotest runner (no scene needed):
## track generator statistics / determinism, drift scoring rules, and a
## corner listing used for diagnostics.


## Generates many seeds: attempts, lengths, radii, determinism (R-01, R-02).
static func track_stats(from: int, to: int) -> Array:
	var worst := 0
	var fallbacks := 0
	var total_ms := 0
	var min_len := INF
	var max_len := 0.0
	var min_r := INF
	for s in range(from, to):
		var t0 := Time.get_ticks_msec()
		var td := TrackGenerator.generate(s)
		total_ms += Time.get_ticks_msec() - t0
		worst = maxi(worst, td.attempts)
		fallbacks += 1 if td.used_fallback else 0
		min_len = minf(min_len, td.length)
		max_len = maxf(max_len, td.length)
		min_r = minf(min_r, td.min_radius)
	var a := TrackGenerator.generate(Config.DEFAULT_SEED)
	var b := TrackGenerator.generate(Config.DEFAULT_SEED)
	var same := a.points == b.points and a.racing_offsets == b.racing_offsets
	print("[autotest] seeds %d..%d: worst attempts %d, fallbacks %d, length %.0f..%.0f m, min radius %.1f m, avg %.0f ms/track" % [
		from, to - 1, worst, fallbacks, min_len, max_len, min_r, float(total_ms) / float(maxi(to - from, 1))])
	print("[autotest] seed 2024: attempts %d, length %.1f m, min radius %.1f m, %d samples, deterministic %s" % [
		a.attempts, a.length, a.min_radius, a.count, same])
	var ok := same and fallbacks == 0 and min_len >= Config.TRACK_LENGTH_MIN and max_len <= Config.TRACK_LENGTH_MAX \
		and min_r >= Config.MIN_CORNER_RADIUS
	return [ok, "deterministic=%s fallbacks=%d" % [same, fallbacks]]


## Deterministic check of the R-14 scoring rules on DriftScorer.
static func drift_rules() -> Array:
	var dt := 1.0 / float(Config.PHYSICS_TICKS)
	var problems := PackedStringArray()
	var d := DriftScorer.new()
	_feed(d, 2.0, 30.0, 30.0, dt)                           # below 40 km/h
	_feed(d, 2.0, 80.0, 10.0, dt)                           # slip below 15 deg
	_feed(d, 2.0, 80.0, 85.0, dt)                           # slip above 80 deg
	if d.pending > 0.0 or d.chain_active:
		problems.append("scored outside the drift window")
	# 5 s at 80 km/h and 30 deg: factor 1, combo x1 (0-2 s), x2 (2-4 s), x3 (4-5 s)
	_feed(d, 5.0, 80.0, 30.0, dt)
	var expect := 8.0 * (2.0 * 1.0 + 2.0 * 2.0 + 1.0 * 3.0)
	print("[autotest] 5 s drift: pending %.2f (expected %.2f), combo x%d" % [d.pending, expect, d.combo])
	if absf(d.pending - expect) > 0.5 or d.combo != 3:
		problems.append("pending/combo wrong")
	_feed(d, 0.9, 80.0, 0.0, dt)
	if d.total > 0.0:
		problems.append("banked before 1 s")
	_feed(d, 0.2, 80.0, 0.0, dt)
	print("[autotest] after 1.1 s without drifting: total %.2f, pending %.2f" % [d.total, d.pending])
	if absf(d.total - expect) > 0.5 or d.pending != 0.0:
		problems.append("not banked after 1 s")
	_feed(d, 12.0, 100.0, 45.0, dt)
	print("[autotest] 12 s drift: combo x%d (cap %d)" % [d.combo, Config.DRIFT_COMBO_MAX])
	if d.combo != Config.DRIFT_COMBO_MAX:
		problems.append("combo cap wrong")
	var before := d.total
	d.on_wall_hit()
	_feed(d, 2.0, 80.0, 0.0, dt)
	print("[autotest] wall hit during drift: total %.2f (before %.2f), event %d" % [d.total, before, d.last_event])
	if d.total != before or d.last_event != DriftScorer.Event.LOST:
		problems.append("wall hit did not discard the drift")
	_feed(d, 0.5, 80.0, 30.0, dt)
	_feed(d, 1.5, 80.0, 0.0, dt)
	if d.total != before:
		problems.append("a 0.5 s drift was banked")
	return [problems.is_empty(), "; ".join(problems) if not problems.is_empty() else "all scoring rules verified"]


static func _feed(d: DriftScorer, seconds: float, kmh: float, slip: float, dt: float) -> void:
	for _i in roundi(seconds / dt):
		d.update(dt, kmh, slip)


## Lists the corners of the track (radius < 160 m for > 20 m) for diagnostics.
static func print_corners(t: TrackData) -> void:
	var inside := false
	var start := 0.0
	var min_r := INF
	for i in t.count + 1:
		var k := absf(t.curvature[i % t.count])
		var curved := k > 1.0 / 160.0
		if curved and not inside:
			inside = true
			start = t.distances[i % t.count]
			min_r = INF
		if curved:
			min_r = minf(min_r, 1.0 / k)
		if not curved and inside:
			inside = false
			var seg_len := t.distances[i % t.count] - start
			if seg_len > 20.0:
				print("[autotest] corner at %.0f m, length %.0f m, min radius %.0f m, %s" % [start, seg_len, min_r, "right" if t.curvature[(i - 2) % t.count] > 0.0 else "left"])
