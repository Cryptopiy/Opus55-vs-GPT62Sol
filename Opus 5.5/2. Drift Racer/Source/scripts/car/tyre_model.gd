class_name TyreModel
extends RefCounted
## Slip-based friction curve (R-10): normalised lateral grip rises to 1.0 at
## the peak slip angle (~8 deg) and falls to 70 % at 25 deg and beyond.

static var _peak_rad := deg_to_rad(Config.TYRE_PEAK_SLIP_DEG)
static var _end_rad := deg_to_rad(Config.TYRE_FALLOFF_END_DEG)


## slip_angle in radians (>= 0). Returns the grip fraction 0..1.
static func lateral_coefficient(slip_angle: float) -> float:
	if slip_angle <= _peak_rad:
		var x := slip_angle / _peak_rad
		# Parabola with zero slope at the peak: linear-ish at small angles.
		return x * (2.0 - x)
	var t := clampf((slip_angle - _peak_rad) / (_end_rad - _peak_rad), 0.0, 1.0)
	return lerpf(1.0, Config.TYRE_SLIDE_RATIO, t * t * (3.0 - 2.0 * t))


## Load sensitivity: friction coefficient drops slightly as load rises, but
## total grip (mu * Fz) still grows, so loaded wheels grip more (R-10).
static func load_factor(wheel_load: float) -> float:
	var ratio := wheel_load / Config.TYRE_NOMINAL_LOAD
	return clampf(1.0 - Config.TYRE_LOAD_SENSITIVITY * (ratio - 1.0), 0.75, 1.15)


## How far beyond the grip peak a slip angle is (0 at/below ~peak, 1 when
## fully sliding) - used to turn lateral velocity into a "sliding" amount.
static func slide_fraction(slip_angle: float) -> float:
	return smoothstep(_peak_rad * 0.9, _peak_rad * 1.8, slip_angle)
