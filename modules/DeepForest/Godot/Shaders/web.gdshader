// Spider webs: thin pale strands, two-sided, catching a faint silvery sheen in the gloom.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

void fragment() {
	ALBEDO = COLOR.rgb * 0.75;
}
