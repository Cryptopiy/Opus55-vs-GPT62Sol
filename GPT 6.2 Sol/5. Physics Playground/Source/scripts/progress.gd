class_name LabProgress
extends RefCounted

const SAVE := "user://save.json"
var scores: Dictionary = {}
var tutorial_seen := false

func restore() -> void:
	if not FileAccess.file_exists(SAVE):
		return
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(SAVE))
	if data is Dictionary:
		var parsed: Variant = data.get("stars", {})
		if parsed is Dictionary:
			scores = parsed
		tutorial_seen = data.get("tutorial_seen", false) == true

func best(index: int) -> int:
	return clampi(int(scores.get("L%d" % (index + 1), 0)), 0, 3)

func unlocked(index: int) -> bool:
	return index == 0 or best(index - 1) > 0

func accept(index: int, earned: int) -> void:
	if earned > best(index):
		scores["L%d" % (index + 1)] = earned
		save()

func save() -> void:
	var file := FileAccess.open(SAVE, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify({"stars": scores, "tutorial_seen": tutorial_seen}, "\t"))
		file.close()
