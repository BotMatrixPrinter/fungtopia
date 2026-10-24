// Dark ferns of the undergrowth: two-sided, darker at the root, swaying a little.
// UV.y runs from 0 at the root to 1 at the tip.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"
#include "res://modules/DeepForest/Godot/Shaders/gloom.gdshaderinc"

uniform float sway = 0.05;

varying vec3 world_pos;

void vertex() {
	vec3 origin = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = origin.x * 0.21 + origin.z * 0.17;
	float bend = UV.y * UV.y * sway;
	VERTEX.x += sin(TIME * 1.1 + phase) * bend;
	VERTEX.z += cos(TIME * 0.9 + phase * 1.3) * bend * 0.6;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	ALBEDO = COLOR.rgb * mix(0.55, 1.15, UV.y);
	AO = 1.0 - 0.5 * gloom_at(world_pos);
	AO_LIGHT_AFFECT = 0.0;
}
