// Simple glow pulse shader for BiltonsYoyoMod (ps_2_0).
// Used via MiscShaderData / DrawWithShader. Falls back to no-op on failure.
sampler uImage0 : register(s0);
float4 uColor;
float uTime;
float uIntensity;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(uImage0, coords);
    float pulse = 0.5 + 0.5 * sin(uTime * 3.0);
    c.rgb *= uColor.rgb * (0.6 + 0.4 * pulse) * uIntensity;
    c.a *= uColor.a;
    return c;
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
