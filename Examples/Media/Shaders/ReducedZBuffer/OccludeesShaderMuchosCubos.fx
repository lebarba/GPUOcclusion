/* ######################################################################################################################## */
/* 	      Shader utilizado para dibujar meshes del escenario normalmente utilizando la informacion de Occlusion 			*/
/* ######################################################################################################################## */

/**************************************************************************************/
/* Variables comunes */
/**************************************************************************************/

//Matrices de transformacion
float4x4 matWorld; //Matriz de transformacion World
float4x4 matWorldView; //Matriz World * View
float4x4 matWorldViewProj; //Matriz World * View * Projection
float4x4 matInverseTransposeWorld; //Matriz Transpose(Invert(World))

//Textura para DiffuseMap
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


//Material del mesh
float3 materialEmissiveColor; //Color RGB
float3 materialAmbientColor; //Color RGB
float4 materialDiffuseColor; //Color ARGB (tiene canal Alpha)
float3 materialSpecularColor; //Color RGB
float materialSpecularExp; //Exponente de specular

//Parametros de la Luz
float3 lightColor[6]; //Color RGB de la luz
float4 lightPosition[6]; //Posicion de la luz
float lightIntensity[6]; //Intensidad de la luz
float lightAttenuation[6]; //Factor de atenuacion de la luz
float4 eyePosition; //Posicion de la camara





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
	float4 Position : POSITION0;
	float2 Texcoord : TEXCOORD0;
	float3 WorldPosition : TEXCOORD1;
	float3 WorldNormal : TEXCOORD2;
};


//Vertex Shader for testing occlusion.
VS_OUTPUT VertDoOcclusionDiscard( VS_INPUT input )
{
   VS_OUTPUT output;

   //If the value is 0 then project the vertex and let it continue through out the pipeline. 
   if (isOccluded() == 0.0f)
   {
		//Caso comun: hacer lo propio del Vertex Shader

		//Proyectar posicion
		output.Position = mul(input.Position, matWorldViewProj);

		//Enviar Texcoord directamente
		output.Texcoord = input.Texcoord;

		//Posicion pasada a World-Space (necesaria para atenuación por distancia)
		output.WorldPosition = mul(input.Position, matWorld);

		/* Pasar normal a World-Space 
		Solo queremos rotarla, no trasladarla ni escalarla.
		Por eso usamos matInverseTransposeWorld en vez de matWorld */
		output.WorldNormal = mul(input.Normal, matInverseTransposeWorld).xyz;

		return output;
    }
	//Assign negative z so the vertex is discarded later.
	else 
	{	
		//Discard vertex by assigning a z value that will be invisible.
		output.Position = float4(0.0f, 0.0f, -1.0f, 1.0f);
		output.Texcoord = 0;
		output.WorldPosition = 0;
		output.WorldNormal = 0;
		return output;
	}
}


//Input del Pixel Shader
struct PS_INPUT
{
	float2 Texcoord : TEXCOORD0;
	float3 WorldPosition : TEXCOORD1;
	float3 WorldNormal : TEXCOORD2;
};

float4 SimplestPixelShader(PS_INPUT input) : COLOR0
{
	float3 Nn = normalize(input.WorldNormal);
	
	//ViewVec (V): vector que va desde el vertice hacia la camara.
	float3 viewVector = eyePosition.xyz - input.WorldPosition;
	
	float3 finalDiffuseColor = float3(0.0f, 0.0f, 0.0f);
	float3 finalSpecularColor = float3(0.0f, 0.0f, 0.0f);
	
	for(int i = 0; i < 6; i++) {
	
		//LightVec (L): vector que va desde el vertice hacia la luz. Usado en Diffuse y Specular
		float3 Ln = normalize(lightPosition[i].xyz - input.WorldPosition);

		//HalfAngleVec (H): vector de reflexion simplificado de Phong-Blinn (H = |V + L|). Usado en Specular
		float3 Hn = normalize(viewVector + Ln);

		
		//Calcular intensidad de luz, con atenuacion por distancia
		float distAtten = length(lightPosition[i].xyz - input.WorldPosition) * lightAttenuation[i];
		float intensity = lightIntensity[i] / distAtten; //Dividimos intensidad sobre distancia (lo hacemos lineal pero tambien podria ser i/d^2)
		
		
		//Componente Ambient
		float3 ambientLight = intensity * lightColor[i] * materialAmbientColor;
		
		//Componente Diffuse: N dot L
		float3 n_dot_l = dot(Nn, Ln);
		float3 diffuseLight = intensity * lightColor[i] * materialDiffuseColor.rgb * max(0.0, n_dot_l); //Controlamos que no de negativo
		
		//Componente Specular: (N dot H)^exp
		float3 n_dot_h = dot(Nn, Hn);
		float3 specularLight = n_dot_l <= 0.0
				? float3(0.0, 0.0, 0.0)
				: (intensity * lightColor[i] * materialSpecularColor * pow(max( 0.0, n_dot_h), materialSpecularExp));
		
		/* Color final: modular (Emissive + Ambient + Diffuse) por el color de la textura, y luego sumar Specular.
		   El color Alpha sale del diffuse material */
		finalDiffuseColor += materialEmissiveColor + ambientLight + diffuseLight;
		finalSpecularColor += specularLight;
	}
	
	//Obtener texel de la textura
	float4 texelColor = tex2D(diffuseMap, input.Texcoord);
	
	//Sumar todo
	float4 finalColor = float4(finalDiffuseColor * texelColor + finalSpecularColor, materialDiffuseColor.a);

	return finalColor;
}


technique RenderWithOcclusionEnabled
{
    pass p0
    {
        VertexShader = compile vs_3_0 VertDoOcclusionDiscard();
        PixelShader = compile ps_3_0 SimplestPixelShader();
    }
}