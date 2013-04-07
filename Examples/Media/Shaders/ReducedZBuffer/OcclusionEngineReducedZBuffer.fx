/* ############################################################## */
/* 				Shader utilizado por OcclusionEngine ReducedZBuffer 			  */
/* ############################################################## */


/* ---------------------------------------- VARIABLES COMUNES -------------------------------------------------- */

//Matrices
float4x4 matWorldViewProj;



/* ---------------------------------------- TECHNIQUE: HiZBuffer -------------------------------------------------- */

struct VS_OUTPUT_HiZBuffer  
{
   float4 Position :     POSITION0;
   float2 Depth    :     TEXCOORD0;
};

//Vertex Shader
VS_OUTPUT_HiZBuffer v_HiZBuffer( float4 Position : POSITION0 )
{
   VS_OUTPUT_HiZBuffer Output;
   
   //Project position
   Output.Position = mul( Position, matWorldViewProj);
   
   //Set x = z and y = w.
   Output.Depth.xy = Output.Position.zw;

   return( Output );
}


//Pixel returns the depth in view space.
float4 p_HiZBuffer( float2 depth: TEXCOORD0) : COLOR0
{
	//Return the depth as z / w.
	return  1 - (depth.x / depth.y);
	
	//return float4(10.0f, 0.0f, 0.0f, 0.0f);
}


technique HiZBuffer
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_HiZBuffer();
        PixelShader = compile ps_3_0 p_HiZBuffer();
    }
}


/* ---------------------------------------- TECHNIQUE: OcclusionTestPyramid -------------------------------------------------- */

//The texture side size. Default = 64 x 64.
int OccludeeTextureSize = 64;

//The total valid occludees that are used from the total array capacity.
int maxOccludees;

//Textura con datos de Depth de Occludees
texture2D OccludeeDataTextureDepth;
sampler OccludeeDataDepthSampler = sampler_state
{
    Texture = <OccludeeDataTextureDepth>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Textura con datos de AABB de Occludees
texture2D OccludeeDataTextureAABB;
sampler OccludeeDataAABBSampler = sampler_state
{
    Texture = <OccludeeDataTextureAABB>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//The total number of mipmap levels in the HiZ.
float maxMipLevels;

//HiZBuffer dimensions
float HiZBufferWidth;
float HiZBufferHeight;

//Mipmaps de HiZBuffer pares
texture2D HiZBufferTex;
sampler HiZBufferSampler = sampler_state
{
    Texture = <HiZBufferTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Quad Vertex Shader Input
struct VS_INPUT_QUAD
{
   float4 Position  : 	 POSITION0;
   float2 TexCoords :	 TEXCOORD0;
};

//Quad Vertex Shader Output
struct VS_OUTPUT_QUAD
{
   float4 Position  :	POSITION0;
   float2 TexCoords :	TEXCOORD0;
};



//Vertex Shader for passing through transformed vertex.
VS_OUTPUT_QUAD v_OcclusionTestPyramid( VS_INPUT_QUAD Input )
{
   VS_OUTPUT_QUAD Output;
   
   //Project position
   Output.Position = Input.Position;
   Output.TexCoords = Input.TexCoords;

   return( Output );
}

//Uses the precalculated Hierachical Depth Buffer to optimize the Occlusion Test.
float4 PixOcclusionTestPyramid( float2 pos: TEXCOORD0 ) : COLOR0
{
	//return 1;
	

	//Get the 2D position inside the texture array
	//TODO: optimizar para jugar directamente con el uv, y evitar calcular index
	int posX = pos.x * (float) OccludeeTextureSize;
	int posY = pos.y * (float) OccludeeTextureSize;
	
	//Get the element number inside the array.
	int index = posY * OccludeeTextureSize + posX;
	
	
	//If the index is greater than the max number of occludees discard and leave original values.
	if( index > maxOccludees)
		discard;

		
	float4 texValue; 
	float occludeeDepth;
	float i, j;
	float2 hiZTexPos;
	float hiZDepth;
	int occludeeX1, occludeeX2, occludeeY1, occludeeY2;
		
	//Get the occludee depth value from texture.
	occludeeDepth = tex2Dlod(OccludeeDataDepthSampler, float4(pos, 0.0f, 0.0f)).r;

	
	//Reserve the -1 value to avoid occlusion test.
	if( occludeeDepth == -1 )
		discard;

	//Get the AABB from the texture at mip level 0.
	texValue = tex2Dlod(OccludeeDataAABBSampler, float4(pos, 0.0f, 0.0f));
	occludeeX1 =  texValue.r;
	occludeeY1 =  texValue.g;
	occludeeX2 =  texValue.b;
	occludeeY2 =  texValue.a;
	
	for( j = occludeeY1 ; j <= occludeeY2 ; j += 1.0f )
	{
		for( i = occludeeX1 ; i <= occludeeX2 ; i += 1.0f )
		{
		
			//Get the uv texture position from i and j positions.
			hiZTexPos.x  = i / HiZBufferWidth;
			hiZTexPos.y  = j / HiZBufferHeight;

			hiZDepth = tex2Dlod(HiZBufferSampler, float4(hiZTexPos, 0.0f, 0 )).r;

			//Check the depth value of the occludee and the one stored in the depth buffer.
			if( occludeeDepth >= hiZDepth )
				discard;       //Occludee visible. Stop searching and discard pixel shader. keep 255 original value.
			
		}
	}



	return 1; //Occludee not visible.
}




technique OcclusionTestPyramid
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_OcclusionTestPyramid();
		PixelShader = compile ps_3_0 PixOcclusionTestPyramid();
    }
}









