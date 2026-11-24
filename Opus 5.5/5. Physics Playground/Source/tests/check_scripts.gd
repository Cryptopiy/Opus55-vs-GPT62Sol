extends SceneTree
## Loads every script and shader of the project so parse errors (and warnings) show up in the log.

func _collect(dir: String, out: Array[String]) -> void:
	for f in DirAccess.get_files_at(dir):
		if f.ends_with(".gd") or f.ends_with(".gdshader"):
			out.append(dir.path_join(f))
	for d in DirAccess.get_directories_at(dir):
		if not d.begins_with("."):
			_collect(dir.path_join(d), out)

func _initialize() -> void:
	var files: Array[String] = []
	_collect("res://scripts", files)
	_collect("res://shaders", files)
	_collect("res://tests", files)
	var bad := 0
	for f in files:
		var r: Resource = load(f)
		if r == null:
			bad += 1
			print("LOAD FAILED: ", f)
		elif r is GDScript and not (r as GDScript).can_instantiate():
			bad += 1
			print("CANNOT INSTANTIATE: ", f)
	print("CHECKED %d files, %d failed" % [files.size(), bad])
	quit(0)
