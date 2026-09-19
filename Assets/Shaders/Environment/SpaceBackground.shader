// Unity 2022.3 / URP 14. Desktop Windows (shader model 3.0).
// Static direction-space nebula and stars; no textures, scene lights or time input.
Shader "Robot Sokoban/Space Background"
{
    Properties
    {
        _SpaceColor ("Deep Space", Color) = (0.034, 0.045, 0.086, 1)
        _NebulaBlue ("Blue Nebula", Color) = (0.15, 0.24, 0.45, 1)
        _NebulaViolet ("Violet Nebula", Color) = (0.26, 0.15, 0.4, 1)
        _StarColor ("Starlight", Color) = (0.68, 0.8, 1, 1)
        _NebulaIntensity ("Nebula Strength", Range(0, 2)) = 1.15
        _StarIntensity ("Star Brightness", Range(0, 2)) = 0.65
        _StarDensity ("Star Density", Range(0, 0.02)) = 0.0035
        _Exposure ("Background Exposure", Range(0.1, 2)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SpaceColor, _NebulaBlue, _NebulaViolet, _StarColor;
                float _NebulaIntensity, _StarIntensity, _StarDensity, _Exposure, _Rotation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 t = frac(p);
                t = t * t * (3 - 2 * t);
                return lerp(
                    lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), t.x),
                         lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), t.x), t.y),
                    lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), t.x),
                         lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + 1), t.x), t.y), t.z);
            }

            float Cloud(float3 p)
            {
                // Four fixed octaves, in 3D so neither the wrap nor the poles have UV seams.
                float result = 0.55 * Noise(p);
                p = p * 2.03 + float3(7.1, 2.8, 4.6);
                result += 0.27 * Noise(p);
                p = p * 2.01 + float3(3.2, 8.6, 1.7);
                result += 0.12 * Noise(p);
                return result + 0.06 * Noise(p * 2.02);
            }

            float Stars(float3 direction, float scale, float seed)
            {
                float3 p = direction * scale + seed;
                float3 cell = floor(p);
                float random = Hash(cell + 19.7);
                float3 centre = 0.2 + 0.6 * float3(Hash(cell), Hash(cell + 8.3), Hash(cell + 17.1));
                float distanceToStar = length(frac(p) - centre);
                float radius = lerp(0.045, 0.085, Hash(cell + 31.2));
                // Pixel derivatives soften subpixel stars when orbiting or changing resolution.
                float pixelWidth = max(length(fwidth(p)) * 0.65, 0.008);
                float core = 1 - smoothstep(radius, radius + pixelWidth, distanceToStar);
                float glow = exp2(-140 * distanceToStar * distanceToStar) * 0.08;
                return (core + glow) * step(1 - _StarDensity, random);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float angle = radians(_Rotation);
                float sine, cosine;
                sincos(angle, sine, cosine);
                direction.xz = float2(cosine * direction.x - sine * direction.z,
                                     sine * direction.x + cosine * direction.z);

                float mist = Cloud(direction * 3.4 + float3(5.2, 8.7, 13.1));
                // The band crosses the lower hemisphere, where both gameplay cameras look.
                float band = 1 - abs(dot(direction, normalize(float3(0.35, 0.17, 0.92))));
                float cloud = smoothstep(0.3, 0.75, mist * band + 0.1);
                float hue = smoothstep(-0.6, 0.7, dot(direction, float3(0.8, 0.1, -0.5)));
                half3 color = _SpaceColor.rgb * (0.8 + 0.4 * mist);
                color += lerp(_NebulaBlue.rgb, _NebulaViolet.rgb, hue) * cloud * cloud * _NebulaIntensity;
                float stars = Stars(direction, 155, 0) + Stars(direction, 270, 47.3) * 0.42;
                color += _StarColor.rgb * stars * _StarIntensity;
                return half4(color * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
