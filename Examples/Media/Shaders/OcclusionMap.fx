float4x4 matWorldView;
float4x4 matWorldViewProj;


//HiZ Vertex Shader Input
struct VS_INPUT 
{
   float4 Position : POSITION0;
};


//HiZ Vertex Shader Output
struct VS_OUTPUT 
{
   float4 Position :     POSITION0;
   float2 Depth :        TEXCOORD0;
};

//Vertex Shader
VS_OUTPUT VertHiZ( VS_INPUT Input )
{
   VS_OUTPUT Output;
   
   //Project position
   Output.Position = mul( Input.Position, matWorldViewProj);
   
   //Set x=z and y = w.
   Output.Depth.xy = Output.Position.zw;

   return( Output );

}


//Pixel returns the depth in view space.
float4 PixHiZ( float4 depth: TEXCOORD0) : COLOR0
{
	//Return the depth as z / w.
	return  depth.x / depth.y;
}


technique HiZBuffer
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertHiZ();
        PixelShader = compile ps_3_0 PixHiZ();
    }
}

technique OcclussionTest
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertHiZ();
        PixelShader = compile ps_3_0 PixHiZ();
    }
}
