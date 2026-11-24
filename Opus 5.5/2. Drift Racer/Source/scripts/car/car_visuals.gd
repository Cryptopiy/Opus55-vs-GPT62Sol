class_name CarVisuals
extends Node3D
## Procedural low-poly sports car: lofted body with wheel arches, glass cabin,
## spoiler, wheels that follow suspension travel / steer / spin (R-08), and
## lights (R-19): headlights always on, brake lights (emissive + omni) while
## braking, reverse lights in reverse.

# Body loft stations: z, lower half-width (narrow at the wheels = arches),
# full half-width, bottom y, top y. Body origin = suspension top (road ~ -0.59).
const BODY_STATIONS := [
	[-2.18, 0.70, 0.74, -0.36, -0.06],
	[-1.95, 0.84, 0.88, -0.42, 0.02],
	[-1.82, 0.62, 0.91, -0.43, 0.05],
	[-0.80, 0.62, 0.92, -0.44, 0.09],
	[-0.68, 0.88, 0.92, -0.44, 0.10],
	[0.68, 0.88, 0.93, -0.44, 0.14],
	[0.80, 0.62, 0.94, -0.44, 0.15],
	[1.82, 0.62, 0.93, -0.43, 0.16],
	[1.98, 0.84, 0.89, -0.40, 0.15],
	[2.18, 0.78, 0.82, -0.36, 0.10],
]
# Cabin stations: z, bottom half-width, top half-width, bottom y, top y.
const CABIN_STATIONS := [
	[-0.62, 0.78, 0.62, 0.10, 0.11],
	[-0.02, 0.74, 0.58, 0.12, 0.50],
	[0.78, 0.74, 0.58, 0.14, 0.50],
	[1.40, 0.78, 0.64, 0.155, 0.165],
]

static var _shared := {}

var _wheel_pivots: Array[Node3D] = []
var _wheel_spinners: Array[Node3D] = []
var _tail_mat: StandardMaterial3D
var _reverse_mat: StandardMaterial3D
var _brake_light: OmniLight3D
var _reverse_light: OmniLight3D
var _brake_on := false
var _reverse_on := false


func build(car: Car) -> void:
	name = "Visuals"
	var paint := StandardMaterial3D.new()
	paint.albedo_color = car.paint_color
	paint.metallic = 0.2
	paint.roughness = 0.38
	paint.clearcoat_enabled = true
	paint.clearcoat = 1.0
	paint.clearcoat_roughness = 0.08
	var body := MeshArrays.new()
	_build_body(body)
	var glass := MeshArrays.new()
	var roof := MeshArrays.new()
	_build_cabin(glass, roof)
	var mesh := ArrayMesh.new()
	body.commit(mesh, paint)
	roof.commit(mesh, paint)
	glass.commit(mesh, _shared_mat("glass"))
	var body_mi := MeshInstance3D.new()
	body_mi.name = "Body"
	body_mi.mesh = mesh
	add_child(body_mi)
	_build_details()
	_build_lights()
	for w in Car.WHEEL_COUNT:
		var pivot := Node3D.new()
		pivot.name = "Wheel%d" % w
		var mount := Car.mount_position(w)
		pivot.position = Vector3(mount.x, mount.y - Config.SUSPENSION_REST, mount.z)
		add_child(pivot)
		var spinner := MeshInstance3D.new()
		spinner.mesh = _wheel_mesh(w % 2 == 1)
		pivot.add_child(spinner)
		_wheel_pivots.append(pivot)
		_wheel_spinners.append(spinner)


## Called every physics tick: wheel travel / steering / spin and lights.
func update_from_car(car: Car) -> void:
	for w in Car.WHEEL_COUNT:
		var pivot := _wheel_pivots[w]
		pivot.position.y = Config.MOUNT_HEIGHT - Config.SUSPENSION_REST + car.wheel_compression[w]
		if w < 2:
			pivot.rotation.y = -car.steer_angle
		# Rolling forward (-Z) is a negative rotation about +X.
		_wheel_spinners[w].rotation.x = -car.wheel_rotation[w]
	var braking := car.is_braking()
	if braking != _brake_on:
		_brake_on = braking
		_tail_mat.emission_energy_multiplier = Config.BRAKE_LIGHT_EMISSION if braking else Config.TAIL_LIGHT_IDLE_EMISSION
		_brake_light.visible = braking
	var reversing := car.drivetrain.is_reverse()
	if reversing != _reverse_on:
		_reverse_on = reversing
		_reverse_mat.emission_energy_multiplier = Config.REVERSE_LIGHT_EMISSION if reversing else 0.0
		_reverse_light.visible = reversing


