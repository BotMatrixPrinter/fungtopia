// The soft blurred view of Mushroom Town behind the start screen: a gaussian blur and a warm,
// light tint so the logo and the cards stand out.
shader_type canvas_item;

uniform sampler2D town : filter_linear, repeat_disable;
uniform float radius_px = 1.0;
uniform vec4 tint : source_color = vec4(1.0, 0.9, 0.93, 0.12);

void fragment() {
	vec2 step = radius_px / vec2(textureSize(town, 0));
	vec3 sum = vec3(0.0);
	float total = 0.0;
	for (int x = -4; x <= 4; x++) {
		for (int y = -4; y <= 4; y++) {
			float k = exp(-float(x * x + y * y) / 9.0);
			sum += texture(town, UV + vec2(float(x), float(y)) * step).rgb * k;
			total += k;
		}
	}
	COLOR = vec4(mix(sum / total, tint.rgb, tint.a), 1.0);
}
