/* ######################################################################################################################## */
/* 	      Discard VS			*/
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


VS_OUTPUT v_normal( VS_INPUT Input )
{
	VS_OUTPUT Output;
	
	//Project position
	Output.Position = mul( Input.Position, matWorldViewProj);
	Output.Texcoord = Input.Texcoord;
	return Output;
	
	return Output; 
}

VS_OUTPUT v_discard( VS_INPUT Input )
{
	VS_OUTPUT Output;
	
	//Discard vertex by assigning a z value that will be invisible.
	Output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
	Output.Texcoord = 0;
	
	return Output; 
}

VS_OUTPUT v_discardTexel( VS_INPUT Input )
{
	VS_OUTPUT Output;
	
	//Discard vertex by assigning a z value that will be invisible.
	Output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
	Output.Texcoord = tex2Dlod(diffuseMap, float4(Input.Texcoord, 0.0f, 0.0f)).rg;
	
	return Output; 
}

VS_OUTPUT v_discardTexelIf( VS_INPUT Input )
{
	VS_OUTPUT Output;
	
	float2 texel = tex2Dlod(diffuseMap, float4(Input.Texcoord.xy, 0.0f, 0.0f)).rg;
	
	if(texel.x > 0.5f) {
		//Discard vertex by assigning a z value that will be invisible.
		Output.Position = float4(1.0f, 0.0f, -1.0f, 1.0f);
		Output.Texcoord = texel;
	} else {
		//Discard vertex by assigning a z value that will be invisible.
		Output.Position = float4(-1.0f, 0.0f, -1.0f, 1.0f);
		Output.Texcoord = texel;
	}
	
	return Output; 
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


technique Normal
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_normal();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}
technique Discard
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_discard();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}
technique DiscardTexel
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_discardTexel();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}
technique DiscardTexelIf
{
    pass p0
    {
        VertexShader = compile vs_3_0 v_discardTexelIf();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}