// Giant and small mushroom caps: instance color on top with pure white spots, pale gills below.
// INSTANCE_CUSTOM.x picks the spot pattern, INSTANCE_CUSTOM.y makes the cap glow, INSTANCE_CUSTOM.z
// is how spotted it is (0 plain, 1 a fly agaric's), INSTANCE_CUSTOM.w > 0.5 pits it like a morel's honeycomb.
// The map's soft two-band toon lighting plus a small glossy specular band and a light rim
// along the cap's edge, so the vivid caps pop without washing out.
shader_type spatial;

uniform vec3 gill_color : source_color = vec3(1.0, 0.95, 0.85);
uniform vec3 spot_color : source_color = vec3(1.0);
// Strength of the glossy highlight band and of the rim light along the edge.
uniform float gloss = 0.32;
uniform float rim = 0.3;

varying vec3 local_pos;
varying vec4 custom;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

void vertex() {
	local_pos = VERTEX;
	custom = INSTANCE_CUSTOM;
}

void fragment() {
	float top = step(0.5, UV.y);
	vec3 dir = normalize(local_pos + vec3(0.0, 0.05, 0.0));
	vec3 p = dir * mix(2.4, 4.2, step(0.5, custom.w)) + vec3(custom.x * 37.0, custom.x * 11.0, custom.x * 23.0);
	vec3 cell = floor(p);
	vec3 f = fract(p);
	float spot = 0.0;
	float f1 = 8.0, f2 = 8.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 c = cell + o;
		vec3 r = vec3(hash3(c), hash3(c + 7.1), hash3(c + 3.3)) * 0.6 + 0.2;
		float d = length(o + r - f);
		float keep = step(1.0 - 0.6 * custom.z, hash3(c + 11.7));
		float size = (0.22 + 0.12 * hash3(c + 5.9)) * (0.45 + 0.55 * custom.z);
		spot = max(spot, keep * (1.0 - smoothstep(size, size + 0.02, d)));
		// The nearest two cell centers, for the honeycomb's ridges.
		if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) { f2 = d; }
	}
	spot *= top * smoothstep(0.12, 0.3, dir.y) * step(0.001, custom.z);
	// Honeycomb: deep pits between light ridges.
	float pits = step(0.5, custom.w) * top * smoothstep(0.08, 0.2, f2 - f1);
	vec3 cap = COLOR.rgb;
	vec3 gills = gill_color * mix(vec3(1.0), cap, 0.12);
	ALBEDO = mix(gills, mix(cap * (1.0 - 0.42 * pits), spot_color, spot), top);
	EMISSION = (cap * 0.8 * top + gills * 0.25 * (1.0 - top) + spot_color * spot * 0.5) * custom.y;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float shade = smoothstep(0.2, 0.6, ATTENUATION);
	float lit = (smoothstep(0.0, 0.14, ndl) * 0.72 + smoothstep(0.45, 0.62, ndl) * 0.28) * shade;
	// A light rim along the edge, strongest on the lit side.
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	float rim_light = smoothstep(0.35, 0.65, edge) * rim * (0.35 + 0.65 * smoothstep(-0.2, 0.4, dot(NORMAL, LIGHT))) * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim_light) / PI;
	// A soft glossy band where the light glints off the cap.
	float glint = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.86, 0.93, glint) * step(0.001, ndl) * shade;
	SPECULAR_LIGHT += LIGHT_COLOR * band * gloss;
}
