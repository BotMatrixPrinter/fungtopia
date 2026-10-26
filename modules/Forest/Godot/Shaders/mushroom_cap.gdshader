// Every mushroom cap and fungus body: vivid instance color on top (UV.y = 1) painted with its
// kind's pattern, a paler underside below (gills, a cup's outside; UV.y = 0). Toon-lit like the
// rest of the forest, plus a soft glossy band on the lit side of the top and a light rim along
// the edge. INSTANCE_CUSTOM.x seeds the pattern, .y makes the cap glow (glow_emission and
// night_glow, set by the view, brighter at night), .z picks the pattern (ForestSpecies'
// CapPattern / 8: plain, spots, scales, honeycomb, shaggy, bands, jelly) and .w how much the
// cap's color tints its underside.
shader_type spatial;

uniform vec3 gill_color : source_color = vec3(1.0, 0.93, 0.8);
uniform vec3 spot_color : source_color = vec3(1.0);
uniform float gloss = 0.32;
uniform float rim = 0.42;
uniform float glow_emission = 2.0;
uniform float night_glow = 1.0;

varying vec3 local_pos;
varying vec4 custom;
varying float cap_top;
varying float shine;

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.14, ndl) * 0.72 + smoothstep(0.45, 0.62, ndl) * 0.28;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + edge * rim * ATTENUATION) / PI;
	// A soft toon specular band (white, not tinted by the cap) on the top's lit side; jelly shines more.
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.88 - 0.06 * shine, 0.93 - 0.05 * shine, ndh) * lit * cap_top;
	SPECULAR_LIGHT += LIGHT_COLOR * band * gloss * (1.0 + shine) * ATTENUATION;
}

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Round spots of a jittered 3D grid at p: 1 inside a kept spot, else 0.
float spots(vec3 p, float keep_above, float size_min) {
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
		float keep = step(keep_above, hash3(c + 11.7));
		float size = size_min + 0.12 * hash3(c + 5.9);
		spot = max(spot, keep * (1.0 - smoothstep(size, size + 0.03, d)));
	}
	return spot;
}

// Honeycomb ridges (a morel's): 1 on the ridges between cells, 0 in the pits.
float ridges(vec3 p) {
	vec3 cell = floor(p);
	vec3 f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 c = cell + o;
		vec3 r = vec3(hash3(c), hash3(c + 7.1), hash3(c + 3.3)) * 0.7 + 0.15;
		float d = length(o + r - f);
		if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
	}
	return 1.0 - smoothstep(0.06, 0.14, d2 - d1);
}

void vertex() {
	local_pos = VERTEX;
	custom = INSTANCE_CUSTOM;
}

void fragment() {
	float top = step(0.5, UV.y);
	int mode = int(custom.z * 8.0 + 0.5);
	vec3 dir = normalize(local_pos + vec3(0.0, 0.05, 0.0));
	vec3 seed = vec3(custom.x * 37.0, custom.x * 11.0, custom.x * 23.0);
	vec3 cap = COLOR.rgb;
	vec3 color = cap;
	float spot = 0.0;
	shine = 0.0;
	if (mode == 1) {
		// Crisp white spots.
		spot = spots(dir * 2.4 + seed, 0.4, 0.22) * smoothstep(0.12, 0.3, dir.y);
	} else if (mode == 2) {
		// A parasol: a darker crown and darker scales scattered over the lighter cap.
		float scale = spots(dir * 5.0 + seed, 0.35, 0.16) * smoothstep(0.2, 0.5, dir.y);
		color = mix(cap * 1.08, cap * 0.55, max(scale, smoothstep(0.9, 0.96, dir.y)));
	} else if (mode == 3) {
		// A morel: light honeycomb ridges round deep pits.
		color = mix(cap * 0.5, min(cap * 1.25, vec3(1.0)), ridges(local_pos * 6.0 + seed));
	} else if (mode == 4) {
		// A shaggy ink cap: white shaggy scales, the rim running into ink.
		float a = atan(local_pos.z, local_pos.x);
		float shag = smoothstep(0.55, 0.9, sin(a * 16.0 + local_pos.y * 9.0 + seed.x) * sin(local_pos.y * 22.0 + a * 3.0));
		vec3 white = vec3(1.0, 0.98, 0.95) * (1.0 - 0.12 * shag);
		color = mix(white, cap, smoothstep(0.38, 0.06, local_pos.y));
	} else if (mode == 5) {
		// A shelf fungus: concentric bands of its color and a cream edge.
		float r = length(local_pos.xz);
		float bands = smoothstep(0.35, 0.65, fract(r * 4.5 + custom.x));
		color = mix(mix(cap, cap * 0.62, bands), vec3(1.0, 0.95, 0.8), smoothstep(0.86, 0.97, r));
	} else if (mode == 6) {
		// A jelly fungus: glossy, a little lighter where it is thin.
		shine = 1.0;
		color = mix(cap, min(cap * 1.3, vec3(1.0)), smoothstep(0.3, 1.0, local_pos.y));
	}
	cap_top = top;
	vec3 gills = mix(gill_color, cap, custom.w);
	ALBEDO = mix(gills, mix(color, spot_color, spot), top);
	EMISSION = (color * 0.8 * top + gills * 0.25 * (1.0 - top) + spot_color * spot * 0.5) * custom.y * glow_emission * night_glow
		+ cap * 0.12 * shine;
}
