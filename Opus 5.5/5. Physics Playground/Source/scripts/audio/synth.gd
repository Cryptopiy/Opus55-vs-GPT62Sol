extends RefCounted
## Sound synthesis: small additive / subtractive recipes rendered sample by sample into float buffers,
## then converted to 16-bit AudioStreamWAV. Noise uses a fixed seed so sounds are identical every launch.


static func wav(samples: PackedFloat32Array, rate: int, loop: bool = false) -> AudioStreamWAV:
	var n := samples.size()
	var bytes := PackedByteArray()
	bytes.resize(n * 2)
	for i in n:
		bytes.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32000.0))
	var w := AudioStreamWAV.new()
	w.format = AudioStreamWAV.FORMAT_16_BITS
	w.mix_rate = rate
	w.stereo = false
	w.data = bytes
	if loop:
		w.loop_mode = AudioStreamWAV.LOOP_FORWARD
		w.loop_begin = 0
		w.loop_end = n
	return w


static func _buf(seconds: float, rate: int) -> PackedFloat32Array:
	var b := PackedFloat32Array()
	b.resize(int(seconds * rate))
	return b


static func _rng() -> RandomNumberGenerator:
	var g := RandomNumberGenerator.new()
	g.seed = 1234567
	return g


## Sum of exponentially decaying sine partials (bells, pings, knocks).
static func _partials(b: PackedFloat32Array, rate: int, freqs: Array, amps: Array, decays: Array, gain: float, offset: int = 0) -> void:
	for i in range(offset, b.size()):
		var t := float(i - offset) / rate
		var v := 0.0
		for k in freqs.size():
			v += float(amps[k]) * sin(TAU * float(freqs[k]) * t) * exp(-float(decays[k]) * t)
		b[i] += v * gain * minf(1.0, t * 2000.0)


static func click(rate: int) -> PackedFloat32Array:
	var b := _buf(0.06, rate)
	var g := _rng()
	for i in b.size():
		var t := float(i) / rate
		b[i] = 0.5 * sin(TAU * 1800.0 * t) * exp(-t * 90.0) + 0.35 * sin(TAU * 190.0 * t) * exp(-t * 45.0) + 0.25 * g.randf_range(-1, 1) * exp(-t * 320.0)
	return b


static func tick(rate: int) -> PackedFloat32Array:
	var b := _buf(0.03, rate)
	for i in b.size():
		var t := float(i) / rate
		b[i] = 0.45 * (sin(TAU * 3200.0 * t) + 0.5 * sin(TAU * 2100.0 * t)) * exp(-t * 260.0)
	return b


static func impact_steel(rate: int) -> PackedFloat32Array:
	var b := _buf(0.55, rate)
	_partials(b, rate, [1250.0, 2980.0, 4630.0, 6900.0], [1.0, 0.6, 0.4, 0.25], [6.0, 9.0, 13.0, 18.0], 0.42)
	var g := _rng()
	for i in mini(b.size(), int(0.006 * rate)):
		b[i] += 0.4 * g.randf_range(-1, 1) * (1.0 - float(i) / (0.006 * rate))
	return b


static func impact_rubber(rate: int) -> PackedFloat32Array:
	var b := _buf(0.28, rate)
	var phase := 0.0
	for i in b.size():
		var t := float(i) / rate
		var f := 105.0 + 170.0 * exp(-t * 22.0)
		phase += TAU * f / rate
		b[i] = 0.85 * sin(phase) * exp(-t * 16.0) * minf(1.0, t * 300.0)
	return b


static func impact_wood(rate: int) -> PackedFloat32Array:
	var b := _buf(0.2, rate)
	_partials(b, rate, [520.0, 1180.0, 2300.0], [1.0, 0.55, 0.3], [35.0, 50.0, 70.0], 0.55)
	var g := _rng()
	var lp := 0.0
	for i in b.size():
		var t := float(i) / rate
		lp += (g.randf_range(-1, 1) - lp) * 0.35
		b[i] += 0.5 * lp * exp(-t * 120.0)
	return b


## Stick-slip creak: an irregular pulse train exciting a resonant filter.
static func creak(rate: int) -> PackedFloat32Array:
	var b := _buf(0.6, rate)
	var g := _rng()
	var r := 0.995
	var w := TAU * 680.0 / rate
	var y1 := 0.0
	var y2 := 0.0
	var next_pulse := 0
	for i in b.size():
		var t := float(i) / rate
		var x := 0.0
		if i >= next_pulse:
			x = 1.0
			var hz := 95.0 + 40.0 * sin(t * 9.0) + g.randf_range(-12.0, 12.0)
			next_pulse = i + int(rate / hz)
		var y := 2.0 * r * cos(w) * y1 - r * r * y2 + x
		y2 = y1
		y1 = y
		var env := sin(PI * t / 0.6)
		b[i] = 0.06 * y * env
	return b


