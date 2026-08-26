struct UiPushData
{
    float4 Rect;
    float2 Viewport;
    float2 Padding;
    float4 Color;
};

[[vk::push_constant]] ConstantBuffer<UiPushData> Push;

struct UiVertex
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

UiVertex VSMain(uint vertexId : SV_VertexID)
{
    static const float2 Corners[6] =
    {
        float2(0.0, 0.0),
        float2(1.0, 0.0),
        float2(1.0, 1.0),
        float2(0.0, 0.0),
        float2(1.0, 1.0),
        float2(0.0, 1.0)
    };

    float2 pixel = Push.Rect.xy + Corners[vertexId] * Push.Rect.zw;
    float2 ndc = float2(
        pixel.x / Push.Viewport.x * 2.0 - 1.0,
        pixel.y / Push.Viewport.y * 2.0 - 1.0);
    UiVertex output;
    output.Position = float4(ndc, 0.0, 1.0);
    output.Color = Push.Color;
    return output;
}

float4 PSMain(UiVertex input) : SV_Target0
{
    return input.Color;
}
