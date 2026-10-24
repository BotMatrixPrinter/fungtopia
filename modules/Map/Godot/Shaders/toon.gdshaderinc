// Soft two-band toon lighting with a gentle rim, shared by the map's shaders.
// Shadowed sides take the environment's warm-lavender ambient light.

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.14, ndl) * 0.72 + smoothstep(0.45, 0.62, ndl) * 0.28;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * 0.18 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
}
