extends Node
## Automated acceptance tests, started by main.gd with `-- --autotest=<mode>` (headless, --fixed-fps 120).
## Modes: full | solutions | det | editor | save | stack | lab | scale | ui | search=<file.json>
## Output lines start with "AUTOTEST" so they can be grepped from the log; the process quits when done.

const C := preload("res://scripts/core/config.gd")
const Registry := preload("res://scripts/objects/registry.gd")
const SimWorld := preload("res://scripts/game/sim_world.gd")
const Ball := preload("res://scripts/objects/ball.gd")
const Lever := preload("res://scripts/objects/obj_lever.gd")

const STUCK_SPEED := 0.03
const STUCK_TIME := 2.5
const TEST_SAVE := "user://save_autotest.json"

var main: Node
var _done := false
var _result: Array = []
var _fails := 0
var _closest := 1e9
var _vmax := Vector3.ZERO
var _max_time := 1e9
var _target := Vector2.INF


func _ready() -> void:
	main = get_parent()
	Save.use_file(TEST_SAVE)
	Save.reset_progress()
	_run.call_deferred()


func _arg(key: String, def: String) -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with(key + "="):
			return a.substr(key.length() + 1)
	return def


func _log(ok: bool, text: String) -> void:
	if not ok:
		_fails += 1
	print("AUTOTEST %s %s" % ["PASS" if ok else "FAIL", text])


func _run() -> void:
	var mode := _arg("--autotest", "full")
	var t0 := Time.get_ticks_msec()
	if mode.begins_with("search"):
		await _search(_arg("--file", ""), int(_arg("--level", "1")) - 1)
	if mode == "full" or mode == "solutions":
		for i in C.LEVELS.size():
			await _test_solution(i)
	if mode == "full" or mode == "det" or mode == "detall":
		for i in ([0, 1, 2, 3, 4] if mode == "detall" else [1, 4]):
			var s: Node = await _open(i)
			var r: Dictionary = await main.dev_tools.run_determinism(s)
			_log(r["ok"], "determinism %s max deviation %.6f m" % [C.LEVELS[i]["id"], r["max_dev"]])
	if mode == "full" or mode == "editor":
		await _test_editor()
	if mode == "full" or mode == "save":
		await _test_save()
	if mode == "full" or mode == "stack":
		var s: Node = await _open(0)
		var r: Dictionary = await main.dev_tools.run_stack_test(s, 10.0)
		_log(r["ok"], "stack of 3 boxes still for 10 s (max displacement %.5f m, max speed %.5f m/s)" % [r["max_disp"], r["max_speed"]])
	if mode == "full" or mode == "lab":
		await _test_rope()
		await _test_cloth()
		await _test_kill_plane()
	if mode == "full" or mode == "scale":
		await _test_time_scale()
	if mode == "full" or mode == "worlds":
		await _test_world_release()
	if mode == "full" or mode == "ui":
		await _test_ui_toggle()
	print("AUTOTEST DONE mode=%s failures=%d (%.1f s)" % [mode, _fails, (Time.get_ticks_msec() - t0) / 1000.0])
	DirAccess.remove_absolute(ProjectSettings.globalize_path(TEST_SAVE))
	main.quit_game(0 if _fails == 0 else 1)


func _open(index: int) -> Node:
	var s: Node = main.session
	if s == null or s.level_index != index:
		s = main.open_level(index)
		await get_tree().process_frame
	s.reset()
	return s


