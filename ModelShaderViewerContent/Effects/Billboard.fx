//------- XNA interface --------
float4x4 World;
float4x4 View;
float4x4 Projection;
float3 CamPos;
float3 AllowedRotDir = float3 (0, 1, 0);
float HalfWidth = 0.5;
float HalfHeight = 0.5;

//------- Texture Samplers --------
Texture BillboardTexture;

sampler textureSampler = sampler_state { texture = <BillboardTexture> ; magfilter = LINEAR; minfilter = LINEAR; mipfilter=LINEAR; AddressU = CLAMP; AddressV = CLAMP;};
struct BBVertexToPixel
{
    float4 Position : POSITION;
    float2 TexCoord    : TEXCOORD0;
};
struct BBPixelToFrame
{
    float4 Color     : COLOR0;
};

//------- Technique: CylBillboard --------
BBVertexToPixel CylBillboardVS(float3 inPos: POSITION0, float2 inTexCoord: TEXCOORD0)
{
    BBVertexToPixel Output;

    float3 center = mul(inPos, World);
    float3 eyeVector = center - CamPos;

    float3 upVector = normalize(AllowedRotDir);
    float3 sideVector = normalize(cross(eyeVector,upVector));

    float3 finalPosition = center;
    finalPosition += HalfWidth*(2*inTexCoord.x-1)*sideVector;
    finalPosition += HalfHeight*(1-2*inTexCoord.y)*upVector;

    float4 finalPosition4 = float4(finalPosition, 1);

    float4x4 preViewProjection = mul (View, Projection);
    Output.Position = mul(finalPosition4, preViewProjection);

    Output.TexCoord = inTexCoord;

    return Output;
}

BBPixelToFrame BillboardPS(BBVertexToPixel PSIn) : COLOR0
{
    BBPixelToFrame Output;
    Output.Color = tex2D(textureSampler, PSIn.TexCoord);

    return Output;
}

technique CylBillboard
{
    pass Pass0
    {        
        VertexShader = compile vs_2_0 CylBillboardVS();
        PixelShader = compile ps_2_0 BillboardPS();        
    }
}