static func boing(rate: int) -> PackedFloat32Array:
	var b := _buf(0.5, rate)
	var phase := 0.0
	for i in b.size():
		var t := float(i) / rate
		var f := (170.0 + 280.0 * exp(-t * 6.0)) * (1.0 + 0.12 * sin(TAU * 17.0 * t) * exp(-t * 4.0))
		phase += TAU * f / rate
		b[i] = 0.7 * (sin(phase) + 0.25 * sin(phase * 2.0)) * exp(-t * 5.5) * minf(1.0, t * 400.0)
	return b


static func switch_click(rate: int) -> PackedFloat32Array:
	var b := _buf(0.16, rate)
	for k in 2:
		var off := int(k * 0.06 * rate)
		_partials(b, rate, [2500.0, 4100.0, 320.0], [0.6, 0.3, 0.5], [120.0, 160.0, 40.0], 0.6, off)
	return b


static func door_slide(rate: int) -> PackedFloat32Array:
	var b := _buf(1.2, rate)
	var g := _rng()
	var lp := 0.0
	var phase := 0.0
	for i in b.size():
		var t := float(i) / rate
		lp += (g.randf_range(-1, 1) - lp) * 0.08
		phase += TAU * (52.0 + 6.0 * t) / rate
		var env := smoothstep(0.0, 0.12, t) * (1.0 - smoothstep(0.95, 1.2, t))
		var ticks := exp(-fmod(t, 0.1) * 80.0) * 0.25
		b[i] = env * (0.9 * lp + 0.35 * sin(phase) + ticks * g.randf_range(-1, 1))
	return b


static func chime(rate: int) -> PackedFloat32Array:
	var b := _buf(1.6, rate)
	_partials(b, rate, [880.0, 1760.0, 2429.0, 4752.0], [1.0, 0.5, 0.35, 0.15], [2.2, 3.0, 4.0, 6.0], 0.32)
	_partials(b, rate, [1320.0, 2640.0, 3643.0], [1.0, 0.45, 0.3], [2.4, 3.2, 4.5], 0.26, int(0.12 * rate))
	return b


static func jingle(rate: int) -> PackedFloat32Array:
	var b := _buf(1.5, rate)
	var notes := [523.25, 659.25, 783.99, 1046.5]
	for k in notes.size():
		var f: float = notes[k]
		var start := int(k * 0.13 * rate)
		var decay := 7.0 if k < notes.size() - 1 else 2.6
		for i in range(start, b.size()):
			var t := float(i - start) / rate
			var v := sin(TAU * f * t) + 0.3 * sin(TAU * f * 3.0 * t) + 0.15 * sin(TAU * f * 2.0 * t)
			b[i] += 0.22 * v * exp(-decay * t) * minf(1.0, t * 300.0)
	return b


static func fail_buzz(rate: int) -> PackedFloat32Array:
	var b := _buf(0.75, rate)
	var lp := 0.0
	var p1 := 0.0
	var p2 := 0.0
	for i in b.size():
		var t := float(i) / rate
		var drop := 1.0 - 0.25 * smoothstep(0.35, 0.75, t)
		p1 += TAU * 110.0 * drop / rate
		p2 += TAU * 116.5 * drop / rate
		var sq := signf(sin(p1)) + signf(sin(p2))
		lp += (sq - lp) * 0.12
		b[i] = 0.28 * lp * exp(-t * 2.2) * minf(1.0, t * 200.0)
	return b


## 1 s loop: 100 Hz motor hum with harmonics (integer cycles per loop => seamless).
static func conveyor_hum(rate: int) -> PackedFloat32Array:
	var b := _buf(1.0, rate)
	for i in b.size():
		var t := float(i) / rate
		b[i] = 0.3 * sin(TAU * 100.0 * t) + 0.18 * sin(TAU * 200.0 * t) + 0.08 * sin(TAU * 300.0 * t) + 0.06 * sin(TAU * 50.0 * t) * sin(TAU * 4.0 * t)
	return b


## 2 s loop: low-passed noise pulsing at the blade rate; the tail is cross-faded into the head (seamless loop).
static func fan_whoosh(rate: int) -> PackedFloat32Array:
	var n := int(2.0 * rate)
	var fade := int(0.25 * rate)
	var raw := _buf(2.25, rate)
	var g := _rng()
	var lp := 0.0
	var lp2 := 0.0
	for i in raw.size():
		var t := float(i) / rate
		lp += (g.randf_range(-1, 1) - lp) * 0.09
		lp2 += (lp - lp2) * 0.25
		raw[i] = lp2 * (0.75 + 0.25 * sin(TAU * 12.0 * t)) * 1.6
	var b := _buf(2.0, rate)
	for i in n:
		b[i] = raw[i]
	for i in fade:
		var k := float(i) / fade
		b[i] = raw[i] * k + raw[n + i] * (1.0 - k)
	return b