## Runs the given placements until success / failure / the ball is stuck. Returns [success, time, pos, reason].
func _simulate(s: Node, records: Array, turbo: bool = true) -> Array:
	s.reset()
	# like a player: stay in build mode until retired physics worlds have been released
	if s.has_retired_worlds():
		while s.has_retired_worlds():
			await get_tree().process_frame
		var t0 := Time.get_ticks_msec()
		while Time.get_ticks_msec() - t0 < 200:
			await get_tree().process_frame
	s.model.replace_all(records)
	s.set_turbo(turbo)
	_done = false
	var cb := func(ok: bool, t: float, pos: Vector3) -> void:
		_done = true
		_result = [ok, t, pos, "" if ok else String(s.sim.fail_reason)]
	s.run_finished.connect(cb)
	s.toggle_play()
	var still := 0.0
	_closest = 1e9
	_vmax = Vector3.ZERO
	var trace := _arg("--trace", "") != ""
	var next_trace := 0.0
	while not _done:
		await get_tree().physics_frame
		var sim: Node = s.sim
		if trace and sim and sim.sim_time() >= next_trace:
			next_trace += 0.1
			var bp: Vector3 = sim.ball_position()
			var bv: Vector3 = sim.ball.linear_velocity
			var extra := ""
			for b: RigidBody3D in sim.dynamic_bodies:
				if b != sim.ball:
					extra += " %s(%.2f,%.2f r%.0f)" % [sim.kind_of(b), b.global_position.x, b.global_position.y, rad_to_deg(b.rotation.z)]
			print("TRACE t=%.2f p=(%.2f,%.2f) v=(%.2f,%.2f)%s" % [sim.sim_time(), bp.x, bp.y, bv.x, bv.y, extra])
		if sim and sim.running and not sim.finished:
			var hole: Vector2 = _target if _target != Vector2.INF else s.level["hole"]
			var bpos: Vector3 = sim.ball_position()
			_closest = minf(_closest, Vector2(bpos.x, bpos.y).distance_to(hole))
			var v: float = sim.ball.linear_velocity.length()
			if absf(sim.ball.linear_velocity.x) > absf(_vmax.x) and sim.sim_time() < 4.0:
				_vmax = sim.ball.linear_velocity
			still = still + C.PHYS_DT if v < STUCK_SPEED else 0.0
			if still > STUCK_TIME and not sim.goal.contains_ball(sim.ball):
				_done = true
				_result = [false, sim.sim_time(), sim.ball_position(), "stuck"]
			elif sim.sim_time() > _max_time and not sim.goal.contains_ball(sim.ball):
				_done = true
				_result = [false, sim.sim_time(), sim.ball_position(), "maxtime"]
	s.run_finished.disconnect(cb)
	s.set_turbo(false)
	return _result


func _test_solution(i: int) -> void:
	var s: Node = await _open(i)
	var lvl: Dictionary = C.LEVELS[i]
	var recs: Array = s.solution
	if recs.is_empty():
		_log(false, "%s has no reference solution" % lvl["id"])
		return
	var valid := true
	for rec: Dictionary in recs:
		if s.model.remaining(String(rec["type"])) <= 0:
			valid = false
	# every object of the solution must be placeable by a player (inside bounds, no overlaps, inventory)
	s.model.replace_all([])
	for rec: Dictionary in recs:
		var reason: String = s.editor.validity_reason(rec, -1)
		if reason != "" or s.model.remaining(String(rec["type"])) <= 0:
			valid = false
			print("AUTOTEST INFO %s %s invalid: %s" % [lvl["id"], rec["type"], reason])
		s.model.place(rec)
	var r: Array = await _simulate(s, recs)
	var stars: int = s.stars_for(recs.size(), r[1]) if r[0] else 0
	_log(r[0] and valid, "solution %s: %s in %.2f s, %d objects (par %d, par_time %.1f) -> %d stars, placement valid=%s, ball (%.3f, %.3f) %s" % [
		lvl["id"], "COMPLETED" if r[0] else "not completed", r[1], recs.size(), lvl["par"], lvl["par_time"], stars, valid, r[2].x, r[2].y, r[3]])


