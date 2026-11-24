class_name EngineSynth
extends RefCounted
## Real-time engine voice for an AudioStreamGenerator (R-30). Two single-cycle
## wavetables (off-load: soft, on-load: rich in odd harmonics) are cross-faded
## by throttle load, so the timbre changes with load. Pitch = firing frequency
## of a 6-cylinder engine (rpm / 60 * 3). Gear shifts add a short pitch / noise
## "blip". Samples are written in fixed chunks into a reused buffer (no
## per-frame allocation).

static var _table_on := PackedFloat32Array()
static var _table_off := PackedFloat32Array()
static var _noise := PackedFloat32Array()

var _buffer := PackedVector2Array()
var _phase := 0.0
var _sub_phase := 0.0
var _noise_pos := 0
var _noise_lp := 0.0
var _rpm := Config.RPM_IDLE
var _load := 0.0
var _blip := 0.0
var _gain := 1.0


func _init(gain: float) -> void:
	_gain = gain
	_buffer.resize(Config.ENGINE_CHUNK)
	if _table_on.is_empty():
		_build_tables()


static func clear_cache() -> void:
	_table_on = PackedFloat32Array()
	_table_off = PackedFloat32Array()
	_noise = PackedFloat32Array()


static func _build_tables() -> void:
	var size := Config.ENGINE_TABLE_SIZE
	_table_on.resize(size)
	_table_off.resize(size)
	var rng := RandomNumberGenerator.new()
	rng.seed = 77
	var phases := PackedFloat32Array()
	for h in Config.ENGINE_HARMONICS:
		phases.append(rng.randf() * TAU)
	var peak_on := 0.0
	var peak_off := 0.0
	for i in size:
		var x := TAU * float(i) / float(size)
		var on := 0.0
		var off := 0.0
		for h in Config.ENGINE_HARMONICS:
			var k := float(h + 1)
			var odd := 1.35 if (h % 2) == 0 else 1.0
			on += sin(x * k + phases[h]) * odd / pow(k, 0.75)
			off += sin(x * k + phases[h]) / pow(k, 1.7)
		_table_on[i] = on
		_table_off[i] = off
		peak_on = maxf(peak_on, absf(on))
		peak_off = maxf(peak_off, absf(off))
	for i in size:
		_table_on[i] /= peak_on
		_table_off[i] /= peak_off
	_noise.resize(8192)
	for i in _noise.size():
		_noise[i] = rng.randf_range(-1.0, 1.0)


## Fills every available chunk of `playback`. rpm / load / shift come from the
## car's drivetrain; values are ramped across each chunk to avoid zipper noise.
func fill(playback: AudioStreamGeneratorPlayback, rpm: float, engine_load: float, shift_event: int) -> void:
	if shift_event != 0:
		_blip = 1.0
	var chunk := Config.ENGINE_CHUNK
	var rate := Config.ENGINE_MIX_RATE
	var size := float(Config.ENGINE_TABLE_SIZE)
	var noise_n := _noise.size()
	var blip_decay := exp(-1.0 / (0.12 * rate))
	while playback.get_frames_available() >= chunk:
		var rpm0 := _rpm
		var load0 := _load
		_rpm = lerpf(_rpm, rpm, 0.35)
		_load = lerpf(_load, engine_load, 0.25)
		for i in chunk:
			var u := float(i) / float(chunk)
			var r := lerpf(rpm0, _rpm, u)
			var l := lerpf(load0, _load, u)
			var freq := r / 60.0 * float(Config.ENGINE_CYLINDERS) * 0.5 * (1.0 + 0.06 * _blip)
			_phase += freq / rate
			_phase -= floorf(_phase)
			_sub_phase += freq * 0.5 / rate
			_sub_phase -= floorf(_sub_phase)
			var fi := _phase * size
			var i0 := int(fi)
			var i1 := (i0 + 1) % Config.ENGINE_TABLE_SIZE
			var frac := fi - float(i0)
			var on := lerpf(_table_on[i0], _table_on[i1], frac)
			var off := lerpf(_table_off[i0], _table_off[i1], frac)
			var tone := lerpf(off, on, l)
			# Combustion noise pulses at the start of each firing cycle.
			_noise_pos = (_noise_pos + 1) % noise_n
			_noise_lp += 0.3 * (_noise[_noise_pos] - _noise_lp)
			var pulse := 1.0 if _phase < 0.3 else 0.35
			var noise := _noise_lp * (0.12 + 0.3 * l + 0.6 * _blip) * pulse
			var sub := sin(_sub_phase * TAU) * 0.3
			var rpm_n := (r - Config.RPM_IDLE) / (Config.RPM_MAX - Config.RPM_IDLE)
			var amp := (0.32 + 0.4 * l) * (0.65 + 0.35 * rpm_n) * _gain
			var v := clampf((tone * 0.7 + sub + noise) * amp, -1.0, 1.0)
			_buffer[i] = Vector2(v, v)
			_blip *= blip_decay
		playback.push_buffer(_buffer)
