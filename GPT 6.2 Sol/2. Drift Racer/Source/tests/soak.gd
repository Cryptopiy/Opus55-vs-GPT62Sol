extends SceneTree

const MAIN = preload("res://main.tscn")

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var game := MAIN.instantiate()
	root.add_child(game)
	game.ui.start_button.pressed.emit()
	game.race.player_autopilot = true
	for frame in 36000:
		await physics_frame
		if game.race.complete:
			print("SOAK: full results reached after %.2f s" % game.race.elapsed)
			game.restart_race()
			game.race.player_autopilot = true
	print("SOAK: 36000 physics ticks / 300 seconds of simulated play")
	quit()
