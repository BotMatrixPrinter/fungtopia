// Plain toon surface colored by the instance (mushroom stems and their root flares), slimmed
// and leaning per instance (stem_bend.gdshaderinc).
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"
#include "res://modules/Forest/Godot/Shaders/stem_bend.gdshaderinc"

void vertex() {
	VERTEX = bend_stem(VERTEX, UV.y, INSTANCE_CUSTOM);
}

void fragment() {
	// A little darker toward the ground.
	ALBEDO = COLOR.rgb * mix(0.82, 1.0, smoothstep(0.0, 0.4, UV.y));
}
