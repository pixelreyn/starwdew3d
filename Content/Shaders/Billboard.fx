float4x4 World;
float4x4 View;
float4x4 Projection;  // Fixed: was "parameter name="Projection;"

texture Texture;
sampler TextureSampler = sampler_state {
    Texture = <Texture>;
};

struct VertexShaderInput {
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput {
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input) {
    VertexShaderOutput output;
    
    // Billboard transformation - always face camera
    float4 worldPos = mul(input.Position, World);
    float4 viewPos = mul(worldPos, View);
    
    // Keep the billboard facing the camera
    viewPos.x += input.Position.x;
    viewPos.y += input.Position.y;
    
    output.Position = mul(viewPos, Projection);
    output.TexCoord = input.TexCoord;
    
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0 {
    float4 color = tex2D(TextureSampler, input.TexCoord);
    clip(color.a - 0.5); // Alpha testing
    return color;
}

technique Billboard {
    pass Pass1 {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}