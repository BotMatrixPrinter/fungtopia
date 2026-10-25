// The pets' toon surface: soft two-band light and a gentle rim; mushroom caps add a glossy
// highlight and a light rim light.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of tiny bright bits (the eye sparkles).
uniform float self_light = 0.0;
// A soft white highlight where the light glints (mushroom caps).
uniform float gloss = 0.0;
// A light white rim around the edge (mushroom caps).
uniform float rim_light = 0.0;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(edge, 3.0) * 0.22 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
	// The caps' gloss: a soft-edged highlight, and a thin white rim on their lit side.
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float glint = smoothstep(0.55, 0.8, pow(ndh, 24.0)) * 0.8 + pow(ndh, 8.0) * 0.2;
	float shine = glint * gloss + smoothstep(0.55, 0.85, edge) * (0.4 + 0.6 * ndl) * rim_light;
	SPECULAR_LIGHT += LIGHT_COLOR * shine * smoothstep(0.2, 0.6, ATTENUATION);
}
