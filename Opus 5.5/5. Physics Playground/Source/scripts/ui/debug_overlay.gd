extends PanelContainer
## F1 debug overlay (common rules + P-27): FPS, frame time, draw calls, physics bodies (total / active),
## physics step time, particles, time scale and the measured rope stretch.

const C := preload("res://scripts/core/config.gd")
const Style := preload("res://scripts/ui/ui_style.gd")

var main: Node
var _label: Label
var _accum := 0.0


func _ready() -> void:
	name = "DebugOverlay"
	set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	position = Vector2(12, 150)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label = Style.label("", 15)
	add_child(_label)
	visible = false
	process_mode = Node.PROCESS_MODE_ALWAYS


func _process(delta: float) -> void:
	if not visible:
		return
	_accum += delta / maxf(Engine.time_scale, 0.001)
	if _accum < C.DEBUG_REFRESH:
		return
	_accum = 0.0
	var fps := Engine.get_frames_per_second()
	var lines := PackedStringArray()
	lines.append("FPS %d   (%.2f ms/frame)" % [fps, 1000.0 / maxf(fps, 1.0)])
	lines.append("Draw calls %d" % int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)))
	var session: Node = main.get(&"session")
	var bodies := 0
	var particles := 0
	var stretch := 0.0
	if session:
		var sim: Node = session.get(&"sim")
		bodies = sim.get(&"body_count") if sim else session.get(&"static_body_count")
		var vfx: Node = session.get(&"vfx")
		particles = vfx.call(&"active_particles")
		if sim:
			particles += int(sim.get(&"fan_count")) * C.FAN_PARTICLES
			stretch = sim.call(&"rope_stretch")
	lines.append("Physics bodies %d   (active %d)" % [bodies, int(Performance.get_monitor(Performance.PHYSICS_3D_ACTIVE_OBJECTS))])
	lines.append("Physics step %.3f ms" % (Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0))
	lines.append("Particles %d" % particles)
	lines.append("Physics tick 1/%d s x %.2f speed" % [C.PHYS_HZ, Engine.time_scale])
	if stretch > 0.0:
		lines.append("Rope max stretch %.2f %%" % (stretch * 100.0))
	lines.append("Memory %.0f MB" % (Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0))
	_label.text = "\n".join(lines)
