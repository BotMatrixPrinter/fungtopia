// Small glowing mushrooms: the cap (UV.y 1) glows softly in the instance color, pulsing slowly;
// the pale stem (UV.y 0) glows faintly. INSTANCE_CUSTOM.x is the phase, .y the glow strength.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"

uniform vec3 stem_color : source_color = vec3(0.82, 0.86, 0.9);

varying vec2 custom;

void vertex() {
	custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float cap = step(0.5, UV.y);
	float pulse = 0.8 + 0.2 * sin(TIME * 0.9 + custom.x * 6.2831);
	vec3 base = mix(stem_color, COLOR.rgb, cap);
	ALBEDO = base * 0.6;
	EMISSION = base * mix(0.25, 1.4, cap) * custom.y * pulse;
}
