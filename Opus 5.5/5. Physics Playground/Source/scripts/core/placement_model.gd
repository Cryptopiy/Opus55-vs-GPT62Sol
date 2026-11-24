extends RefCounted
## The player's placements for one level + inventory counts + undo/redo (P-18: Ctrl+Z / Ctrl+Y, >= 50 ops).
## Operations: place, delete, modify (move / rotate / handle edits) and batch (load solution / clear).
## Records are deep-copied into every operation so undo always restores the exact previous state.

signal changed

const C := preload("res://scripts/core/config.gd")

var records: Array[Dictionary] = []
var inventory := {}            # type -> count allowed by the level
var order: Array[String] = []  # inventory slot order (hotkeys 1-9)

var _next_id := 1
var _undo: Array[Dictionary] = []
var _redo: Array[Dictionary] = []


func setup(level_inventory: Array) -> void:
	inventory.clear()
	order.clear()
	for entry: Array in level_inventory:
		inventory[entry[0]] = int(entry[1])
		order.append(String(entry[0]))
	records.clear()
	_undo.clear()
	_redo.clear()


func used(type: String) -> int:
	var n := 0
	for r in records:
		if r["type"] == type:
			n += 1
	return n


func remaining(type: String) -> int:
	return int(inventory.get(type, 0)) - used(type)


func count() -> int:
	return records.size()


func index_of(id: int) -> int:
	for i in records.size():
		if int(records[i]["id"]) == id:
			return i
	return -1


func get_record(id: int) -> Dictionary:
	var i := index_of(id)
	return records[i] if i >= 0 else {}


func place(rec: Dictionary) -> int:
	var r := rec.duplicate(true)
	r["id"] = _next_id
	_next_id += 1
	var op := {"kind": "place", "rec": r.duplicate(true), "index": records.size()}
	_apply(op, true)
	_push(op)
	return int(r["id"])


func delete(id: int) -> void:
	var i := index_of(id)
	if i < 0:
		return
	var op := {"kind": "delete", "rec": records[i].duplicate(true), "index": i}
	_apply(op, true)
	_push(op)


## Replaces record `id` with `new_rec` (move / rotate / fulcrum / rope length). No-op if nothing changed.
func modify(id: int, new_rec: Dictionary) -> void:
	var i := index_of(id)
	if i < 0:
		return
	var after := new_rec.duplicate(true)
	after["id"] = id
	if after == records[i]:
		return
	var op := {"kind": "modify", "before": records[i].duplicate(true), "after": after, "index": i}
	_apply(op, true)
	_push(op)


## Replaces everything at once (F9 reference solution, Restart). Undoable as one step.
func replace_all(new_records: Array) -> void:
	var ops: Array[Dictionary] = []
	for i in range(records.size() - 1, -1, -1):
		ops.append({"kind": "delete", "rec": records[i].duplicate(true), "index": i})
	for rec: Dictionary in new_records:
		var r := rec.duplicate(true)
		r["id"] = _next_id
		_next_id += 1
		ops.append({"kind": "place", "rec": r, "index": -1})  # -1 = append
	var batch := {"kind": "batch", "ops": ops}
	_apply(batch, true)
	_push(batch)


func can_undo() -> bool:
	return not _undo.is_empty()


func can_redo() -> bool:
	return not _redo.is_empty()


func undo() -> bool:
	if _undo.is_empty():
		return false
	var op: Dictionary = _undo.pop_back()
	_apply(op, false)
	_redo.append(op)
	return true


func redo() -> bool:
	if _redo.is_empty():
		return false
	var op: Dictionary = _redo.pop_back()
	_apply(op, true)
	_undo.append(op)
	return true


func undo_depth() -> int:
	return _undo.size()


func _push(op: Dictionary) -> void:
	_undo.append(op)
	if _undo.size() > C.UNDO_LIMIT:
		_undo.pop_front()
	_redo.clear()


func _apply(op: Dictionary, forward: bool) -> void:
	_apply_quiet(op, forward)
	changed.emit()


func _apply_quiet(op: Dictionary, forward: bool) -> void:
	match String(op["kind"]):
		"place":
			if forward:
				var idx: int = op["index"]
				records.insert(clampi(idx, 0, records.size()) if idx >= 0 else records.size(), op["rec"].duplicate(true))
			else:
				records.remove_at(index_of(int(op["rec"]["id"])))
		"delete":
			if forward:
				records.remove_at(index_of(int(op["rec"]["id"])))
			else:
				records.insert(clampi(int(op["index"]), 0, records.size()), op["rec"].duplicate(true))
		"modify":
			var i := index_of(int(op["after"]["id"]))
			records[i] = (op["after"] if forward else op["before"]).duplicate(true)
		"batch":
			var ops: Array = op["ops"]
			if forward:
				for sub: Dictionary in ops:
					_apply_quiet(sub, true)
			else:
				for k in range(ops.size() - 1, -1, -1):
					_apply_quiet(ops[k], false)


## Deep copy of the current state (tests compare snapshots after undo/redo sequences).
func snapshot() -> Array:
	return records.duplicate(true)
