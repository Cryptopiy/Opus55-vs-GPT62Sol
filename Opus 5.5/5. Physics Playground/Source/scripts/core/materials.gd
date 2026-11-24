extends RefCounted
## Material factory. Materials are cached and shared so identical objects reuse one material
## (fewer state changes / draw calls). Physics materials are cached the same way.

const C := preload("res://scripts/core/config.gd")
const SH_ROUNDED := preload("res://shaders/rounded_box.gdshader")
const SH_BACKDROP := preload("res://shaders/backdrop.gdshader")
const SH_HATCH := preload("res://shaders/hatch.gdshader")
const SH_CONVEYOR := preload("res://shaders/conveyor.gdshader")
const SH_OUTLINE := preload("res://shaders/outline.gdshader")
const SH_GHOST := preload("res://shaders/ghost.gdshader")
const SH_STREAK := preload("res://shaders/fan_streak.gdshader")

static var _cache := {}


static func _cached(key: String) -> Material:
	return _cache.get(key, null)


## Flat-coloured "rounded edge" material for box meshes (needs the half_extents instance uniform).
static func rounded(color: Color, rough: float = 0.6, metal: float = 0.0, glow: Color = Color.BLACK, glow_energy: float = 0.0) -> ShaderMaterial:
	var key := "rounded_%s_%.2f_%.2f_%s_%.2f" % [color.to_html(), rough, metal, glow.to_html(), glow_energy]
	var m := _cached(key) as ShaderMaterial
	if m:
		return m
	m = ShaderMaterial.new()
	m.shader = SH_ROUNDED
	m.set_shader_parameter("albedo", color)
	m.set_shader_parameter("roughness", rough)
	m.set_shader_parameter("metallic", metal)
	m.set_shader_parameter("emission", glow)
	m.set_shader_parameter("emission_energy", glow_energy)
	_cache[key] = m
	return m


static func standard(color: Color, rough: float = 0.6, metal: float = 0.0) -> StandardMaterial3D:
	var key := "std_%s_%.2f_%.2f" % [color.to_html(), rough, metal]
	var m := _cached(key) as StandardMaterial3D
	if m:
		return m
	m = StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = rough
	m.metallic = metal
	_cache[key] = m
	return m


static func emissive(color: Color, energy: float) -> StandardMaterial3D:
	var key := "emi_%s_%.2f" % [color.to_html(), energy]
	var m := _cached(key) as StandardMaterial3D
	if m:
		return m
	m = StandardMaterial3D.new()
	m.albedo_color = color
	m.emission_enabled = true
	m.emission = color
	m.emission_energy_multiplier = energy
	m.roughness = 0.4
	_cache[key] = m
	return m


## Unique (not cached) emissive material for things that animate their glow (switch, cup rim).
static func emissive_unique(color: Color, energy: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.emission_enabled = true
	m.emission = color
	m.emission_energy_multiplier = energy
	m.roughness = 0.4
	return m


static func double_sided(color: Color, rough: float = 0.8) -> StandardMaterial3D:
	var key := "ds_%s_%.2f" % [color.to_html(), rough]
	var m := _cached(key) as StandardMaterial3D
	if m:
		return m
	m = StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = rough
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	_cache[key] = m
	return m


static func ghost(ok: bool) -> ShaderMaterial:
	return _flat_shader("ghost_%s" % ok, SH_GHOST, C.COL_GHOST_OK if ok else C.COL_GHOST_BAD)


static func hint() -> ShaderMaterial:
	return _flat_shader("hint", SH_GHOST, C.COL_HINT)


static func outline() -> ShaderMaterial:
	return _flat_shader("outline", SH_OUTLINE, C.COL_OUTLINE)


static func hatch() -> ShaderMaterial:
	var c := C.COL_FORBIDDEN
	return _flat_shader("hatch", SH_HATCH, Color(c.r, c.g, c.b, 0.55))


static func streak() -> ShaderMaterial:
	return _flat_shader("streak", SH_STREAK, Color(0.85, 0.95, 1.0, 0.55))


static func _flat_shader(key: String, sh: Shader, col: Color) -> ShaderMaterial:
	var m := _cached(key) as ShaderMaterial
	if m:
		return m
	m = ShaderMaterial.new()
	m.shader = sh
	m.set_shader_parameter("color", col)
	_cache[key] = m
	return m


static func backdrop(y_min: float, y_max: float) -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = SH_BACKDROP
	m.set_shader_parameter("top_color", C.COL_BG_TOP)
	m.set_shader_parameter("bottom_color", C.COL_BG_BOTTOM)
	m.set_shader_parameter("grid_color", C.COL_GRID)
	m.set_shader_parameter("y_min", y_min)
	m.set_shader_parameter("y_max", y_max)
	return m


static func conveyor() -> ShaderMaterial:
	var m := _cached("conveyor") as ShaderMaterial
	if m:
		return m
	m = ShaderMaterial.new()
	m.shader = SH_CONVEYOR
	m.set_shader_parameter("base_color", C.COL_CONVEYOR)
	m.set_shader_parameter("stripe_color", C.COL_CONVEYOR_STRIPE)
	m.set_shader_parameter("spacing", C.CONVEYOR_STRIPE_SPACING)
	_cache["conveyor"] = m
	return m


## Unshaded vertex-coloured material for particles (dust, confetti); alpha comes from the colour ramp.
static func particle(additive: bool = false) -> StandardMaterial3D:
	var key := "particle_%s" % additive
	var m := _cached(key) as StandardMaterial3D
	if m:
		return m
	m = StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.vertex_color_use_as_albedo = true
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	if additive:
		m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	_cache[key] = m
	return m


static func uses_extents(mat: Material) -> bool:
	var sm := mat as ShaderMaterial
	return sm != null and (sm.shader == SH_ROUNDED or sm.shader == SH_CONVEYOR)


static func phys(friction: float, bounce: float, absorbent: bool = false) -> PhysicsMaterial:
	var key := "phys_%.3f_%.3f_%s" % [friction, bounce, absorbent]
	if _cache.has(key):
		return _cache[key]
	var m := PhysicsMaterial.new()
	m.friction = friction
	m.bounce = bounce
	m.absorbent = absorbent
	_cache[key] = m
	return m
