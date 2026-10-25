// The player character's toon surface: soft two-band light, a gentle rim, and a white hit flash.
// Mushroom caps add a soft glossy highlight band and a lighter rim (MushroomLook, set per surface).
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of tiny bright bits (the eye sparkles).
uniform float self_light = 0.0;
// 0..1: how white the character flashes when hit; set per character.
instance uniform float flash = 0.0;
// 0..1: the soft glossy highlight band of a mushroom cap (0: none).
uniform float gloss = 0.0;
// Extra rim light along a mushroom's edge, added to the gentle 0.22.
uniform float rim_light = 0.0;

void fragment() {
	ALBEDO = mix(albedo, vec3(1.0, 0.97, 0.95), flash * 0.75);
	EMISSION = albedo * self_light + vec3(1.0, 0.85, 0.85) * flash * 0.35;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * (0.22 + rim_light) * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.975, 0.99, ndh) * 0.35 + smoothstep(0.9, 0.975, ndh) * 0.05;
	SPECULAR_LIGHT += LIGHT_COLOR * band * gloss * smoothstep(0.2, 0.6, ATTENUATION);
}
