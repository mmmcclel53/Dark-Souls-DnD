// Darkens the screen from the edges inward. `radius` is where the dark starts, measured
// from the centre in half-screen-heights so the shape stays round on a widescreen viewport;
// tween it down to close the world in around the fire.
shader_type canvas_item;

uniform float radius : hint_range(0.0, 2.5) = 1.7;
uniform float softness : hint_range(0.0, 1.5) = 0.75;
uniform vec4 colour : source_color = vec4(0.02, 0.012, 0.006, 1.0);

void fragment() {
	vec2 screen = 1.0 / SCREEN_PIXEL_SIZE;
	float aspect = screen.x / screen.y;
	vec2 centred = (UV - 0.5) * vec2(aspect, 1.0) * 2.0;
	float dist = length(centred);
	float dark = smoothstep(radius - softness, radius, dist);
	COLOR = vec4(colour.rgb, colour.a * dark);
}
