// Fallen logs: bark sides (UV.y = 1) and pale cut ends with growth rings (UV.y = 0).
// The log mesh is a cylinder around local Y with radius 1.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"

uniform vec3 bark_color : source_color = vec3(0.5, 0.33, 0.22);
uniform vec3 wood_color : source_color = vec3(0.92, 0.76, 0.55);
uniform vec3 moss_color : source_color = vec3(0.4, 0.62, 0.3);

varying vec3 local_pos;
varying vec3 world_normal;

void vertex() {
	local_pos = VERTEX;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float side = step(0.5, UV.y);
	float rings = smoothstep(0.35, 0.5, abs(fract(length(local_pos.xz) * 4.0) - 0.5));
	vec3 ends = mix(wood_color, wood_color * 0.78, rings);
	float stripes = 0.9 + 0.1 * sin(atan(local_pos.z, local_pos.x) * 14.0);
	vec3 bark = mix(bark_color * stripes, moss_color, smoothstep(0.55, 0.8, world_normal.y));
	ALBEDO = mix(ends, bark, side);
}
