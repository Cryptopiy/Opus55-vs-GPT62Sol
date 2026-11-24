extends Node3D

const C = preload("res://game/config.gd")
enum Screen { MENU, RACE, PAUSE, RESULTS }

var race: RaceSession
var camera: RaceCamera
var skids: SkidPool
var audio: RaceAudio
var effects: Array[CarEffects] = []
var models: Array[TouringBody] = []
var world: Node3D
var screen := Screen.MENU
var countersteer := true
var ui: CircuitInterface
var quitting := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	get_tree().auto_accept_quit = false
	_build_world(C.DEFAULT_SEED, 1)
	ui = CircuitInterface.new()
	add_child(ui)
	ui.setup(self)

func _build_world(seed_value: int, difficulty: int) -> void:
	if is_instance_valid(world):
		if is_instance_valid(audio):
			audio.stop_engines()
		remove_child(world)
		world.free()
	effects.clear()
	models.clear()
	world = Node3D.new()
	world.name = "Circuit"
	world.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(world)
	race = RaceSession.new()
	world.add_child(race)
	race.build(seed_value, difficulty)
	race.set_physics_process(false)
	race.respawned.connect(_on_respawn)
	race.race_finished.connect(_on_finished)
	race.countdown_changed.connect(_on_countdown)
	race.player.assist = countersteer
	var scenery := CircuitScenery.new()
	scenery.build(race.track)
	world.add_child(scenery)
	for car in race.cars:
		var body := TouringBody.new()
		car.add_child(body)
		body.build(car)
		models.append(body)
		var vfx := CarEffects.new()
		world.add_child(vfx)
		vfx.build(car)
		effects.append(vfx)
	skids = SkidPool.new()
	world.add_child(skids)
	camera = RaceCamera.new()
	camera.far = 3500.0
	camera.doppler_tracking = Camera3D.DOPPLER_TRACKING_PHYSICS_STEP
	world.add_child(camera)
	camera.current = true
	camera.showcase(race.track.data)
	if is_instance_valid(ui):
		ui.world_changed()

func start_race(seed_value: int, difficulty: int) -> void:
	get_tree().paused = false
	if screen != Screen.MENU or race.seed_value != seed_value:
		_build_world(seed_value, difficulty)
	race.difficulty = difficulty
	screen = Screen.RACE
	audio = RaceAudio.new()
	world.add_child(audio)
	audio.build(race.cars)
	camera.cut()
	camera.follow(race.player, 0.0)
	race.set_physics_process(true)

func restart_race() -> void:
	start_race(race.seed_value, race.difficulty)

func go_to_menu(seed_selection: int) -> void:
	get_tree().paused = false
	_build_world(seed_selection, race.difficulty)
	screen = Screen.MENU

func pause_race() -> void:
	screen = Screen.PAUSE
	get_tree().paused = true

func resume_race() -> void:
	screen = Screen.RACE
	get_tree().paused = false

func _physics_process(delta: float) -> void:
	if screen != Screen.RACE:
		return
	skids.drive(race.cars, delta)
	for vfx in effects:
		vfx.update_effects(delta)

func _process(delta: float) -> void:
	if screen == Screen.RACE:
		for body in models:
			body.update_body()
		camera.follow(race.player, delta)

func _on_respawn(car: DriftCar) -> void:
	for i in 4:
		skids.started[car.driver_id * 4 + i] = 0
	if car == race.player:
		camera.cut()

func _on_countdown(value: int) -> void:
	audio.beep(value)

func _on_finished() -> void:
	screen = Screen.RESULTS
	audio.stop_engines()

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		quit_game()

func quit_game() -> void:
	if quitting:
		return
	quitting = true
	if is_instance_valid(audio):
		audio.stop_engines()
		# Give the audio thread a mix cycle to release the stopped streams.
		await get_tree().create_timer(0.08).timeout
	get_tree().quit()

func _input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo:
		return
	match event.physical_keycode:
		KEY_F1:
			ui.toggle_debug()
		KEY_ESCAPE:
			if screen == Screen.RACE:
				pause_race()
				get_viewport().set_input_as_handled()
			elif screen == Screen.PAUSE:
				resume_race()
				get_viewport().set_input_as_handled()
		KEY_F3:
			countersteer = not countersteer
			race.player.assist = countersteer
		KEY_F8:
			ui.toggle_gameplay_ui()
		KEY_F12:
			save_screenshot()
		KEY_R:
			if screen == Screen.RACE and race.running and race.player.finish_time <= 0.0:
				race.reset_car(race.player)
		KEY_C:
			if screen == Screen.RACE:
				camera.hood = not camera.hood
				camera.cut()

func save_screenshot() -> void:
	if DisplayServer.get_name() == "headless":
		return
	await RenderingServer.frame_post_draw
	var path := ProjectSettings.globalize_path("res://Screenshots") if OS.has_feature("editor") else OS.get_executable_path().get_base_dir().path_join("Screenshots")
	DirAccess.make_dir_recursive_absolute(path)
	var stamp := Time.get_datetime_string_from_system().replace(":", "-")
	var suffix := "%03d" % int(Time.get_ticks_msec() % 1000)
	var result := get_viewport().get_texture().get_image().save_png(path.path_join("timberline_%s_%s.png" % [stamp, suffix]))
	if result != OK:
		push_error("Screenshot could not be written: %s" % error_string(result))
