// Pet City's toon surface for every ordinary mesh (toon.gdshaderinc). It reads no per-instance
// data: real GPU drivers leave that undefined on a MeshInstance3D (fungus.gdshader tints the fungi).
shader_type spatial;
render_mode specular_disabled;

#include "toon.gdshaderinc"

void fragment() {
	ALBEDO = albedo;
	EMISSION = ALBEDO * self_light;
}
