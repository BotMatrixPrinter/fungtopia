// Ink outline for the previews: an inverted hull pushed out a few pixels on screen.
shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;

uniform vec4 ink : source_color = vec4(0.22, 0.12, 0.14, 1.0);
uniform float width_px = 2.6;

void vertex() {
	vec4 clip = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	vec4 ahead = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX + NORMAL * 0.01, 1.0));
	vec2 dir = (ahead.xy / ahead.w - clip.xy / clip.w) * VIEWPORT_SIZE;
	float len = length(dir);
	dir = len > 1e-6 ? dir / len : vec2(0.0);
	clip.xy += dir * width_px * 2.0 / VIEWPORT_SIZE * clip.w;
	POSITION = clip;
}

void fragment() {
	ALBEDO = ink.rgb;
}
