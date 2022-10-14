#include "Effect.fxh"		// "Effect.fxh" contains all declarations

//------------------------------------------------------------------------
// Shading Functions
//------------------------------------------------------------------------

// Diffuse Specular Shading
float3 Shade(float3 p,
			 float3 n,
			 float2 t,
			 uniform float3 Kd,
			 uniform float3 Ks,
			 uniform float m,
			 uniform int lightCount,
			 uniform float3 l[MAXLIGHTS],
			 uniform float3 EL[MAXLIGHTS])
{
	float3 V = camPos - p;
	float3 v = normalize(V);
	float3 Lo = float3(0.0f, 0.0f, 0.0f);
	for (int k = 0; k < lightCount; k++)
	{
		float3 h = normalize(v + l[k]);
		float cosTh = saturate(dot(n, h));
		float cosTi = saturate(dot(n, l[k]));

		// Disregard material if the model has diffuse and specular maps
		if (diffuse_mapped)
			Kd = tex2D(DiffuseMapSampler, t);
		if (specular_mapped)
			Ks = tex2D(SpecularMapSampler, t);
			
		Lo += (Kd + Ks*pow(cosTh, m)) * EL[k] * cosTi;
	}

	// Apply Texture
	if (texture_mapped)
		Lo *= tex2D(TextureMapSampler, t);

	// Apply Fog
	//float z = length(V);
	//float f = saturate((zFar - z)/(zFar - zNear));
	//float f = saturate(exp(-df*z));
	//Lo = f*Lo + (1 - f)*FogColor;

	return Lo;
}

// Shadow Map Lookup
float4 ShadowMapLookup(float3 p, float4 color, int channel)
{
	if (channel < 0 || channel >= shadow_map_channels)
		return color;

	float4 lightViewPos = mul(float4(p, 1), LightViewProj[channel]);

	float2 shadowTex = lightViewPos.xy / lightViewPos.w;
	shadowTex = float2( shadowTex.x + 1, 1 - shadowTex.y)/2;

	float4 shadowDepthMap = tex2D(ShadowMapSampler, shadowTex);
	float pixelDepth = 1 - lightViewPos.z/lightViewPos.w;

	float shadowDepth = 0;
	if (channel == 0)
		shadowDepth = shadowDepthMap.r;
	else if (channel == 1)
		shadowDepth = shadowDepthMap.g;
	else if (channel == 2)
		shadowDepth = shadowDepthMap.b;

	float bias = 0.005;
	if (shadowDepth > pixelDepth + bias)
		color = float4(0,0,0,1);

	return color;
}

float4 MultiSample(sampler s, float2 c, float w, float h)
{
	return (4*tex2D(s, c) + tex2D(s, float2(c.x - w, c.y - h)) + tex2D(s, float2(c.x + w, c.y - h)) + tex2D(s, float2(c.x - w, c.y + h)) + tex2D(s, float2(c.x + w, c.y + h)))/8;
}

float3x3 GetSampleMatrix(sampler s, float2 c, float w, float h)
{
	return float3x3(tex2D(s, float2(c.x - w, c.y - h)).r,	tex2D(s, float2(c.x, c.y - h)).r,	tex2D(s, float2(c.x + w, c.y - h)).r,
					tex2D(s, float2(c.x - w, c.y)).r,		tex2D(s, float2(c.x, c.y)).r,		tex2D(s, float2(c.x + w, c.y)).r,
					tex2D(s, float2(c.x - w, c.y + h)).r,	tex2D(s, float2(c.x, c.y + h)).r,	tex2D(s, float2(c.x + w, c.y + h)).r);
}

// Tangent Space Matrix
float3x3 GetTangentMatrix(float3 n, float3 t, float3 b)
{
	n = normalize(n);
	t = normalize(t);
	b = normalize(b);

	float3x3 tbnMatrix = float3x3(	t.x, t.y, t.z,
									b.x, b.y, b.z,
									n.x, n.y, n.z);

	return tbnMatrix;
}