func _build_body(a: MeshArrays) -> void:
	var rings: Array[PackedVector3Array] = []
	for st_variant in BODY_STATIONS:
		var st: Array = st_variant
		rings.append(_body_ring(st))
	for s in rings.size() - 1:
		var r0 := rings[s]
		var r1 := rings[s + 1]
		var mid_y := 0.5 * (r0[0].y + r0[6].y)
		for k in r0.size() - 1:
			var centre := (r0[k] + r0[k + 1] + r1[k + 1] + r1[k]) * 0.25
			# Outward = away from the body's long axis at this height.
			_flat_quad(a, r0[k], r0[k + 1], r1[k + 1], r1[k], centre - Vector3(0.0, mid_y, centre.z))
	_cap(a, rings[0], Vector3.FORWARD)
	_cap(a, rings[rings.size() - 1], Vector3.BACK)


## Closed cross-section ring (left side mirrored), bottom-centre first.
func _body_ring(st: Array) -> PackedVector3Array:
	var z: float = st[0]
	var w_low: float = st[1]
	var w_max: float = st[2]
	var b: float = st[3]
	var t: float = st[4]
	var f := minf(0.08, t - 0.1)
	var half := PackedVector3Array([
		Vector3(0.0, b, z), Vector3(w_low, b, z), Vector3(w_low, f, z),
		Vector3(w_max, f + 0.06, z), Vector3(w_max, t - 0.07, z),
		Vector3(w_max - 0.1, t, z), Vector3(0.0, t, z),
	])
	var ring := PackedVector3Array()
	for p in half:
		ring.append(p)
	for i in range(half.size() - 2, -1, -1):
		var p := half[i]
		ring.append(Vector3(-p.x, p.y, p.z))
	return ring


func _build_cabin(glass: MeshArrays, roof: MeshArrays) -> void:
	for s in CABIN_STATIONS.size() - 1:
		var a: Array = CABIN_STATIONS[s]
		var b: Array = CABIN_STATIONS[s + 1]
		var a_pts := _cabin_points(a)
		var b_pts := _cabin_points(b)
		# a_pts: [left bottom, left top, right top, right bottom]
		_cabin_quad(glass, a_pts[0], a_pts[1], b_pts[1], b_pts[0])
		_cabin_quad(glass, a_pts[2], a_pts[3], b_pts[3], b_pts[2])
		var top_target := roof if s == 1 else glass
		_cabin_quad(top_target, a_pts[1], a_pts[2], b_pts[2], b_pts[1])


## Cabin faces point away from a point inside the cabin (windshield and rear
## window are steep, so "away from the long axis" would be ambiguous there).
func _cabin_quad(a: MeshArrays, p0: Vector3, p1: Vector3, p2: Vector3, p3: Vector3) -> void:
	var centre := (p0 + p1 + p2 + p3) * 0.25
	_flat_quad(a, p0, p1, p2, p3, centre - Vector3(0.0, 0.15, 0.4))


func _cabin_points(st: Array) -> PackedVector3Array:
	var z: float = st[0]
	var wb: float = st[1]
	var wt: float = st[2]
	var yb: float = st[3]
	var yt: float = st[4]
	return PackedVector3Array([Vector3(-wb, yb, z), Vector3(-wt, yt, z), Vector3(wt, yt, z), Vector3(wb, yb, z)])


## Faceted quad with its own vertices and flat normal, facing `facing`.
func _flat_quad(a: MeshArrays, p0: Vector3, p1: Vector3, p2: Vector3, p3: Vector3, facing: Vector3) -> void:
	var n := (p2 - p0).cross(p1 - p0)
	if n.length_squared() < 0.0000001:
		n = (p3 - p0).cross(p2 - p0)
	if n.dot(facing) < 0.0:
		n = -n
	n = n.normalized()
	var i0 := a.add_vertex(p0, n, Vector2.ZERO)
	var i1 := a.add_vertex(p1, n, Vector2.ZERO)
	var i2 := a.add_vertex(p2, n, Vector2.ZERO)
	var i3 := a.add_vertex(p3, n, Vector2.ZERO)
	a.add_quad_facing(i0, i1, i2, i3, n)


func _cap(a: MeshArrays, ring: PackedVector3Array, facing: Vector3) -> void:
	var centre := Vector3.ZERO
	for p in ring:
		centre += p
	centre /= float(ring.size())
	var c := a.add_vertex(centre, facing, Vector2.ZERO)
	for k in ring.size():
		var i0 := a.add_vertex(ring[k], facing, Vector2.ZERO)
		var i1 := a.add_vertex(ring[(k + 1) % ring.size()], facing, Vector2.ZERO)
		a.add_tri_facing(c, i0, i1, facing)


