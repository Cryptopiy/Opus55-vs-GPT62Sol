class_name RaceAudio
extends Node

const C = preload("res://game/config.gd")
const SAMPLE_RATE = 22050.0
const BLOCK = 128
const STEP = 1.0 / SAMPLE_RATE

class Motor:
	var node: Node
	var stream: AudioStreamGeneratorPlayback
	var phase := 0.0
	var pitch := 34.0
	var load := 0.0
	var noise := 0.0
	var squeal_phase := 0.0
	var road_hiss := 0.0
	var impact := 0.0
	var last_speed := 0.0
	var recent_impact := 0.0

var motors: Array[Motor] = []
var voices: Array[DriftCar] = []
var master := PackedVector2Array()
var cue_stream: AudioStreamGeneratorPlayback
var cue: AudioStreamPlayer
var cue_age := 1.0
var cue_frequency := 500.0
var cue_duration := 0.14
var silent := false

func build(cars: Array[DriftCar]) -> void:
	silent = DisplayServer.get_name() == "headless"
	if silent:
		return
	voices = cars
	master.resize(BLOCK)
	for car in voices:
		var motor := Motor.new()
		if car.driver_id == 0:
			var listener := AudioStreamPlayer.new()
			listener.stream = _generator()
			add_child(listener)
			listener.play()
			motor.node = listener
			motor.stream = listener.get_stream_playback()
		else:
			var source := AudioStreamPlayer3D.new()
			source.stream = _generator()
			source.unit_size = 7.0
			source.max_distance = 96.0
			source.volume_db = -2.0
			source.doppler_tracking = AudioStreamPlayer3D.DOPPLER_TRACKING_PHYSICS_STEP
			car.add_child(source)
			source.play()
			motor.node = source
			motor.stream = source.get_stream_playback()
		motors.append(motor)
	cue = AudioStreamPlayer.new()
	cue.stream = _generator()
	add_child(cue)
	cue.play()
	cue_stream = cue.get_stream_playback()

func stop_engines() -> void:
	set_process(false)
	for motor in motors:
		motor.stream = null
		if is_instance_valid(motor.node):
			motor.node.stop()
			motor.node.stream = null
	motors.clear()
	voices = []
	cue_stream = null
	if is_instance_valid(cue):
		cue.stop()
		cue.stream = null

func _exit_tree() -> void:
	stop_engines()

func _generator() -> AudioStreamGenerator:
	var stream := AudioStreamGenerator.new()
	stream.mix_rate = SAMPLE_RATE
	stream.buffer_length = 0.085
	return stream

func beep(number: int) -> void:
	cue_age = 0.0
	cue_frequency = 880.0 if number == 0 else 440.0
	cue_duration = 0.29 if number == 0 else 0.14

func _process(_delta: float) -> void:
	if silent or motors.is_empty():
		return
	for i in motors.size():
		var motor := motors[i]
		var car := voices[i]
		var scrape := car.wall_contact and maxf(car.speed, motor.last_speed) * C.KMH > C.SCRAPE_KMH
		if car.collision_impact > 235.0 and car.collision_impact > motor.recent_impact * 1.05:
			motor.impact = maxf(motor.impact, clampf(car.collision_impact / 5000.0, 0.22, 0.92))
		motor.recent_impact = car.collision_impact
		motor.last_speed = car.speed
		var slip := 0.0
		for wheel in car.wheel_slip:
			slip = maxf(slip, wheel)
		var voices_nearby := i == 0 or car.global_position.distance_squared_to(voices[0].global_position) < 108.0 * 108.0
		var blocks := mini(12, floori(float(motor.stream.get_frames_available()) / BLOCK))
		for _block in blocks:
			if voices_nearby:
				_fill_motor(motor, car, slip, scrape)
			else:
				master.fill(Vector2.ZERO)
			motor.stream.push_buffer(master)
	var cue_blocks := mini(12, floori(float(cue_stream.get_frames_available()) / BLOCK))
	for _block in cue_blocks:
		_fill_beep()
		cue_stream.push_buffer(master)

func _fill_motor(motor: Motor, car: DriftCar, slip: float, scrape: bool) -> void:
	var target_hz := car.rpm / 29.0
	var load := car.throttle if car.shift_left <= 0.0 else 0.02
	var shift := clampf(car.shift_left / C.SHIFT_TIME, 0.0, 1.0) * 0.072
	var wind := pow(clampf(car.speed / C.TOP_SPEED, 0.0, 1.0), 1.55) * 0.105
	var squeal := smoothstep(C.SKID_THRESHOLD, 0.79, slip) * smoothstep(2.0, 16.0, car.speed) * 0.116
	var scrape_gain := 0.17 if scrape else 0.0
	for j in BLOCK:
		motor.pitch = lerpf(motor.pitch, target_hz, 0.008)
		motor.load = lerpf(motor.load, load, 0.011)
		motor.phase = fposmod(motor.phase + motor.pitch * TAU * STEP, TAU)
		motor.squeal_phase = fposmod(motor.squeal_phase + (640.0 + slip * 390.0) * TAU * STEP, TAU)
		var noise := randf() * 2.0 - 1.0
		motor.road_hiss = lerpf(motor.road_hiss, noise, 0.13)
		var drive := (sin(motor.phase) * 0.77 + sin(motor.phase * 2.0) * (0.23 + motor.load * 0.74)
			+ sin(motor.phase * 4.0) * motor.load * 0.22) * (0.060 + motor.load * 0.124)
		var tyre := (sin(motor.squeal_phase + motor.road_hiss * 2.0) * 0.62 + noise * 0.29) * squeal
		var hit := (sin(motor.phase * 0.5) * 0.28 + motor.road_hiss * 0.61) * motor.impact
		motor.impact *= 0.99924
		var skitter := lerpf(motor.noise, noise, 0.51) * scrape_gain
		motor.noise = noise
		var sample := clampf(drive + tyre + hit + skitter + motor.road_hiss * wind + noise * shift, -0.9, 0.9)
		master[j] = Vector2(sample, sample)

func _fill_beep() -> void:
	if cue_age >= cue_duration:
		master.fill(Vector2.ZERO)
		return
	for j in BLOCK:
		var envelope := smoothstep(0.0, 0.012, cue_age) * (1.0 - smoothstep(cue_duration * 0.47, cue_duration, cue_age))
		var wave := sin(cue_age * TAU * cue_frequency) * envelope * 0.25
		master[j] = Vector2(wave, wave)
		cue_age += STEP
