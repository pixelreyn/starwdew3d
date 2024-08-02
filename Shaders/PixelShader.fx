

// Maximum number of tiles
#define MAX_TILES 64

// Tile positions and sizes
float3 TilePositions[MAX_TILES];
float3 TileSizes[MAX_TILES];
float4 TileColors[MAX_TILES];

// Number of tiles
int TileCount;

// Pixel input structure
struct PS_INPUT
{
    float4 Position : SV_POSITION;
};

// Pixel shader
float4 PS_Main(PS_INPUT input) : SV_TARGET
{
    float2 pixelPos = input.Position.xy;

    // Check each tile AABB
    //[unroll(MAX_TILES)]
    for (int i = 0; i < TileCount; i++)
    {
        float3 tilePos = TilePositions[i];
        float3 tileSize = TileSizes[i];

        // Calculate tile AABB in screen space
        float2 minBounds = tilePos.xy;
        float2 maxBounds = tilePos.xy + tileSize.xy;

        // Check if the current pixel is within the AABB
        if (pixelPos.x >= minBounds.x && pixelPos.x <= maxBounds.x &&
            pixelPos.y >= minBounds.y && pixelPos.y <= maxBounds.y)
        {
            // Return the tile's color
            return float4(1.0, 0.0, 0.0, 1.0); // Red for demonstration
        }
    }

    // Default background color
    return float4(0.0, 0.0, 0.0, 1.0);
}

technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_3_0 PS_Main();
    }
}