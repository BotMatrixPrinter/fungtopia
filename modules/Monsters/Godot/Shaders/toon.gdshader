// The monsters' toon surface: soft two-band light, a gentle rim, a white hit flash and a green
// poison tint.
shader_type spatial;
render_mode specular_disabled;

uniform vec3 albedo : source_color = vec3(1.0);
// Glow of tiny bright bits (the eye sparkles).
uniform float self_light = 0.0;
// 0..1: how white the monster flashes when hit; set per monster.
instance uniform float flash = 0.0;
// 0..1: how green the monster is from poison; set per monster.
instance uniform float poison = 0.0;

void fragment() {
	vec3 sick = mix(albedo, vec3(0.42, 0.9, 0.25) * (0.55 + 0.45 * dot(albedo, vec3(0.33))), 0.6);
	vec3 color = mix(albedo, sick, poison);
	ALBEDO = mix(color, vec3(1.0, 0.97, 0.95), flash * 0.75);
	EMISSION = albedo * self_light + vec3(1.0, 0.85, 0.85) * flash * 0.35 + vec3(0.1, 0.35, 0.05) * poison * 0.4;
}

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = smoothstep(0.0, 0.12, ndl) * 0.7 + smoothstep(0.42, 0.58, ndl) * 0.3;
	lit *= smoothstep(0.2, 0.6, ATTENUATION);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * 0.22 * ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * (lit + rim) / PI;
}
