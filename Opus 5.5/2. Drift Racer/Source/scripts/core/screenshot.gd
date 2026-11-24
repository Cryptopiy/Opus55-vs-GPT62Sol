class_name Screenshot
extends RefCounted
## F12: saves the current frame as PNG to <project>/Screenshots/ with a
## timestamp in the file name (exported builds: next to the executable).


static func folder() -> String:
	if OS.has_feature("editor"):
		return ProjectSettings.globalize_path("res://" + Config.SCREENSHOT_DIR)
	return OS.get_executable_path().get_base_dir().path_join(Config.SCREENSHOT_DIR)


## Returns the saved path, or "" when no frame is available (headless).
static func capture(viewport: Viewport, tag: String = "") -> String:
	var tex := viewport.get_texture()
	if tex == null:
		return ""
	var img := tex.get_image()
	if img == null or img.is_empty():
		return ""
	var dir := folder()
	DirAccess.make_dir_recursive_absolute(dir)
	# Keeps the editor from importing the PNGs as project resources.
	var ignore := dir.path_join(".gdignore")
	if not FileAccess.file_exists(ignore):
		var f := FileAccess.open(ignore, FileAccess.WRITE)
		if f != null:
			f.close()
	var stamp := Time.get_datetime_string_from_system(false, true).replace(":", "-").replace(" ", "_")
	var suffix := "" if tag.is_empty() else "_" + tag
	var path := dir.path_join("screenshot_%s_%03d%s.png" % [stamp, Time.get_ticks_msec() % 1000, suffix])
	if img.save_png(path) != OK:
		return ""
	return path
