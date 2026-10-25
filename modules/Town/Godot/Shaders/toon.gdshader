// The town's toon surface: soft two-band light, a gentle rim, optional glow (lamps, windows) and,
// on mushroom caps, a soft glossy band and a light rim along the edge.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of bright bits (lamp bulbs, glowing caps and spots, windows).
uniform float self_light = 0.0;
// Mushroom caps only (0 elsewhere): the small specular band and the light rim.
uniform float gloss = 0.0;
uniform float rim_light = 0.0;

void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * self_light;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float facing = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(facing, 3.0) * 0.3 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
	if (gloss > 0.0 || rim_light > 0.0) {
		// Specular is disabled (no sky reflections), so the shine goes into the diffuse light,
		// divided by the albedo so it shows near white, only lightly tinted by the cap.
		vec3 sheen = mix(ALBEDO, vec3(1.0), 0.75) / max(ALBEDO, vec3(0.05));
		float spec = pow(clamp(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0, 1.0), 48.0);
		float band = smoothstep(0.35, 0.6, spec) * gloss * lit;
		float edge = smoothstep(0.55, 0.8, facing) * rim_light * (0.35 + 0.65 * lit);
		DIFFUSE_LIGHT += LIGHT_COLOR * sheen * (band + edge) / PI;
	}
}
