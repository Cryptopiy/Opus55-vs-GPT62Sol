class_name LabSound
extends Node

const RATE := 22050
const VOICES := 10
const CUES := {
	"place": [0.085, 830.0, 6.0, 0.18], "rotate": [0.065, 1220.0, 10.0, 0.11],
	"steel": [0.28, 780.0, 5.0, 0.055], "wood": [0.14, 240.0, 6.5, 0.14],
	"rubber": [0.15, 137.0, 4.0, 0.07], "heavy": [0.27, 71.0, 6.0, 0.31],
	"spring": [0.37, 155.0, 3.5, 0.035], "rope": [0.49, 62.0, 2.3, 0.14],
	"switch": [0.14, 1080.0, 8.0, 0.25], "door": [1.0, 48.0, 2.8, 0.25],
	"win": [0.94, 440.0, 1.8, 0.0], "fail": [0.40, 148.0, 2.8, 0.09],
	"conveyor": [0.80, 50.0, 0.0, 0.038], "fan": [0.80, 65.0, 0.0, 0.29],
}
var clips: Dictionary = {}
var channels: Array[AudioStreamPlayer] = []
var ambiences: Dictionary = {}
var next_voice := 0
var quiet := false
var rng := RandomNumberGenerator.new()

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	quiet = DisplayServer.get_name() == "headless"
	if quiet:
		return
	rng.seed = 705661
	for cue in CUES:
		clips[cue] = _make_wave(cue, CUES[cue])
	for index in range(VOICES):
		channels.append(_player())
	for cue in ["fan", "conveyor"]:
		var player := _player()
		player.stream = clips[cue]
		player.volume_db = -26.0 if cue == "fan" else -25.0
		ambiences[cue] = player

func _player() -> AudioStreamPlayer:
	var player := AudioStreamPlayer.new()
	add_child(player)
	return player

func play(cue: String, strength: float) -> void:
	if quiet or not clips.has(cue):
		return
	var player := channels[next_voice]
	next_voice = (next_voice + 1) % VOICES
	player.stream = clips[cue]
	player.volume_db = linear_to_db(clampf(strength / 7.0, 0.12, 0.50)) if cue in ["steel", "wood", "rubber", "heavy"] else -9.0
	player.play()

func loop_devices(board: LabBoard, enabled: bool) -> void:
	if quiet:
		return
	var fan: AudioStreamPlayer = ambiences.fan
	var belt: AudioStreamPlayer = ambiences.conveyor
	_loop(fan, enabled and not board.mechanisms.fans.is_empty())
	_loop(belt, enabled and not board.mechanisms.belts.is_empty())

func _loop(player: AudioStreamPlayer, enabled: bool) -> void:
	if enabled and not player.playing:
		player.play()
	elif not enabled and player.playing:
		player.stop()

func _make_wave(cue: String, settings: Array) -> AudioStreamWAV:
	var count := roundi(settings[0] * RATE)
	var data := PackedByteArray()
	data.resize(count * 2)
	var angle := 0.0
	var wind := 0.0
	var looping := cue in ["fan", "conveyor"]
	for index in range(count):
		var t := float(index) / RATE
		var fraction: float = t / settings[0]
		var frequency: float = settings[1]
		var envelope: float = minf(1.0, t * 355.0) * exp(-fraction * settings[2])
		match cue:
			"spring": frequency += 166.0 * sin(fraction * 9.0) * exp(-fraction * 2.3)
			"rubber", "wood": frequency *= 1.0 - 0.43 * fraction
			"rope": frequency += 14.0 * sin(t * 26.0)
			"fail": frequency -= fraction * 53.0
			"win":
				var note := mini(floori(t / 0.163), 4)
				frequency = [523.25, 659.25, 783.99, 1046.50, 1318.51][note]
				envelope *= exp(-fmod(t, 0.163) * 7.0)
		angle += TAU * frequency / RATE
		var noise := rng.randf_range(-1.0, 1.0)
		wind = lerpf(wind, noise, 0.12)
		var overtone := 0.21 if cue == "steel" else 0.08
		var sample := (sin(angle) * 0.58 + sin(angle * 2.73) * overtone + noise * float(settings[3])) * envelope
		if looping:
			var blend := smoothstep(0.0, 0.028, t) * smoothstep(0.0, 0.028, settings[0] - t)
			sample = (sin(angle) * (0.18 if cue == "conveyor" else 0.06) + wind * float(settings[3]) * 2.2) * blend
		data.encode_s16(index * 2, roundi(clampf(sample, -1.0, 1.0) * 28000.0))
	var wave := AudioStreamWAV.new()
	wave.format = AudioStreamWAV.FORMAT_16_BITS
	wave.mix_rate = RATE
	wave.data = data
	if looping:
		wave.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wave.loop_end = count
	return wave
