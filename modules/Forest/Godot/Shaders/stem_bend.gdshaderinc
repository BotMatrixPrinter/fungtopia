// How a mushroom stem bends and slims (ForestScenery.StemRadiusAt and LeanOffset): UV.y runs from
// 0 at the foot to 1 at the top; INSTANCE_CUSTOM.x is how slender it gets above its foot (1: not at
// all), INSTANCE_CUSTOM.y how far (in local units) its top leans along local +X.
vec3 bend_stem(vec3 v, float t, vec4 custom) {
	float u = clamp((t - 0.1) / 0.5, 0.0, 1.0);
	v.xz *= 1.0 - (1.0 - custom.x) * u * u * (3.0 - 2.0 * u);
	v.x += custom.y * t * t;
	return v;
}
