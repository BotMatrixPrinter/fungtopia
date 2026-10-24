// Old giant stems, side stems, roots and shelves: toon surface colored by the instance, darker
// and mossier toward the foot, and less ambient light the deeper in the gloom.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"
#include "res://modules/DeepForest/Godot/Shaders/gloom.gdshaderinc"

uniform vec3 moss : source_color = vec3(0.2, 0.3, 0.2);
uniform float moss_height = 1.2;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float foot = 1.0 - smoothstep(0.0, moss_height, world_pos.y - (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).y);
	// Old bark: uneven grooves running up and around the stem (UV.x goes around it).
	vec3 bark = COLOR.rgb * (0.86 + 0.14 * sin(UV.x * 6.2831 * 9.0 + sin(UV.y * 7.0) * 1.5));
	ALBEDO = mix(bark, moss, foot * 0.55);
	AO = 1.0 - 0.55 * gloom_at(world_pos);
	AO_LIGHT_AFFECT = 0.0;
}
