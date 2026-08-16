using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Godot equivalent of the project-owned Tactics/Battle/BackdropGradient shader.</summary>
public partial class GodotBattleBackdrop : ColorRect
{
    public const string ShaderCode = """
        shader_type canvas_item;
        render_mode unshaded;
        uniform vec4 center_color : source_color = vec4(0.0706, 0.1961, 0.2784, 1.0);
        uniform vec4 edge_color : source_color = vec4(0.0235, 0.0627, 0.0824, 1.0);
        uniform vec4 bottom_color : source_color = vec4(0.0353, 0.1373, 0.1882, 1.0);
        uniform vec2 center_offset = vec2(0.0, 0.02);
        uniform vec2 ellipse_radius = vec2(0.62, 0.55);
        uniform float vignette_strength = 0.45;
        uniform float noise_strength = 0.015;
        uniform float noise_scale = 6.0;

        float hash21(vec2 value) {
            return fract(sin(dot(value, vec2(127.1, 311.7))) * 43758.5453);
        }
        float value_noise(vec2 value) {
            vec2 cell = floor(value);
            vec2 fraction = fract(value);
            vec2 blend = fraction * fraction * (3.0 - 2.0 * fraction);
            float bottom = mix(hash21(cell), hash21(cell + vec2(1.0, 0.0)), blend.x);
            float top = mix(hash21(cell + vec2(0.0, 1.0)), hash21(cell + vec2(1.0)), blend.x);
            return mix(bottom, top, blend.y);
        }
        void fragment() {
            vec2 center = vec2(0.5) + center_offset;
            vec2 radius = max(ellipse_radius, vec2(0.001));
            float radial_blend = smoothstep(0.0, 1.0, length((UV - center) / radius));
            float edge_distance = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
            float vignette = 1.0 - smoothstep(0.0, 0.35, edge_distance);
            float edge_blend = clamp(radial_blend + vignette * vignette_strength, 0.0, 1.0);
            vec3 color = mix(center_color.rgb, edge_color.rgb, edge_blend);
            float bottom_blend = 1.0 - smoothstep(0.0, 0.45, UV.y);
            color = mix(color, bottom_color.rgb, bottom_blend * 0.35);
            float noise = (value_noise(UV * noise_scale) - 0.5) * 2.0;
            COLOR = vec4(clamp(color + noise * noise_strength, vec3(0.0), vec3(1.0)), 1.0);
        }
        """;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var shader = new Shader { Code = ShaderCode };
        Material = new ShaderMaterial { Shader = shader };
    }
}
