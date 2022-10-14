// Declarations
//------------------------------------------------------------------------
// Maximum Shader Allowances
static const int MAXLIGHTS = 2;
static const int MAXSHADOWCHANNELS = 3;

// Transforms
float4x4 World;
float4x4 ViewProj;
float4x4 WorldViewProj;
float4x4 WorldInverseTranspose;
float4x4 LightViewProj[MAXSHADOWCHANNELS];

// Parallax Mapping
float2 scaleBias = float2(0.002, 0.0);

// Pixel Size
float pixel_height = 0;
float pixel_width = 0;

// Fog Constants
float4 FogColor = float4(0.0, 0.0, 0.0049, 1.0);
float zFar = 50;
float zNear = 0.1;
float df = 0.66;

// Color Constants
float3 Kd = float3(0,0,0);
float3 Ks = float3(0,0,0);
float m = 0;

// Shader Mod Flags
bool normal_mapping = true;
bool shadow_mapping = true;
bool one = false;
bool two = false;
bool three = false;

// Model Flags
bool normal_mapped = false;
bool texture_mapped = false;
bool specular_mapped = false;
bool diffuse_mapped = false;

// Shadow Mapping Channels
int shadow_map_channels = 0;

// Camera Position/Look
float3 camPos;
float3 camLook;


// Lights
//------------------------------------------------------------------------
struct BasicLight
{
	float3 l;
	float3 EL;
};

struct PointLight
{
	float3 Color;
	float3 Position;
};

struct DirectionalLight
{
	float3 Color;
	float3 Direction;
	float Intensity;
	float Ambience;
	int ShadowChannel;
};

struct SpotLight
{
	float3 Color;
	float3 Position;
	float3 Direction;
	float Penumbra;
	float Umbra;
	float rstart;
	float rend;
	int Exponent;
	int ShadowChannel;
};

uniform int spotlightCount = 0;
uniform int pointlightCount = 0;
uniform int directionalCount = 0;
uniform SpotLight spotlight[MAXLIGHTS];
uniform PointLight pointlight[MAXLIGHTS];
uniform DirectionalLight directional[MAXLIGHTS];


// Textures and Samplers
//------------------------------------------------------------------------
Texture2D TextureMap;
sampler TextureMapSampler = 
sampler_state
{
    Texture = <TextureMap>;
    MipFilter = LINEAR;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

Texture2D NormalMap;
sampler NormalMapSampler = 
sampler_state
{
    Texture = <NormalMap>;
    MipFilter = POINT;
    MinFilter = POINT;
    MagFilter = POINT;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

Texture2D DiffuseMap;
sampler DiffuseMapSampler = 
sampler_state
{
    Texture = <DiffuseMap>;
    MipFilter = LINEAR;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

Texture2D SpecularMap;
sampler SpecularMapSampler = 
sampler_state
{
    Texture = <SpecularMap>;
    MipFilter = LINEAR;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

Texture2D ShadowMap;
sampler ShadowMapSampler = 
sampler_state
{
    Texture = <ShadowMap>;
    MipFilter = LINEAR;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

SamplerState LinearClamp
{
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU  = Clamp;
	AddressV  = Clamp;
};