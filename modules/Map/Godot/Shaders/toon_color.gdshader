// Plain toon surface colored by the instance (mushroom stems).
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Map/Godot/Shaders/toon.gdshaderinc"

void fragment() {
	// A little darker toward the ground.
	ALBEDO = COLOR.rgb * mix(0.82, 1.0, smoothstep(0.0, 0.4, UV.y));
}
