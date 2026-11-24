extends Node
## Autoload "Save" (P-21): progress in user://save.json, loaded at start, written after every change.
## Format: {"version": 1, "levels": {"L1": {"stars": 3, "best_time": 4.1}}, "tutorial_done": true}

const C := preload("res://scripts/core/config.gd")
const PATH := "user://save.json"

var data := {"version": 1, "levels": {}, "tutorial_done": false}
var path := PATH


func _ready() -> void:
	load_file()


## Tests redirect progress to their own file so the player's save is never touched.
func use_file(p: String) -> void:
	path = p
	data = {"version": 1, "levels": {}, "tutorial_done": false}
	load_file()


func load_file() -> void:
	if not FileAccess.file_exists(path):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if typeof(parsed) == TYPE_DICTIONARY and (parsed as Dictionary).has("levels"):
		data = parsed
	else:
		push_warning("save.json unreadable, starting fresh")


func write_file() -> void:
	var f := FileAccess.open(path, FileAccess.WRITE)
	if f == null:
		push_error("Cannot write " + path)
		return
	f.store_string(JSON.stringify(data, "  "))
	f.close()


func best_stars(level_id: String) -> int:
	var l: Dictionary = data["levels"].get(level_id, {})
	return int(l.get("stars", 0))


func best_time(level_id: String) -> float:
	var l: Dictionary = data["levels"].get(level_id, {})
	return float(l.get("best_time", -1.0))


func is_completed(level_id: String) -> bool:
	return best_stars(level_id) > 0


## Level N+1 unlocks after level N is completed (level 1 always open).
func is_unlocked(index: int) -> bool:
	return index == 0 or is_completed(String(C.LEVELS[index - 1]["id"]))


func record_result(level_id: String, stars: int, time: float) -> void:
	var levels: Dictionary = data["levels"]
	var l: Dictionary = levels.get(level_id, {})
	l["stars"] = maxi(int(l.get("stars", 0)), stars)
	var prev := float(l.get("best_time", -1.0))
	l["best_time"] = time if prev < 0.0 else minf(prev, time)
	levels[level_id] = l
	write_file()


func tutorial_done() -> bool:
	return bool(data.get("tutorial_done", false))


func set_tutorial_done() -> void:
	data["tutorial_done"] = true
	write_file()


func reset_progress() -> void:
	data = {"version": 1, "levels": {}, "tutorial_done": false}
	write_file()
