class_name TrackWorld
extends Node3D
## Builds the complete circuit world for one TrackData: environment, road,
## barriers, ground, landmarks, trees, hills and checkpoint gates. Scenery
## randomness is seeded from the track seed, so a seed always looks the same.

var track: TrackData
var barrier_body: StaticBody3D
var checkpoints: Checkpoints
var ground: GroundBuilder.Field
var tree_count := 0
var sun: DirectionalLight3D


func build(data: TrackData) -> void:
	name = "TrackWorld"
	track = data
	var rng := RandomNumberGenerator.new()
	rng.seed = data.derived_seed + 1
	sun = EnvironmentBuilder.build(self)
	RoadBuilder.build(data, self)
	barrier_body = BarrierBuilder.build(data, self)
	ground = GroundBuilder.build(data, self, rng)
	LandmarksBuilder.build_arch(data, self)
	LandmarksBuilder.build_grandstand(data, self, rng)
	tree_count = VegetationBuilder.build(data, ground, self, rng, LandmarksBuilder.keepouts(data))
	LandmarksBuilder.build_hills(data, self, rng, ground.outer_height)
	checkpoints = Checkpoints.new()
	add_child(checkpoints)
	checkpoints.build(data)
