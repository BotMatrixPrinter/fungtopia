// Soft two-band toon lighting with a gentle rim, shared by the Deep Forest's shaders.
// Shadowed sides take the environment's warm-lavender ambient light.
// Mushroom caps (#define TOON_GLOSS before including) add a soft glossy highlight, a small
// specular band, where 1 - ROUGHNESS (set in fragment, 0 for none) says, and a lighter rim
// along their edge. Both are near-white: added to the diffuse light divided by the albedo (the
// shaders keep specular_disabled so the sky's reflections do not streak the caps).

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.14, ndl) * 0.72 + smoothstep(0.45, 0.62, ndl) * 0.28;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * 0.18 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
#ifdef TOON_GLOSS
	float gloss = 1.0 - ROUGHNESS;
	float ndh = clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0);
	float band = smoothstep(0.93, 0.97, ndh) * smoothstep(0.2, 0.6, ATTENUATION);
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0) * smoothstep(0.0, 0.3, ndl + 0.15);
	DIFFUSE_LIGHT += LIGHT_COLOR * gloss * (band * 0.4 + edge * 0.22) / max(ALBEDO, vec3(0.06));
#endif
}
