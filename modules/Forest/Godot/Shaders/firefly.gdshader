// Fireflies: soft additive glowing billboards drifting and blinking near dusk.
// INSTANCE_CUSTOM.x is each firefly's phase.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform vec3 glow_color : source_color = vec3(1.0, 0.92, 0.45);
uniform float size = 0.16;

varying float blink;

void vertex() {
	float phase = INSTANCE_CUSTOM.x * 6.2831;
	vec3 drift = vec3(sin(TIME * 0.45 + phase) * 1.2,
		sin(TIME * 0.7 + phase * 1.7) * 0.45,
		cos(TIME * 0.38 + phase * 0.6) * 1.2);
	vec3 center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz + drift;
	vec3 world = center + (INV_VIEW_MATRIX[0].xyz * VERTEX.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y) * size;
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world, 1.0));
	blink = smoothstep(0.1, 0.9, 0.5 + 0.5 * sin(TIME * 1.7 + phase * 3.0));
}

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	float glow = pow(max(1.0 - d, 0.0), 2.2);
	ALBEDO = glow_color * 2.5;
	ALPHA = glow * blink;
}
