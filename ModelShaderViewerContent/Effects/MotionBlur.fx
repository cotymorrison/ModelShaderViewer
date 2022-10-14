texture OldScene;
sampler ScreenSOld = sampler_state
{
	Texture = <OldScene>;	
};

texture NewScene;
sampler ScreenSNew = sampler_state
{
	Texture = <NewScene>;	
};

texture AccumulationBuffer;
sampler ScreenSBuffer = sampler_state
{
	Texture = <AccumulationBuffer>;	
};


float4 PixelShaderFunction0(float4 color : COLOR0, float2 tex : TEXCOORD0) : SV_TARGET
{
	//return float4(0,0,0,1);
	//return tex2D(ScreenSNew, tex);
	//return tex2D(ScreenSBuffer, tex);
	return tex2D(ScreenSBuffer, tex) + tex2D(ScreenSNew, tex) - color;
}


float4 PixelShaderFunction(float4 color : COLOR0, float2 tex : TEXCOORD0) : SV_TARGET
{
	//return color;
	//return float4(0.1,0.1,0.1,1);
	//return tex2D(ScreenSBuffer, tex);
	//return float4(tex2D(ScreenSNew, tex).rgb, 1);
	//return float4(tex2D(ScreenSOld, tex).rgb, 1);
	return tex2D(ScreenSBuffer, tex)/2 + tex2D(ScreenSNew, tex) - tex2D(ScreenSOld, tex) + color;
}


technique Technique1
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 PixelShaderFunction0();
    }
}


technique Technique2
{
    pass Pass1
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
