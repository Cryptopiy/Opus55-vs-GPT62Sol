class_name DriftScorer
extends RefCounted
## Drift scoring (R-14). A drift is active while the body slip angle is
## between 15 and 80 deg above 40 km/h. While drifting the pending score grows
## by speed(km/h)/10 x slip factor x combo per second; the combo (x1..x5)
## grows every 2 s of drifting. When no drift happened for 1 s the chain ends
## and the pending score is banked (if the chain drifted >= 1 s in total).
## Touching a wall loses the pending score.

enum Event { NONE, BANKED, LOST }

var total := 0.0               # banked score
var pending := 0.0             # current chain
var combo := 1
var drifting := false
var chain_active := false
var last_event := Event.NONE
var last_event_value := 0.0
var event_age := 99.0

var _drift_time := 0.0         # drifting time in this chain (drives the combo)
var _idle_time := 0.0          # time since the last drifting tick


func update(delta: float, speed_kmh: float, slip_deg: float) -> void:
	event_age += delta
	drifting = speed_kmh > Config.DRIFT_MIN_SPEED_KMH \
		and slip_deg >= Config.DRIFT_MIN_SLIP_DEG and slip_deg <= Config.DRIFT_MAX_SLIP_DEG
	if drifting:
		chain_active = true
		_idle_time = 0.0
		_drift_time += delta
		combo = mini(Config.DRIFT_COMBO_MAX, 1 + int(_drift_time / Config.DRIFT_COMBO_STEP_TIME))
		var factor := clampf(slip_deg / Config.DRIFT_SLIP_FACTOR_REF_DEG,
			Config.DRIFT_SLIP_FACTOR_MIN, Config.DRIFT_SLIP_FACTOR_MAX)
		pending += speed_kmh / Config.DRIFT_SPEED_DIVISOR * factor * float(combo) * delta
	elif chain_active:
		_idle_time += delta
		if _idle_time >= Config.DRIFT_BANK_DELAY:
			bank()


## Ends the chain and adds the pending score to the total (min 1 s drifting).
func bank() -> void:
	if chain_active and _drift_time >= Config.DRIFT_MIN_DURATION and pending > 0.0:
		total += pending
		_set_event(Event.BANKED, pending)
	_reset_chain()


## Called when the car hits a barrier: the pending chain is lost.
func on_wall_hit() -> void:
	if chain_active and pending > 0.0:
		_set_event(Event.LOST, pending)
	_reset_chain()


func _set_event(e: Event, value: float) -> void:
	last_event = e
	last_event_value = value
	event_age = 0.0


func _reset_chain() -> void:
	pending = 0.0
	combo = 1
	chain_active = false
	_drift_time = 0.0
	_idle_time = 0.0
