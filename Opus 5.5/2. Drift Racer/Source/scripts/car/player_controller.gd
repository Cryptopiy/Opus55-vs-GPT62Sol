class_name PlayerController
extends CarController
## Keyboard (W/S/A/D/Space) and optional gamepad (left stick + triggers)
## input for the player's car (R-12). Steering smoothing happens in the car.


func update_controls(car: Car, _delta: float) -> void:
	car.throttle = Input.get_action_strength(InputSetup.THROTTLE)
	car.brake = Input.get_action_strength(InputSetup.BRAKE)
	car.steer_input = Input.get_axis(InputSetup.STEER_LEFT, InputSetup.STEER_RIGHT)
	car.handbrake = Input.is_action_pressed(InputSetup.HANDBRAKE)
