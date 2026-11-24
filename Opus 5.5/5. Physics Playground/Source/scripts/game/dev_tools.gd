extends Node
## Developer tests (F10 menu, also driven by the autotest):
## - P-02 determinism: run the level's reference solution 5 times (fresh world each run, fast-forward with the
##   same 1/120 s step) and compare the ball's final position (must agree within 1 cm).
## - P-28 stack test: three 1 m boxes stacked in a lab area must stay still for 10 s.

signal report(text: String)

const C := preload("res://scripts/core/config.gd")
const Common := preload("res://scripts/objects/obj_common.gd")
const ML := preload("res://scripts/core/mesh_lib.gd")
const Mats := preload("res://scripts/core/materials.gd")

const LAB_X := 200.0
const STACK_SETTLE := 0.5

var busy := false


func run_determinism(session: Node) -> Dictionary:
	busy = true
	var keep_index: int = session.time_scale_index
	session.save_results = false
	var positions: Array[Vector3] = []
	var lines := PackedStringArray()
	for i in C.DETERMINISM_RUNS:
		session.reset()
		session.load_solution()
		session.set_turbo(true)
		session.toggle_play()
		var res: Array = await session.run_finished
		var pos: Vector3 = res[2]
		positions.append(pos)
		lines.append("run %d: %s at %.3f s, ball (%.4f, %.4f)" % [i + 1, "success" if res[0] else "FAILED", res[1], pos.x, pos.y])
	session.set_turbo(false)
	session.set_time_scale_index(keep_index)
	session.reset()
	session.save_results = true
	var max_dev := 0.0
	for p in positions:
		max_dev = maxf(max_dev, p.distance_to(positions[0]))
	var ok := max_dev <= C.DETERMINISM_TOLERANCE
	var text := "Determinism %s (%s): max deviation %.6f m over %d runs (limit %.2f m)\n%s" % [
		"PASS" if ok else "FAIL", session.level["id"], max_dev, C.DETERMINISM_RUNS, C.DETERMINISM_TOLERANCE, "\n".join(lines)]
	print(text)
	report.emit(text)
	busy = false
	return {"ok": ok, "max_dev": max_dev, "positions": positions}


func run_stack_test(session: Node, seconds: float = 10.0) -> Dictionary:
	busy = true
	session.reset()
	session.hold_world_release = true
	session.visible = false
	session.editor.enabled = false
	var lab := Node3D.new()
	lab.name = "StackLab"
	session.get_parent().add_child(lab)
	session.swap_world()
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-50, -30, 0)
	light.shadow_enabled = true
	lab.add_child(light)
	var cam := Camera3D.new()
	cam.position = Vector3(LAB_X, 2.0, 9.0)
	cam.fov = C.CAM_FOV
	lab.add_child(cam)
	cam.current = true
	var floor_body := Common.static_body(Transform3D(Basis(), Vector3(LAB_X, -0.5, 0)), C.SCENERY_FRICTION)
	ML.add_box_shape(floor_body, Vector3(8, 1, C.OBJECT_DEPTH))
	ML.add_box(floor_body, Vector3(8, 1, C.SCENERY_DEPTH), Vector3.ZERO, Mats.rounded(C.COL_SCENERY_DARK, 0.8))
	lab.add_child(floor_body)
	var boxes: Array[RigidBody3D] = []
	var start: Array[Vector3] = []
	for i in 3:
		var b := Common.rigid_body(Transform3D(Basis(), Vector3(LAB_X, 0.5 + i * C.BOX_SIZE, 0)), C.BOX_S_MASS, C.BOX_FRICTION)
		ML.add_box_shape(b, Vector3(C.BOX_SIZE, C.BOX_SIZE, C.BOX_SIZE))
		ML.add_box(b, Vector3(C.BOX_SIZE, C.BOX_SIZE, C.BOX_SIZE), Vector3.ZERO, Mats.rounded(C.COL_BOX_S, 0.8))
		lab.add_child(b)
		boxes.append(b)
		start.append(b.global_position)
	report.emit("Stack test running (%.0f s)..." % seconds)
	PhysicsServer3D.set_active(true)
	var max_disp := 0.0
	var max_speed := 0.0
	var max_rot := 0.0
	var ticks := int(seconds * C.PHYS_HZ)
	for t in ticks:
		await get_tree().physics_frame
		for i in boxes.size():
			max_disp = maxf(max_disp, boxes[i].global_position.distance_to(start[i]))
			max_rot = maxf(max_rot, absf(boxes[i].rotation.z))
			if float(t) / C.PHYS_HZ > STACK_SETTLE:
				max_speed = maxf(max_speed, boxes[i].linear_velocity.length())
	PhysicsServer3D.set_active(false)
	var sleeping := 0
	for b in boxes:
		if b.sleeping:
			sleeping += 1
	lab.queue_free()
	session.get_parent().remove_child(lab)
	session.visible = true
	session.editor.enabled = true
	session.camera_rig.cam.current = true
	session.swap_world()
	session.hold_world_release = false
	var ok := max_disp < 0.01 and max_speed < 0.05 and max_rot < deg_to_rad(0.5)
	var text := "Stack test %s: 3 boxes for %.0f s, max displacement %.5f m, max speed after %.1f s %.5f m/s, max tilt %.4f deg, %d/3 asleep" % [
		"PASS" if ok else "FAIL", seconds, max_disp, STACK_SETTLE, max_speed, rad_to_deg(max_rot), sleeping]
	print(text)
	report.emit(text)
	busy = false
	return {"ok": ok, "max_disp": max_disp, "max_speed": max_speed}