func _build_details() -> void:
	var trim := _shared_mat("trim")
	_box(Vector3(1.3, 0.14, 0.06), Vector3(0.0, -0.26, -2.17), trim)          # grille
	_box(Vector3(1.4, 0.1, 0.12), Vector3(0.0, -0.37, 2.15), trim)           # diffuser
	_box(Vector3(1.56, 0.04, 0.32), Vector3(0.0, 0.36, 1.98), trim)          # spoiler wing
	for x in [-0.55, 0.55]:
		_box(Vector3(0.05, 0.2, 0.12), Vector3(x, 0.26, 1.98), trim)          # spoiler struts
	for x in [-0.82, 0.82]:
		_box(Vector3(0.12, 0.08, 0.16), Vector3(x, 0.2, -0.5), trim)          # mirrors
	var head := _shared_mat("headlight")
	for x in [-0.52, 0.52]:
		_box(Vector3(0.34, 0.07, 0.06), Vector3(x, -0.13, -2.15), head)
	_tail_mat = StandardMaterial3D.new()
	_tail_mat.albedo_color = Color(0.5, 0.02, 0.02)
	_tail_mat.emission_enabled = true
	_tail_mat.emission = Color(1.0, 0.05, 0.03)
	_tail_mat.emission_energy_multiplier = Config.TAIL_LIGHT_IDLE_EMISSION
	_reverse_mat = StandardMaterial3D.new()
	_reverse_mat.albedo_color = Color(0.8, 0.8, 0.8)
	_reverse_mat.emission_enabled = true
	_reverse_mat.emission = Color(1.0, 1.0, 0.95)
	_reverse_mat.emission_energy_multiplier = 0.0
	for x in [-0.56, 0.56]:
		_box(Vector3(0.38, 0.08, 0.05), Vector3(x, 0.03, 2.16), _tail_mat)
	for x in [-0.27, 0.27]:
		_box(Vector3(0.12, 0.06, 0.05), Vector3(x, 0.03, 2.17), _reverse_mat)


func _build_lights() -> void:
	for x in [-0.52, 0.52]:
		var spot := SpotLight3D.new()
		spot.name = "Headlight"
		spot.position = Vector3(x, -0.13, -2.22)
		spot.rotation = Vector3(deg_to_rad(-4.0), 0.0, 0.0)
		spot.spot_range = Config.HEADLIGHT_RANGE
		spot.spot_angle = Config.HEADLIGHT_ANGLE_DEG
		spot.light_energy = Config.HEADLIGHT_ENERGY
		spot.light_color = Color(1.0, 0.96, 0.85)
		spot.shadow_enabled = false
		add_child(spot)
	_brake_light = OmniLight3D.new()
	_brake_light.name = "BrakeLight"
	_brake_light.position = Vector3(0.0, 0.05, 2.45)
	_brake_light.light_color = Color(1.0, 0.08, 0.05)
	_brake_light.light_energy = Config.BRAKE_OMNI_ENERGY
	_brake_light.omni_range = Config.BRAKE_OMNI_RANGE
	_brake_light.visible = false
	add_child(_brake_light)
	_reverse_light = OmniLight3D.new()
	_reverse_light.name = "ReverseLight"
	_reverse_light.position = Vector3(0.0, 0.0, 2.5)
	_reverse_light.light_color = Color(1.0, 1.0, 0.95)
	_reverse_light.light_energy = Config.REVERSE_OMNI_ENERGY
	_reverse_light.omni_range = Config.BRAKE_OMNI_RANGE
	_reverse_light.visible = false
	add_child(_reverse_light)


func _box(size: Vector3, pos: Vector3, mat: Material) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	mesh.material = mat
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.position = pos
	add_child(mi)


## Releases the static material / mesh cache (called on shutdown).
static func clear_cache() -> void:
	_shared.clear()


static func _shared_mat(kind: String) -> StandardMaterial3D:
	if _shared.has(kind):
		var cached: StandardMaterial3D = _shared[kind]
		return cached
	var m := StandardMaterial3D.new()
	match kind:
		"glass":
			m.albedo_color = Color(0.06, 0.08, 0.11)
			m.metallic = 0.3
			m.roughness = 0.05
		"trim":
			m.albedo_color = Color(0.04, 0.04, 0.045)
			m.roughness = 0.5
		"headlight":
			m.albedo_color = Color(0.9, 0.9, 0.85)
			m.emission_enabled = true
			m.emission = Color(1.0, 0.97, 0.88)
			m.emission_energy_multiplier = 3.0
		"rubber":
			m.albedo_color = Color(0.05, 0.05, 0.05)
			m.roughness = 0.88
		"rim":
			m.albedo_color = Color(0.72, 0.72, 0.75)
			m.metallic = 0.9
			m.roughness = 0.25
	_shared[kind] = m
	return m


