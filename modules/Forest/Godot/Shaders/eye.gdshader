// Big round cute eyes: white, a large dark pupil looking slightly up, a sparkle.
// The eye mesh is a dome around +Y; local x/z span -1..1.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Forest/Godot/Shaders/toon.gdshaderinc"

uniform vec3 pupil_color : source_color = vec3(0.16, 0.09, 0.1);

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec2 p = local_pos.xz;
	float pupil = 1.0 - smoothstep(0.6, 0.64, length(p - vec2(0.0, -0.18)));
	float sparkle = 1.0 - smoothstep(0.17, 0.2, length(p - vec2(-0.2, -0.42)));
	float sparkle2 = 1.0 - smoothstep(0.07, 0.09, length(p - vec2(0.18, 0.05)));
	vec3 color = mix(vec3(1.0, 0.99, 0.96), pupil_color, pupil);
	color = mix(color, vec3(1.0), max(sparkle, sparkle2) * pupil);
	ALBEDO = color;
	EMISSION = vec3(max(sparkle, sparkle2) * pupil * 0.3);
}
