class_name RaceManager
extends Node
## Race flow (R-25, R-26): staggered grid, 3-2-1-GO countdown with cars locked,
## laps counted only when all 8 gates are crossed in order, lap / best / total
## timing, live positions from track progress, wrong-way detection, respawns
## at the last checkpoint and the finish order.

signal countdown_tick(value: int)            # 3, 2, 1, then 0 = GO
signal lap_completed(car: Car, lap: int, lap_time: float)
signal car_finished(car: Car, place: int)
signal player_finished

enum State { COUNTDOWN, RACING }


class Entry:
	extends RefCounted
	var car: Car
	var grid_slot := 0
	var gates_passed := 0
	var next_gate := 0
	var last_gate := 0
	var laps_done := 0
	var lap_start := 0.0
	var lap_times := PackedFloat32Array()
	var best_lap := 0.0
	var finished := false
	var finish_time := 0.0
	var finish_place := 0
	var progress := 0.0
	var position := 1
	var wrong_way_time := 0.0
	var drift := DriftScorer.new()


var state := State.COUNTDOWN
var race_time := 0.0
var countdown_left := 0.0
var cars: Array[Car] = []
var entries: Array[Entry] = []
var order: Array[Entry] = []                 # current standings, leader first
var player: Car
var track: TrackData
var finish_count := 0

var _last_tick := 0


func setup(track_data: TrackData, car_list: Array[Car], checkpoints: Checkpoints) -> void:
	name = "RaceManager"
	track = track_data
	cars = car_list
	var ai_slot := 0
	for car in cars:
		var e := Entry.new()
		e.car = car
		if car.is_player:
			player = car
			e.grid_slot = Config.PLAYER_GRID_SLOT
		else:
			if ai_slot == Config.PLAYER_GRID_SLOT:
				ai_slot += 1
			e.grid_slot = ai_slot
			ai_slot += 1
		entries.append(e)
		order.append(e)
		car.respawn_requested.connect(_on_respawn_requested)
		car.barrier_hit.connect(_on_barrier_hit.bind(e))
		car.place(grid_transform(e.grid_slot))
		car.controls_locked = true
	checkpoints.crossed.connect(_on_gate_crossed)
	state = State.COUNTDOWN
	countdown_left = float(Config.COUNTDOWN_FROM) * Config.COUNTDOWN_STEP
	_last_tick = Config.COUNTDOWN_FROM + 1


## R-25: 2x2 staggered grid behind the start line (slot 0 = pole).
func grid_transform(slot: int) -> Transform3D:
	var row := slot >> 1
	var col := slot & 1
	var back := Config.GRID_FIRST_ROW + float(row) * Config.GRID_ROW_SPACING + (Config.GRID_STAGGER if col == 1 else 0.0)
	var lateral := Config.GRID_COLUMN_OFFSET if col == 1 else -Config.GRID_COLUMN_OFFSET
	return track.spawn_transform(track.index_at_distance(-back), lateral, 0.0)


func _physics_process(delta: float) -> void:
	if state == State.COUNTDOWN:
		var tick := ceili(countdown_left / Config.COUNTDOWN_STEP)
		if tick < _last_tick and tick >= 1:
			_last_tick = tick
			countdown_tick.emit(tick)
		countdown_left -= delta
		if countdown_left <= 0.0:
			_go()
	else:
		race_time += delta
	for e in entries:
		e.progress = _compute_progress(e)
		_update_wrong_way(e, delta)
		if state == State.RACING and not e.finished:
			e.drift.update(delta, e.car.speed_kmh, e.car.body_slip_deg)
	_sort_order()


func _go() -> void:
	state = State.RACING
	race_time = 0.0
	for e in entries:
		e.car.controls_locked = false
		e.lap_start = 0.0
	countdown_tick.emit(0)


func _on_gate_crossed(gate: int, body: Node3D) -> void:
	if state != State.RACING or not (body is Car):
		return
	var e := entry_of(body as Car)
	if e == null or e.finished or gate != e.next_gate:
		return
	e.gates_passed += 1
	e.last_gate = gate
	e.next_gate = (gate + 1) % Config.CHECKPOINT_COUNT
	if gate == 0 and e.gates_passed > 1:
		var lap_time := race_time - e.lap_start
		e.lap_start = race_time
		e.lap_times.append(lap_time)
		if e.best_lap <= 0.0 or lap_time < e.best_lap:
			e.best_lap = lap_time
		e.laps_done += 1
		lap_completed.emit(e.car, e.laps_done, lap_time)
		if e.laps_done >= Config.LAP_COUNT:
			_finish(e)


