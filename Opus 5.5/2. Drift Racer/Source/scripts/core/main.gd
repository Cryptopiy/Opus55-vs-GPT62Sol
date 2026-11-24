class_name GameMain
extends Node
## Entry point (main scene, everything else is built in code). Owns the menus,
## HUD and the current Race; starts / restarts / leaves races; handles the
## global keys F1 (debug overlay), F12 (screenshot) and Esc (pause). F8 (hide
## all UI) lives in UiVisibility, which is created first so it sees every UI node.

var seed_value := Config.DEFAULT_SEED
var difficulty := Config.DEFAULT_DIFFICULTY
var assist_enabled := true
var hood_camera := false

var race: Race
var menu: MainMenu
var hud: Hud
var pause_menu: PauseMenu
var results: ResultsScreen
var debug_overlay: DebugOverlay
var autotest: AutoTest
var ui_visibility: UiVisibility

var _loading: CanvasLayer
var _starting := false
var _quitting := false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	get_tree().auto_accept_quit = false
	Engine.physics_ticks_per_second = Config.PHYSICS_TICKS
	InputSetup.register()
	ui_visibility = UiVisibility.new()
	add_child(ui_visibility)
	menu = MainMenu.new()
	ui_visibility.keep_visible(menu)
	add_child(menu)
	menu.start_requested.connect(start_race)
	menu.quit_requested.connect(quit_game)
	hud = Hud.new()
	hud.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(hud)
	pause_menu = PauseMenu.new()
	ui_visibility.keep_visible(pause_menu)
	add_child(pause_menu)
	pause_menu.resume_requested.connect(_resume)
	pause_menu.restart_requested.connect(func() -> void: start_race(seed_value, difficulty))
	pause_menu.menu_requested.connect(open_menu)
	pause_menu.quit_requested.connect(quit_game)
	results = ResultsScreen.new()
	ui_visibility.keep_visible(results)
	add_child(results)
	results.restart_requested.connect(func() -> void: start_race(seed_value, difficulty))
	results.new_seed_requested.connect(func() -> void: start_race(randi_range(Config.SEED_MIN, 99999), difficulty))
	results.menu_requested.connect(open_menu)
	debug_overlay = DebugOverlay.new()
	add_child(debug_overlay)
	_loading = _make_loading_layer()
	var scenario := AutoTest.requested_scenario()
	if scenario.is_empty():
		open_menu()
	else:
		autotest = AutoTest.new()
		add_child(autotest)
		autotest.run(self, scenario)


func _exit_tree() -> void:
	CarVisuals.clear_cache()
	FxFactory.clear_cache()
	Synth.clear_cache()
	EngineSynth.clear_cache()
	UiStyle.clear_cache()


## Builds a new race (track from the seed) and shows the HUD.
func start_race(new_seed: int, new_difficulty: int) -> void:
	if _starting:
		return
	_starting = true
	seed_value = new_seed
	difficulty = new_difficulty
	_end_race()
	menu.visible = false
	results.close()
	pause_menu.visible = false
	get_tree().paused = false
	_loading.visible = true
	# Let the loading screen render before the (blocking) track generation.
	await get_tree().process_frame
	await get_tree().process_frame
	race = Race.new()
	race.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(race)
	race.setup(seed_value, difficulty, assist_enabled, hood_camera)
	race.player_finished.connect(_on_player_finished)
	race.settings_changed.connect(_on_race_settings_changed)
	hud.bind(race)
	debug_overlay.race = race
	_loading.visible = false
	_starting = false


func open_menu() -> void:
	_end_race()
	results.close()
	pause_menu.visible = false
	get_tree().paused = false
	menu.open(seed_value, difficulty)


## Stops every sound and gives the audio server a few mixes to release its
## playbacks before quitting (otherwise they are reported as leaked).
func quit_game() -> void:
	if _quitting:
		return
	_quitting = true
	get_tree().paused = false
	for p in find_children("*", "AudioStreamPlayer", true, false):
		(p as AudioStreamPlayer).stop()
	for p in find_children("*", "AudioStreamPlayer3D", true, false):
		(p as AudioStreamPlayer3D).stop()
	_end_race()
	if autotest != null:
		autotest.queue_free()
	for _i in 4:
		OS.delay_msec(30)
		await get_tree().process_frame
	get_tree().quit()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		quit_game()


func _end_race() -> void:
	if race == null:
		return
	hud.unbind()
	debug_overlay.race = null
	remove_child(race)
	race.queue_free()
	race = null


func _on_player_finished() -> void:
	results.open(race)


func _on_race_settings_changed() -> void:
	assist_enabled = race.player.assist_enabled
	hood_camera = race.camera.mode == ChaseCamera.Mode.HOOD


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed(InputSetup.DEBUG):
		debug_overlay.toggle()
	elif event.is_action_pressed(InputSetup.SCREENSHOT):
		var path := Screenshot.capture(get_viewport())
		if not path.is_empty():
			print("[main] screenshot saved: ", path)
	elif event.is_action_pressed(InputSetup.PAUSE):
		if race == null or results.visible or _starting:
			return
		if get_tree().paused:
			_resume()
		else:
			get_tree().paused = true
			pause_menu.open()


func _resume() -> void:
	pause_menu.visible = false
	get_tree().paused = false


func _make_loading_layer() -> CanvasLayer:
	var layer := CanvasLayer.new()
	layer.layer = 40
	var bg := ColorRect.new()
	bg.color = Color(0.02, 0.03, 0.06)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	layer.add_child(bg)
	var l := UiStyle.label("GENERATING TRACK...", 40, UiStyle.ACCENT, 8)
	l.set_anchors_preset(Control.PRESET_CENTER)
	l.grow_horizontal = Control.GROW_DIRECTION_BOTH
	l.grow_vertical = Control.GROW_DIRECTION_BOTH
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	layer.add_child(l)
	layer.visible = false
	add_child(layer)
	return layer
