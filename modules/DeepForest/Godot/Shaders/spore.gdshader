// Spores: tiny soft glowing billboards rising slowly and drifting, flickering.
// INSTANCE_CUSTOM.x is each spore's phase; the instance scale is its size.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying float flicker;

void vertex() {
	float phase = INSTANCE_CUSTOM.x * 6.2831;
	float size = length(MODEL_MATRIX[0].xyz);
	// Rise about 2.5 m over a slow cycle, then start again, swaying as they go.
	float rise = fract(TIME * 0.03 + INSTANCE_CUSTOM.x) * 2.5;
	vec3 drift = vec3(sin(TIME * 0.31 + phase) * 0.8, rise, cos(TIME * 0.27 + phase * 0.7) * 0.8);
	vec3 center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz + drift;
	vec3 world = center + (INV_VIEW_MATRIX[0].xyz * VERTEX.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y) * size;
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world, 1.0));
	float fade = smoothstep(0.0, 0.2, rise / 2.5) * (1.0 - smoothstep(0.8, 1.0, rise / 2.5));
	flicker = fade * (0.6 + 0.4 * sin(TIME * 2.3 + phase * 3.0));
}

void fragment() {
	float d = length(UV - vec2(0.5)) * 2.0;
	float glow = pow(max(1.0 - d, 0.0), 2.0);
	ALBEDO = COLOR.rgb * 2.2;
	ALPHA = glow * flicker;
}
