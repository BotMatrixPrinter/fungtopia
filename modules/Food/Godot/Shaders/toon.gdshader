// The Food module's toon surface: soft two-band light, a gentle rim, optional glow (the pie's spores);
// mushroom caps add a soft glossy band and a brighter rim (FoodLook).
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of bright bits (the spores).
uniform float self_light = 0.0;
// Soft specular band (mushroom caps); 0 for none.
uniform float gloss = 0.0;
// Rim light along the silhouette.
uniform float rim = 0.3;

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
	float ndh = clamp(dot(NORMAL, normalize(VIEW + LIGHT)), 0.0, 1.0);
	float band = smoothstep(0.95, 0.98, ndh) * gloss * ATTENUATION;
	SPECULAR_LIGHT += LIGHT_COLOR * band;
}
