// Giant and small mushroom caps: vivid instance color on top with pure white spots, pale gills
// below. Toon-lit like the rest of the forest, plus a soft glossy band on the lit side of the top
// and a light rim along the edge. INSTANCE_CUSTOM.x picks the spot pattern, INSTANCE_CUSTOM.y
// makes the cap glow; glow_emission and night_glow (set by the view, brighter at night) scale it.
shader_type spatial;

uniform vec3 gill_color : source_color = vec3(1.0, 0.93, 0.8);
uniform vec3 spot_color : source_color = vec3(1.0);
uniform float gloss = 0.32;
uniform float rim = 0.42;
uniform float glow_emission = 2.0;
uniform float night_glow = 1.0;

varying vec3 local_pos;
varying vec2 custom;
varying float cap_top;

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.14, ndl) * 0.72 + smoothstep(0.45, 0.62, ndl) * 0.28;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + edge * rim * ATTENUATION) / PI;
	// A soft toon specular band (white, not tinted by the cap) on the top's lit side.
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.88, 0.93, ndh) * lit * cap_top;
	SPECULAR_LIGHT += LIGHT_COLOR * band * gloss * ATTENUATION;
}

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
	cap_top = top;
	vec3 cap = COLOR.rgb;
	vec3 gills = gill_color * mix(vec3(1.0), cap, 0.18);
	ALBEDO = mix(gills, mix(cap, spot_color, spot), top);
	EMISSION = (cap * 0.8 * top + gills * 0.25 * (1.0 - top) + spot_color * spot * 0.5) * custom.y * glow_emission * night_glow;
}