func _search(path: String, index: int) -> void:
	_max_time = float(_arg("--maxtime", "25"))
	var tgt := _arg("--target", "")
	if tgt != "":
		_target = Vector2(float(tgt.get_slice(",", 0)), float(tgt.get_slice(",", 1)))
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	var s: Node = await _open(index)
	var n := 0
	for cand: Dictionary in data["candidates"]:
		var recs: Array = []
		for o: Dictionary in cand["objects"]:
			var rec := Registry.new_record(String(o["type"]), Vector2(float(o["x"]), float(o["y"])), float(o.get("rot", 0.0)))
			for key: String in ["length", "fulcrum"]:
				if o.has(key):
					rec[key] = float(o[key])
			Registry.module(String(rec["type"])).constrain(rec)
			recs.append(rec)
		var ok := true
		var why := ""
		s.model.replace_all([])
		for rec: Dictionary in recs:
			var reason: String = s.editor.validity_reason(rec, -1)
			if reason != "":
				ok = false
				why += "[%s: %s]" % [rec["type"], reason.replace(" ", "_")]
			s.model.place(rec)
		var r: Array = await _simulate(s, recs, not OS.get_cmdline_user_args().has("--noturbo"))
		n += 1
		print("AUTOTEST SEARCH %s valid=%s %s%s t=%.2f closest=%.2f ball=(%.2f,%.2f) %s vmax=(%.2f,%.2f) :: %s" % ["HIT" if r[0] else "miss", ok, cand.get("tag", ""), why, r[1], _closest, r[2].x, r[2].y, r[3], _vmax.x, _vmax.y, JSON.stringify(cand["objects"])])
	print("AUTOTEST SEARCH finished %d candidates" % n)


func _test_editor() -> void:
	var s: Node = await _open(2)
	var ed: Node = s.editor
	var m: RefCounted = s.model
	m.replace_all([])
	var snaps: Array = []
	snaps.append(m.snapshot())
	# 10 operations: place x4, rotate x2, move x2, delete x1, Alt-drag fulcrum x1
	var ops_ok := true
	ops_ok = ed.try_place(Registry.new_record("plank", Vector2(-4, 6), 0)) and ops_ok
	snaps.append(m.snapshot())
	ops_ok = ed.try_place(Registry.new_record("box_h", Vector2(-2, 4), 0)) and ops_ok
	snaps.append(m.snapshot())
	ops_ok = ed.try_place(Registry.new_record("box_s", Vector2(2, 4), 0)) and ops_ok
	snaps.append(m.snapshot())
	ops_ok = ed.try_place(Registry.new_record("lever", Vector2(-6, 1.5), 0)) and ops_ok
	snaps.append(m.snapshot())
	var ids: Array[int] = []
	for r: Dictionary in m.records:
		ids.append(int(r["id"]))
	ed.select(ids[0])
	ed.rotate_by(C.SNAP_ROT)
	snaps.append(m.snapshot())
	ed.rotate_by(-C.FINE_ROT)
	snaps.append(m.snapshot())
	var moved := (m.get_record(ids[1]) as Dictionary).duplicate(true)
	moved["x"] = 0.0
	m.modify(ids[1], moved)
	snaps.append(m.snapshot())
	var moved2 := (m.get_record(ids[2]) as Dictionary).duplicate(true)
	moved2["y"] = 6.0
	m.modify(ids[2], moved2)
	snaps.append(m.snapshot())
	var lever_rec: Dictionary = m.get_record(ids[3])
	m.modify(ids[3], Lever.drag_fulcrum(lever_rec, Vector2(-7.0, 1.5)))
	snaps.append(m.snapshot())
	ed.select(ids[2])
	ed.delete_selected()
	snaps.append(m.snapshot())
	var undo_ok := true
	for k in 10:
		m.undo()
		if m.snapshot() != snaps[snaps.size() - 2 - k]:
			undo_ok = false
	var redo_ok := true
	for k in 10:
		m.redo()
		if m.snapshot() != snaps[k + 1]:
			redo_ok = false
	await get_tree().process_frame
	var visuals_ok: bool = ed._visuals.size() == m.records.size()
	var fulcrum_ok: bool = float(m.get_record(ids[3])["fulcrum"]) == -1.0
	_log(ops_ok and undo_ok and redo_ok and visuals_ok and fulcrum_ok, "editor: 10 ops (place/rotate/move/fulcrum/delete), undo x10 and redo x10 consistent (ops=%s undo=%s redo=%s visuals=%s fulcrum=%s, %d records)" % [ops_ok, undo_ok, redo_ok, visuals_ok, fulcrum_ok, m.records.size()])
	# depth: 60 operations stay undoable (>= 50 required)
	m.replace_all([])
	for k in 60:
		var r: Dictionary = m.records[0] if m.count() > 0 else {}
		if r.is_empty():
			m.place(Registry.new_record("plank", Vector2(-4, 6), 0))
		else:
			var r2 := r.duplicate(true)
			r2["x"] = -4.0 + 0.25 * float(k % 4)
			r2["y"] = 6.25 if k % 2 == 0 else 6.0
			m.modify(int(r["id"]), r2)
	var depth: int = m.undo_depth()
	var n_undo := 0
	while m.undo():
		n_undo += 1
	_log(depth >= 50 and n_undo >= 50, "undo stack depth %d (>= 50), undid %d operations" % [depth, n_undo])
	# ghost validity: overlapping scenery / forbidden zone / another object is rejected
	m.replace_all([])
	var bad_scenery: String = ed.validity_reason(Registry.new_record("plank", Vector2(0, -0.5), 0), -1)
	var bad_forbidden: String = ed.validity_reason(Registry.new_record("plank", Vector2(6, 5), 0), -1)
	ed.try_place(Registry.new_record("plank", Vector2(-4, 6), 0))
	var bad_other: String = ed.validity_reason(Registry.new_record("box_s", Vector2(-4, 6), 0), -1)
	var good: String = ed.validity_reason(Registry.new_record("box_s", Vector2(-4, 8), 0), -1)
	_log(bad_scenery != "" and bad_forbidden != "" and bad_other != "" and good == "", "placement validity: scenery='%s' forbidden='%s' object='%s' free='%s'" % [bad_scenery, bad_forbidden, bad_other, good])
	m.replace_all([])


