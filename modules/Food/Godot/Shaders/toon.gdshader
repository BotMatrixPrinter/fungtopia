// The Food module's toon surface: soft two-band light, a gentle rim, optional glow (the pie's spores).
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of bright bits (the spores).
uniform float self_light = 0.0;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * 0.3 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
}
