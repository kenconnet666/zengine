struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Color : COLOR0;
};

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    static const float2 Positions[3] =
    {
        float2(0.0, -0.6),
        float2(0.6, 0.6),
        float2(-0.6, 0.6)
    };

    static const float3 Colors[3] =
    {
        float3(1.0, 0.2, 0.2),
        float3(0.2, 1.0, 0.2),
        float3(0.2, 0.4, 1.0)
    };

    VertexOutput output;
    output.Position = float4(Positions[vertexId], 0.0, 1.0);
    output.Color = Colors[vertexId];
    return output;
}

float4 PSMain(VertexOutput input) : SV_Target0
{
    return float4(input.Color, 1.0);
}