func _test_save() -> void:
	var s: Node = await _open(0)
	var r: Array = await _simulate(s, s.solution)
	await get_tree().process_frame
	var stars := Save.best_stars("L1")
	Save.use_file(TEST_SAVE)   # reload from disk = what a restarted game sees
	var reloaded := Save.best_stars("L1")
	_log(r[0] and r[1] < 6.0 and s.solution.size() <= 2 and stars == 3 and reloaded == 3 and Save.is_unlocked(1),
		"L1 with %d objects in %.2f s -> %d stars saved; after reload %d stars, L2 unlocked=%s" % [s.solution.size(), r[1], stars, reloaded, Save.is_unlocked(1)])


func _lab_begin(s: Node) -> SimWorld:
	s.reset()
	s.hold_world_release = true
	s.visible = false
	s.swap_world()
	var lab := SimWorld.new()
	lab.name = "Lab"
	main.add_child(lab)
	return lab


func _lab_end(s: Node, lab: SimWorld) -> void:
	PhysicsServer3D.set_active(false)
	main.remove_child(lab)
	lab.queue_free()
	s.visible = true
	s.swap_world()
	s.hold_world_release = false


func _test_rope() -> void:
	var s: Node = await _open(1)
	for angle: float in [-90.0, 0.0]:
		var lab := _lab_begin(s)
		var rec := Registry.new_record("rope", Vector2(200, 13), angle)
		rec["length"] = C.ROPE_MAX
		Registry.module("rope").build_sim(rec, lab)
		PhysicsServer3D.set_active(true)
		var max_speed_late := 0.0
		for t in 8 * C.PHYS_HZ:
			await get_tree().physics_frame
			for tk in lab._tickers:
				tk.sim_tick(lab)
			if angle == 0.0 and t > 3 * C.PHYS_HZ:
				for b in lab.dynamic_bodies:
					max_speed_late = maxf(max_speed_late, b.linear_velocity.length())
		var stretch: float = lab.rope_stretch()
		_lab_end(s, lab)
		if angle == 0.0:
			_log(stretch < 0.05 and max_speed_late < 0.02, "rope 6 m hanging: max stretch %.2f%%, bob speed after 3 s %.4f m/s (no jitter)" % [stretch * 100.0, max_speed_late])
		else:
			_log(stretch < 0.05, "rope 6 m released horizontally: max stretch %.2f%% over 8 s (limit 5%%)" % (stretch * 100.0))


