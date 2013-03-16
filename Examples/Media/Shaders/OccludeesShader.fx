/* ######################################################################################################################## */
/* 	      Shader utilizado para dibujar meshes del escenario normalmente utilizando la informacion de Occlusion 			*/
/* ######################################################################################################################## */

float4x4 matWorld;
float4x4 matWorldView;
float4x4 matWorldViewProj;


//Textura utilizada por el Pixel Shader
texture diffuseMap_Tex;
sampler2D diffuseMap = sampler_state
{
   Texture = (diffuseMap_Tex);
   ADDRESSU = WRAP;
   ADDRESSV = WRAP;
   MINFILTER = LINEAR;
   MAGFILTER = LINEAR;
   MIPFILTER = LINEAR;
};




// --------------------------- VARIABLES DE OCCLUSION -------------------------------------- //


//The current occludee number in the array of occludees.
int ocludeeIndexInTexture;

//The texture side size. Default = 64 x 64.
int OccludeeTextureSize = 64;

//Informacion de occlusion del OcclusionEngine
texture2D occlusionResult;
sampler occlusionResultSampler = sampler_state
{
    Texture = <occlusionResult>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Chequear si el occludee es visible
float isOccluded()
{
	
	float2 posInTexture;
	
	//Get the u v coordinates from the 1D position index.
	//TODO: Try with 1D textures to avoid this conversion.
	posInTexture.x = (float)(ocludeeIndexInTexture % OccludeeTextureSize) / (float) OccludeeTextureSize;
	posInTexture.y =  (float)(ocludeeIndexInTexture / (float) OccludeeTextureSize) / (float) OccludeeTextureSize;

	//Get the Occlusion Result texture value to see if occludee is visible. Use mipmap level 0.
	return tex2Dlod(occlusionResultSampler, float4(posInTexture.xy, 0.0f, 0.0f)).r;
}


// ----------------------------------------------------------------------------------------------- //


//Input del Vertex Shader
struct VS_INPUT 
{
   float4 Position : POSITION0;
   float3 Normal :   NORMAL0;
   float4 Color : COLOR;
   float2 Texcoord : TEXCOORD0;
};

//Output del Vertex Shader
struct VS_OUTPUT 
{
   float4 Position :        POSITION0;
   float2 Texcoord :        TEXCOORD0;
};


//Vertex Shader for testing occlusion.
VS_OUTPUT VertDoOcclusionDiscard( VS_INPUT Input )
{
   VS_OUTPUT Output;

   //If the value is 0 then project the vertex and let it continue through out the pipeline. 
   if (isOccluded() == 0.0f)
   {
		//Caso comun: hacer lo propio del Vertex Shader
   
	   //Project position
	   Output.Position = mul( Input.Position, matWorldViewProj);
	   Output.Texcoord = Input.Texcoord;

	   return( Output );
    }
	//Assign negative z so the vertex is discarded later.
	else 
	{	
		//Discard vertex by assigning a z value that will be invisible.
		Output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
		Output.Texcoord = 0;
		return( Output );
	}
}


//Input del Pixel Shader
struct PS_INPUT 
{
   float2 Texcoord : TEXCOORD0;   
};

float4 SimplestPixelShader(PS_INPUT Input) : COLOR0
{
	return tex2D( diffuseMap, Input.Texcoord );
}


technique RenderWithOcclusionEnabled
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertDoOcclusionDiscard();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}