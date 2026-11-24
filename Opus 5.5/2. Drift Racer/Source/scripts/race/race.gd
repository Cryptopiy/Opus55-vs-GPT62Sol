class_name Race
extends Node3D
## One race session: generates the track from the seed, builds the world,
## spawns 4 cars that share the same Car physics (player + 3 AI with their own
## colours, R-20), the race manager, chase camera, skid marks and countdown
## beeps. Handles the in-race keys R (respawn), C (camera) and F3 (assist).

signal player_finished
signal settings_changed

var track: TrackData
var world: TrackWorld
var manager: RaceManager
var cars: Array[Car] = []
var player: Car
var camera: ChaseCamera
var skid_marks: SkidMarks
var difficulty := 1
var seed_value := 0
var build_time_ms := 0
var player_done := false

var _beeper: AudioStreamPlayer
var _rng := RandomNumberGenerator.new()


func setup(user_seed: int, difficulty_index: int, assist: bool, hood_camera: bool) -> void:
	var t0 := Time.get_ticks_msec()
	name = "Race"
	seed_value = user_seed
	difficulty = difficulty_index
	_rng.seed = user_seed + 7
	track = TrackGenerator.generate(user_seed)
	world = TrackWorld.new()
	add_child(world)
	world.build(track)
	skid_marks = SkidMarks.new()
	add_child(skid_marks)
	skid_marks.setup(track)
	for i in Config.CAR_COUNT:
		var car := Car.new()
		add_child(car)
		car.setup(i, i == 0, track, world.barrier_body, skid_marks)
		car.assist_enabled = assist if i == 0 else true
		cars.append(car)
	player = cars[0]
	manager = RaceManager.new()
	add_child(manager)
	manager.setup(track, cars, world.checkpoints)
	var skill: float = Config.DIFFICULTY_SPEED[difficulty]
	for car in cars:
		if car == player:
			set_controller(car, PlayerController.new())
		else:
			var ai := AIController.new()
			ai.setup(skill, _rng, manager)
			set_controller(car, ai)
	camera = ChaseCamera.new()
	camera.name = "ChaseCamera"
	add_child(camera)
	camera.set_target(player)
	if hood_camera:
		camera.toggle_mode()
	_beeper = AudioStreamPlayer.new()
	_beeper.name = "CountdownBeeper"
	_beeper.volume_db = Config.BEEP_VOLUME_DB
	add_child(_beeper)
	manager.countdown_tick.connect(_on_countdown_tick)
	manager.lap_completed.connect(_on_lap_completed)
	manager.car_finished.connect(_on_car_finished)
	manager.player_finished.connect(_on_player_finished)
	build_time_ms = Time.get_ticks_msec() - t0
	print("[race] seed %d (attempt %d, derived %d) length %.0f m, min radius %.1f m, %d trees, built in %d ms"
		% [track.seed_value, track.attempts, track.derived_seed, track.length, track.min_radius, world.tree_count, build_time_ms])


func set_controller(car: Car, ctrl: CarController) -> void:
	if car.controller != null:
		car.controller.queue_free()
	ctrl.name = "Controller"
	car.add_child(ctrl)
	car.controller = ctrl


## Replaces the player's input with the AI driver (autotests / cool-down lap).
func give_player_to_ai(cooldown: bool) -> void:
	var ai := AIController.new()
	ai.setup(Config.DIFFICULTY_SPEED[difficulty], _rng, manager)
	ai.cooldown = cooldown
	set_controller(player, ai)


## Parks the player's car (neutral inputs) - used by the AI-only autotest.
func park_player() -> void:
	set_controller(player, CarController.new())


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed(InputSetup.RESPAWN):
		if manager.is_racing() and not player_done:
			player.request_respawn()
	elif event.is_action_pressed(InputSetup.CAMERA):
		camera.toggle_mode()
		settings_changed.emit()
	elif event.is_action_pressed(InputSetup.ASSIST):
		player.assist_enabled = not player.assist_enabled
		settings_changed.emit()


func _on_countdown_tick(value: int) -> void:
	if value > 0:
		_beeper.stream = Synth.beep(Config.BEEP_FREQ, 0.18)
	else:
		_beeper.stream = Synth.beep(Config.BEEP_GO_FREQ, 0.5)
	_beeper.play()


func _on_lap_completed(car: Car, lap: int, lap_time: float) -> void:
	print("[race] %s lap %d: %s (respawns %d)" % [car.display_name, lap, format_time(lap_time), car.respawn_count])


func _on_car_finished(car: Car, place: int) -> void:
	print("[race] %s finished P%d total %s" % [car.display_name, place, format_time(manager.total_time(car))])
	if car != player and car.controller is AIController:
		(car.controller as AIController).cooldown = true


func _on_player_finished() -> void:
	player_done = true
	give_player_to_ai(true)
	player_finished.emit()


func skid_segments_active() -> int:
	return skid_marks.active_count()


func estimated_particles() -> int:
	var n := 0
	for car in cars:
		n += car.fx.estimated_particles()
	return n


static func format_time(seconds: float) -> String:
	if seconds <= 0.0:
		return "--:--.---"
	var minutes := int(seconds / 60.0)
	var rest := seconds - float(minutes) * 60.0
	return "%d:%06.3f" % [minutes, rest]