func _test_cloth() -> void:
	var s: Node = await _open(3)
	for drop: float in [1.0, 3.0]:
		var lab := _lab_begin(s)
		var rec := Registry.new_record("cloth", Vector2(200, 5), 0)
		Registry.module("cloth").build_sim(rec, lab)
		var ball := Ball.build_sim("steel", Vector2(200, 5 + drop), lab)
		PhysicsServer3D.set_active(true)
		var peak_after := -1.0
		var hit := false
		var finite := true
		for t in 8 * C.PHYS_HZ:
			await get_tree().physics_frame
			if ball.linear_velocity.y > 0.5:
				hit = true
			if hit:
				peak_after = maxf(peak_after, ball.global_position.y)
			finite = finite and ball.global_position.is_finite()
		var sb: SoftBody3D = lab.cloths[0]
		var lowest := 99.0
		for k in C.CLOTH_VERTS * C.CLOTH_VERTS:
			lowest = minf(lowest, PhysicsServer3D.soft_body_get_point_global_position(sb.get_physics_rid(), k).y)
		var ball_y := ball.global_position.y
		var speed := ball.linear_velocity.length()
		_lab_end(s, lab)
		var sag := 5.0 - lowest
		if drop == 1.0:
			_log(finite and sag > 0.3 and sag < 0.5 and speed < 0.2, "cloth: 1 kg ball resting sags cloth %.3f m (ball bottom %.3f m below anchors), ball speed %.3f m/s, stable=%s" % [sag, 5.0 - (ball_y - 0.25), speed, finite])
		else:
			_log(finite and peak_after > 5.5, "cloth sling: ball dropped from 3 m rebounds to %.2f m above the anchors" % (peak_after - 5.0))


func _test_kill_plane() -> void:
	var s: Node = await _open(0)
	s.reset()
	s.model.replace_all([])
	_done = false
	var cb := func(ok: bool, _t: float, _p: Vector3) -> void:
		_done = true
		_result = [ok, s.sim.fail_reason]
	s.run_finished.connect(cb)
	s.toggle_play()
	await get_tree().physics_frame
	await get_tree().physics_frame
	s.sim.ball.global_position = Vector3(0, -16, 0)
	while not _done:
		await get_tree().physics_frame
	s.run_finished.disconnect(cb)
	_log(not _result[0] and _result[1] == "Ball lost", "kill plane y=-15 -> failure '%s'" % _result[1])
	_done = false
	s.run_finished.connect(cb)
	s.reset()
	s.set_turbo(true)
	s.toggle_play()
	while not _done:
		await get_tree().physics_frame
	s.run_finished.disconnect(cb)
	s.set_turbo(false)
	_log(not _result[0] and _result[1] == "Too slow" and absf(s.sim.sim_time() - C.SIM_TIMEOUT) < 0.02, "timeout -> failure '%s' at %.2f s" % [_result[1], s.sim.sim_time()])
	s.reset()


## Key press delivered through the root viewport, the same path real key events take (works headless too).
func _press(code: Key, ctrl: bool = false) -> void:
	for pressed: bool in [true, false]:
		var e := InputEventKey.new()
		e.keycode = code
		e.physical_keycode = code
		e.ctrl_pressed = ctrl
		e.pressed = pressed
		get_tree().root.push_input(e)
	await get_tree().process_frame


func _move_mouse(pos: Vector2) -> void:
	var e := InputEventMouseMotion.new()
	e.position = pos
	e.global_position = pos
	get_tree().root.push_input(e)


