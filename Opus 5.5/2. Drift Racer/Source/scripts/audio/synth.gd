class_name Synth
extends RefCounted
## Generates every sound effect in code (R-30, R-25): countdown beeps, impact
## thud, tyre squeal, wind and barrier-scrape loops. Results are 16-bit mono
## AudioStreamWAV resources cached after the first request. Loops are made
## seamless by cross-fading their tail into their head.

static var _cache := {}


static func clear_cache() -> void:
	_cache.clear()


static func beep(freq: float, duration: float) -> AudioStreamWAV:
	var key := "beep_%d_%d" % [int(freq), int(duration * 1000.0)]
	if _cache.has(key):
		var cached: AudioStreamWAV = _cache[key]
		return cached
	var rate := float(Config.SFX_MIX_RATE)
	var n := int(duration * rate)
	var s := PackedFloat32Array()
	s.resize(n)
	for i in n:
		var t := float(i) / rate
		var env := minf(t / 0.008, 1.0) * clampf((duration - t) / 0.06, 0.0, 1.0)
		s[i] = (sin(TAU * freq * t) * 0.8 + sin(TAU * freq * 2.0 * t) * 0.15) * env * 0.8
	var wav := _to_wav(s, false)
	_cache[key] = wav
	return wav


static func thud() -> AudioStreamWAV:
	if _cache.has("thud"):
		var cached: AudioStreamWAV = _cache["thud"]
		return cached
	var rate := float(Config.SFX_MIX_RATE)
	var n := int(0.5 * rate)
	var s := PackedFloat32Array()
	s.resize(n)
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	var phase := 0.0
	var low := 0.0
	for i in n:
		var t := float(i) / rate
		var f := lerpf(95.0, 42.0, minf(t / 0.25, 1.0))
		phase += TAU * f / rate
		low += 0.12 * (rng.randf_range(-1.0, 1.0) - low)
		var body := sin(phase) * exp(-t * 9.0)
		var crunch := low * exp(-t * 28.0) * 2.2
		s[i] = clampf((body * 0.9 + crunch) * minf(t / 0.003, 1.0), -1.0, 1.0) * 0.9
	var wav := _to_wav(s, false)
	_cache["thud"] = wav
	return wav


## Tyre squeal loop: two detuned tones with integer-Hz vibrato (periodic in
## exactly one second) plus a little band-limited hiss.
static func squeal() -> AudioStreamWAV:
	if _cache.has("squeal"):
		var cached: AudioStreamWAV = _cache["squeal"]
		return cached
	var rate := float(Config.SFX_MIX_RATE)
	var n := int(rate)
	var fade := int(rate * 0.1)
	var s := PackedFloat32Array()
	s.resize(n + fade)
	var rng := RandomNumberGenerator.new()
	rng.seed = 23
	var bp := BandPass.new(2600.0, 3.0, rate)
	for i in n + fade:
		var t := float(i) / rate
		var a := sin(TAU * 930.0 * t + 2.2 * sin(TAU * 7.0 * t))
		var b := sin(TAU * 1410.0 * t + 1.6 * sin(TAU * 5.0 * t + 1.0)) * 0.55
		var hiss := bp.process(rng.randf_range(-1.0, 1.0)) * 0.6
		var am := 0.8 + 0.2 * sin(TAU * 3.0 * t)
		s[i] = (a * 0.5 + b * 0.35 + hiss) * am * 0.55
	var wav := _to_wav(_loopable(s, n, fade), true)
	_cache["squeal"] = wav
	return wav


static func wind() -> AudioStreamWAV:
	if _cache.has("wind"):
		var cached: AudioStreamWAV = _cache["wind"]
		return cached
	var rate := float(Config.SFX_MIX_RATE)
	var n := int(rate * 2.0)
	var fade := int(rate * 0.3)
	var s := PackedFloat32Array()
	s.resize(n + fade)
	var rng := RandomNumberGenerator.new()
	rng.seed = 37
	var lp := 0.0
	var bp := BandPass.new(650.0, 0.7, rate)
	for i in n + fade:
		var t := float(i) / rate
		var white := rng.randf_range(-1.0, 1.0)
		lp += 0.05 * (white - lp)
		var gust := 0.75 + 0.25 * sin(TAU * 0.5 * t) * sin(TAU * 1.5 * t + 0.7)
		s[i] = (lp * 2.2 + bp.process(white) * 0.5) * gust * 0.7
	var wav := _to_wav(_loopable(s, n, fade), true)
	_cache["wind"] = wav
	return wav


## Metal-on-concrete grinding: resonant noise bands plus random crackles.
static func scrape() -> AudioStreamWAV:
	if _cache.has("scrape"):
		var cached: AudioStreamWAV = _cache["scrape"]
		return cached
	var rate := float(Config.SFX_MIX_RATE)
	var n := int(rate)
	var fade := int(rate * 0.12)
	var s := PackedFloat32Array()
	s.resize(n + fade)
	var rng := RandomNumberGenerator.new()
	rng.seed = 53
	var b1 := BandPass.new(1900.0, 6.0, rate)
	var b2 := BandPass.new(3300.0, 8.0, rate)
	var b3 := BandPass.new(620.0, 2.0, rate)
	var crackle := 0.0
	for i in n + fade:
		var t := float(i) / rate
		var white := rng.randf_range(-1.0, 1.0)
		if rng.randf() < 0.004:
			crackle = rng.randf_range(0.5, 1.0)
		crackle *= 0.93
		var grind := b1.process(white) * 0.8 + b2.process(white) * 0.6 + b3.process(white) * 0.7
		var am := 0.7 + 0.3 * sin(TAU * 11.0 * t) * sin(TAU * 4.0 * t)
		s[i] = clampf((grind * am + crackle * white * 1.4) * 0.6, -1.0, 1.0)
	var wav := _to_wav(_loopable(s, n, fade), true)
	_cache["scrape"] = wav
	return wav


## Blends the `fade` samples after `n` into the start so the loop is seamless.
static func _loopable(s: PackedFloat32Array, n: int, fade: int) -> PackedFloat32Array:
	var out := s.slice(0, n)
	for i in fade:
		var w := float(i) / float(fade)
		out[i] = s[i] * w + s[n + i] * (1.0 - w)
	return out


static func _to_wav(samples: PackedFloat32Array, loop: bool) -> AudioStreamWAV:
	var data := PackedByteArray()
	data.resize(samples.size() * 2)
	for i in samples.size():
		data.encode_s16(i * 2, clampi(int(samples[i] * 32767.0), -32768, 32767))
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = Config.SFX_MIX_RATE
	wav.stereo = false
	wav.data = data
	if loop:
		wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wav.loop_begin = 0
		wav.loop_end = samples.size()
	return wav


## Chamberlin state-variable band-pass filter (used for noise colouring).
class BandPass:
	extends RefCounted
	var f := 0.0
	var q := 1.0
	var low := 0.0
	var band := 0.0

	func _init(cutoff: float, resonance: float, rate: float) -> void:
		f = 2.0 * sin(PI * cutoff / rate)
		q = 1.0 / resonance

	func process(x: float) -> float:
		low += f * band
		var high := x - low - q * band
		band += f * high
		return band
