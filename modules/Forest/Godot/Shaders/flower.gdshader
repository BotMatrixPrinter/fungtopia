// Little flowers: a green stem (UV.y < 1) under petals colored by the instance (UV.y = 1). Two-sided.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"

uniform vec3 stem_color : source_color = vec3(0.22, 0.62, 0.12);

void fragment() {
	ALBEDO = mix(stem_color, COLOR.rgb, step(0.99, UV.y));
}
