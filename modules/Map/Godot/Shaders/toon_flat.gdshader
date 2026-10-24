// Plain toon surface of one color (home walls, door, window frames).
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Map/Godot/Shaders/toon.gdshaderinc"

uniform vec3 albedo : source_color = vec3(1.0);
uniform float glow = 0.0;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * glow;
}
