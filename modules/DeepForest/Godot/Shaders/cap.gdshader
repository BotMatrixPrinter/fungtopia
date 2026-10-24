// Old giant caps: the instance color on top with a few faint, worn spots (INSTANCE_CUSTOM.y,
// 0 for none), dark gills below (UV.y is 1 on top, 0 underneath). INSTANCE_CUSTOM.x picks the
// spot pattern. No eyes, no faces.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"
#include "res://modules/DeepForest/Godot/Shaders/gloom.gdshaderinc"

uniform vec3 gill_color : source_color = vec3(0.36, 0.26, 0.3);
uniform vec3 spot_color : source_color = vec3(0.78, 0.7, 0.72);

varying vec3 local_pos;
varying vec3 world_pos;
varying vec2 custom;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

void vertex() {
	local_pos = VERTEX;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float top = step(0.5, UV.y);
	vec3 dir = normalize(local_pos + vec3(0.0, 0.1, 0.0));
	vec3 p = dir * 2.6 + vec3(custom.x * 37.0, custom.x * 11.0, custom.x * 23.0);
	vec3 cell = floor(p);
	vec3 f = fract(p);
	float spot = 0.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 c = cell + o;
		vec3 r = vec3(hash3(c), hash3(c + 7.1), hash3(c + 3.3)) * 0.6 + 0.2;
		float d = length(o + r - f);
		float keep = step(0.55, hash3(c + 11.7));
		float size = 0.16 + 0.1 * hash3(c + 5.9);
		spot = max(spot, keep * (1.0 - smoothstep(size, size + 0.05, d)));
	}
	spot *= top * smoothstep(0.15, 0.35, dir.y) * custom.y * 0.6;
	// Old caps: mottled, a little lighter toward the crown.
	float mottle = 0.9 + 0.1 * sin(local_pos.x * 9.0 + sin(local_pos.z * 7.0) * 2.0) + 0.08 * dir.y;
	vec3 cap = COLOR.rgb * mottle;
	ALBEDO = mix(gill_color * mix(vec3(1.0), COLOR.rgb * 2.0, 0.3), mix(cap, spot_color * cap * 2.2, spot), top);
	AO = 1.0 - 0.55 * gloom_at(world_pos);
	AO_LIGHT_AFFECT = 0.0;
}
