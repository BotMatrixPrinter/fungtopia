// Little flowers: a green stem (UV.y < 1) under petals colored by the instance (UV.y = 1). Two-sided.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"

void fragment() {
	ALBEDO = mix(vec3(0.3, 0.55, 0.25), COLOR.rgb, step(0.99, UV.y));
}
