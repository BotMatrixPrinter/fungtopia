// The little fungi's tinted caps: toon.gdshader's look (toon.gdshaderinc), the color times the
// instance's tint (linear) from its custom data. Only for MultiMeshes with custom data on.
shader_type spatial;
render_mode specular_disabled;

#include "toon.gdshaderinc"

varying vec3 tint;

void vertex() {
	tint = INSTANCE_CUSTOM.rgb;
}

void fragment() {
	ALBEDO = albedo * tint;
	EMISSION = ALBEDO * self_light;
}
