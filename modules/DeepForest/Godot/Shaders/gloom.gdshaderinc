// The Deep Forest's gloom on its own props: the ambient light fades as one walks in from the
// path (x = -96) to past the entrance (x = -135), the same ramp as DeepForestGloom.At.

float gloom_at(vec3 world) {
	return smoothstep(-96.0, -135.0, world.x);
}
