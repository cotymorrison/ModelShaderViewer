// Declarations
//------------------------------------------------------------------------
static const uint MAXLIGHTS = 3;
float4x4 World;
float4x4 WorldViewProj;
float4x4 WorldInverseTranspose;
float3 Kd = float3(0,0,0);
float3 Ks = float3(0,0,0);
float m = 0;

bool normal_mapping = true;
bool one = false;
bool two = false;
bool three = false;

// Lights
//------------------------------------------------------------------------
struct PointLight
{
	float3 Position;
	float4 Intensity;
};

struct DirectionalLight
{
	float3 Direction;
	float4 Color;
	float Intensity;
	float Ambience;
};

struct SpotLight
{
	float3 Position;
	float3 Direction;
	float4 Intensity;
	float Penumbra;
	float Umbra;
	float rstart;
	float rend;
	int Exponent;
};

uniform uint spotlightCount = 0;
uniform uint pointlightCount = 0;
uniform uint directionalCount = 0;
uniform SpotLight spotlight[MAXLIGHTS];
uniform PointLight pointlight[MAXLIGHTS];
uniform DirectionalLight directional[MAXLIGHTS];


// Camera
//------------------------------------------------------------------------
struct Camera
{
	float3 Position;
};
Camera camera;


// Textures and Samplers
//------------------------------------------------------------------------
texture DiffuseMap;
sampler diffuseSampler = sampler_state
{
   Texture = <DiffuseMap>;
   MinFilter = Linear;
   MagFilter = Linear;
   MipFilter = Linear;
   AddressU  = Clamp;
   AddressV  = Clamp;
};

texture SpecularMap;
sampler specularSampler = sampler_state
{
   Texture = <SpecularMap>;
   MinFilter = Linear;
   MagFilter = Linear;
   MipFilter = Linear;
   AddressU  = Clamp;
   AddressV  = Clamp;
};

texture NormalMap;
sampler bumpSampler = sampler_state
{
   Texture = <NormalMap>;
   MinFilter = Linear;
   MagFilter = Linear;
   MipFilter = Linear;
   AddressU  = Clamp;
   AddressV  = Clamp;
};


// Pixel Shader Tones
//------------------------------------------------------------------------
float3 WarmColor<
	string UIName = "Gooch Warm Tone";
	string UIWidget = "Color";
> = {1.3f, 0.9f, 0.15f};

float3 CoolColor<
	string UIName = "Gooch Cool Tone";
	string UIWidget = "Color";
> = {0.05f, 0.05f, 0.6f};


//------------------------------------------------------------------------
// Shading Functions
//------------------------------------------------------------------------

// Diffuse Specular Shading
float3 Shade(float3 v,
			 float3 n,
			 float2 t,
			 uniform float3 Kd,
			 uniform float3 Ks,
			 uniform float m,
			 uniform uint lightCount,
			 uniform float3 l[MAXLIGHTS],
			 uniform float3 EL[MAXLIGHTS])
{
	float3 Lo = float3(0.0f, 0.0f, 0.0f);
	for (uint k = 0; k < lightCount; k++)
	{
		float3 h = normalize(v + l[k]);
		float cosTh = saturate(dot(n, h));
		float cosTi = saturate(dot(n, l[k]));
		Lo += (Kd*tex2D(diffuseSampler, t) + Ks*pow(cosTh, m)*tex2D(specularSampler, t)) * EL[k] * cosTi;
	}
	return Lo;
}

// Normal Mapping
float3 TransformNormal(float3 n, float3 t, float3 b, float2 tex)
{
	float3x3 tbnMatrix = float3x3(	t.x, t.y, t.z,
									b.x, b.y, b.z,
									n.x, n.y, n.z);

	float3 N = tex2D(bumpSampler, tex).rgb*2 - 1;
	return normalize(n + mul(N, transpose(tbnMatrix)));
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
    float4 ScreenPos: POSITION0;
	float3 WorldPos	: TEXCOORD1;
	float3 Normal	: TEXCOORD2;
	float3 Tangent	: TEXCOORD3;
	float3 Binormal	: TEXCOORD4;
	float2 Texture	: TEXCOORD0;
};


// Vertex Shaders
//------------------------------------------------------------------------
VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

	// Calculate positions
    output.WorldPos = mul(input.Position, World);
    output.ScreenPos = mul(input.Position, WorldViewProj);

	// Compute normal data
	output.Normal = normalize(mul(float4(input.Normal, 0), WorldInverseTranspose));
	output.Tangent = normalize(mul(float4(input.Tangent, 0), WorldInverseTranspose));
	output.Binormal = normalize(mul(float4(input.Binormal, 0), WorldInverseTranspose));

	// Transfer texture coordinates
	output.Texture = input.Texture;

    return output;
}