func _finish(e: Entry) -> void:
	e.finished = true
	e.finish_time = race_time
	finish_count += 1
	e.finish_place = finish_count
	e.drift.bank()
	car_finished.emit(e.car, finish_count)
	if e.car == player:
		player_finished.emit()


## Distance raced so far: laps * length + distance from the last valid gate.
func _compute_progress(e: Entry) -> float:
	var car := e.car
	var idx := car.track_index
	if idx < 0:
		idx = track.find_closest_index_full(car.global_position)
	var s := track.distance_along(car.global_position, idx)
	var gate_s := track.distances[track.checkpoint_indices[e.last_gate]]
	var d := wrapf(s - gate_s, -track.length * 0.5, track.length * 0.5)
	if e.gates_passed == 0:
		return d
	var laps_at_gate := int(float(e.gates_passed - 1) / float(Config.CHECKPOINT_COUNT))
	return float(laps_at_gate) * track.length + gate_s + d


func _update_wrong_way(e: Entry, delta: float) -> void:
	var car := e.car
	if state != State.RACING or e.finished or car.track_index < 0:
		e.wrong_way_time = 0.0
		return
	var heading := -car.global_transform.basis.z
	if heading.dot(track.tangents[car.track_index]) < Config.WRONG_WAY_DOT:
		e.wrong_way_time += delta
	else:
		e.wrong_way_time = 0.0


## Insertion sort of 4 entries (allocation free): finished cars by finishing
## place first, then by progress.
func _sort_order() -> void:
	for i in range(1, order.size()):
		var e := order[i]
		var j := i - 1
		while j >= 0 and _is_ahead(e, order[j]):
			order[j + 1] = order[j]
			j -= 1
		order[j + 1] = e
	for i in order.size():
		order[i].position = i + 1


func _is_ahead(a: Entry, b: Entry) -> bool:
	if a.finished != b.finished:
		return a.finished
	if a.finished:
		return a.finish_place < b.finish_place
	return a.progress > b.progress


func _on_respawn_requested(car: Car) -> void:
	var e := entry_of(car)
	var xf := grid_transform(e.grid_slot)
	if e.gates_passed > 0:
		xf = _free_spawn(track.checkpoint_indices[e.last_gate], car)
	car.respawn(xf)
	e.wrong_way_time = 0.0


## Spawn at a checkpoint on a lateral slot that is clear of other cars.
func _free_spawn(index: int, car: Car) -> Transform3D:
	for back in [0.0, 8.0, 16.0]:
		var back_m: float = back
		var i := track.index_at_distance(track.distances[index] - back_m)
		for lat_variant in Config.SPAWN_LATERAL_SLOTS:
			var lat: float = lat_variant
			var xf := track.spawn_transform(i, lat, 0.0)
			if _is_clear(xf.origin, car):
				return xf
	return track.spawn_transform(index, 0.0, 0.0)


func _is_clear(p: Vector3, me: Car) -> bool:
	for c in cars:
		if c != me and c.global_position.distance_to(p) < Config.SPAWN_CLEARANCE:
			return false
	return true


func _on_barrier_hit(_impact: float, e: Entry) -> void:
	e.drift.on_wall_hit()


func entry_of(car: Car) -> Entry:
	if car.car_index < 0 or car.car_index >= entries.size():
		return null
	return entries[car.car_index]


func progress_of(car: Car) -> float:
	return entries[car.car_index].progress


func is_racing() -> bool:
	return state == State.RACING


func is_wrong_way(car: Car) -> bool:
	return entries[car.car_index].wrong_way_time > Config.WRONG_WAY_TIME


func current_lap(car: Car) -> int:
	return mini(entries[car.car_index].laps_done + 1, Config.LAP_COUNT)


func current_lap_time(car: Car) -> float:
	var e := entries[car.car_index]
	if state != State.RACING:
		return 0.0
	if e.finished:
		return e.lap_times[e.lap_times.size() - 1] if e.lap_times.size() > 0 else 0.0
	return race_time - e.lap_start


func total_time(car: Car) -> float:
	var e := entries[car.car_index]
	return e.finish_time if e.finished else race_time


func all_finished() -> bool:
	return finish_count >= cars.size()