## Wheel mesh (axis = X). The rim face points outward: +X for right wheels.
static func _wheel_mesh(right_side: bool) -> ArrayMesh:
	var key := "wheel_r" if right_side else "wheel_l"
	if _shared.has(key):
		var cached: ArrayMesh = _shared[key]
		return cached
	var out := 1.0 if right_side else -1.0
	var r := Config.WHEEL_RADIUS
	var hw := Config.WHEEL_WIDTH * 0.5
	var segs := 18
	var tyre := MeshArrays.new()
	var rim := MeshArrays.new()
	for s in segs:
		var a0 := TAU * float(s) / float(segs)
		var a1 := TAU * float(s + 1) / float(segs)
		var d0 := Vector3(0.0, cos(a0), sin(a0))
		var d1 := Vector3(0.0, cos(a1), sin(a1))
		var dm := (d0 + d1).normalized()
		var i0 := tyre.add_vertex(d0 * r + Vector3(-hw, 0, 0), d0, Vector2.ZERO)
		var i1 := tyre.add_vertex(d0 * r + Vector3(hw, 0, 0), d0, Vector2.ZERO)
		var i2 := tyre.add_vertex(d1 * r + Vector3(hw, 0, 0), d1, Vector2.ZERO)
		var i3 := tyre.add_vertex(d1 * r + Vector3(-hw, 0, 0), d1, Vector2.ZERO)
		tyre.add_quad_facing(i0, i1, i2, i3, dm)
		# Sidewalls (ring from tyre radius to rim radius) on both faces.
		for face in [-1.0, 1.0]:
			var fx: float = face * hw
			var n := Vector3(face, 0.0, 0.0)
			var j0 := tyre.add_vertex(d0 * r + Vector3(fx, 0, 0), n, Vector2.ZERO)
			var j1 := tyre.add_vertex(d1 * r + Vector3(fx, 0, 0), n, Vector2.ZERO)
			var j2 := tyre.add_vertex(d1 * r * 0.66 + Vector3(fx, 0, 0), n, Vector2.ZERO)
			var j3 := tyre.add_vertex(d0 * r * 0.66 + Vector3(fx, 0, 0), n, Vector2.ZERO)
			tyre.add_quad_facing(j0, j1, j2, j3, n)
		# Rim barrel disc on the outer face, slightly recessed.
		var rn := Vector3(out, 0.0, 0.0)
		var rx := out * (hw - 0.03)
		var c := rim.add_vertex(Vector3(rx, 0, 0), rn, Vector2.ZERO)
		var k0 := rim.add_vertex(d0 * r * 0.66 + Vector3(rx, 0, 0), rn, Vector2.ZERO)
		var k1 := rim.add_vertex(d1 * r * 0.66 + Vector3(rx, 0, 0), rn, Vector2.ZERO)
		rim.add_tri_facing(c, k0, k1, rn)
	var mesh := ArrayMesh.new()
	tyre.commit(mesh, _shared_mat("rubber"))
	rim.commit(mesh, _shared_mat("rim"))
	# Five dark spokes on top of the rim disc so the rotation is visible.
	var spokes := MeshArrays.new()
	var sx := out * (hw - 0.02)
	for k in 5:
		var ang := TAU * float(k) / 5.0
		var d := Vector3(0.0, cos(ang), sin(ang))
		var perp := Vector3(0.0, -sin(ang), cos(ang)) * 0.028
		var n := Vector3(out, 0.0, 0.0)
		var i0 := spokes.add_vertex(Vector3(sx, 0, 0) + perp, n, Vector2.ZERO)
		var i1 := spokes.add_vertex(Vector3(sx, 0, 0) + d * r * 0.62 + perp, n, Vector2.ZERO)
		var i2 := spokes.add_vertex(Vector3(sx, 0, 0) + d * r * 0.62 - perp, n, Vector2.ZERO)
		var i3 := spokes.add_vertex(Vector3(sx, 0, 0) - perp, n, Vector2.ZERO)
		spokes.add_quad_facing(i0, i1, i2, i3, n)
	spokes.commit(mesh, _shared_mat("trim"))
	_shared[key] = mesh
	return mesh