// Normal Mapping
float4 TransformNormal(float3 n, float3 t, float3 b, float2 tex)
{
	if (!normal_mapped)
		return float4((normalize(n)+1)/2, 0);

	// Sobel Filter
	float3x3 samples = GetSampleMatrix(NormalMapSampler, tex, pixel_width/4, pixel_height/4);
	float xgrad = -samples[0][0] - 2*samples[1][0] - samples[2][0] + samples[0][2] + 2*samples[1][2] + samples[2][2];
	float ygrad = -samples[0][0] - 2*samples[0][1] - samples[0][2] + samples[2][0] + 2*samples[2][1] + samples[2][2];
	float h = samples[1][1];

	n = normalize(n);
	float3 u = normalize(b);
	float3 v = normalize(t);

	float3 normal = normalize(n + u*xgrad + v*ygrad);

	return float4((normal+1)/2, h);
}

// Normal Map Lookup
float4 NormalMapLookup(float4 p)
{
	//float4 worldCoords = mul(p, ViewProj);
	float2 texCoords = p.xy / p.w;
	texCoords = float2(texCoords.x + 1, 1 - texCoords.y)/2;

	float4 normal = tex2D(NormalMapSampler, texCoords);
	//normal = MultiSample(NormalMapSampler, texCoords, pixel_width/2, pixel_height/2);
	normal = float4(normal.xyz*2 - 1, normal.w);

	return normal;
}


// Irradiance Functions
//------------------------------------------------------------------------

// Point Light Irradiance Calculation
BasicLight PointLightIrradiance(uint index, float3 p)
{
	BasicLight output;

	// Calculate light intensity
	float r = length(pointlight[index].Position - p);
	output.l = normalize(pointlight[index].Position - p);

	// Compute the irradiance
	float falloff = 1 / r;
	output.EL = pointlight[index].Color * falloff;

	return output;
}

// Spot Light Irradiance Calculation
BasicLight SpotLightIrradiance(uint index, float3 p)
{
	BasicLight output;

	// Calculate light intensity
	float3 L = spotlight[index].Position - p;
	float r = length(L);
	output.l = L/r;
	float3 s = normalize(spotlight[index].Direction);

	// Compute spot light intensity
	float3 I;
	float lds = dot(-output.l,s);
	float cosp = cos(spotlight[index].Penumbra);
	float cosu = cos(spotlight[index].Umbra);
	if (lds >= cosp)
		I = spotlight[index].Color;
	else if (cosu < lds && lds < cosp)
		I = spotlight[index].Color * pow(abs((lds - cosu)/(cosp - cosu)), spotlight[index].Exponent);
	else
		I = 0;

	// Compute the irradiance
	float falloff;
	if (r <= spotlight[index].rstart)
		falloff = 1;
	else if (spotlight[index].rstart < r && r < spotlight[index].rend)
		falloff = (spotlight[index].rend - r)/(spotlight[index].rend - spotlight[index].rstart);
	else
		falloff = 0;

	output.EL = I * falloff;

	return output;
}

// Directional Light Irradiance Calculation
BasicLight DirectionalIrradiance(uint index)
{
	BasicLight output;
	output.l = normalize(-directional[index].Direction);
	output.EL = directional[index].Intensity*directional[index].Color;

	return output;
}

// Ambience
float4 AmbientIrradiance(float2 t)
{
	float3 Ambience = 0;

	for (int k = 0; k < directionalCount; k++)
		Ambience += directional[k].Color * directional[k].Ambience;

	if (texture_mapped)
		Ambience *= tex2D(TextureMapSampler, t);

	return float4(Ambience, 1);
}


//------------------------------------------------------------------------
// Vertex Shading
//------------------------------------------------------------------------

// Vertex Structs
struct VertexShaderInput
{
    float4 Position : POSITION0;
	float3 Normal	: NORMAL0;
	float3 Binormal	: BINORMAL0;
	float3 Tangent	: TANGENT0;
    float2 Texture	: TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position	: POSITION0;
	float4 ScreenPos: TEXCOORD0;
	float4 WorldPos	: TEXCOORD1;
	float3 Normal	: TEXCOORD2;
	float3 Tangent	: TEXCOORD3;
	float3 Binormal	: TEXCOORD4;
	float2 Texture	: TEXCOORD5;
};

struct ShadowVertexOutput
{
    float4 Position	: POSITION0;
	float4 WorldPos	: TEXCOORD0;
};


