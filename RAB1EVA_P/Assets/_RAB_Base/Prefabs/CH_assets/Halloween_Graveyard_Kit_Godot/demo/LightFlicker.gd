extends OmniLight3D
## Gentle candle / pumpkin flicker. Attach to any OmniLight3D.
@export var flicker := 0.3
@export var speed := 8.0
var _base := 1.0
var _t := 0.0
var _off := 0.0

func _ready() -> void:
	_base = light_energy
	_off = randf() * 100.0

func _process(delta: float) -> void:
	_t += delta * speed
	var n := sin(_t + _off) * 0.5 + sin(_t * 2.31 + _off * 1.7) * 0.3 + sin(_t * 5.13) * 0.2
	light_energy = _base * (1.0 + flicker * n)
