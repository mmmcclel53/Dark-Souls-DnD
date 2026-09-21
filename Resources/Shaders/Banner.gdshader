// The dark band a banner sits on, with a warm glow pooled behind the words. Drawn in one
// pass so the glow can never be cut off by a texture edge: it falls to nothing well inside
// the rect, and the band's top and bottom soften into whatever is behind.
shader_type canvas_item;

uniform vec4 band_colour : source_color = vec4(0.03, 0.02, 0.012, 0.92);
uniform vec4 glow_colour : source_color = vec4(1.0, 0.6, 0.22, 1.0);
// Fraction of the half-height that is fully dark before the band softens off.
uniform float band_core : hint_range(0.0, 1.0) = 0.55;
uniform float glow_strength : hint_range(0.0, 2.0) = 0.6;
// Glow radii as fractions of the half-width and half-height.
uniform float glow_width : hint_range(0.05, 1.0) = 0.36;
uniform float glow_height : hint_range(0.05, 1.0) = 0.85;

void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float band = 1.0 - smoothstep(band_core, 1.0, abs(p.y));
	float d = length(vec2(p.x / glow_width, p.y / glow_height));
	float glow = exp(-d * d * 2.4) * (1.0 - smoothstep(0.6, 1.0, d)) * glow_strength;

	float band_a = band * band_colour.a;
	float alpha = clamp(band_a + glow, 0.0, 1.0);
	vec3 rgb = (band_colour.rgb * band_a + glow_colour.rgb * glow) / max(alpha, 0.0001);
	COLOR = vec4(rgb, alpha);
}
