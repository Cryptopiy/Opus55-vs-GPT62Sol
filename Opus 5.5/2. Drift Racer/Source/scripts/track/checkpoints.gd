class_name Checkpoints
extends Node3D
## 8 invisible checkpoint gates evenly spaced along the track (R-06). Gate 0
## is the start/finish line. Emits `crossed` when a car body enters a gate;
## the race manager enforces the order.

signal crossed(gate: int, body: Node3D)


func build(data: TrackData) -> void:
	name = "Checkpoints"
	var width := (RoadBuilder.wall_line() + 1.0) * 2.0
	for gate in data.checkpoint_indices.size():
		var i := data.checkpoint_indices[gate]
		var area := Area3D.new()
		area.name = "Gate%d" % gate
		area.collision_layer = Config.LAYER_CHECKPOINT
		area.collision_mask = Config.LAYER_CAR
		area.monitorable = false
		var shape := BoxShape3D.new()
		shape.size = Vector3(width, Config.CHECKPOINT_HEIGHT, Config.CHECKPOINT_DEPTH)
		var cs := CollisionShape3D.new()
		cs.shape = shape
		area.add_child(cs)
		var up := data.normals[i]
		area.transform = Transform3D(Basis.looking_at(data.tangents[i], up),
			data.points[i] + up * (Config.CHECKPOINT_HEIGHT * 0.5 - 1.0))
		area.body_entered.connect(_on_body_entered.bind(gate))
		add_child(area)


func _on_body_entered(body: Node3D, gate: int) -> void:
	crossed.emit(gate, body)
