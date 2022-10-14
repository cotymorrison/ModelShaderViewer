#include "Effect2.fxh"		// "Effect.fxh" contains all declarations

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

	return transpose(tbnMatrix);
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

	float3x3 tbnMatrix = GetTangentMatrix(n, t, b);
	float N = length(n);
	float3 normal = mul(float3(-xgrad, -ygrad, N), transpose(tbnMatrix));
	float h = samples[1][1];

	return float4(normal, h);
}

float GetHeight(float2 tex)
{
	return tex2D(NormalMapSampler, tex).r;
}

float2 GetParallaxTextureCoords(float2 tex, float3 p, float3 n, float3 t, float3 b)
{
	float2 TexCoords = tex;
	float3x3 tbnMatrix = GetTangentMatrix(n, t, b);

	float base = tex2D(NormalMapSampler, tex).r;
	float3 P = mul(p, tbnMatrix) + float3(0,0,base);
	float3 C = mul(camPos, tbnMatrix);
	float3 E = normalize(P - C);

	const int samples = 5;

	//float3 step = E/samples;
	float3 pos = E/2;
	float3 lastpos = E;//float3(tex, base) - E;
	float lastheight = lastpos.z;
	for (int i = 0; i < samples; i++)
	{
		float height = tex2D(NormalMapSampler, tex + pos.xy).r;
		if (pos.z < height)
		{
			float a = lastheight - lastpos.z;
			TexCoords = tex + pos.xy;// + (pos.xyz - lastpos.xy) * a/(pos.z - height + a);
			break;
		}
		else
		{
			lastpos = pos;
			pos += pos/2;
			lastheight = height;
		}
	}

	return TexCoords;
}

// Normal Map Lookup
float4 NormalMapLookup(float2 p)
{
	//float4 worldCoords = mul(p, ViewProj);
	//float2 texCoords = p.xy / p.w;
	//texCoords = float2(texCoords.x + 1, 1 - texCoords.y)/2;

	float2 texCoords = float2(p.x/1920, p.y/1080);

	float4 normal = tex2D(NormalMapSampler, texCoords);
	//normal = MultiSample(NormalMapSampler, texCoords, pixel_width/2, pixel_height/2);
	normal = float4(normal.xyz*2 - 1, normal.w);

	return normal;
}


// Irradiance Functions
//------------------------------------------------------------------------

