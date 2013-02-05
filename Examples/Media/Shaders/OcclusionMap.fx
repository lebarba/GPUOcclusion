float4x4 matWorldView;
float4x4 matWorldViewProj;

//The current occludee number in the array of occludees.
int ocludeeIndexInTexture;

//The total valid occludees that are used from the total array capacity.
int maxOccludees;

//The texture side size. 64 x 64.
uniform int DefaultTextureSize = 64;

texture OccludeeDataTextureAABB : register(s0);
texture OccludeeDataTextureDepth: register(s1);
texture HiZBufferTex;//			: register(s2);
texture OcclusionResult			: register(s2);

sampler OccludeeDataAABBSampler = sampler_state
{
    Texture = <OccludeeDataTextureAABB>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler OccludeeDataDepthSampler = sampler_state
{
    Texture = <OccludeeDataTextureDepth>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler HiZBufferSampler = sampler_state
{
    Texture = <HiZBufferTex>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    MipFilter = NONE;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

sampler OcclusionResultSampler = sampler_state
{
    Texture = <OcclusionResult>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
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
   float4 Position :     POSITION0;
   float2 TexCoords : TEXCOORD0;

};

//Input for vertices with Occlusion culling enabled
struct VS_INPUT_WITH_OCCLUSION
{
   float4 Position : POSITION0;
   float2 TexCoords : TEXCOORD0;
};

//Output for vertices with Occlusion culling enabled
struct VS_OUTPUT_WITH_OCCLUSION
{
   float4 Position :     POSITION0;
   float2 TexCoords : TEXCOORD0;
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
	return  1 -(depth.x / depth.y);
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




//Pixel shader to execute Occlusion Test (overlap + depth).
float4 PixOcclusionTest( float2 pos: TEXCOORD0) : COLOR0
{

	//Get the 2D position inside the texture array
	int posX = pos.x * (float) DefaultTextureSize;
	int posY = pos.y * (float) DefaultTextureSize;
	
	//Get the element number inside the array.
	int index = posY * 64 + posX;
	
	int occludeeX1, occludeeX2, occludeeY1, occludeeY2;
	
	//Get the AABB from the texture at mip level 0.
	float4 texValue = tex2Dlod(OccludeeDataAABBSampler, float4(pos, 0.0f, 0.0f));
	
	//Get the AABB extremes
	occludeeX1 = texValue.r;
	occludeeY1 = texValue.g;
	occludeeX2 = texValue.b;
	occludeeY2 = texValue.a;
	
	int x;
	int y;
	
	float hiZDepth;
	float occludeeDepth;
	float s, t;
	
	
		
	/*if( occludeeY2 == 3)
		return 1.0f;
	else
		discard;*/
		
		
	//If the index is greater than the max number of occludees discard and leave original values.
	if( index > maxOccludees)
		discard;
		
	//Get the occludee depth value from texture.
	occludeeDepth = tex2Dlod(OccludeeDataDepthSampler, float4(pos, 0.0f, 0.0f)).r;
	
	for( y = occludeeY1 ; y < occludeeY2 ; y++ )
	{
		for( x = occludeeX1 ; x < occludeeX2 ; x++ )
		{
		
			//Get Hierarchical Z Buffer depth for the given position.
			hiZDepth = tex2Dlod(HiZBufferSampler, float4(pos, 0.0f, 0.0f)).r;
			
			
			//Check the depth value of the occludee and the one stored in the depth buffer.
			if( occludeeDepth <= hiZDepth )
				discard;       //Occludee visible. Stop searching and discard pixel shader. keep 255 original value.
			
		}
	}
	
	//The occludee is not visible.
	return 1;
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

technique OcclusionTest
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertPassThru();
        PixelShader = compile ps_3_0 PixOcclusionTest();
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
