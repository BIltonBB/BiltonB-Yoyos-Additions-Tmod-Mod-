// BiltonsYoyoMod - VertexStrip trail shader (vs_2_0 / ps_2_0).
// Used by MiniYoyoFx.RibbonDrawVertexStrip for the 9 mini-boss body trails.
// Self-contained: transforms world positions with its own projection matrix
// (the same matrix the world SpriteBatch uses) so it never depends on ambient
// SpriteBatch/EffectParameter state. No texture sampling (the strip has no art).
// Cross-section shading: dark outer rim (x0.35) -> body colour -> white-hot
// additive-style core. Output is PREMULTIPLIED; the C# side blends with
// One / OneMinusSrcAlpha. Compiled to Effects/YoyoStrip.xnb via EasyXnb.

float4x4 uProjection; // GameViewMatrix.TransformationMatrix * ortho(0, W, H, 0, 0, 1)
float4 uCoreColor;    // hot core colour (trail profile .Core)
float uCoreBoost;     // core brightness; 0 turns the whole core layer off

struct VertexShaderInput
{
    float2 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 Coord    : TEXCOORD0; // .y: 0 = one side of the ribbon, 1 = other side
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 Coord    : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Color : COLOR0;
    float2 Coord : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(float4(input.Position, 0.0, 1.0), uProjection);
    output.Color = input.Color;
    output.Coord = input.Coord;
    return output;
}

float4 PixelShaderFunction(PixelShaderInput input) : COLOR0
{
    float t = abs(input.Coord.y * 2.0 - 1.0); // 0 = centreline, 1 = outer edge
    float a = input.Color.a;
    float3 body = input.Color.rgb;

    // Rounded cross-section: dark rim fading to full body colour to the centre.
    float rim = lerp(0.35, 1.0, saturate(1.0 - t * 1.6));
    float3 col = body * rim;

    // White-hot core (max: adds light, never darkens the body).
    float coreMask = pow(saturate(1.0 - t), 3.0);
    float3 hot = lerp(uCoreColor.rgb, float3(1.0, 1.0, 1.0), 0.25);
    col = max(col, hot * coreMask * uCoreBoost);

    // Premultiplied output; centre sits a touch more opaque.
    float outA = saturate(a * lerp(1.0, 1.18, coreMask * min(uCoreBoost, 1.0)));
    return float4(col * outA, outA);
}

technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
