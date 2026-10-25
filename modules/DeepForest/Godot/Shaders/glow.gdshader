// Small glowing mushrooms: the cap (UV.y 1) glows in the instance color, pulsing slowly, with a
// soft glossy highlight; the cream stem (UV.y 0) glows faintly. INSTANCE_CUSTOM.x is the phase,
// .y the glow strength (DeepForestScenery.GlowStrength..twice it).
shader_type spatial;
render_mode specular_disabled;

#define TOON_GLOSS
#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"

uniform vec3 stem_color : source_color = vec3(0.96, 0.92, 0.8);

varying vec2 custom;

void vertex() {
	custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float cap = step(0.5, UV.y);
	float pulse = 0.8 + 0.2 * sin(TIME * 0.9 + custom.x * 6.2831);
	vec3 base = mix(stem_color, COLOR.rgb, cap);
	ALBEDO = base * 0.6;
	ROUGHNESS = 1.0 - cap * 0.9;
	EMISSION = base * mix(0.25, 1.4, cap) * custom.y * pulse;
}
