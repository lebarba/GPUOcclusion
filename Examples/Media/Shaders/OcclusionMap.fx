float4x4 matWorld;
float4x4 matWorldView;
float4x4 matWorldViewProj;

float HiZBufferWidth, HiZBufferHeight;

//The current occludee number in the array of occludees.
int ocludeeIndexInTexture;

//The total valid occludees that are used from the total array capacity.
int maxOccludees;

//The texture side size. 64 x 64.
uniform int DefaultTextureSize = 64;

texture2D OccludeeDataTextureAABB;
texture2D OccludeeDataTextureDepth;
texture2D HiZBufferTex;
texture2D OcclusionResult;
texture2D LastMip;

texture2D HiZBufferEvenTex;
texture2D HiZBufferOddTex;

//Used to show a particular mipmap level for debug.
float mipLevel;

//Based on Nick Darnells post about occlusion Culling
// x = width, y = height, z = mip level
float3 LastMipInfo;

//The total number of mipmap levels in the HiZ.
float maxMipLevels;

sampler OccludeeDataAABBSampler = sampler_state
{
    Texture = <OccludeeDataTextureAABB>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler OccludeeDataDepthSampler = sampler_state
{
    Texture = <OccludeeDataTextureDepth>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler HiZBufferSampler = sampler_state
{
    Texture = <HiZBufferTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler OcclusionResultSampler = sampler_state
{
    Texture = <OcclusionResult>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};


sampler LastMipSampler = sampler_state
{
    Texture = <LastMip>;
    MinFilter = POINT;
    MagFilter = POINT;
    MipFilter = POINT;
	
};


sampler HiZBufferEvenSampler = sampler_state
{
    Texture = <HiZBufferEvenTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};


sampler HiZBufferOddSampler = sampler_state
{
    Texture = <HiZBufferOddTex>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};



//HiZ Vertex Shader Input
struct VS_INPUT 
{
   float4 Position : POSITION0;
};


//HiZ Vertex Shader Output
struct VS_OUTPUT 
{
   float4 Position :     POSITION0;
   float2 Depth    :     TEXCOORD0;
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

//Input for vertices with Occlusion culling enabled
struct VS_INPUT_WITH_OCCLUSION
{
   float4 Position  :	POSITION0;
   float2 TexCoords :	TEXCOORD0;
};

//Output for vertices with Occlusion culling enabled
struct VS_OUTPUT_WITH_OCCLUSION
{
   float4 Position  :	POSITION0;
   float2 TexCoords :	TEXCOORD0;
};


//Vertex Shader
VS_OUTPUT VertHiZ( VS_INPUT Input )
{
   VS_OUTPUT Output;
   
   //Project position
   Output.Position = mul( Input.Position, matWorldViewProj);
   
   //Set x = z and y = w.
   Output.Depth.xy = Output.Position.zw;

   return( Output );
}


//Pixel returns the depth in view space.
float4 PixHiZ( float2 depth: TEXCOORD0) : COLOR0
{
	//Return the depth as z / w.

	return  1 - (depth.x / depth.y);
}




float4 DownSamplePS( in float4 Position   : POSITION0,
           in float2 PositionSS : TEXCOORD0 ) : Color0
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


//Vertex Shader for passing through transformed vertex.
VS_OUTPUT VertPassThru( VS_INPUT_QUAD Input )
{
   VS_OUTPUT_QUAD Output;
   
   //Project position
   Output.Position = Input.Position;
   Output.TexCoords = Input.TexCoords;

   return( Output );
}



//Vertex Shader for testing occlusion.
VS_OUTPUT VertDoOcclusionDiscard( VS_INPUT_WITH_OCCLUSION Input )
{
   VS_OUTPUT_WITH_OCCLUSION Output;
   
   float2 posInTexture;
   
   //Get the u v coordinates from the 1D position index.
   //TODO: Try with 1D textures to avoid this conversion.
   posInTexture.x = (float)(ocludeeIndexInTexture % DefaultTextureSize) / (float) DefaultTextureSize;
   posInTexture.y =  (float)(ocludeeIndexInTexture / (float) DefaultTextureSize) / (float) DefaultTextureSize;
   
   float4 visibleResult;

   //Get the Occlusion Result texture value to see if occludee is visible. Use mipmap level 0.
   visibleResult = tex2Dlod(OcclusionResultSampler, float4(posInTexture.xy, 0.0f, 0.0f));
  
   
   //If the value is 0 then project the vertex and let it continue through out the pipeline. 
   if ( visibleResult.r == 0.0f )
   {
     	
	   //Project position
	   Output.Position = mul( Input.Position, matWorldViewProj);
	   Output.TexCoords = Input.TexCoords;

	   return( Output );
    }
	else //Assign negative z so the vertex is discarded later.
	{	

		//Discard vertex by assigning a z value that will be invisible.
		Output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
		Output.TexCoords = 0;
		return( Output );

	}
	
	
}

//Used to render a textured quad at particular mipmap level.
//Used to debug the mipmap chain.
float4 SelectedMipMapLevel( in float4 Position   : POSITION0,
							in float2 texCoord : TEXCOORD0 ) : Color0
{
  
	return tex2Dlod( LastMipSampler, float4(texCoord, 0, mipLevel) );
	
}



//Uses the precalculated Hierachical Depth Buffer to optimize the Occlusion Test.
float4 PixOcclusionTestPyramid( float2 pos: TEXCOORD0) : COLOR0
{
	//Get the 2D position inside the texture array
	//TODO: optimizar para jugar directamente con el uv, y evitar calcular index
	int posX = pos.x * (float) DefaultTextureSize;
	int posY = pos.y * (float) DefaultTextureSize;
	
	//Get the element number inside the array.
	int index = posY * DefaultTextureSize + posX;
	
	//If the index is greater than the max number of occludees discard and leave original values.
	if( index > maxOccludees)
		discard;


	float4 texValue; 
	float occludeeDepth;
	int n;
	float i, j;
	float2 hiZTexPos;
	float hiZDepth;
	int mipSizeX,mipSizeY;
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
	
	n = log2(maxSide);

	//4 = mipmap 8x8
	n = clamp(n, 0, maxMipLevels-4);

	
	//Set the mip level for that occludee size.
	

	//Get the mipmap size
	mipSize.x = (int) (HiZBufferWidth / pow(2, n));
	mipSize.y = (int) (HiZBufferHeight / pow(2, n));

	//Get the AABB extremes in that mipmap level.
	occludeeX1 = ((float)(texValue.r) / HiZBufferWidth) * mipSize.x;
	occludeeY1 = ((float)(texValue.g) / HiZBufferHeight) * mipSize.y;
	occludeeX2 = ((float)(texValue.b) / HiZBufferWidth) * mipSize.x;
	occludeeY2 = ((float)(texValue.a) / HiZBufferHeight) * mipSize.y;


	for( j = occludeeY1 ; j < occludeeY2 ; j += 1.0f )
	{
		for( i = occludeeX1 ; i < occludeeX2 ; i += 1.0f )
		{
		
			//Get the uv texture position from i and j positions.
			hiZTexPos.x  = i / mipSize.x;
			hiZTexPos.y  = j / mipSize.y;
			
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


float4 SimplestPixelShader( ) : COLOR0
{
	return  255;
}

technique HiZBuffer
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertHiZ();
        PixelShader = compile ps_3_0 PixHiZ();
    }
}

technique OcclusionTestPyramid
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertPassThru();
		PixelShader = compile ps_3_0 PixOcclusionTestPyramid();
    }
}

technique RenderWithOcclusionEnabled
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertDoOcclusionDiscard();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}
technique DebugSpritesMipLevel
{
    pass p0
    {
        //Sampler[0] = (LastMipSampler);
        pixelShader = compile ps_3_0 SelectedMipMapLevel();
    }
}

technique HiZBufferDownSampling
{
    pass p0
    {
        //Sampler[0] = (LastMipSampler);
        VertexShader = compile vs_3_0 VertPassThru();
        pixelShader = compile ps_3_0 DownSamplePS();
    }
}