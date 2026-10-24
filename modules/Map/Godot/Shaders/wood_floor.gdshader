// The home's wood floor: warm planks along X with soft seams and a little grain.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Map/Godot/Shaders/toon.gdshaderinc"

uniform vec3 wood : source_color = vec3(0.78, 0.55, 0.34);

varying vec3 world_pos;

float hash(float n) {
	return fract(sin(n * 127.1) * 43758.5453);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float row = floor(world_pos.z / 0.6);
	float along = world_pos.x + hash(row) * 3.0;
	float plank = floor(along / 2.4);
	float seam = smoothstep(0.0, 0.04, fract(world_pos.z / 0.6)) * smoothstep(0.0, 0.03, fract(along / 2.4));
	float tone = 0.9 + 0.14 * hash(row * 13.0 + plank);
	float grain = 0.96 + 0.04 * sin(world_pos.x * 9.0 + sin(world_pos.z * 31.0) * 2.0);
	ALBEDO = wood * tone * grain * mix(0.55, 1.0, seam);
}
