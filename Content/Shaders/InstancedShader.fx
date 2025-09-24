// InstancedShader.fx - HLSL shader for hardware instancing in MonoGame/XNA
// Save this as Content/Effects/InstancedShader.fx

// Matrices
float4x4 View;
float4x4 Projection;

// Lighting
float3 LightDirection = float3(0.5, -1.0, 0.5);
float3 LightColor = float3(1, 1, 1);
float3 AmbientColor = float3(0.3, 0.3, 0.3);

// Fog
float FogStart = 200;
float FogEnd = 800;
float3 FogColor = float3(0.5, 0.7, 0.9);

// Texture
texture Texture;
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

// Vertex shader input structures
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    
    // Instance data
    float4 InstanceTransform0 : TEXCOORD1;  // World matrix row 0
    float4 InstanceTransform1 : TEXCOORD2;  // World matrix row 1
    float4 InstanceTransform2 : TEXCOORD3;  // World matrix row 2
    float4 InstanceTransform3 : TEXCOORD4;  // World matrix row 3
    float4 InstanceColor : COLOR1;          // Instance tint color
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float3 WorldPos : TEXCOORD2;
    float4 Color : COLOR0;
    float FogFactor : TEXCOORD3;
};

// Vertex Shader
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    // Reconstruct world matrix from instance data
    float4x4 worldMatrix = float4x4(
        input.InstanceTransform0,
        input.InstanceTransform1,
        input.InstanceTransform2,
        input.InstanceTransform3
    );    
    // Transform position
    float4 worldPosition = mul(input.Position, worldMatrix);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    output.WorldPos = worldPosition.xyz;
    
    // Transform normal
    output.Normal = normalize(mul(input.Normal, (float3x3)worldMatrix));
    
    // Pass through texture coordinates and color
    output.TexCoord = input.TexCoord;
    output.Color = input.InstanceColor;
    
    // Calculate fog
    float distance = length(viewPosition.xyz);
    output.FogFactor = saturate((FogEnd - distance) / (FogEnd - FogStart));
    
    return output;
}

// Pixel Shader
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    // Sample texture
    float4 textureColor = tex2D(TextureSampler, input.TexCoord);
    
    // Basic lighting
    float3 normal = normalize(input.Normal);
    float lightIntensity = saturate(dot(normal, -normalize(LightDirection)));
    float3 lighting = AmbientColor + LightColor * lightIntensity;
    
    // Combine texture, instance color, and lighting
    float4 finalColor = textureColor * input.Color;
    finalColor.rgb *= lighting;
    
    // Apply fog
    finalColor.rgb = lerp(FogColor, finalColor.rgb, input.FogFactor);
    
    return finalColor;
}

// Techniques
technique Instanced
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

// Fallback technique for non-instanced rendering
float4x4 World;
float4 TintColor = float4(1, 1, 1, 1);

struct BasicVertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput BasicVertexShaderFunction(BasicVertexShaderInput input)
{
    VertexShaderOutput output;
    
    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    output.WorldPos = worldPosition.xyz;
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    output.Color = TintColor;
    
    float distance = length(viewPosition.xyz);
    output.FogFactor = saturate((FogEnd - distance) / (FogEnd - FogStart));
    
    return output;
}

technique Basic
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 BasicVertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}