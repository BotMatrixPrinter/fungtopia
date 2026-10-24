// The floor grid shown while decorating: soft cream lines every 0.5 m, fading at the room's edge.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, shadows_disabled, depth_draw_never;

uniform vec4 line_color : source_color = vec4(1.0, 0.96, 0.82, 0.55);
uniform float cell = 0.5;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 g = abs(fract(world.xz / cell + 0.5) - 0.5) / fwidth(world.xz / cell);
	float line = 1.0 - clamp(min(g.x, g.y) - 0.5, 0.0, 1.0);
	ALBEDO = line_color.rgb;
	ALPHA = line * line_color.a;
}
