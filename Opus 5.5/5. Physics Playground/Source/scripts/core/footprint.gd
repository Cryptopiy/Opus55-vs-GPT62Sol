extends RefCounted
## 2D footprints on the play plane (X right, Y up, angles CCW in radians).
## Used for placement validity (ghost green/red) and mouse picking. Pure math: no physics bodies exist in
## build mode, so the editor can never disturb the deterministic physics state.

class FShape:
	var is_circle := false
	var c := Vector2.ZERO   # centre
	var he := Vector2.ZERO  # half extents (rectangles)
	var ang := 0.0          # rotation (rectangles)
	var r := 0.0            # radius (circles)


static func rect(center: Vector2, full_size: Vector2, ang_rad: float = 0.0) -> FShape:
	var s := FShape.new()
	s.c = center
	s.he = full_size * 0.5
	s.ang = ang_rad
	return s


static func circle(center: Vector2, radius: float) -> FShape:
	var s := FShape.new()
	s.is_circle = true
	s.c = center
	s.r = radius
	return s


## Rectangle given in an object's local frame (origin + rotation of the object).
static func local_rect(origin: Vector2, rot_rad: float, local_center: Vector2, full_size: Vector2, local_ang: float = 0.0) -> FShape:
	return rect(origin + local_center.rotated(rot_rad), full_size, rot_rad + local_ang)


static func local_circle(origin: Vector2, rot_rad: float, local_center: Vector2, radius: float) -> FShape:
	return circle(origin + local_center.rotated(rot_rad), radius)


## Thin rectangle covering the segment a-b (used for the rope line).
static func segment(a: Vector2, b: Vector2, thickness: float) -> FShape:
	var d := b - a
	return rect((a + b) * 0.5, Vector2(d.length(), thickness), d.angle())


static func _axes(s: FShape) -> Array[Vector2]:
	var ax := Vector2(cos(s.ang), sin(s.ang))
	return [ax, Vector2(-ax.y, ax.x)]


static func _radius_on(s: FShape, axis: Vector2) -> float:
	if s.is_circle:
		return s.r
	var ax := Vector2(cos(s.ang), sin(s.ang))
	var ay := Vector2(-ax.y, ax.x)
	return s.he.x * absf(axis.dot(ax)) + s.he.y * absf(axis.dot(ay))


## True when the shapes overlap by more than `eps` (touching surfaces are allowed).
static func overlaps(a: FShape, b: FShape, eps: float) -> bool:
	if a.is_circle and b.is_circle:
		return a.c.distance_to(b.c) < a.r + b.r - eps
	if a.is_circle or b.is_circle:
		var cir := a if a.is_circle else b
		var box := b if a.is_circle else a
		# circle centre in the box frame, clamp to the box -> closest point
		var lp := (cir.c - box.c).rotated(-box.ang)
		var cp := Vector2(clampf(lp.x, -box.he.x, box.he.x), clampf(lp.y, -box.he.y, box.he.y))
		var inside := absf(lp.x) <= box.he.x and absf(lp.y) <= box.he.y
		if inside:
			return true
		return lp.distance_to(cp) < cir.r - eps
	# separating axis test for two oriented rectangles
	var d := b.c - a.c
	for axis in _axes(a) + _axes(b):
		if absf(d.dot(axis)) >= _radius_on(a, axis) + _radius_on(b, axis) - eps:
			return false
	return true


static func any_overlap(fa: Array, fb: Array, eps: float) -> bool:
	for sa: FShape in fa:
		for sb: FShape in fb:
			if overlaps(sa, sb, eps):
				return true
	return false


static func contains_point(f: Array, p: Vector2) -> bool:
	for s: FShape in f:
		if s.is_circle:
			if p.distance_to(s.c) <= s.r:
				return true
		else:
			var lp := (p - s.c).rotated(-s.ang)
			if absf(lp.x) <= s.he.x and absf(lp.y) <= s.he.y:
				return true
	return false


static func aabb(s: FShape) -> Rect2:
	if s.is_circle:
		return Rect2(s.c - Vector2(s.r, s.r), Vector2(s.r, s.r) * 2.0)
	var ext := Vector2(_radius_on(s, Vector2.RIGHT), _radius_on(s, Vector2.UP))
	return Rect2(s.c - ext, ext * 2.0)


static func inside_bounds(f: Array, bounds: Rect2) -> bool:
	for s: FShape in f:
		var bb := aabb(s)
		if bb.position.x < bounds.position.x or bb.position.y < bounds.position.y:
			return false
		if bb.end.x > bounds.end.x or bb.end.y > bounds.end.y:
			return false
	return true
