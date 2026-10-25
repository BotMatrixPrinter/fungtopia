// The bow's and arrows' toon surface: soft two-band light and a gentle rim; glossy surfaces (the
// mushroom caps) add a soft specular band and a brighter rim light.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow (the poison spots on arrow tips), a linear color.
uniform vec3 glow = vec3(0.0);
// 0..1: how glossy (mushroom caps 1, everything else 0).
uniform float gloss = 0.0;

void fragment() {
	ALBEDO = albedo;
	EMISSION = glow;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * (0.22 + 0.28 * gloss) * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
	// A small soft highlight band, white like light on a glossy cap.
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.955, 0.975, ndh) * lit * gloss;
	SPECULAR_LIGHT += LIGHT_COLOR * band * 0.22;
}
