extends Node
## Autoload "Sfx" (P-26): every sound is synthesized in code at startup into AudioStreamWAV buffers
## (no audio files). One-shots play through a fixed pool of players; conveyor hum and fan whoosh are loops.

const C := preload("res://scripts/core/config.gd")
const Synth := preload("res://scripts/audio/synth.gd")

var _streams := {}
var _players: Array[AudioStreamPlayer] = []
var _next := 0
var _loops := {}
var _impact_cooldown := 0.0
# the headless test runs have no audio device (the dummy driver never releases playbacks): stay silent there
var _enabled := true


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_enabled = DisplayServer.get_name() != "headless"
	var r := C.AUDIO_RATE
	_streams[&"click"] = Synth.wav(Synth.click(r), r)
	_streams[&"tick"] = Synth.wav(Synth.tick(r), r)
	_streams[&"impact_steel"] = Synth.wav(Synth.impact_steel(r), r)
	_streams[&"impact_rubber"] = Synth.wav(Synth.impact_rubber(r), r)
	_streams[&"impact_wood"] = Synth.wav(Synth.impact_wood(r), r)
	_streams[&"creak"] = Synth.wav(Synth.creak(r), r)
	_streams[&"boing"] = Synth.wav(Synth.boing(r), r)
	_streams[&"switch"] = Synth.wav(Synth.switch_click(r), r)
	_streams[&"door"] = Synth.wav(Synth.door_slide(r), r)
	_streams[&"chime"] = Synth.wav(Synth.chime(r), r)
	_streams[&"jingle"] = Synth.wav(Synth.jingle(r), r)
	_streams[&"fail"] = Synth.wav(Synth.fail_buzz(r), r)
	_streams[&"hum"] = Synth.wav(Synth.conveyor_hum(r), r, true)
	_streams[&"whoosh"] = Synth.wav(Synth.fan_whoosh(r), r, true)
	for i in C.AUDIO_POOL:
		var p := AudioStreamPlayer.new()
		add_child(p)
		_players.append(p)
	for loop_name: StringName in [&"hum", &"whoosh"]:
		var lp := AudioStreamPlayer.new()
		lp.stream = _streams[loop_name]
		lp.volume_db = -14.0 if loop_name == &"hum" else -12.0
		add_child(lp)
		_loops[loop_name] = lp


func play(sound: StringName, volume_db: float = 0.0, pitch: float = 1.0) -> void:
	if not _enabled:
		return
	var s: AudioStreamWAV = _streams.get(sound, null)
	if s == null:
		push_error("Unknown sound " + String(sound))
		return
	var p := _players[_next]
	_next = (_next + 1) % _players.size()
	p.stream = s
	p.volume_db = volume_db
	p.pitch_scale = pitch
	p.play()


## Ball impact: timbre by ball material, loudness and brightness scaled by the velocity change.
func play_impact(ball_type: String, dv: float) -> void:
	var now := Time.get_ticks_msec() / 1000.0
	if now < _impact_cooldown:
		return
	_impact_cooldown = now + C.IMPACT_COOLDOWN
	var t := clampf((dv - C.IMPACT_MIN_DV) / (C.IMPACT_FULL_DV - C.IMPACT_MIN_DV), 0.0, 1.0)
	play(StringName("impact_" + ball_type), lerpf(-26.0, 0.0, sqrt(t)), 0.92 + 0.16 * t)


func set_loop(sound: StringName, on: bool) -> void:
	if not _enabled:
		return
	var p: AudioStreamPlayer = _loops[sound]
	if on and not p.playing:
		p.play()
	elif not on and p.playing:
		p.stop()


func stop_loops() -> void:
	for p: AudioStreamPlayer in _loops.values():
		p.stop()


## Before quitting: stop every playback so the audio server releases its streams (no leaks at exit).
func stop_all() -> void:
	stop_loops()
	for p in _players:
		p.stop()
		p.stream = null
