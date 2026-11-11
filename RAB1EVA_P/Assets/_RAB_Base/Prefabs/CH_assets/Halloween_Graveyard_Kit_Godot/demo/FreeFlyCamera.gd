extends Camera3D
## Demo fly camera: hold Right Mouse to look, WASD to move, Q/E down/up, Shift = fast.
@export var speed := 5.0
@export var fast_multiplier := 3.0
@export var mouse_sensitivity := 0.003
var _yaw := 0.0
var _pitch := 0.0

func _ready() -> void:
	_yaw = rotation.y
	_pitch = rotation.x

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if event.pressed else Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		_yaw -= event.relative.x * mouse_sensitivity
		_pitch = clamp(_pitch - event.relative.y * mouse_sensitivity, -1.5, 1.5)
		rotation = Vector3(_pitch, _yaw, 0.0)

func _process(delta: float) -> void:
	var dir := Vector3.ZERO
	if Input.is_key_pressed(KEY_W): dir -= transform.basis.z
	if Input.is_key_pressed(KEY_S): dir += transform.basis.z
	if Input.is_key_pressed(KEY_A): dir -= transform.basis.x
	if Input.is_key_pressed(KEY_D): dir += transform.basis.x
	if Input.is_key_pressed(KEY_E): dir += Vector3.UP
	if Input.is_key_pressed(KEY_Q): dir -= Vector3.UP
	var s := speed * (fast_multiplier if Input.is_key_pressed(KEY_SHIFT) else 1.0)
	if dir != Vector3.ZERO:
		global_position += dir.normalized() * s * delta
