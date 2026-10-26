// The home's toon surface: soft two-band light, a gentle rim, optional glow (lamps, eye sparkles),
// on mushroom caps a soft glossy highlight band and a light rim along the edge, and an optional
// per-instance tint (the little fungi's caps).
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of bright bits (the eye sparkles, the lamp).
uniform float self_light = 0.0;
// The mushroom caps' small specular band (0: none).
uniform float gloss = 0.0;
// The mushroom caps' light rim along the edge (0: none).
uniform float rim_light = 0.0;
// The little fungi (a MultiMesh): each cap tinted by its instance's custom data (linear color).
uniform bool instance_tint = false;

varying vec3 tint;

void vertex() {
	tint = instance_tint ? INSTANCE_CUSTOM.rgb : vec3(1.0);
}

void fragment() {
	ALBEDO = albedo * tint;
	EMISSION = ALBEDO * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	float rim = edge * 0.3 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
	if (gloss > 0.0 || rim_light > 0.0) {
		float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
		float band = smoothstep(0.9, 0.96, ndh) * gloss;
		float sheen = smoothstep(0.35, 0.75, edge) * rim_light * (0.4 + 0.6 * ndl);
		SPECULAR_LIGHT += LIGHT_COLOR * (band + sheen) * smoothstep(0.2, 0.6, ATTENUATION);
	}
}
