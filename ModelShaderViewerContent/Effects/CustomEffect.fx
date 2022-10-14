// Declarations
//------------------------------------------------------------------------
static const int MAXLIGHTS = 5;
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

struct PointLight
{
	float3 Position;
	float4 Intensity;
};

struct DirectionalLight
{
	float3 Direction;
	float4 Color;
	float DiffuseIntensity;
	float AmbientIntensity;
};

SpotLight spotlight;
PointLight pointlight;
DirectionalLight directional;


// Camera
//------------------------------------------------------------------------
struct Camera
{
	float3 Position;
	float3 Look;
};
Camera camera;


// Materials
//------------------------------------------------------------------------
struct Material
{
	float Smoothness;
};
Material material;


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
			 float3 l,
			 float3 EL)
{
	float3 h = normalize(v + l);
	float cosTh = saturate(dot(n, h));
	float cosTi = saturate(dot(n, l));
	float3 Lo = (Kd*tex2D(diffuseSampler, t) + Ks*pow(cosTh, m)*tex2D(specularSampler, t)) * EL * cosTi;
	return Lo;
}

// Normal Mapping
void MapNormals(float3 n, float3 t, float3 b, float2 tex, out float3 normal)
{
	float3x3 tbnMatrix = float3x3(	t.x, t.y, t.z,
									b.x, b.y, b.z,
									n.x, n.y, n.z);

	float3 N = tex2D(bumpSampler, tex).rgb*2 - 1;
	normal = normalize(n + mul(N, transpose(tbnMatrix)));
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
	float2 Texture	: TEXCOORD0;
	float2 ScreenPos: VPOS;
};


// Pixel Shaders
//------------------------------------------------------------------------
// Point Light Pixel Shader
float4 PS_PointLight(PixelShaderInput input) : COLOR0
{
	float3 n = normalize(input.Normal);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		MapNormals(n, normalize(input.Tangent), normalize(input.Binormal), input.Texture, n);

	// Calculate light intensity
	float r = length(pointlight.Position.xyz - input.WorldPos.xyz);
	float3 l = float4(pointlight.Position - input.WorldPos, 0) / r;
	float ldn = dot(l,n);

	// Compute the irradiance
	float falloff = 1 / (3*r);
	float4 EL = pointlight.Intensity * falloff;
	float3 Color = Shade(v, normalize(input.Normal), input.Texture, Kd, Ks, m, l, EL);

	return float4(Color, 1);
}

// Spot Light Pixel Shader
float4 PS_SpotLight(PixelShaderInput input) : COLOR0
{
	float3 n = normalize(input.Normal);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		MapNormals(n, normalize(input.Tangent), normalize(input.Binormal), input.Texture, n);
	
	// Calculate light intensity
	float r = length(spotlight.Position.xyz - input.WorldPos.xyz);
	float3 l = float4(spotlight.Position - input.WorldPos, 0) / r;
	float3 s = normalize(spotlight.Direction);
	float ldn = dot(l,n);

	// Compute spot light intensity
	float4 I;
	float lds = dot(-l,s);
	float cosp = cos(spotlight.Penumbra);
	float cosu = cos(spotlight.Umbra);
	if (lds >= cosp)
		I = spotlight.Intensity;
	else if (cosu < lds && lds < cosp)
		I = spotlight.Intensity * pow((lds - cosu)/(cosp - cosu), spotlight.Exponent);
	else
		I = 0;

	// Compute the irradiance
	float falloff;
	if (r <= spotlight.rstart)
		falloff = 1;
	else if (spotlight.rstart < r && r < spotlight.rend)
		falloff = (spotlight.rend-r)/(spotlight.rend-spotlight.rstart);
	else
		falloff = 0;

	float3 EL = I * falloff;
	float3 Color = Shade(v, normalize(input.Normal), input.Texture, Kd, Ks, m, l, EL);

	return float4(Color, 1);
}

// Directional Light Pixel Shader
float4 PS_DirectionalLight(PixelShaderInput input) : COLOR0
{
	float3 n = normalize(input.Normal);
	float3 l = normalize(-directional.Direction);
	float3 v = normalize(camera.Position - input.WorldPos);
	
	// Perform normal mapping
	if (normal_mapping)
		MapNormals(n, normalize(input.Tangent), normalize(input.Binormal), input.Texture, n);

	// Set lights
	float3 EL = directional.DiffuseIntensity*directional.Color;
	float3 Color = Shade(v, n, input.Texture, Kd, Ks, m, l, EL) + directional.AmbientIntensity*tex2D(diffuseSampler,input.Texture);

	return float4(Color, 1);
}


// Techniques
//------------------------------------------------------------------------
technique PointLightShading
{
    pass Pass1
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_DirectionalLight();
		ZEnable = true;
		ZWriteEnable = true;
		ZFunc = LessEqual;
		AlphaBlendEnable = false;
    }
    pass Pass2
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PS_SpotLight();
		AlphaBlendEnable = true;
    }
    pass Pass3
    {
		//VertexShader = compile vs_3_0 VertexShaderFunction();
		//PixelShader = compile ps_3_0 PS_PointLight();
		//AlphaBlendEnable = true;
    }
}
