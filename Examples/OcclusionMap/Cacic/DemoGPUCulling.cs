using System;
using System.Collections.Generic;
using System.Text;
using TgcViewer.Example;
using TgcViewer;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using Microsoft.DirectX;
using TgcViewer.Utils.Modifiers;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer.Utils.TgcGeometry;
using Examples.OcclusionMap.DLL;
using TgcViewer.Utils._2D;
using TgcViewer.Utils.Terrain;
using TgcViewer.Utils;

namespace Examples.OcclusionMap.Cacic
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class DemoGPUCulling : TgcExample
    {

        #region Members

        //The maximum number of total occludees in scene.
        const int MAX_OCCLUDEES = 4096;
        const float TextureSize = 64;

        //The hierarchical Z-Buffer (HiZ) texture.
        Texture HiZBufferTex;

        //The results of the occlusion test texture;
        Texture OcclusionResultTex;

        //The surface to store the Hi Z
        Surface HiZSurface;

        //The surface to store the results of the occlusion test.
        Surface OcclusionResultSurface;

        //The effect to render the Hi Z buffer.
        Effect OcclusionEffect;

        Device d3dDevice;

        //The mesh to draw as example.
        Mesh teapot;

        //The textures to store the Occludees AABB and Depth.
        Texture OccludeeDataTextureAABB, OccludeeDataTextureDepth;

        //The vertices that form the quad needed to execute the occlusion test pixel shaders.
        CustomVertex.TransformedTextured[] ScreenQuadVertices;

        Surface pOldRT;
        VertexFormats oldVertexFormat;

        Random rnd = new Random();

        #endregion

        public override string getCategory()
        {
            return "GPUCulling";
        }

        public override string getName()
        {
            return "Lea - GPU Culling";
        }

        public override string getDescription()
        {
            return "Lea - GPU Culling";
        }

        public override void init()
        {
            d3dDevice = GuiController.Instance.D3dDevice;

            //Pasar a modo render customizado
            GuiController.Instance.CustomRenderEnabled = true;


            //Crear matriz de proyeccion para el nuevo tamaño a la mitada
            float aspectRatio = (float)GuiController.Instance.Panel3d.Width / GuiController.Instance.Panel3d.Height;
            d3dDevice.Transform.Projection = Matrix.PerspectiveFovLH(TgcD3dDevice.fieldOfViewY, aspectRatio, TgcD3dDevice.zNearPlaneDistance, TgcD3dDevice.zFarPlaneDistance);


            //Camara
            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(0, 0, -10), new Vector3(0, 0, 0));

            //Create the Occlusion map (Hierarchical Z Buffer).
            //Format.R32F
            HiZBufferTex = new Texture(d3dDevice, GuiController.Instance.D3dDevice.Viewport.Width,
                GuiController.Instance.D3dDevice.Viewport.Height, 1, Usage.RenderTarget,
                Format.X8R8G8B8, Pool.Default);

            //Get the surface.
            HiZSurface = HiZBufferTex.GetSurfaceLevel(0);


            //Create the texture that will hold the results of the occlusion test.
            OcclusionResultTex = new Texture(d3dDevice, (int)TextureSize, (int)TextureSize, 1, Usage.RenderTarget, Format.R16F, Pool.Default);

            //Get the surface.
            OcclusionResultSurface = OcclusionResultTex.GetSurfaceLevel(0);



            string MyShaderDir = GuiController.Instance.ExamplesDir + "media\\Shaders\\";

            //Load the Shader
            string compilationErrors;
            OcclusionEffect = Effect.FromFile(d3dDevice, MyShaderDir + "OcclusionMap.fx", null, null, ShaderFlags.None, null, out compilationErrors);
            if (OcclusionEffect == null)
            {
                throw new Exception("Error al cargar shader. Errores: " + compilationErrors);
            }

            teapot = Mesh.Teapot(d3dDevice);

            //Create a quad big enough to force the occlusion testpixel shader execution
            //CreatePSExecutionQuad(MAX_OCCLUDEES);

            //Create the vertex buffer with occludees.
            createOccludees();
        }

  
        public override void render(float elapsedTime)
        {
            DrawOcclusionBuffer();


        }

        private void DrawSprite(Texture tex, Point pos, float scale)
        {

            using (Sprite spriteobject = new Sprite(d3dDevice))
            {
                spriteobject.Begin(SpriteFlags.DoNotModifyRenderState);
                spriteobject.Transform = Matrix.Scaling(scale, scale, scale);
                spriteobject.Draw(tex, new Rectangle(0, 0, tex.GetSurfaceLevel(0).Description.Width, tex.GetSurfaceLevel(0).Description.Height), new Vector3(0, 0, 0), new Vector3(pos.X, pos.Y, 0), Color.White);
                spriteobject.End();
            }

        }

        private void DrawOcclusionBuffer()
        {


            //Draw the low detail occluders. Generate the Hi Z buffer
            DrawOccluders();


           
            //Perform the occlusion culling test. Obtain the visible set.
            PerformOcclussionCulling();


            //Draw the visible set.
            DrawGeometryWithOcclusionEnabled();


            
            #region DEBUG_TO_SCREEN


            
            d3dDevice.BeginScene();

            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Set screen as render target.
            d3dDevice.SetRenderTarget(0, pOldRT);


            //d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            d3dDevice.VertexFormat = oldVertexFormat;

            d3dDevice.SetTexture(0, OccludeeDataTextureAABB);
            d3dDevice.SetTexture(1, OccludeeDataTextureDepth);
            d3dDevice.SetTexture(2, HiZBufferTex);
            d3dDevice.SetTexture(3, OcclusionResultTex);


            //DrawTeapots(false, "HiZBuffer");

            //Draw the debug texture.
            DrawSprite(HiZBufferTex, new Point(20, 20), 0.25f);

            DrawSprite(OcclusionResultTex, new Point(10, 350), 1.0f);

            DrawSprite(OccludeeDataTextureAABB, new Point(20, 100), 2.0f);
            DrawSprite(OccludeeDataTextureDepth, new Point(20, 200), 2.0f);
            
            


            d3dDevice.EndScene();
             
            #endregion
            
            

        }

        private void DrawOccluders()
        {
            d3dDevice.BeginScene();

            //Store the original render target.
            pOldRT = d3dDevice.GetRenderTarget(0);

            //Get the Hierarchical zBuffer surface.
            Surface pHiZBufferSurface = HiZBufferTex.GetSurfaceLevel(0);

            //Set the render target.
            d3dDevice.SetRenderTarget(0, pHiZBufferSurface);


            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Enable Z test and Z write.
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);




            //Draw the objects being occluded
            DrawTeapots(true, "HiZBuffer");

            d3dDevice.EndScene();
        }

        private void DrawGeometryWithOcclusionEnabled()
        {


            d3dDevice.BeginScene();

            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Set screen as render target.
            d3dDevice.SetRenderTarget(0, pOldRT);
            d3dDevice.VertexFormat = oldVertexFormat;

            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);



            //d3dDevice.SetTexture(0, OccludeeDataTextureAABB);
            //d3dDevice.SetTexture(1, OccludeeDataTextureDepth);
            //d3dDevice.SetTexture(2, HiZBufferTex);
            d3dDevice.SetTexture(2, OcclusionResultTex);


            DrawTeapots(true, "RenderWithOcclusionEnabled");

            
            d3dDevice.EndScene();
        }

        private void PerformOcclussionCulling()
        {

            d3dDevice.BeginScene();


            //Save the previous vertex format for later use.
            oldVertexFormat = d3dDevice.VertexFormat;


            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            d3dDevice.SetTexture(0, OccludeeDataTextureAABB);
            d3dDevice.SetTexture(1, OccludeeDataTextureDepth);
            d3dDevice.SetTexture(2, HiZBufferTex);
            d3dDevice.SetTexture(3, OcclusionResultTex);

            d3dDevice.SetRenderTarget(0, OcclusionResultSurface);

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);


            d3dDevice.Clear(ClearFlags.Target, Color.FromArgb(0, 0, 0, 0), 1, 0);


            Matrix matWorldViewProj = d3dDevice.Transform.World * d3dDevice.Transform.View * d3dDevice.Transform.Projection;
            Matrix matWorldView = d3dDevice.Transform.World * d3dDevice.Transform.View;

            OcclusionEffect.SetValue("matWorldViewProj", matWorldViewProj);
            OcclusionEffect.SetValue("matWorldView", matWorldView);
            
            OcclusionEffect.SetValue("OccludeeDataTextureAABB", OccludeeDataTextureAABB);
            OcclusionEffect.SetValue("OccludeeDataTextureDepth", OccludeeDataTextureDepth);
            OcclusionEffect.SetValue("HiZBufferTex", HiZBufferTex);
            //OcclusionEffect.SetValue("OcclusionResult", OcclusionResultTex);

            OcclusionEffect.Technique = "OcclusionTest";

            int numPasses = OcclusionEffect.Begin(0);

            for (int n = 0; n < numPasses; n++)
            {

                OcclusionEffect.BeginPass(n);
                //Clear the target ( Result surface) to 255 so all occludees are visible by default unless proven occludeed.

                //Draw the quad making the pixel shaders inside of it execute.
                d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, ScreenQuadVertices);

                OcclusionEffect.EndPass();
            }
            OcclusionEffect.End();

            d3dDevice.EndScene();

        }

        //Vertices for a screen aligned quad
        private void QuadVertexDeclaration()
        {

            ScreenQuadVertices = new CustomVertex.TransformedTextured[4];

            ScreenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            ScreenQuadVertices[0].Rhw = 1.0f;
            ScreenQuadVertices[0].Tu = 0.0f;
            ScreenQuadVertices[0].Tv = 0.0f;

            ScreenQuadVertices[1].Position = new Vector4(TextureSize, 0f, 0f, 1f);
            ScreenQuadVertices[1].Rhw = 1.0f;
            ScreenQuadVertices[1].Tu = 1.0f;
            ScreenQuadVertices[1].Tv = 0.0f;


            ScreenQuadVertices[2].Position = new Vector4(TextureSize, TextureSize, 0f, 1f);
            ScreenQuadVertices[2].Rhw = 1.0f;
            ScreenQuadVertices[2].Tu = 1.0f;
            ScreenQuadVertices[2].Tv = 1.0f;

            ScreenQuadVertices[3].Position = new Vector4(0f, TextureSize, 0f, 1f);
            ScreenQuadVertices[3].Rhw = 1.0f;
            ScreenQuadVertices[3].Tu = 0.0f;
            ScreenQuadVertices[3].Tv = 1.0f;


        }

        private void DrawTeapots(bool withShader, string technique)
        {
            int index = 0;

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    d3dDevice.Transform.World = Matrix.Translation(i * 4, 0, j * 4);

                    if (withShader)
                    {
                        Matrix matWorldViewProj = d3dDevice.Transform.World * d3dDevice.Transform.View * d3dDevice.Transform.Projection;
                        Matrix matWorldView = d3dDevice.Transform.World * d3dDevice.Transform.View;

                        OcclusionEffect.SetValue("matWorldViewProj", matWorldViewProj);
                        OcclusionEffect.SetValue("matWorldView", matWorldView);

                        OcclusionEffect.SetValue("ocludeeIndexInTexture", index);
                        OcclusionEffect.SetValue("maxOccludees", 100);
                        OcclusionEffect.SetValue("OcclusionResult", OcclusionResultTex);
                        //OcclusionEffect.SetValue("OccludeeDataTextureAABB", OccludeeDataTextureAABB);
                        //OcclusionEffect.SetValue("OccludeeDataTextureDepth", OccludeeDataTextureDepth);
                        //OcclusionEffect.SetValue("HiZBufferTex", HiZBufferTex);                        

                        OcclusionEffect.Technique = technique;
                        int numPasses = OcclusionEffect.Begin(0);

                        for (int n = 0; n < numPasses; n++)
                        {

                            OcclusionEffect.BeginPass(n);
                            teapot.DrawSubset(0);
                            OcclusionEffect.EndPass();
                        }
                        OcclusionEffect.End();
                        
                        index++;
                    }
                    else
                    {
                        teapot.DrawSubset(0);
                    }
                }


            }

            d3dDevice.Transform.World = Matrix.Identity;
        }


        private void createOccludees()
        {

            //Get a texture size based on the max number of occludees.
            int textureSize = (int)Math.Sqrt(MAX_OCCLUDEES);

            float[] occludeeAABBdata = new float[MAX_OCCLUDEES * 4];

            float[] occludeeDepthData = new float[MAX_OCCLUDEES];



            //Populate Occludees AABB with random position and sizes.
            for (int i = 0; i < MAX_OCCLUDEES*4; i += 4)
            {
                //x1, y1, x2, y2
                //occludeeAABBdata[i] = (UInt16)rnd.Next(50);
                //occludeeAABBdata[i + 1] = (UInt16)rnd.Next(100);
                //occludeeAABBdata[i + 2] = (UInt16)(occludeeAABBdata[i] + 200);
                //occludeeAABBdata[i + 3] = (UInt16)(occludeeAABBdata[i + 1] + 200);

                occludeeAABBdata[i] = 300; //r
                occludeeAABBdata[i + 1] = 400; //g
                occludeeAABBdata[i + 2] = 800; //b
                occludeeAABBdata[i + 3] = 800; //a
            }

            //Populate Occludees depth with random depth.
            for (int i = 0; i < MAX_OCCLUDEES; i++)
            {
                //occludeeDepthData[i] = (float)rnd.NextDouble();
                occludeeDepthData[i] = 0.5f;
            }

            //Stores the AABB in the texure as float32 x1,y1, x2, y2 
            OccludeeDataTextureAABB = new Texture(d3dDevice, textureSize, textureSize, 0, Usage.None, Format.A32B32G32R32F, Pool.Managed);
            GraphicsStream stream = OccludeeDataTextureAABB.LockRectangle(0, LockFlags.None);
            stream.Write(occludeeAABBdata);
            OccludeeDataTextureAABB.UnlockRectangle(0);


            //Stores the occludee depth as int8.
            OccludeeDataTextureDepth = new Texture(d3dDevice, textureSize, textureSize, 0, Usage.None, Format.R32F, Pool.Managed);

            stream = OccludeeDataTextureDepth.LockRectangle(0, LockFlags.None);
            stream.Write(occludeeDepthData);
            OccludeeDataTextureDepth.UnlockRectangle(0);


            QuadVertexDeclaration();

        }



        public override void close()
        {

        }

    }
}
