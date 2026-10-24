// Giant and small mushroom caps: instance color on top with cream spots, pale gills below.
// INSTANCE_CUSTOM.x picks the spot pattern, INSTANCE_CUSTOM.y makes the cap glow softly.
shader_type spatial;
render_mode specular_disabled;

#include "res://modules/Map/Godot/Shaders/toon.gdshaderinc"

uniform vec3 gill_color : source_color = vec3(0.98, 0.88, 0.76);
uniform vec3 spot_color : source_color = vec3(1.0, 0.97, 0.9);

varying vec3 local_pos;
varying vec2 custom;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

void vertex() {
	local_pos = VERTEX;
	custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float top = step(0.5, UV.y);
	vec3 dir = normalize(local_pos + vec3(0.0, 0.05, 0.0));
	vec3 p = dir * 2.4 + vec3(custom.x * 37.0, custom.x * 11.0, custom.x * 23.0);
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
		float keep = step(0.4, hash3(c + 11.7));
		float size = 0.22 + 0.12 * hash3(c + 5.9);
		spot = max(spot, keep * (1.0 - smoothstep(size, size + 0.03, d)));
	}
	spot *= top * smoothstep(0.12, 0.3, dir.y);
	vec3 cap = COLOR.rgb;
	vec3 gills = gill_color * mix(vec3(1.0), cap, 0.18);
	ALBEDO = mix(gills, mix(cap, spot_color, spot), top);
	EMISSION = (cap * 0.8 * top + gills * 0.25 * (1.0 - top) + spot_color * spot * 0.5) * custom.y;
}