//------------------------------------------------------------------------
// Pixel Shading
//------------------------------------------------------------------------

// Pixel Structs
//------------------------------------------------------------------------
struct PixelShaderInput
{
	float3 WorldPos	: TEXCOORD1;
	float3 Normal	: TEXCOORD2;
	float3 Tangent	: TEXCOORD3;
	float3 Binormal	: TEXCOORD4;
	float2 TexCoord	: TEXCOORD0;
	float2 ScreenPos: VPOS;
};

struct PixelShaderOutput
{
	float4 Color	: COLOR0;
};


// Irradiance Functions
//------------------------------------------------------------------------

struct BasicLight
{
	float3 l;
	float3 EL;
};

// Point Light Irradiance Calculation
BasicLight PointLightIrradiance(uint index, float3 p)
{
	BasicLight output;

	// Calculate light intensity
	float r = length(pointlight[index].Position.xyz - p);
	output.l = normalize(pointlight[index].Position.xyz - p);

	// Compute the irradiance
	float falloff = 1 / r;
	output.EL = pointlight[index].Intensity.xyz * falloff;

	return output;
}

// Spot Light Irradiance Calculation
BasicLight SpotLightIrradiance(uint index, float3 p)
{
	BasicLight output;

	// Calculate light intensity
	float r = length(spotlight[index].Position.xyz - p);
	output.l = normalize(spotlight[index].Position.xyz - p);
	float3 s = normalize(spotlight[index].Direction);

	// Compute spot light intensity
	float3 I;
	float lds = dot(-output.l,s);
	float cosp = cos(spotlight[index].Penumbra);
	float cosu = cos(spotlight[index].Umbra);
	if (lds >= cosp)
		I = spotlight[index].Intensity.xyz;
	else if (cosu < lds && lds < cosp)
		I = spotlight[index].Intensity.xyz * pow(abs((lds - cosu)/(cosp - cosu)), spotlight[index].Exponent);
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


// Pixel Shaders
//------------------------------------------------------------------------

PixelShaderOutput PS_Directional(PixelShaderInput input)
{
	PixelShaderOutput output;
	
	float3 n = normalize(input.Normal);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		n = TransformNormal(n, normalize(input.Tangent), normalize(input.Binormal), input.TexCoord);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (uint k = 0; k < MAXLIGHTS; k++)
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

	output.Color = float4(Shade(v, n, input.TexCoord, Kd, Ks, m, directionalCount, l, EL), 1);

	return output;
}

PixelShaderOutput PS_SpotLight(PixelShaderInput input)
{
	PixelShaderOutput output;
	
	float3 n = normalize(input.Normal);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		n = TransformNormal(n, normalize(input.Tangent), normalize(input.Binormal), input.TexCoord);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (uint k = 0; k < MAXLIGHTS; k++)
	{
		if (k < spotlightCount)
		{
			BasicLight light = SpotLightIrradiance(k, input.WorldPos);
			l[k] = light.l;
			EL[k] = light.EL;
		}
		else
			l[k] = EL[k] = 0;
	}

	output.Color = float4(Shade(v, n, input.TexCoord, Kd, Ks, m, spotlightCount, l, EL), 1);

	return output;
}

PixelShaderOutput PS_PointLight(PixelShaderInput input)
{
	PixelShaderOutput output;
	
	float3 n = normalize(input.Normal);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		n = TransformNormal(n, normalize(input.Tangent), normalize(input.Binormal), input.TexCoord);

	// Set lights
	float3 l[MAXLIGHTS];
	float3 EL[MAXLIGHTS];

	for (uint k = 0; k < MAXLIGHTS; k++)
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

	output.Color = float4(Shade(v, n, input.TexCoord, Kd, Ks, m, pointlightCount, l, EL), 1);

	return output;
}


// Techniques
//------------------------------------------------------------------------
technique MultiPassShading
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_Directional();
		ZEnable = true;
		ZWriteEnable = true;
		ZFunc = LessEqual;
		AlphaBlendEnable = false;
    }
    pass Pass3
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_PointLight();
		AlphaBlendEnable = true;
    }
    pass Pass2
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_SpotLight();
		AlphaBlendEnable = true;
    }
}
