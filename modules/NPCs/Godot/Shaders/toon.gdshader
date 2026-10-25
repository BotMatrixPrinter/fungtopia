// The NPCs' toon surface: soft two-band light and a gentle rim. Mushroom caps (MushroomLook) add a
// small glossy highlight band and a stronger rim light along their edge.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Optional self-lit glow (0 = none).
uniform float self_light = 0.0;
// The soft glossy highlight band (0 = none).
uniform float gloss = 0.0;
// The rim light along the edge.
uniform float rim = 0.22;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * rim * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + edge) / PI;
	if (gloss > 0.0) {
		float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
		float band = smoothstep(0.84, 0.93, ndh) * smoothstep(0.2, 0.6, ATTENUATION);
		SPECULAR_LIGHT += LIGHT_COLOR * band * gloss;
	}
}
