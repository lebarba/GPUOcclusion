/* ############################################################## */
/* 				Shader utilizado por OcclusionEngine 			  */
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
	//return  1 - (depth.x / depth.y);
	
	return float4(10.0f, 0.0f, 0.0f, 0.0f);
}


technique HiZBuffer
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_HiZBuffer();
        PixelShader = compile ps_3_0 p_HiZBuffer();
    }
}


/* ---------------------------------------- TECHNIQUE: HiZBufferDownSampling -------------------------------------------------- */

//Based on Nick Darnells post about occlusion Culling
// x = width, y = height, z = mip level
float3 LastMipInfo;

//Mipmap anterior
texture2D LastMip;
sampler LastMipSampler = sampler_state
{
    Texture = <LastMip>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
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
VS_OUTPUT_QUAD v_HiZBufferDownSampling( VS_INPUT_QUAD Input )
{
   VS_OUTPUT_QUAD Output;
   
   //Project position
   Output.Position = Input.Position;
   Output.TexCoords = Input.TexCoords;

   return( Output );
}


//Pixel Shader para DownSampling de HiZBuffer
float4 p_HiZBufferDownSampling( in float4 Position : POSITION0, in float2 PositionSS : TEXCOORD0 ) : Color0
{
	float width = LastMipInfo.x;
    float height = LastMipInfo.y;
    float mip = LastMipInfo.z;


    // get the upper left pixel coordinates of the 2x2 block we're going to downsample.
    // we need to muliply the screenspace positions components by 2 because we're sampling
    // the previous mip, so the maximum x/y values will be half as much of the mip we're
    // rendering to.
    float2 nCoords0 = float2(PositionSS.x, PositionSS.y);

	
	//TODO: Find out why this doesnt seem to work.
	//float2 nCoords0 = float2((PositionSS.x * 2) / width, (PositionSS.y * 2) / height); 
	    
    float2 nCoords1 = float2(nCoords0.x + (1 / width), nCoords0.y);
    float2 nCoords2 = float2(nCoords0.x, nCoords0.y + (1 / height));
    float2 nCoords3 = float2(nCoords1.x, nCoords2.y);
    
		
    // fetch a 2x2 neighborhood and compute the min (1 close to camera, 0 far)
    float4 vTexels;
    vTexels.x = tex2Dlod( LastMipSampler, float4(nCoords0, 0, mip) ).x;
    vTexels.y = tex2Dlod( LastMipSampler, float4(nCoords1, 0, mip) ).x;
    vTexels.z = tex2Dlod( LastMipSampler, float4(nCoords2, 0, mip) ).x;
    vTexels.w = tex2Dlod( LastMipSampler, float4(nCoords3, 0, mip) ).x;
    float fMinDepth = min( min( vTexels.x, vTexels.y ), min( vTexels.z, vTexels.w ) );
        
    return fMinDepth;
}


technique HiZBufferDownSampling
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_HiZBufferDownSampling();
        pixelShader = compile ps_3_0 p_HiZBufferDownSampling();
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
texture2D HiZBufferEvenTex;
sampler HiZBufferEvenSampler = sampler_state
{
    Texture = <HiZBufferEvenTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Mipmaps de HiZBuffer impares
texture2D HiZBufferOddTex;
sampler HiZBufferOddSampler = sampler_state
{
    Texture = <HiZBufferOddTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
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
	int n;
	float i, j;
	float2 hiZTexPos;
	float hiZDepth;
	int occludeeX1, occludeeX2, occludeeY1, occludeeY2;
	float2 mipSize;
		
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
		
	
	float maxSide =  max(occludeeX2  - occludeeX1, occludeeY2  - occludeeY1);
	
	
	/*
	n = log2(maxSide);
	n = n - 4;
	//4 = mipmap 8x8
	n = clamp(n, 0, maxMipLevels-4);
	*/
	//n = 0;
	
	
	n = ceil(log2(max(maxSide / 5, 1)));
	
	
	if(n % 0 == 1) {
		n--;
	}
	
	
	//Set the mip level for that occludee size.
	

	//Get the mipmap size
	mipSize.x = (int) (HiZBufferWidth / pow(2, n));
	mipSize.y = (int) (HiZBufferHeight / pow(2, n));

	//Get the AABB extremes in that mipmap level.
	occludeeX1 = ((float)(texValue.r) / HiZBufferWidth) * mipSize.x;
	occludeeY1 = ((float)(texValue.g) / HiZBufferHeight) * mipSize.y;
	occludeeX2 = ((float)(texValue.b) / HiZBufferWidth) * mipSize.x;
	occludeeY2 = ((float)(texValue.a) / HiZBufferHeight) * mipSize.y;

	float a = 0.0f;
	
	for( j = occludeeY1 ; j <= occludeeY2 ; j += 1.0f )
	{
		for( i = occludeeX1 ; i <= occludeeX2 ; i += 1.0f )
		{
		
			//Get the uv texture position from i and j positions.
			hiZTexPos.x  = i / mipSize.x;
			hiZTexPos.y  = j / mipSize.y;

			
			//TODO: Hacer texture array de HiZBufferEvenSampler y HiZBufferOddSampler

			//Get Hierarchical Z Buffer depth for the given position.
			if( n % 2 ==  0)
				hiZDepth = tex2Dlod(HiZBufferEvenSampler, float4(hiZTexPos, 0.0f, n )).r;
			else				
				hiZDepth = tex2Dlod(HiZBufferOddSampler, float4(hiZTexPos, 0.0f, n )).r;

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