// Vertex Shaders
//------------------------------------------------------------------------
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

	// Calculate positions
    output.WorldPos = mul(input.Position, World);
    output.Position = mul(input.Position, WorldViewProj);
    output.ScreenPos = output.Position;

	// Compute normal data
	output.Normal = normalize(mul(float4(input.Normal, 0), WorldInverseTranspose));
	output.Tangent = normalize(mul(float4(input.Tangent, 0), WorldInverseTranspose));
	output.Binormal = normalize(mul(float4(input.Binormal, 0), WorldInverseTranspose));

	// Transfer texture coordinates
	output.Texture = input.Texture;

    return output;
}

float4 VS_Slim(float4 Position : POSITION0)	: POSITION0
{
    return mul(Position, WorldViewProj);
}


//------------------------------------------------------------------------
// Pixel Shading
//------------------------------------------------------------------------

// Pixel Structs
//------------------------------------------------------------------------
struct PixelShaderInput
{
	float4 ScreenPos: TEXCOORD0;
	float4 WorldPos	: TEXCOORD1;
	float3 Normal	: TEXCOORD2;
	float3 Tangent	: TEXCOORD3;
	float3 Binormal	: TEXCOORD4;
	float2 TexCoord	: TEXCOORD5;
	float2 Position	: SV_POSITION;
};

struct PixelShaderOutput
{
	float4 Color	: SV_Target;
};


// Pixel Shaders
//------------------------------------------------------------------------

float4 PS_Slim() : SV_Target
{
	return 1;
}

float4 PS_NormalMapping(PixelShaderInput input) : SV_Target
{
	float4 n;

	// Perform normal mapping
	if (normal_mapping)
		n = TransformNormal(input.Normal, input.Tangent, input.Binormal, input.TexCoord);
	else
		n = float4((normalize(input.Normal)+1)/2, 0);

	return n;
}

float4 PS_Shadow(PixelShaderInput input) : SV_Target
{
	float4 pos = mul(input.WorldPos, LightViewProj[shadow_map_channels % (MAXSHADOWCHANNELS + 1)]);
	float depth = 1 - pos.z/pos.w;

	float4 color = float4(0, 0, 0, 1);
	if (shadow_map_channels == 0)
		color.r = depth;
	else if (shadow_map_channels == 1)
		color.g = depth;
	else if (shadow_map_channels == 2)
		color.b = depth;

	return color;
}

PixelShaderOutput PS_Ambient(PixelShaderInput input)
{
	PixelShaderOutput output;

	float4 Ambience = float4(0,0,0,1);

	for (int k = 0; k < directionalCount; k++)
		Ambience += float4(directional[k].Color.rgb*directional[k].Ambience, 0);

	if (texture_mapped)
		Ambience *= tex2D(TextureMapSampler, input.TexCoord);

	output.Color = Ambience;

	return output;
}

PixelShaderOutput PS_Directional(PixelShaderInput input)
{
	PixelShaderOutput output;

	float3x3 tbnMatrix = GetTangentMatrix(input.Normal, input.Tangent, input.Binormal);

	float4 n;
	float4 p = input.WorldPos;
	float3 v = mul(normalize(camPos - input.WorldPos), tbnMatrix);
	float height = 0;
	if (normal_mapping && normal_mapped)
	{
		n = NormalMapLookup(input.ScreenPos);
		height = n.w * scaleBias.x;
		height = clamp(height + scaleBias.y, -height, height);
		n = float4(n.xyz, 0);
		p = p + float4(mul(height*float3(v.xy, 1), transpose(tbnMatrix)), 1);
	}
	else
		n = float4(normalize(input.Normal), 0);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (int k = 0; k < MAXLIGHTS; k++)
	{
		if (k < directionalCount)
		{
			BasicLight light = DirectionalIrradiance(k);
			l[k] = light.l;
			EL[k] = light.EL;
		}
		else
			l[k] = EL[k] = 0;
	}

	float2 texCoord = input.TexCoord;
	if (normal_mapping && normal_mapped)
	{
		float3 h = normalize(v + mul(l[0], tbnMatrix));
		//texCoord += (height * h.xy);
	}

	float4 color = float4(Shade(p, n, texCoord, Kd, Ks, m, directionalCount, l, EL), 1);

	if (shadow_mapping)
		//for (int i = 0; i < directionalCount % (MAXLIGHTS + 1); i++)
			color = ShadowMapLookup(input.WorldPos, color, directional[0].ShadowChannel);

	output.Color = color;

	return output;
}

