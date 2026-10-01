// Cuts a texture to a soft-edged circle, for token art scanned with black corners (the
// Aggro token). The centre and radius are in UV, measured against the art.
shader_type canvas_item;

uniform vec2 centre = vec2(0.5, 0.5);
uniform float radius : hint_range(0.0, 0.5) = 0.5;
uniform float feather : hint_range(0.0, 0.1) = 0.01;

void fragment() {
	float d = distance(UV, centre);
	COLOR.a *= 1.0 - smoothstep(radius - feather, radius, d);
}
