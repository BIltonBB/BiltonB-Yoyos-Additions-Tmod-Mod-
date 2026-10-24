// Metaball threshold shader (ps_2_0). Fuses accumulated radial alpha into a
// smooth organic silhouette via smoothstep. Compiled with EasyXnb -> .xnb.
sampler rtSampler : register(s0);
float lowThreshold;
float highThreshold;
float globalAlpha;

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float density = tex2D(rtSampler, coords).a;
    float mask = smoothstep(lowThreshold, highThreshold, density);
    float3 core = float3(1.0, 1.0, 1.0);
    float3 rim  = float3(0.6, 0.8, 1.0);
    float3 col = lerp(rim, core, saturate(density * 2.0 - 1.0));
    return float4(col, mask * globalAlpha);
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