## Moves the mouse onto a control, waits past the tooltip delay and returns how many popup windows are shown.
func _hover_tooltips(c: Control) -> int:
	_move_mouse(c.get_global_rect().get_center() + Vector2(1, 0))
	_move_mouse(c.get_global_rect().get_center())
	var delay: float = ProjectSettings.get_setting("gui/timers/tooltip_delay_sec", 0.5)
	await get_tree().create_timer(delay + 0.3, true, false, true).timeout
	return _visible_windows(get_tree().root)


static func _visible_windows(n: Node) -> int:
	var count := 0
	for ch in n.get_children(true):
		if ch is Window and (ch as Window).visible:
			count += 1
		count += _visible_windows(ch)
	return count


static func _has_named(list: Array, node_name: String) -> bool:
	for c: Control in list:
		if c.name == node_name:
			return true
	return false


## U-01..U-06: F8 hides every interface element, hotkeys keep working while hidden, the state survives restarts,
## level changes and newly created UI, the screens needed to continue stay visible, and F8 brings everything back.
func _test_ui_toggle() -> void:
	var vis: Node = main.ui_visibility
	Save.reset_progress()  # fresh progress -> level 1 shows its tutorial text as well
	var s: Node = main.open_level(0)
	await get_tree().process_frame
	await get_tree().process_frame
	await _press(KEY_F1)
	await _press(KEY_F10)
	s.hud.flash("notification")
	await _press(KEY_1)
	var before: Array = vis.drawn_controls()
	var parts_ok: bool = before.has(s.hud) and before.has(main.get(&"_debug")) and before.has(main.get(&"_dev_menu")) 		and _has_named(before, "Tutorial") and _has_named(before, "Inventory")
	var tool_before: String = s.editor.tool
	await _press(KEY_F8)
	var hidden_count: int = vis.drawn_controls().size()
	var same_state: bool = s.state == 0 and s.editor.tool == tool_before
	await _press(KEY_F8)
	var after: Array = vis.drawn_controls()
	var returned := after.size() == before.size()
	for c: Control in before:
		returned = returned and after.has(c)
	_log(parts_ok and hidden_count == 0 and same_state and returned, "U-01/U-02/U-06 F8 hides all %d drawn UI controls (HUD, inventory, banner, tutorial, F1 overlay, F10 menu) -> %d drawn while hidden; F8 again -> %d back (all returned=%s, state unchanged=%s)" % [before.size(), hidden_count, after.size(), returned, same_state])
	# U-02 tooltips: hover the Play button until its tooltip pops up; F8 must remove it, and none may appear while hidden
	var play_btn: Control = s.hud.get(&"_play_btn")
	var tip_shown := await _hover_tooltips(play_btn)
	await _press(KEY_F8)
	var tip_hidden := _visible_windows(get_tree().root)
	tip_hidden += await _hover_tooltips(play_btn)
	await _press(KEY_F8)
	_move_mouse(get_viewport().get_visible_rect().get_center())
	_log(tip_shown == 1 and tip_hidden == 0, "U-02 tooltip: hovering Play shows its tooltip (%d window) -> F8 removes it, and hovering while hidden shows none (%d)" % [tip_shown, tip_hidden])
	# U-03: every hotkey still works while hidden
	await _press(KEY_F8)
	var ok := true
	await _press(KEY_1)
	ok = ok and s.editor.tool == ""
	await _press(KEY_1)
	ok = ok and s.editor.tool == "plank"
	await _press(KEY_Q)
	ok = ok and is_equal_approx(float(s.editor.tool_rot), C.SNAP_ROT)
	await _press(KEY_1)
	await _press(KEY_F9)
	ok = ok and s.model.count() == s.solution.size()
	await _press(KEY_Z, true)
	ok = ok and s.model.count() == 0
	await _press(KEY_Y, true)
	ok = ok and s.model.count() == s.solution.size()
	await _press(KEY_SPACE)
	for k in 30:
		await get_tree().physics_frame
	ok = ok and s.state == 1 and s.sim.sim_time() > 0.1
	await _press(KEY_SPACE)
	ok = ok and s.state == 2
	await _press(KEY_BRACKETRIGHT)
	ok = ok and s.time_scale_index == 2
	await _press(KEY_BRACKETLEFT)
	ok = ok and s.time_scale_index == 1
	await _press(KEY_F)
	await _press(KEY_R)
	ok = ok and s.state == 0
	await _press(KEY_ESCAPE)
	var pause_drawn: bool = main.get(&"_pause_menu") != null and (main.get(&"_pause_menu") as Control).is_visible_in_tree()
	await _press(KEY_ESCAPE)
	ok = ok and pause_drawn and main.get(&"_pause_menu") == null
	_log(ok and vis.drawn_controls().is_empty(), "U-03 hotkeys work while hidden (1, Q, F9, Ctrl+Z/Y, Space, [ ], F, R, Esc) and the pause menu still shows (%s); UI stays hidden" % pause_drawn)
	# U-04: restart, level change and UI created while hidden stay hidden; results screen is not affected
	s.restart_level()
	s.hud.flash("created while hidden")
	await _press(KEY_F1)
	await _press(KEY_F1)
	var s2: Node = main.open_level(1)
	await get_tree().process_frame
	var new_hidden: bool = vis.drawn_controls().is_empty() and is_instance_valid(s2.hud) and not s2.hud.is_visible_in_tree()
	var r: Array = await _simulate(s2, s2.solution)
	var waited := 0
	while not is_instance_valid(s2.hud.get(&"_modal")) and waited < 400:
		await get_tree().process_frame
		waited += 1
	var results: Control = s2.hud.get(&"_modal")
	var results_drawn: bool = is_instance_valid(results) and results.is_visible_in_tree()
	_log(new_hidden and r[0] and results_drawn and vis.drawn_controls().is_empty(), "U-04 restart + level change + new banner/overlay while hidden: 0 UI drawn; level-2 results screen still shown (%s)" % results_drawn)
	await _press(KEY_F8)
	var back: Array = vis.drawn_controls()
	_log(vis.ui_visible and back.has(s2.hud) and _has_named(back, "Inventory") and back.has(main.get(&"_debug")) and back.has(main.get(&"_dev_menu")), "U-06 F8 again in level 2: HUD, inventory, F1 overlay and F10 menu drawn again (%d controls)" % back.size())
	await _press(KEY_F1)
	await _press(KEY_F10)
	s2.reset()


