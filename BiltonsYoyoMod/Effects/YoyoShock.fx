// BiltonsYoyoMod - screen shockwave filter (ps_2_0).
// Registered as Filters.Scene["BiltonsYoyoMod:YoyoShock"]; driven by
// YoyoScreenShockwaveSystem. Expands ONE thin compression ring from
// uTargetPosition (converted to UV via ScreenShaderData's auto uniforms) with
// radial refraction - never a filled disc, no round bloom. uOpacity carries
// the whole envelope and scales ONLY the distortion + ring light, so the ring
// can die anywhere on screen without dimming the frame (the filter replaces
// the captured screen). Vertex colour is ignored on purpose: EndCapture draws
// the quad tinted with Main.ColorOfTheSkies. Compiled to Effects/YoyoShock.xnb.

sampler uImage0 : register(s0);
float4 uColor;
float uOpacity;
float uProgress;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float2 res = max(uScreenResolution, float2(1.0, 1.0));
    float2 center = (uTargetPosition - uScreenPosition) / res; // UV of the blast
    float aspect = res.x / res.y;
    float2 d = coords - center;
    d.x *= aspect;                                             // circular in pixel space
    float dist = length(d);
    float2 dir = dist > 0.0001 ? d / dist : float2(0.0, 0.0);

    float radius = lerp(0.04, 0.9, uProgress);
    float band = exp(-abs(dist - radius) * 22.0);              // soft compression band
    float lip  = exp(-abs(dist - radius) * 60.0);              // bright leading lip

    // Hard fade at the screen border so wrap-around sampling never shows.
    float edgeFade = saturate(coords.x * 16.0) * saturate((1.0 - coords.x) * 16.0)
                   * saturate(coords.y * 16.0) * saturate((1.0 - coords.y) * 16.0);

    // Refraction: sample outward along the band (envelope-scaled only).
    float2 uvDir = float2(dir.x / aspect, dir.y);
    float2 offs = uvDir * (band * uOpacity * edgeFade * 0.03);
    float3 col = tex2D(uImage0, coords + offs).rgb;

    // Compression lip + faint haze behind the front (envelope-scaled only).
    float3 tint = lerp(uColor.rgb, float3(1.0, 1.0, 1.0), 0.25);
    col += tint * (lip * uOpacity * 0.5);
    col += tint * (band * uOpacity * 0.10);
    return float4(col, 1.0);
}

technique Technique1
{
    pass ShockPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
