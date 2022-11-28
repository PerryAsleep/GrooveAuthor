/*
Shader for recoloring the waveform.
Expects the waveform texture to use the following colors prior to recoloring:
    Background: pure black
    Sparse Color: pure red
    Dense Color: pure green
*/

Texture2D SpriteTexture;

sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

float4 bgColor;
float4 denseColor;
float4 sparseColor;

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR
{
    float4 color = tex2D(SpriteTextureSampler, input.TextureCoordinates);
    if (color.r == 0 && color.g == 0 && color.b == 0)
    {
        color = bgColor;
    }
    else if (color.r == 1 && color.g == 0 && color.b == 0)
    {
        color = sparseColor;
    }
    else if (color.r == 0 && color.g == 1 && color.b == 0)
    {
        color = denseColor;
    }
    return color;
}

technique color
{
    pass Pass1
    {
        PixelShader = compile ps_5_0 PixelShaderFunction();
    }
}