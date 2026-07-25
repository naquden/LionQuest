#ifndef STYLIZED_TERRAIN_TINT_INCLUDED
#define STYLIZED_TERRAIN_TINT_INCLUDED

CBUFFER_START(_StylizedTerrain)
    float _MacroNoiseScale;
    float _MacroNoiseStrength;
    float4 _MacroTintA;
    float4 _MacroTintB;
    float _SlopeDarkening;
    float _HeightLightening;
    float _HeightRange;
CBUFFER_END

float StylizedHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
}

float StylizedValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = StylizedHash(i);
    float b = StylizedHash(i + float2(1.0, 0.0));
    float c = StylizedHash(i + float2(0.0, 1.0));
    float d = StylizedHash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Two octaves is enough to break up tiling without reading as noise itself.
float StylizedMacroNoise(float2 p)
{
    return StylizedValueNoise(p) * 0.65 + StylizedValueNoise(p * 2.17) * 0.35;
}

half3 ApplyStylizedTint(half3 albedo, float3 positionWS)
{
    float scale = max(_MacroNoiseScale, 0.0001);
    float n = StylizedMacroNoise(positionWS.xz / scale);

    half3 tint = lerp(_MacroTintA.rgb, _MacroTintB.rgb, n);
    albedo *= lerp(half3(1.0, 1.0, 1.0), tint, _MacroNoiseStrength);

    // Fake AO in crevices, sun bleach on exposed tops.
    float heightT = saturate(positionWS.y / max(_HeightRange, 0.0001));
    albedo *= 1.0 - _SlopeDarkening * (1.0 - heightT);
    albedo += _HeightLightening * heightT * albedo;

    return albedo;
}

#endif
