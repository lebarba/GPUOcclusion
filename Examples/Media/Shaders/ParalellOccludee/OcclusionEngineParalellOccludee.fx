/* ############################################################## */
/* 	ParalellOccludee			  */
/* ############################################################## */


/* ---------------------------------------- VARIABLES COMUNES -------------------------------------------------- */

//Matrices
float4x4 matWorldViewProj;



/* ---------------------------------------- TECHNIQUE: ZBuffer -------------------------------------------------- */

struct VS_OUTPUT_ZBuffer  
{
   float4 Position :     POSITION0;
   float2 Depth    :     TEXCOORD0;
};

//Vertex Shader
VS_OUTPUT_ZBuffer v_ZBuffer( float4 Position : POSITION0 )
{
   VS_OUTPUT_ZBuffer Output;
   
   //Project position
   Output.Position = mul( Position, matWorldViewProj);
   
   //Set x = z and y = w.
   Output.Depth.xy = Output.Position.zw;

   return( Output );
}


//Pixel returns the depth in view space.
float4 p_ZBuffer( float2 depth: TEXCOORD0) : COLOR0
{
	//Return the depth as z / w.
	return  1 - (depth.x / depth.y);
	
	//return float4(10.0f, 0.0f, 0.0f, 0.0f);
}


technique ZBuffer
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_ZBuffer();
        PixelShader = compile ps_3_0 p_ZBuffer();
    }
}


/* ---------------------------------------- TECHNIQUE: ParalellOverlapTest -------------------------------------------------- */

//Dimensiones del occludee original
float2 occludeeMin;
float2 occludeeMax;

//Z del occludee
float occludeeDepth;

//Dimensiones del quad
float2 quadSize;

//ZBuffer dimensions
float zBufferWidth;
float zBufferHeight;

//ZBuffer
texture2D zBufferTex;
sampler zBufferSampler = sampler_state
{
    Texture = <zBufferTex>;
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
VS_OUTPUT_QUAD v_passThrough( VS_INPUT_QUAD Input )
{
   VS_OUTPUT_QUAD Output;
   
   //Project position
   Output.Position = Input.Position;
   Output.TexCoords = Input.TexCoords;

   return Output;
}

//Overlap Test
float4 p_ParalellOverlapTest( float2 pos: TEXCOORD0 ) : COLOR0
{
	//Pixel del cual arrancar el bloque de 8x8
	float2 base = occludeeMin + pos * quadSize * 8;
	
	
	//Maximo texel a iterar (8x8 o menos si estamos justo en el borde del occludee)
	float2 maxTexel = float2(
		min(7, occludeeMax.x - base.x),
		min(7, occludeeMax.y - base.y)
	);
	
	//Recorrer 8x8 del occludee
	float i, j;
	float2 zBufferUV;
	float zBufferDepth;
	float result = 0.0f; //Hidden
	for( i = 0 ; i <= maxTexel.x && result == 0.0f; i += 1.0f )
	{
		for( j = 0 ; j <= maxTexel.y && result == 0.0f; j += 1.0f )
		{
			//Texel actual
			zBufferUV = base + float2(i, j);
			
			//Pasar a UV [0, 1]
			zBufferUV.x /= zBufferWidth;
			zBufferUV.y /= zBufferHeight;
	
			//Read zBuffer
			zBufferDepth = tex2Dlod(zBufferSampler, float4(zBufferUV, 0.0f, 0 )).r;

			//Check the depth value of the occludee and the one stored in the depth buffer.
			if( occludeeDepth >= zBufferDepth )
			{
				 result = 1.0f; //Visible
			}
				
			
		}
	}

	return result;
}


technique ParalellOverlapTest
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_passThrough();
		PixelShader = compile ps_3_0 p_ParalellOverlapTest();
    }
}

/* ---------------------------------------- TECHNIQUE: MarkAsVisibleOccludee -------------------------------------------------- */

//Simplemente marcar como visible
float4 p_MarkAsVisibleOccludee( float2 pos: TEXCOORD0 ) : COLOR0
{
	return 1.0f; //Visible
}

technique MarkAsVisibleOccludee
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_passThrough();
		PixelShader = compile ps_3_0 p_MarkAsVisibleOccludee();
    }
}

/* ---------------------------------------- TECHNIQUE: Reduce1erPass -------------------------------------------------- */

//Dimensiones de textura de look up
float resultsTexWidth;
float resultsTexHeight;

//Textura con resultado de los 32x32 bloques de cada occludee
texture2D paralellOccludeeOutputTexture;
sampler paralellOccludeeOutputSampler = sampler_state
{
    Texture = <paralellOccludeeOutputTexture>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Horizontal reduce
float4 p_Reduce1erPass( float2 pos: TEXCOORD0 ) : COLOR0
{
	//Texel del cual arrancar
	float2 base = float2(
		(pos.x * quadSize.x * 32) / resultsTexWidth,
		(pos.y * quadSize.y) / resultsTexHeight
	);
	
	
	float i;
	float minDepth = tex2Dlod(paralellOccludeeOutputSampler, float4(base, 0.0f, 0 )).r;
	float delta = 1.0f / resultsTexWidth;
	for( i = 1.0f ; i < 32.0f ; i += 1.0f )
	{
		base.x += delta;
		minDepth = max(minDepth, tex2Dlod(paralellOccludeeOutputSampler, float4(base, 0.0f, 0 )).r);
	}

	return minDepth;
}

technique Reduce1erPass
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_passThrough();
		PixelShader = compile ps_3_0 p_Reduce1erPass();
    }
}

/* ---------------------------------------- TECHNIQUE: Reduce2doPass -------------------------------------------------- */


//Textura con resultado de los 1x32 bloques de cada occludee
texture2D halfReduceOccludeeTexture;
sampler halfReduceOccludeeSampler = sampler_state
{
    Texture = <halfReduceOccludeeTexture>;
    MagFilter = POINT;
    MinFilter = POINT;
    MipFilter = POINT;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Vertical reduce
float4 p_Reduce2doPass( float2 pos: TEXCOORD0 ) : COLOR0
{
	//Texel del cual arrancar
	float2 base = float2(
		(pos.x * quadSize.x) / resultsTexWidth,
		(pos.y * quadSize.y * 32) / resultsTexHeight
	);
	
	float i;
	float minDepth = tex2Dlod(halfReduceOccludeeSampler, float4(base, 0.0f, 0 )).r;
	float delta = 1.0f / resultsTexHeight;
	for( i = 1.0f ; i < 32.0f ; i += 1.0f )
	{
		base.y += delta;
		minDepth = max(minDepth, tex2Dlod(halfReduceOccludeeSampler, float4(base, 0.0f, 0 )).r);
	}
	
	return minDepth;
}



technique Reduce2doPass
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_passThrough();
		PixelShader = compile ps_3_0 p_Reduce2doPass();
    }
}

/* ---------------------------------------- TECHNIQUE: GpuReduce -------------------------------------------------- */

//Textura con resultado de los 32x32 bloques de cada occludee
texture2D paralellOccludeeOutputLinearTexture;
sampler paralellOccludeeOutputLinearSampler = sampler_state
{
    Texture = <paralellOccludeeOutputLinearTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    MipFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Vertical reduce
float4 p_GpuReduce( float2 pos: TEXCOORD0 ) : COLOR0
{
	return tex2D(paralellOccludeeOutputLinearSampler, pos);
}

technique GpuReduce
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_passThrough();
		PixelShader = compile ps_3_0 p_GpuReduce();
    }
}