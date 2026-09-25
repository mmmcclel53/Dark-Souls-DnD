shader_type canvas_item;

// Clips a TextureRect to a disc and tints the bottom `damage` fraction red, the same damage
// treatment the portrait pane and activation bar use, just inside a circle.
//
// The mask is computed from the vertex position rather than UV: with KEEP_ASPECT_COVERED
// the rect draws a sub-region of the texture, so UV does not span 0..1 across the rect.
uniform vec2 size = vec2(96.0, 96.0);
uniform float damage : hint_range(0.0, 1.0) = 0.0;
uniform vec4 damage_colour : source_color = vec4(0.639, 0.11, 0.09, 0.55);

varying vec2 local;

void vertex() {
	local = VERTEX;
}

void fragment() {
	vec2 p = local / size - vec2(0.5);
	float r = length(p) * 2.0;
	float aa = 2.0 / min(size.x, size.y);
	float mask = 1.0 - smoothstep(1.0 - aa, 1.0, r);

	vec4 col = texture(TEXTURE, UV);
	float y = local.y / size.y;
	if (y > 1.0 - damage) {
		col.rgb = mix(col.rgb, damage_colour.rgb, damage_colour.a);
	}
	COLOR = vec4(col.rgb, col.a * mask);
}
