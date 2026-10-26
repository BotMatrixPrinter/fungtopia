// Every cap (giant, side, medium and small; every cap mesh): the vivid instance color on top with
// a pattern, a soft glossy highlight and a light rim, darker gills below (UV.y is 1 on top, 0
// underneath). INSTANCE_CUSTOM: x the seed (spot layout, glow pulse phase), y the crisp pure
// white spots (0 none), z the glow (bioluminescent kinds; 0 none), w the pattern
// (DeepForest CapPattern: 0 spots, 1 a parasol's scales, 2 a morel's honeycomb, 3 an ink cap's
// inky shaggy lower half). No eyes, no faces.
shader_type spatial;
render_mode specular_disabled;

#define TOON_GLOSS
#include "res://modules/DeepForest/Godot/Shaders/toon.gdshaderinc"
#include "res://modules/DeepForest/Godot/Shaders/gloom.gdshaderinc"

uniform vec3 gill_color : source_color = vec3(0.4, 0.26, 0.36);
uniform vec3 spot_color : source_color = vec3(1.0);
uniform vec3 ink_color : source_color = vec3(0.22, 0.08, 0.36);

varying vec3 local_pos;
varying vec3 world_pos;
varying vec4 custom;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Round cells on p: x the spot mask (cells kept by `keep`, of `size`), y the distance to the
// cell edge (F2 - F1, for the honeycomb).
vec2 cells(vec3 p, float keep_above, float size) {
	vec3 cell = floor(p);
	vec3 f = fract(p);
	float spot = 0.0, f1 = 9.0, f2 = 9.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 c = cell + o;
		vec3 r = vec3(hash3(c), hash3(c + 7.1), hash3(c + 3.3)) * 0.6 + 0.2;
		float d = length(o + r - f);
		if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) { f2 = d; }
		float keep = step(keep_above, hash3(c + 11.7));
		float s = size + 0.1 * hash3(c + 5.9);
		spot = max(spot, keep * (1.0 - smoothstep(s, s + 0.015, d)));
	}
	return vec2(spot, f2 - f1);
}

void vertex() {
	local_pos = VERTEX;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	custom = INSTANCE_CUSTOM;
}

void fragment() {
	float top = step(0.5, UV.y);
	int pattern = int(custom.w + 0.5);
	vec3 dir = normalize(local_pos + vec3(0.0, 0.1, 0.0));
	vec3 seed = vec3(custom.x * 37.0, custom.x * 11.0, custom.x * 23.0);
	// Barely mottled, a little lighter toward the crown, so the color stays clean and rich.
	float mottle = 0.95 + 0.05 * sin(local_pos.x * 9.0 + sin(local_pos.z * 7.0) * 2.0) + 0.06 * dir.y;
	vec3 cap = COLOR.rgb * mottle;
	float spot = 0.0;
	if (pattern == 1) {
		// Parasol: a dark crown and rings of darker scales on the light cap.
		vec2 c = cells(dir * 5.0 + seed, 0.35, 0.12);
		cap = mix(cap, COLOR.rgb * 0.45, max(c.x * 0.8, smoothstep(0.9, 0.97, dir.y)));
	} else if (pattern == 2) {
		// Morel: honeycomb pits, lighter ridges between them.
		vec2 c = cells(local_pos * vec3(5.0, 3.5, 5.0) + seed, 2.0, 0.0);
		cap = mix(COLOR.rgb * 0.5, COLOR.rgb * 1.15, smoothstep(0.08, 0.2, c.y));
	} else if (pattern == 3) {
		// Ink cap: shaggy, inky toward the rim.
		float shag = 0.5 + 0.5 * sin(atan(local_pos.z, local_pos.x) * 18.0 + local_pos.y * 9.0);
		float ink = smoothstep(0.45, 0.05, local_pos.y + 0.08 * shag);
		cap = mix(cap * (0.92 + 0.08 * shag), ink_color, ink);
	} else {
		spot = cells(dir * 2.6 + seed, 0.55, 0.16).x * step(0.25, dir.y) * step(0.01, custom.y);
	}
	spot *= top;
	ALBEDO = mix(gill_color * mix(vec3(1.0), COLOR.rgb * 2.0, 0.3), mix(cap, spot_color, spot), top);
	// Glossy on top (not the spots or the gills): ROUGHNESS is the gloss mask the toon light reads.
	ROUGHNESS = 1.0 - top * (1.0 - spot) * 0.9;
	// Bioluminescent kinds glow, pulsing slowly; their gills glow softly too.
	float pulse = 0.82 + 0.18 * sin(TIME * 0.9 + custom.x * 6.2831);
	EMISSION = COLOR.rgb * custom.z * pulse * mix(0.45, 1.0, top) * (1.0 - spot * 0.5);
	AO = 1.0 - 0.55 * gloom_at(world_pos);
	AO_LIGHT_AFFECT = 0.0;
}
