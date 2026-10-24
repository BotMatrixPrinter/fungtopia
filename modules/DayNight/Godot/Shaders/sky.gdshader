// DayNight's sky: a toon gradient with a crisp sun, a big spotted moon and twinkling stars.
shader_type sky;

uniform vec3 top_color : source_color = vec3(0.35, 0.6, 0.95);
uniform vec3 horizon_color : source_color = vec3(0.8, 0.9, 1.0);
uniform vec3 ground_color : source_color = vec3(0.35, 0.4, 0.3);
uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.8);
uniform float sun_amount = 1.0;
uniform vec3 moon_dir = vec3(0.0, -1.0, 0.0);
uniform float moon_amount = 0.0;
uniform float star_amount = 0.0;

float hash(vec3 p) {
	p = fract(p * vec3(443.897, 441.423, 437.195));
	p += dot(p, p.yzx + 19.19);
	return fract((p.x + p.y) * p.z);
}

vec3 stars(vec3 d) {
	vec3 p = d * 90.0;
	vec3 cell = floor(p);
	float h = hash(cell);
	if (h < 0.93)
		return vec3(0.0);
	vec3 center = cell + 0.5 + (vec3(hash(cell + 3.1), hash(cell + 7.7), hash(cell + 1.3)) - 0.5) * 0.5;
	float r = length(p - center);
	float size = mix(0.09, 0.22, fract(h * 37.0));
	float twinkle = 0.65 + 0.35 * sin(TIME * (1.5 + 3.0 * fract(h * 91.0)) + h * 60.0);
	vec3 tint = mix(vec3(1.0, 0.92, 0.75), vec3(0.75, 0.85, 1.0), fract(h * 13.0));
	return tint * smoothstep(size, size * 0.3, r) * twinkle * 1.6;
}

void sky() {
	vec3 d = EYEDIR;
	float y = d.y;
	vec3 col = y >= 0.0
		? mix(horizon_color, top_color, pow(smoothstep(0.0, 1.0, y), 0.55))
		: mix(horizon_color, ground_color, smoothstep(0.0, 0.12, -y));
	float above = smoothstep(-0.02, 0.03, y);

	vec3 s = normalize(sun_dir);
	float sd = dot(d, s);
	col += sun_color * pow(max(sd, 0.0), 12.0) * 0.35 * sun_amount;
	col = mix(col, sun_color * 3.0, smoothstep(0.9988, 0.9991, sd) * sun_amount * above);

	vec3 m = normalize(moon_dir);
	float md = dot(d, m);
	if (!AT_CUBEMAP_PASS) {
		col += stars(d) * star_amount * smoothstep(0.0, 0.25, y) * (1.0 - smoothstep(0.985, 0.994, md));
	}
	col += vec3(0.55, 0.65, 1.0) * pow(max(md, 0.0), 40.0) * 0.35 * moon_amount;
	// The moon: about 6 degrees across, pale cream with soft round spots like a mushroom cap.
	float radius = 0.055;
	vec3 t = normalize(cross(m, abs(m.y) > 0.95 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0)));
	vec3 b = cross(t, m);
	vec2 uv = vec2(dot(d, t), dot(d, b)) / radius;
	float disk = smoothstep(1.0, 0.96, length(uv)) * step(0.0, md);
	float spots = smoothstep(0.32, 0.27, length(uv - vec2(-0.35, 0.3)))
		+ smoothstep(0.22, 0.18, length(uv - vec2(0.4, 0.42)))
		+ smoothstep(0.26, 0.21, length(uv - vec2(0.2, -0.45)))
		+ smoothstep(0.13, 0.1, length(uv - vec2(-0.45, -0.3)));
	vec3 moon = mix(vec3(1.25, 1.2, 0.95), vec3(0.85, 0.82, 0.78), clamp(spots, 0.0, 1.0));
	moon *= 1.0 - 0.18 * smoothstep(0.4, 1.0, length(uv - vec2(-0.25, 0.25)));
	col = mix(col, moon * 1.6, disk * moon_amount * above);

	COLOR = col;
}