PixelShaderOutput PS_SpotLight(PixelShaderInput input)
{
	PixelShaderOutput output;

	float3x3 tbnMatrix = GetTangentMatrix(input.Normal, input.Tangent, input.Binormal);

	float4 n;
	float4 p = input.WorldPos;
	float3 v = mul(normalize(camPos - input.WorldPos), tbnMatrix);
	float height = 0;
	if (normal_mapping && normal_mapped)
	{
		n = NormalMapLookup(input.ScreenPos);
		height = n.w * scaleBias.x;
		height = clamp(height + scaleBias.y, -height, height);
		p = p + float4(mul(height*float3(v.xy, 1), transpose(tbnMatrix)), 1);
	}
	else
		n = float4(normalize(input.Normal), 0);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (int k = 0; k < MAXLIGHTS; k++)
	{
		if (k < spotlightCount)
		{
			BasicLight light = SpotLightIrradiance(k, p);
			l[k] = light.l;
			EL[k] = light.EL;
		}
		else
			l[k] = EL[k] = 0;
	}
	
	float2 texCoord = input.TexCoord;
	if (normal_mapping && normal_mapped)
	{
		float3 h = normalize(v + mul(l[0], tbnMatrix));
		texCoord += (height * h.xy);
	}

	float4 color = float4(Shade(p, n, texCoord, Kd, Ks, m, spotlightCount, l, EL), 1);

	if (shadow_mapping)
		//for (int i = 0; i < spotlightCount % (MAXLIGHTS + 1); i++)
			color = ShadowMapLookup(input.WorldPos, color, spotlight[0].ShadowChannel);

	output.Color = color;

	return output;
}

PixelShaderOutput PS_PointLight(PixelShaderInput input)
{
	PixelShaderOutput output;

	float3x3 tbnMatrix = GetTangentMatrix(input.Normal, input.Tangent, input.Binormal);

	float4 n;
	float4 p = input.WorldPos;
	float3 v = mul(normalize(camPos - input.WorldPos), tbnMatrix);
	float height = 0;
	if (normal_mapping && normal_mapped)
	{
		n = NormalMapLookup(input.ScreenPos);
		height = n.w * scaleBias.x;
		height = clamp(height + scaleBias.y, -height, height);
		p = p + float4(mul(height*float3(v.xy, 1), transpose(tbnMatrix)), 1);
	}
	else
		n = float4(normalize(input.Normal), 0);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (int k = 0; k < MAXLIGHTS; k++)
	{
		if (k < pointlightCount)
		{
			BasicLight light = PointLightIrradiance(k, input.WorldPos);
			l[k] = light.l;
			EL[k] = light.EL;
		}
		else
			l[k] = EL[k] = 0;
	}
	
	float2 texCoord = input.TexCoord;
	if (normal_mapping)
	{
		float3 h = normalize(v + mul(l[0], tbnMatrix));
		texCoord += (height * h.xy);
	}

	float4 color = float4(Shade(input.WorldPos, n, texCoord, Kd, Ks, m, pointlightCount, l, EL), 1);

	//if (shadow_mapping)
		//for (int i = 0; i < MAXLIGHTS; i++)
			//color = ShadowMapLookup(input.WorldPos, color, pointlight[i].ShadowChannel);

	output.Color = color;

	return output;
}


// Techniques
//------------------------------------------------------------------------
technique AmbientShading
{
	pass Pass0
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_Ambient();
		AlphaBlendEnable = false;
	}
}

technique ShadowMapShading
{
	pass Pass0
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_Shadow();
	}
}

technique SlimShading
{
	pass Pass0
	{
		VertexShader = compile vs_3_0 VS_Slim();
		PixelShader = compile ps_3_0 PS_Slim();
	}
}
technique NormalMapping
{
	pass Pass0
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_NormalMapping();
		AlphaBlendEnable = false;
	}
}
technique MultiPassShading
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_Directional();
		AlphaBlendEnable = false;
    }
    pass Pass2
    {
		//VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_PointLight();
		AlphaBlendEnable = true;
    }
    pass Pass3
    {
		//VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_SpotLight();
		AlphaBlendEnable = true;
    }
}