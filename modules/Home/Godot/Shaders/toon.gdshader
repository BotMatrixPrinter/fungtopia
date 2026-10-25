// The home's toon surface: soft two-band light, a gentle rim, optional glow (lamps, eye sparkles),
// and on mushroom caps a soft glossy highlight band and a brighter rim.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of bright bits (the eye sparkles, the lamp).
uniform float self_light = 0.0;
// The glossy highlight's strength (mushroom caps; 0: none).
uniform float gloss = 0.0;
// The rim light's strength along the silhouette.
uniform float rim = 0.3;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + edge * rim) / PI;
	if (gloss > 0.0) {
		// A small soft white band where the light glints off the cap, and a light white rim.
		float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
		float band = smoothstep(0.965, 0.985, ndh) * smoothstep(0.0, 0.12, ndl);
		SPECULAR_LIGHT += LIGHT_COLOR * gloss * (band * 0.3 + edge * ndl * 0.2) * ATTENUATION;
	}
}
