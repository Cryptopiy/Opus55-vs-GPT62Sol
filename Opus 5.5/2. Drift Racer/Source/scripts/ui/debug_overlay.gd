class_name DebugOverlay
extends CanvasLayer
## F1 debug overlay (common rules + R-31): FPS, frame time, draw calls,
## active physics bodies, cars / AI, particles (estimated), active skid
## segments, track info and player telemetry. Refreshes 4 times a second.

var race: Race

var _label: Label
var _accum := 0.0
var _frames := 0
var _time_sum := 0.0


func _ready() -> void:
	layer = 30
	process_mode = Node.PROCESS_MODE_ALWAYS
	var panel := UiStyle.panel()
	panel.position = Vector2(24.0, 230.0)
	add_child(panel)
	_label = UiStyle.label("", 16, Color(0.75, 1.0, 0.75), 4)
	panel.add_child(_label)
	visible = false


func toggle() -> void:
	visible = not visible
	_accum = Config.DEBUG_UPDATE_INTERVAL


func _process(delta: float) -> void:
	_frames += 1
	_time_sum += delta
	_accum += delta
	if not visible or _accum < Config.DEBUG_UPDATE_INTERVAL:
		return
	var frame_ms := _time_sum / float(maxi(_frames, 1)) * 1000.0
	_accum = 0.0
	_frames = 0
	_time_sum = 0.0
	var fps := Engine.get_frames_per_second()
	var draws := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)
	var prims := RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME)
	var lines := PackedStringArray()
	lines.append("FPS %.0f   frame %.2f ms" % [fps, frame_ms])
	lines.append("draw calls %d   primitives %d" % [draws, prims])
	if race != null and is_instance_valid(race):
		var p := race.player
		# Jolt does not feed Godot's active-object monitor, so count directly:
		# awake rigid bodies (the cars) + static bodies (road, barriers, ground).
		var awake := 0
		for car in race.cars:
			if not car.sleeping:
				awake += 1
		lines.append("active physics bodies %d rigid + 3 static" % awake)
		lines.append("cars %d (AI %d)" % [race.cars.size(), race.cars.size() - 1])
		lines.append("smoke/spark particles ~%d" % race.estimated_particles())
		lines.append("skid segments %d / %d" % [race.skid_segments_active(), Config.SKID_MAX_SEGMENTS])
		lines.append("seed %d  attempt %d  %.0f m  min R %.1f m" % [race.track.seed_value, race.track.attempts, race.track.length, race.track.min_radius])
		lines.append("player %.0f km/h  gear %d  %.0f rpm  slip %.0f deg  respawns %d" % [
			p.speed_kmh, p.drivetrain.gear, p.drivetrain.rpm, p.body_slip_deg, p.respawn_count])
	_label.text = "\n".join(lines)
