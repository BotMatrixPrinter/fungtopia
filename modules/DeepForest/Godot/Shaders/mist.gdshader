// Low mist puffs: big soft billboards, faint, drifting slowly, fading out near the camera so
// they never fill the view. INSTANCE_CUSTOM.x is the phase, .y the opacity; the instance scale
// is the size. They show the fog even where volumetric fog is off (the OpenGL renderer).
shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled, shadows_disabled;

uniform float near_fade = 6.0;

varying float alpha;

void vertex() {
	float phase = INSTANCE_CUSTOM.x * 6.2831;
	float size = length(MODEL_MATRIX[0].xyz);
	vec3 drift = vec3(sin(TIME * 0.05 + phase) * 1.5, 0.0, cos(TIME * 0.04 + phase) * 1.5);
	vec3 center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz + drift;
	// Upright to the camera's sides, but flattened: mist hugs the ground.
	vec3 world = center + (INV_VIEW_MATRIX[0].xyz * VERTEX.x + vec3(0.0, 0.45, 0.0) * VERTEX.y) * size;
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world, 1.0));
	float dist = length(center - INV_VIEW_MATRIX[3].xyz);
	alpha = INSTANCE_CUSTOM.y * smoothstep(near_fade * 0.4, near_fade, dist);
}

void fragment() {
	vec2 q = UV - vec2(0.5);
	float d = length(q * vec2(1.0, 1.4)) * 2.0;
	float soft = pow(max(1.0 - d, 0.0), 1.6);
	ALBEDO = COLOR.rgb;
	ALPHA = soft * alpha * 0.5;
}
