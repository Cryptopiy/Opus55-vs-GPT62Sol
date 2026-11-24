class_name TrackData
extends RefCounted
## A generated closed circuit: centreline samples (~2 m apart, index 0 = start
## line), per-sample frames, racing line, AI speed profile and spatial queries.

var seed_value := 0                         # seed typed by the player (R-01)
var derived_seed := 0                       # seed of the accepted attempt (R-02)
var attempts := 0
var used_fallback := false
var control_points := PackedVector3Array()

var count := 0
var length := 0.0
var spacing := 0.0
var points := PackedVector3Array()          # centreline positions
var tangents := PackedVector3Array()        # unit 3D driving direction
var rights := PackedVector3Array()          # unit horizontal right vector
var normals := PackedVector3Array()         # road surface normal
var curvature := PackedFloat32Array()       # signed horizontal curvature (1/m, + = right turn)
var distances := PackedFloat32Array()       # distance from the start line (m)
var min_radius := 0.0

var racing_offsets := PackedFloat32Array()  # racing line lateral offset (+ = right)
var racing_points := PackedVector3Array()
var target_speeds := PackedFloat32Array()   # AI speed profile (m/s, 100 % skill)

var checkpoint_indices := PackedInt32Array()
var bounds := AABB()
var min_height := 0.0

var _grid_cell := 32.0
var _grid := {}                             # Vector2i -> PackedInt32Array of sample indices


## Fills frames, distances, curvature and bounds from `points` (closed loop).
func finalize_geometry() -> void:
	count = points.size()
	tangents.resize(count)
	rights.resize(count)
	normals.resize(count)
	distances.resize(count)
	curvature.resize(count)
	var acc := 0.0
	for i in count:
		distances[i] = acc
		acc += points[i].distance_to(points[wrap_index(i + 1)])
	length = acc
	spacing = length / float(count)
	for i in count:
		var t := (points[wrap_index(i + 1)] - points[wrap_index(i - 1)]).normalized()
		tangents[i] = t
		# The cross-section stays horizontal, so "right" ignores the slope.
		var r := Vector3(-t.z, 0.0, t.x).normalized()
		rights[i] = r
		normals[i] = r.cross(t).normalized()
	min_radius = INF
	for i in count:
		var k := signed_curvature_at(points, i, Config.CURVATURE_STENCIL)
		curvature[i] = k
		if absf(k) > 0.000001:
			min_radius = minf(min_radius, 1.0 / absf(k))
	_build_bounds()
	_build_grid()


## Signed horizontal curvature through p[i-k], p[i], p[i+k] (circumcircle).
func signed_curvature_at(pts: PackedVector3Array, i: int, k: int) -> float:
	var n := pts.size()
	var a := pts[posmod(i - k, n)]
	var b := pts[i]
	var c := pts[posmod(i + k, n)]
	var ab := Vector2(b.x - a.x, b.z - a.z)
	var bc := Vector2(c.x - b.x, c.z - b.z)
	var ac := Vector2(c.x - a.x, c.z - a.z)
	var denom := ab.length() * bc.length() * ac.length()
	if denom < 0.000001:
		return 0.0
	# 2D cross > 0 means a clockwise (right-hand) turn in Godot's X/Z plane.
	return 2.0 * ab.cross(bc) / denom


func wrap_index(i: int) -> int:
	return posmod(i, count)


## Closest centreline sample, searching `window` samples around `hint`.
## A negative hint triggers a full search (after teleports).
func find_closest_index(pos: Vector3, hint: int, window: int) -> int:
	if hint < 0 or hint >= count:
		return find_closest_index_full(pos)
	var best := hint
	var best_d := INF
	for k in range(-window, window + 1):
		var i := wrap_index(hint + k)
		var p := points[i]
		var dx := p.x - pos.x
		var dz := p.z - pos.z
		var d := dx * dx + dz * dz
		if d < best_d:
			best_d = d
			best = i
	return best


func find_closest_index_full(pos: Vector3) -> int:
	var best := 0
	var best_d := INF
	for i in count:
		var p := points[i]
		var d := (p.x - pos.x) * (p.x - pos.x) + (p.z - pos.z) * (p.z - pos.z)
		if d < best_d:
			best_d = d
			best = i
	return best


## Distance along the track (0..length) of a position near sample `index`.
func distance_along(pos: Vector3, index: int) -> float:
	var s := distances[index] + (pos - points[index]).dot(tangents[index])
	return fposmod(s, length)


## Signed lateral offset from the centreline (+ = right of driving direction).
func lateral_offset(pos: Vector3, index: int) -> float:
	return (pos - points[index]).dot(rights[index])


## Index of the sample at distance `s` (wraps around the loop).
func index_at_distance(s: float) -> int:
	return wrap_index(int(floorf(fposmod(s, length) / spacing)))


## Racing-line point at distance `s` with an extra lateral shift (m).
func racing_point_at(s: float, extra_offset: float) -> Vector3:
	var f := fposmod(s, length) / spacing
	var i0 := wrap_index(int(floorf(f)))
	var i1 := wrap_index(i0 + 1)
	var t := f - floorf(f)
	var p := racing_points[i0].lerp(racing_points[i1], t)
	var r := rights[i0].lerp(rights[i1], t)
	return p + r * extra_offset


func racing_offset_at(s: float) -> float:
	var f := fposmod(s, length) / spacing
	var i0 := wrap_index(int(floorf(f)))
	var i1 := wrap_index(i0 + 1)
	return lerpf(racing_offsets[i0], racing_offsets[i1], f - floorf(f))


## World transform on the centreline at `index`, shifted sideways / backwards,
## facing the driving direction, raised to spawn height.
func spawn_transform(index: int, lateral: float, back: float) -> Transform3D:
	var i := wrap_index(index)
	var fwd := tangents[i]
	var pos := points[i] + rights[i] * lateral - fwd * back
	# Re-sample height where the car really is (the track may climb).
	var j := find_closest_index(pos, i, 8)
	pos.y = points[j].y + Config.SPAWN_HEIGHT
	var basis := Basis.looking_at(fwd, normals[i])
	return Transform3D(basis, pos)


## 2D distance from (x, z) to the nearest centreline sample, or `max_dist`
## when nothing is that close (uses the spatial hash).
func distance_to_centerline(x: float, z: float, max_dist: float) -> float:
	var reach := int(ceilf(max_dist / _grid_cell))
	var cx := int(floorf(x / _grid_cell))
	var cz := int(floorf(z / _grid_cell))
	var best := max_dist * max_dist
	for gx in range(cx - reach, cx + reach + 1):
		for gz in range(cz - reach, cz + reach + 1):
			var key := Vector2i(gx, gz)
			if not _grid.has(key):
				continue
			var ids: PackedInt32Array = _grid[key]
			for id in ids:
				var p := points[id]
				var d := (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z)
				if d < best:
					best = d
	return sqrt(best)


func _build_bounds() -> void:
	var b := AABB(points[0], Vector3.ZERO)
	min_height = points[0].y
	for p in points:
		b = b.expand(p)
		min_height = minf(min_height, p.y)
	bounds = b


func _build_grid() -> void:
	_grid.clear()
	for i in count:
		var p := points[i]
		var key := Vector2i(int(floorf(p.x / _grid_cell)), int(floorf(p.z / _grid_cell)))
		var ids: PackedInt32Array = _grid.get(key, PackedInt32Array())
		ids.append(i)
		_grid[key] = ids
