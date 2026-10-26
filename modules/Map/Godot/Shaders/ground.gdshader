// Rolling ground: vertex colors (grass, moss, dirt paths) with a soft hand-painted mottle. On the
// grass (vertex alpha: how much of it is meadow grass) sunny yellow-green patches and little flowers.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Map/Godot/Shaders/toon.gdshaderinc"

uniform vec3 highlight : source_color = vec3(0.74, 0.92, 0.22);
// The flowers' colors, linear (the view converts them).
uniform vec3 flowers[6];
// Flowers per 0.9 m cell on full grass (0..1).
uniform float flower_density = 0.3;

varying vec3 world_pos;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float mottle = value_noise(world_pos.xz * 0.9) * 0.6 + value_noise(world_pos.xz * 3.1) * 0.4;
	vec3 color = COLOR.rgb * (0.88 + 0.22 * mottle);
	float grass = COLOR.a;
	// Sunny yellow-green patches over the grass.
	float sun = smoothstep(0.55, 0.85, value_noise(world_pos.xz * 0.18 + 3.7));
	color = mix(color, highlight * (0.92 + 0.12 * mottle), sun * 0.55 * grass);
	// Little flowers: at most one per cell, five round petals around a golden middle.
	vec2 cell = floor(world_pos.xz / 0.9);
	vec2 at = (cell + 0.2 + 0.6 * vec2(hash(cell + 1.3), hash(cell + 5.1))) * 0.9;
	vec2 off = world_pos.xz - at;
	float d = length(off);
	float petals = 0.13 + 0.05 * cos(5.0 * atan(off.y, off.x) + hash(cell + 4.4) * 6.28);
	float bloom = step(hash(cell + 9.7), flower_density * grass) * (1.0 - smoothstep(petals - 0.015, petals, d));
	vec3 petal = flowers[int(min(hash(cell + 2.9) * 6.0, 5.0))];
	color = mix(color, mix(petal, vec3(1.0, 0.78, 0.1), 1.0 - smoothstep(0.04, 0.05, d)), bloom);
	ALBEDO = color;
}