## Every Play swaps in a fresh World3D; the old worlds (and their physics spaces) must be released, otherwise
## each leaked space would keep stepping every tick.
func _test_world_release() -> void:
	var s: Node = await _open(0)
	var refs: Array[WeakRef] = []
	for k in 8:
		refs.append(weakref(get_viewport().world_3d))
		await _simulate(s, s.solution, not OS.get_cmdline_user_args().has("--noturbo"))
		s.reset()
	while s.has_retired_worlds():
		await get_tree().process_frame
	for k in 3:
		await get_tree().process_frame
	var alive := 0
	for r in refs:
		if r.get_ref() != null:
			alive += 1
	_log(alive == 0, "world swap: %d of %d replaced World3D objects still alive after 8 plays and a calm build phase (must be 0)" % [alive, refs.size()])


func _test_time_scale() -> void:
	# L1 (rigid bodies only) and L4 (soft-body hammock) must give identical runs at x1, x2 and x0.25
	for index in [0, 3]:
		var s: Node = await _open(index)
		var pos: Array[Vector3] = []
		var times: Array[float] = []
		for idx in [1, 2, 0]:
			s.set_time_scale_index(idx)
			var r: Array = await _simulate(s, s.solution, false)
			pos.append(r[2])
			times.append(r[1])
		s.set_time_scale_index(1)
		var dev := maxf(pos[1].distance_to(pos[0]), pos[2].distance_to(pos[0]))
		_log(dev < 0.0001 and absf(times[1] - times[0]) < 0.0001 and absf(times[2] - times[0]) < 0.0001,
			"%s time scale x1 / x2 / x0.25 give identical results (max deviation %.6f m, times %.3f / %.3f / %.3f s)" % [C.LEVELS[index]["id"], dev, times[0], times[1], times[2]])
