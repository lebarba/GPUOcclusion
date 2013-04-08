/* ######################################################################################################################## */
/* 	      Shader utilizado para dibujar meshes del escenario normalmente utilizando la informacion de Occlusion 			*/
/* ######################################################################################################################## */

float4x4 matWorld;
float4x4 matWorldView;
float4x4 matWorldViewProj;
float4x4 matInverseTransposeWorld; //Matriz Transpose(Invert(World))


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

//Chequear si el occludee es visible (1 = visible, 0 = oculto)
float isVisible()
{
	
	float2 posInTexture;
	
	//Get the u v coordinates from the 1D position index.
	//TODO: Try with 1D textures to avoid this conversion.
	posInTexture.x = (float)(ocludeeIndexInTexture % OccludeeTextureSize) / (float) OccludeeTextureSize;
	posInTexture.y =  (float)(ocludeeIndexInTexture / (float) OccludeeTextureSize) / (float) OccludeeTextureSize;

	//Get the Occlusion Result texture value to see if occludee is visible. Use mipmap level 0.
	return tex2Dlod(occlusionResultSampler, float4(posInTexture.xy, 0.0f, 0.0f)).r;
}


// -----------------------------RenderWithOcclusionEnabled--------------------------------- //


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

   //Ver si es visible (1 = visible, 0 = oculto) 
   if (isVisible() == 1.0f)
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

struct PS_OUTPUT
{
   float4 Color : COLOR0;   
};

float4 SimplestPixelShader(PS_INPUT Input) : COLOR
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

// -------------------------------HeavyRender------------------------------ //

//Output del Vertex Shader
struct VS_OUTPUT_HEAVY 
{
   float4 Position :        POSITION0;
   float2 Texcoord :        TEXCOORD0;
   float2 PosZW :        	TEXCOORD1;
};


//Vertex Shader for testing occlusion.
VS_OUTPUT_HEAVY v_HeavyRender( VS_INPUT Input )
{
   VS_OUTPUT_HEAVY Output;

   //Ver si es visible (1 = visible, 0 = oculto) 
   if (isVisible() == 1.0f)
   {
		//Caso comun: hacer lo propio del Vertex Shader
   
	   //Project position
	   Output.Position = mul( Input.Position, matWorldViewProj);
	   Output.Texcoord = Input.Texcoord;
	   Output.PosZW = Output.Position.zw;
	   
	   return( Output );
    }
	//Assign negative z so the vertex is discarded later.
	else 
	{	
		//Discard vertex by assigning a z value that will be invisible.
		Output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
		Output.Texcoord = 0;
		Output.PosZW = 0;
		return( Output );
	}
}


//Input del Pixel Shader
struct PS_INPUT_HEAVY 
{
   float2 Texcoord : TEXCOORD0;  
   float2 PosZW :        	TEXCOORD1;   
};

struct PS_OUTPUT_HEAVY
{
   float4 Color : COLOR0;   
   float Depth : DEPTH;   
};

PS_OUTPUT_HEAVY p_HeavyRender(PS_INPUT_HEAVY Input)
{
	PS_OUTPUT_HEAVY output;

	/*
	float4 texelColor = 0;
	float cant = 2;
	for(int i = 0; i < cant; i++) {
		for(int j = 0; j < cant; j++) {
			texelColor += tex2Dlod(diffuseMap, float4(Input.Texcoord + float2(i, j), 0, 0));
		}
	}
	*/
	
	float4 texelColor = 0;
	//texelColor += tex2Dlod(diffuseMap, float4(Input.Texcoord + float2(0, 0.1f), 0, 0));
	//texelColor += tex2Dlod(diffuseMap, float4(Input.Texcoord + float2(0.1f, 0), 0, 0));
	//texelColor += tex2Dlod(diffuseMap, float4(Input.Texcoord + float2(1, 0.1f), 0, 0));
	//texelColor += tex2Dlod(diffuseMap, float4(Input.Texcoord + float2(0.1f, 1), 0, 0));
	
	
	float4 realColor = tex2D( diffuseMap, Input.Texcoord );
	float4 finalColor = texelColor * 0.0001f;
	output.Color = realColor + finalColor;
	output.Depth =  Input.PosZW.x / Input.PosZW.y;
	
	return output;
}


technique HeavyRender
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_HeavyRender();
        PixelShader = compile ps_3_0 p_HeavyRender();
    }
}