// Get Light Irradiance using Light struct
BasicLight GetIrradiance(uniform Light l, float3 p)
{
	BasicLight output;

	if (l.Type == 1)	// Directional
	{
		output.l = normalize(-l.Direction);
		output.EL = l.Intensity * l.Color;
	}
	else 
	{
		float r = length(l.Position - p);
		float falloff = 1.0f/(l.a0 + l.a1*r + l.a2*r*r);

		output.l = normalize(l.Position - p);

		if (l.Type == 2)	// Point
			output.EL = l.Intensity * l.Color * falloff;
		else if (l.Type == 3)	// Spot
		{
			float3 s = normalize(l.Direction);
			float lds = dot(-output.l,s);

			if (lds >= l.cosphi)
				output.EL = l.Intensity * l.Color * falloff;
			else if (l.costheta < lds && lds < l.cosphi)
				output.EL = l.Intensity * l.Color * pow(abs((lds - l.costheta)/(l.cosphi - l.costheta)), l.spotfactor) * falloff;
			else
				output.EL = 0;
		}
		else	// None
		{
			output.l = 0;
			output.EL = 0;
		}
	}

	return output;
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
	float2 Vpos		: VPOS;
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

PixelShaderOutput PS_ParallaxOcclusion(PixelShaderInput input)
{
	PixelShaderOutput output;

	float3x3 tbnMatrix = GetTangentMatrix(input.Normal, input.Tangent, input.Binormal);

	float4 n;
	float4 p = input.WorldPos;
	float3 v = mul(normalize(camPos - input.WorldPos), tbnMatrix);
	float height = 0;
	if (normal_mapping && normal_mapped)
	{
		//n = NormalMapLookup(input.Vpos);
		//n = TransformNormal(input.Normal, input.Tangent, input.Binormal, input.TexCoord);
		//height = n.z * scale - bias;
		//n = float4(n.xyz, 0);
		//p = p + float4(mul(height*float3(v.xy, 0), transpose(tbnMatrix)), 0);
	}
	else
		n = float4(normalize(input.Normal), 0);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	//for (int i = 0; i < MAXLIGHTS; i++)
	//{
	//	if (i < lightCount)
	//	{
			BasicLight light = GetIrradiance(lights[0], p);
			l[0] = light.l;
			EL[0] = light.EL;
			//light = GetIrradiance(lights[1], p);
			l[1] = 0;
			EL[1] = 0;
	//	}
	//	else
	//	{
	//		l[i] = 0;
	//		EL[i] = 0;
	//	}
	//}

	float2 texCoord = input.TexCoord;
	if (normal_mapping && normal_mapped)
	{
		texCoord = GetParallaxTextureCoords(texCoord, p, input.Normal, input.Tangent, input.Binormal);
		n = TransformNormal(input.Normal, input.Tangent, input.Binormal, texCoord);
		height = n.w * scale - bias;
		n = float4(n.xyz, 0);
	}

	float4 color = float4(Shade(p, n, texCoord, Kd, Ks, m, lightCount, l, EL), 1);

	//if (shadow_mapping)
		//for (int i = 0; i < directionalCount % (MAXLIGHTS + 1); i++)
			//color = ShadowMapLookup(input.WorldPos, color, directional[0].ShadowChannel);

	output.Color = color;

	return output;
}

PixelShaderOutput PS_Parallax(PixelShaderInput input)
{
	PixelShaderOutput output;

	float3x3 tbnMatrix = GetTangentMatrix(input.Normal, input.Tangent, input.Binormal);

	float4 n;
	float4 p = input.WorldPos;
	float3 v = mul(normalize(camPos - input.WorldPos), tbnMatrix);
	float height = 0;
	if (normal_mapping && normal_mapped)
	{
		//n = NormalMapLookup(input.Vpos);
		n = TransformNormal(input.Normal, input.Tangent, input.Binormal, input.TexCoord);
		height = n.w * scale - bias;
		//n = float4(n.xyz, 0);
		//p = p + float4(mul(height*float3(v.xy, 0), transpose(tbnMatrix)), 0);
	}
	else
		n = float4(normalize(input.Normal), 0);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (int i = 0; i < MAXLIGHTS; i++)
	{
		if (i < lightCount)
		{
			BasicLight light = GetIrradiance(lights[i], p);
			l[i] = light.l;
			EL[i] = light.EL;
		}
		else
		{
			l[i] = 0;
			EL[i] = 0;
		}
	}

	float2 texCoord = input.TexCoord;
	if (normal_mapping && normal_mapped)
	{
		//float3 h = normalize(v + mul(l[0], tbnMatrix));
		//float3 h = mul(normalize(normalize(camPos - input.WorldPos) + normalize(l[0])), tbnMatrix);
		texCoord += (height * v.xy);

		n = TransformNormal(input.Normal, input.Tangent, input.Binormal, texCoord);
		//height = n.w * scale - bias;
		n = float4(n.xyz, 0);
		p = p + float4(mul(height*float3(v.xy, 0), transpose(tbnMatrix)), 0);
	}

	float4 color = float4(Shade(p, n, texCoord, Kd, Ks, m, lightCount, l, EL), 1);

	//if (shadow_mapping)
		//for (int i = 0; i < directionalCount % (MAXLIGHTS + 1); i++)
			//color = ShadowMapLookup(input.WorldPos, color, directional[0].ShadowChannel);

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
		//PixelShader = compile ps_3_0 PS_Parallax();
		PixelShader = compile ps_3_0 PS_ParallaxOcclusion();
		AlphaBlendEnable = false;
    }
    //pass Pass2
    //{
		//VertexShader = compile vs_3_0 VertexShaderFunction();
	//	PixelShader = compile ps_3_0 PS_PointLight();
	//	AlphaBlendEnable = true;
    //}
    //pass Pass3
    //{
	//	//VertexShader = compile vs_3_0 VertexShaderFunction();
	//	PixelShader = compile ps_3_0 PS_SpotLight();
	//	AlphaBlendEnable = true;
    //}
}