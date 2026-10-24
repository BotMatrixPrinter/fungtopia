// Ferns and grass tufts: two-sided, darker at the root, swaying gently in the breeze.
// UV.y runs from 0 at the root to 1 at the tip.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"

uniform float sway = 0.06;
uniform vec3 tip_tint : source_color = vec3(1.15, 1.12, 0.85);

void vertex() {
	vec3 origin = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = origin.x * 0.21 + origin.z * 0.17;
	float bend = UV.y * UV.y * sway;
	VERTEX.x += sin(TIME * 1.3 + phase) * bend;
	VERTEX.z += cos(TIME * 1.1 + phase * 1.3) * bend * 0.6;
}

void fragment() {
	ALBEDO = COLOR.rgb * mix(vec3(0.6), tip_tint, UV.y);
}
