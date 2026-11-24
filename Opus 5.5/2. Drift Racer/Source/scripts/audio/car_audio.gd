class_name CarAudio
extends Node3D
## All sounds of one car (R-30). The player's car uses non-positional players;
## AI cars use AudioStreamPlayer3D (distance attenuation + Doppler) and are
## only synthesised while within AI_AUDIO_RANGE of the camera.
## Engine: EngineSynth on an AudioStreamGenerator. Tyre squeal by slip, wind
## by speed (player), barrier scrape loop, collision thud.

const SILENT_DB := -60.0

var _car: Car
var _positional := false
var _engine_player: Node
var _playback: AudioStreamGeneratorPlayback
var _synth: EngineSynth
var _squeal: Node
var _scrape: Node
var _wind: AudioStreamPlayer
var _thud: Node
var _active := false


func setup(car: Car) -> void:
	name = "Audio"
	_car = car
	_positional = not car.is_player
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = Config.ENGINE_MIX_RATE
	gen.buffer_length = Config.ENGINE_BUFFER_SEC
	_engine_player = _make_player(gen, Config.AI_ENGINE_VOLUME_DB if _positional else Config.ENGINE_VOLUME_DB)
	_synth = EngineSynth.new(1.0)
	_squeal = _make_player(Synth.squeal(), SILENT_DB)
	_scrape = _make_player(Synth.scrape(), SILENT_DB)
	_thud = _make_player(Synth.thud(), Config.THUD_VOLUME_DB)
	if not _positional:
		_wind = AudioStreamPlayer.new()
		_wind.stream = Synth.wind()
		_wind.volume_db = SILENT_DB
		add_child(_wind)
	car.barrier_hit.connect(_on_impact)
	car.car_hit.connect(_on_impact)


func _make_player(stream: AudioStream, volume: float) -> Node:
	if _positional:
		var p3 := AudioStreamPlayer3D.new()
		p3.stream = stream
		p3.volume_db = volume
		p3.unit_size = Config.AUDIO_UNIT_SIZE
		p3.max_distance = Config.AI_AUDIO_RANGE
		p3.attenuation_model = AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
		p3.doppler_tracking = AudioStreamPlayer3D.DOPPLER_TRACKING_PHYSICS_STEP
		add_child(p3)
		return p3
	var p := AudioStreamPlayer.new()
	p.stream = stream
	p.volume_db = volume
	add_child(p)
	return p


func _process(_delta: float) -> void:
	var want := _in_range()
	if want != _active:
		_set_active(want)
	if not _active or _playback == null:
		return
	var dt := _car.drivetrain
	_synth.fill(_playback, dt.rpm, dt.engine_load, dt.shift_event)
	dt.shift_event = 0
	var slip := _car.max_wheel_slip()
	var squeal_amt := clampf((slip - Config.SKID_SLIP_THRESHOLD) / 10.0, 0.0, 1.0) if _car.grounded_count > 0 else 0.0
	_set_volume(_squeal, Config.SQUEAL_VOLUME_DB, squeal_amt)
	_set_pitch(_squeal, 0.9 + 0.25 * squeal_amt)
	var scrape_amt := clampf(_car.speed_kmh / 120.0, 0.3, 1.0) if _car.fx.is_scraping else 0.0
	_set_volume(_scrape, Config.SCRAPE_VOLUME_DB, scrape_amt)
	_set_pitch(_scrape, 0.8 + 0.4 * clampf(_car.speed_kmh / 150.0, 0.0, 1.0))
	if _wind != null:
		var w := clampf(_car.speed_kmh / Config.TOP_SPEED_KMH, 0.0, 1.0)
		_wind.volume_db = Config.WIND_VOLUME_DB + linear_to_db(maxf(w * w, 0.001))
		_wind.pitch_scale = 0.7 + 0.6 * w


func _in_range() -> bool:
	if not _positional:
		return true
	var cam := get_viewport().get_camera_3d()
	if cam == null:
		return false
	return cam.global_position.distance_to(_car.global_position) < Config.AI_AUDIO_RANGE


func _set_active(on: bool) -> void:
	_active = on
	for p in [_engine_player, _squeal, _scrape, _wind]:
		if p == null:
			continue
		if on:
			p.call(&"play")
		else:
			p.call(&"stop")
	if on:
		_playback = _engine_player.call(&"get_stream_playback")


func _set_volume(player: Node, base_db: float, amount: float) -> void:
	var db := SILENT_DB if amount <= 0.001 else base_db + linear_to_db(amount)
	player.set(&"volume_db", db)


func _set_pitch(player: Node, pitch: float) -> void:
	player.set(&"pitch_scale", pitch)


func _on_impact(impact_speed: float) -> void:
	if not _active or impact_speed < Config.IMPACT_MIN_SPEED:
		return
	var amount := clampf(impact_speed / 15.0, 0.25, 1.0)
	_thud.set(&"volume_db", Config.THUD_VOLUME_DB + linear_to_db(amount))
	_thud.set(&"pitch_scale", 1.15 - 0.3 * amount)
	_thud.call(&"play")
