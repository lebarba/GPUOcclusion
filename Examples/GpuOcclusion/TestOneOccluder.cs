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
using TgcViewer.Utils._2D;
using TgcViewer.Utils.Terrain;
using TgcViewer.Utils;
using Examples.Shaders;
using System.IO;

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class TestOneOccluder : TgcExample
    {

        #region Members

        //The maximum number of total occludees in scene.
        //TODO: Mati, mete codigo aca.
        const int MAX_OCCLUDEES = 4096;
        const float TextureSize = 64;
        const bool enableZPyramid = true;

        //The hierarchical Z-Buffer (HiZ) texture.
        //Sepparated as even and odd mip levels. See Nick Darnells' blog.
        // 0 is even 1 is odd.
        Texture[] HiZBufferTex;

        //The hierarchical Z-Buffer mipmap chains.
       
         //The number of mip levels for the Hi Z texture;
        int mipLevels;

        //The results of the occlusion test texture;
        Texture OcclusionResultTex;

 
        //The surface to store the results of the occlusion test.
        Surface OcclusionResultSurface;

        //The effect to render the Hi Z buffer.
        Effect OcclusionEffect;

        Device d3dDevice;

        //The textures to store the Occludees AABB and Depth.
        Texture OccludeeDataTextureAABB, OccludeeDataTextureDepth;

        //The vertices that form the quad needed to execute the occlusion test pixel shaders.
        CustomVertex.TransformedTextured[] ScreenQuadVertices;

        Surface pOldRT;



        //Escenario
        List<TgcMeshShader> occluders;
        List<TgcMeshShader> occludees;
        Viewport screenViewport;

        //Buffers para occludees
        float[] occludeeAABBdata;
        float[] occludeeDepthData;
        int textureSize;


        //Debug
        Texture OcclusionResultTexCopy;
        Surface OcclusionResultSurfaceCopy;


        #endregion



        public override string getCategory()
        {
            return "GPUCulling";
        }

        public override string getName()
        {
            return "Test One Occluder";
        }

        public override string getDescription()
        {
            return "Test One Occluder";
        }

        public override void init()
        {
            d3dDevice = GuiController.Instance.D3dDevice;


            /*
            Texture t = new Texture(d3dDevice, 256, 256, 0, Usage.RenderTarget, Format.R32F, Pool.Default);
            Surface s = t.GetSurfaceLevel(2);
            */


            GuiController.Instance.CustomRenderEnabled = true;

            screenViewport = d3dDevice.Viewport;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-40.1941f, 0f, 102.0864f), new Vector3(-39.92f, -0.0593f, 101.1265f));



            HiZBufferTex = new Texture[2];
            //Create the Occlusion map (Hierarchical Z Buffer).
            //Format.R32F


            /*
            int screenWidth = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Width);
            int screenHeigth = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Height); 
            */
            int screenWidth = screenViewport.Width;
            int screenHeigth = screenViewport.Height; 

            for (int i = 0; i < 2; i++)
            {
                HiZBufferTex[i] = new Texture(d3dDevice, screenWidth,
                                             screenHeigth, /*0*/1, Usage.RenderTarget,
                                             Format.R32F, Pool.Default);
            }
 



            //Get the number of mipmap levels.
            mipLevels = HiZBufferTex[0].LevelCount;



            //Create the texture that will hold the results of the occlusion test.
            OcclusionResultTex = new Texture(d3dDevice, (int)TextureSize, (int)TextureSize, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);

            //Get the surface.
            OcclusionResultSurface = OcclusionResultTex.GetSurfaceLevel(0);




            string MyShaderDir = GuiController.Instance.ExamplesDir + "media\\Shaders\\";

            //Load the Shader
            string compilationErrors;
            OcclusionEffect = Effect.FromFile(d3dDevice, MyShaderDir + "OcclusionMap.fx", null, null, ShaderFlags.None, null, out compilationErrors);
            //OcclusionEffect = Effect.FromFile(d3dDevice, MyShaderDir + "OcclusionMap.fxo", null, null, ShaderFlags.NotCloneable, null, out compilationErrors);
            if (OcclusionEffect == null)
            {
                throw new Exception("Error al cargar shader. Errores: " + compilationErrors);
            }








            //Escenario


            //Box de occluder
            TgcBox box = TgcBox.fromSize(new Vector3(0, 0, 0), new Vector3(100, 30, 5), Color.Green);
            TgcMesh meshOccluder = box.toMesh("occluder");
            TgcMeshShader meshOccluderShader = TgcMeshShader.fromTgcMesh(meshOccluder, OcclusionEffect);
            meshOccluder.dispose();
            occluders = new List<TgcMeshShader>();
            occluders.Add(meshOccluderShader);


            //Occludee
            TgcMesh meshOccludee = TgcBox.fromSize(new Vector3(0, 0, -50), new Vector3(10, 30, 10), Color.Red).toMesh("occludee");
            TgcMeshShader meshOccludeeShader = TgcMeshShader.fromTgcMesh(meshOccludee, OcclusionEffect);
            meshOccludee.dispose();
            occludees = new List<TgcMeshShader>();
            occludees.Add(meshOccludeeShader);

            //Texturas de occludees
            initOccludeeBuffers();

            //Crear Quad para occludees
            createQuadVertexDeclaration();





            //Debug
            OcclusionResultTexCopy = new Texture(d3dDevice, (int)TextureSize, (int)TextureSize, 1, Usage.Dynamic, Format.R32F, Pool.SystemMemory);
            OcclusionResultSurfaceCopy = OcclusionResultTexCopy.GetSurfaceLevel(0);






            //UserVars
            GuiController.Instance.UserVars.addVar("visible", false);

        }


        public override void render(float elapsedTime)
        {

            //Draw the low detail occluders. Generate the Hi Z buffer
            DrawOccluders();

            //Perform the occlusion culling test. Obtain the visible set.
            PerformOcclussionCulling();

            //Draw the visible set.
            DrawGeometryWithOcclusionEnabled();

            //Show the occlusion related textures for debugging.
            //DebugTexturesToScreen();

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.Yellow);
            
            //Debug
            for (int i = 0; i < occludees.Count; i++)
            {
                occludees[i].BoundingBox.render();
            }
            for (int i = 0; i < occluders.Count; i++)
            {
                //occluders[i].Effect = "";
                occluders[i].BoundingBox.render();
            }
            

            //Leer textura de visibilidad de occludees para debug
            GuiController.Instance.D3dDevice.GetRenderTargetData(OcclusionResultSurface, OcclusionResultSurfaceCopy);
            GraphicsStream debugStream = OcclusionResultSurfaceCopy.LockRectangle(LockFlags.ReadOnly);
            BinaryReader reader = new BinaryReader(debugStream);
            float debugValue = reader.ReadSingle();
            OcclusionResultSurfaceCopy.UnlockRectangle();
            GuiController.Instance.UserVars["visible"] = debugValue == 0.0f ? "SI" : "NO";

        }

        















        /*
        //Renders the debug textures.
        private void DebugTexturesToScreen()
        {
            d3dDevice.BeginScene();

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Set screen as render target.
            d3dDevice.SetRenderTarget(0, pOldRT);


            d3dDevice.VertexFormat = oldVertexFormat;
            
            //Transformed vertices don't need vertex shader execution.
            CustomVertex.TransformedTextured[] MipMapQuadVertices = new CustomVertex.TransformedTextured[4];

            int originalMipWidth = HiZBufferTex[0].GetSurfaceLevel(0).Description.Width;
            int originalMipHeight = HiZBufferTex[0].GetSurfaceLevel(0).Description.Height;

            int posXMipMap = 0;

            for (int i = 1; i < mipLevels; i++)
            {

                OcclusionEffect.SetValue("mipLevel", i); 
                OcclusionEffect.Technique = "DebugSpritesMipLevel";

                if ( i > 0 )
                    OcclusionEffect.SetValue("LastMip", HiZBufferTex[(i) % 2]);
                else
                    OcclusionEffect.SetValue("LastMip", HiZBufferTex[0]); //CHANGE THIS with 0
                

                d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

                //Get the mip map level dimensions.
                int mipWidth = originalMipWidth >> (i);
                int mipHeight = originalMipHeight >> (i);
                
                //Create a screenspace quad for the position and size of the mip map.
                UpdateMipMapVertices(ref MipMapQuadVertices, posXMipMap, 0, mipWidth, mipHeight);
                
                int numPasses = OcclusionEffect.Begin(0);
                for (int n = 0; n < numPasses; n++)
                {
                    OcclusionEffect.BeginPass(n);

                    d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, MipMapQuadVertices);
                    OcclusionEffect.EndPass();
                }
                OcclusionEffect.End();
                posXMipMap += mipWidth + 10;

            }

            DrawSprite(OcclusionResultTex, new Point(20, 250), 2.0f);
            DrawSprite(OccludeeDataTextureAABB, new Point(20, 100), 2.0f);
            DrawSprite(OccludeeDataTextureDepth, new Point(20, 175), 2.0f);



            //Surface offScreenSurface;

            //offScreenSurface = d3dDevice.CreateOffscreenPlainSurface(OcclusionResultSurface.Description.Width, OcclusionResultSurface.Description.Height, OcclusionResultSurface.Description.Format, Pool.SystemMemory);
            //d3dDevice.GetRenderTargetData(OcclusionResultSurface, offScreenSurface);

            //GraphicsStream stream = offScreenSurface.LockRectangle(LockFlags.ReadOnly);

            //int texCount = OcclusionResultSurface.Description.Width * OcclusionResultSurface.Description.Height;
            //float[] values = new float[texCount];

            //values = (float[])stream.Read(typeof(float), 0, texCount);
            //offScreenSurface.UnlockRectangle();

            d3dDevice.EndScene();


            
        }
        */




        private void UpdateMipMapVertices(ref CustomVertex.TransformedTextured[] MipMapQuadVertices, int x, int y, int mipWidth, int mipHeight)
        {

            const float texelOffset = 0.5f;

            MipMapQuadVertices[0].Position = new Vector4(x - texelOffset, y - texelOffset, 0f, 1f);
            MipMapQuadVertices[0].Rhw = 1.0f;
            MipMapQuadVertices[0].Tu = 0.0f;
            MipMapQuadVertices[0].Tv = 0.0f;

            MipMapQuadVertices[1].Position = new Vector4(x + mipWidth - texelOffset, y - texelOffset, 0f, 1f);
            MipMapQuadVertices[1].Rhw = 1.0f;
            MipMapQuadVertices[1].Tu = 1.0f;
            MipMapQuadVertices[1].Tv = 0.0f;


            MipMapQuadVertices[2].Position = new Vector4(x + mipWidth - texelOffset, y + mipHeight - texelOffset, 0f, 1f);
            MipMapQuadVertices[2].Rhw = 1.0f;
            MipMapQuadVertices[2].Tu = 1.0f;
            MipMapQuadVertices[2].Tv = 1.0f;

            MipMapQuadVertices[3].Position = new Vector4(x - texelOffset, y + mipHeight - texelOffset, 0f, 1f);
            MipMapQuadVertices[3].Rhw = 1.0f;
            MipMapQuadVertices[3].Tu = 0.0f;
            MipMapQuadVertices[3].Tv = 1.0f;

        }

        /// <summary>
        /// Mandar occluders a la GPU para generar un depth buffer
        /// </summary>
        private void DrawOccluders()
        {
            d3dDevice.BeginScene();

            //Store the original render target.
            pOldRT = d3dDevice.GetRenderTarget(0);

            //Get the Hierarchical zBuffer surface at mip level 0.
            Surface pHiZBufferSurface = HiZBufferTex[0].GetSurfaceLevel(0);

            //Set the render target.
            d3dDevice.SetRenderTarget(0, pHiZBufferSurface);

            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Enable Z test and Z write.
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Draw the objects being occluded
            for (int i = 0; i < occluders.Count; i++)
            {
                TgcMeshShader occluder = occluders[i];

                //Shader que genera depthBuffer
                occluder.Effect.Technique = "HiZBuffer";
                occluders[i].render();
            }



            d3dDevice.EndScene();

            BuildMipMapChain();

            pHiZBufferSurface.Dispose();
            d3dDevice.SetRenderTarget(0, pOldRT);
        }

        /// <summary>
        /// Crear jerarquia de DepthBuffer
        /// </summary>
        private void BuildMipMapChain()
        {

            int originalWidth = HiZBufferTex[0].GetSurfaceLevel(0).Description.Width;
            int originalHeight = HiZBufferTex[0].GetSurfaceLevel(0).Description.Height;

            //Transformed vertices don't need vertex shader execution.
            CustomVertex.TransformedTextured[] MipMapQuadVertices = new CustomVertex.TransformedTextured[4];

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            d3dDevice.BeginScene();

            OcclusionEffect.Technique = "HiZBufferDownSampling";

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;


            for (int i = 1; i < mipLevels; i++)
            {

                //Get the Hierarchical zBuffer surface.
                //If it is even set 0 in the tex array otherwise if it is odd use the 1 in the array.
                Surface pHiZBufferSurface = HiZBufferTex[i % 2].GetSurfaceLevel(i);

                //Set the render target.
                d3dDevice.SetRenderTarget(0, pHiZBufferSurface);

                //d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

                //Viewport viewport = new Microsoft.DirectX.Direct3D.Viewport();
                //viewport.Width = pHiZBufferSurface.Description.Width;
                //viewport.Height = pHiZBufferSurface.Description.Height;
                //viewport.MaxZ = 1.0f;
                //viewport.MinZ = 0.0f;
                //viewport.X = 0;
                //viewport.Y = 0;
                //d3dDevice.Viewport = viewport;

                
                //Send the PS the previous size and mip level values.
                Vector4 LastMipInfo;
                LastMipInfo.X = originalWidth >> (i - 1); //The previous mipmap width.
                LastMipInfo.Y = originalHeight >> (i - 1);
                LastMipInfo.Z = i - 1; // previous mip level.
                LastMipInfo.W = 0;

                if (LastMipInfo.X == 0) LastMipInfo.X = 1;
                if (LastMipInfo.Y == 0) LastMipInfo.Y = 1;

                                 
                //Set the texture of the previous mip level.
                OcclusionEffect.SetValue("LastMipInfo", LastMipInfo);
                OcclusionEffect.SetValue("LastMip", HiZBufferTex[(i - 1) % 2]);

                //Update the mipmap vertices.
                UpdateMipMapVertices( ref MipMapQuadVertices, 0, 0, pHiZBufferSurface.Description.Width, pHiZBufferSurface.Description.Height);


                int numPasses = OcclusionEffect.Begin(0);

                for (int n = 0; n < numPasses; n++)
                {

                    OcclusionEffect.BeginPass(n);
                    d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, MipMapQuadVertices);
                    OcclusionEffect.EndPass();
                }
                OcclusionEffect.End();


            }
            d3dDevice.EndScene();
        }


        /// <summary>
        /// Dibujar objetos reales
        /// </summary>
        private void DrawGeometryWithOcclusionEnabled()
        {
            d3dDevice.BeginScene();

            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);


            //TODO: Frustum Culling


            //Set screen as render target.
            d3dDevice.SetRenderTarget(0, pOldRT);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Render
            for (int i = 0; i < occludees.Count; i++)
            {
                TgcMeshShader occludee = occludees[i];
                occludee.Effect.Technique = "RenderWithOcclusionEnabled";

                //Indice del mesh
                occludee.Effect.SetValue("ocludeeIndexInTexture", i);


                occludee.Effect.SetValue("OcclusionResult", OcclusionResultTex);
                occludee.render();
            }

            d3dDevice.EndScene();
        }


        /// <summary>
        /// Mandar Occludees a la GPU
        /// </summary>
        private void PerformOcclussionCulling()
        {
            d3dDevice.BeginScene();
            
            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            d3dDevice.SetRenderTarget(0, OcclusionResultSurface);

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Clear the result surface with 0 values, which mean they are "visible".
            d3dDevice.Clear(ClearFlags.Target, Color.FromArgb(0, 0, 0, 0), 1, 0);


            //Proyectar occludees y guardarlo en las dos texturas
            updateOccludeesData();

            OcclusionEffect.SetValue("OccludeeDataTextureAABB", OccludeeDataTextureAABB);
            OcclusionEffect.SetValue("OccludeeDataTextureDepth", OccludeeDataTextureDepth);
            OcclusionEffect.SetValue("maxOccludees", occludees.Count);

            //Tamaño del depthBuffer
            OcclusionEffect.SetValue("HiZBufferWidth", (float)(HiZBufferTex[0].GetLevelDescription(0).Width));
            OcclusionEffect.SetValue("HiZBufferHeight", (float)(HiZBufferTex[0].GetLevelDescription(0).Height));

            OcclusionEffect.SetValue("maxMipLevels", HiZBufferTex[0].LevelCount); //Send number of mipmaps.


            //Set even and odd hierarchical z buffer textures.
            OcclusionEffect.SetValue("HiZBufferEvenTex", HiZBufferTex[0]);
            OcclusionEffect.SetValue("HiZBufferOddTex", HiZBufferTex[1]);

            //Render quad
            OcclusionEffect.Technique = "OcclusionTestPyramid";
            OcclusionEffect.Begin(0);
            OcclusionEffect.BeginPass(0);
            //Draw the quad making the pixel shaders inside of it execute.
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, ScreenQuadVertices);
            OcclusionEffect.EndPass();
            OcclusionEffect.End();
            d3dDevice.EndScene();

        }

        /// <summary>
        /// Vertices for a screen aligned quad
        /// </summary>
        private void createQuadVertexDeclaration()
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


        /// <summary>
        /// Crear arrays usados en texturas de Occludees
        /// </summary>
        private void initOccludeeBuffers()
        {
            //Get a texture size based on the max number of occludees.
            textureSize = (int)Math.Sqrt(MAX_OCCLUDEES);
            occludeeAABBdata = new float[MAX_OCCLUDEES * 4];
            occludeeDepthData = new float[MAX_OCCLUDEES];

            //Iniciar arrays
            for (int i = 0; i < occludeeAABBdata.Length; i++)
            {
                occludeeAABBdata[i] = 0;
            }
            for (int i = 0; i < occludeeDepthData.Length; i++)
            {
                occludeeDepthData[i] = 0;
            }


            //Crear texturas para occludees (AABB y Depth)
            OccludeeDataTextureAABB = new Texture(d3dDevice, textureSize, textureSize, 0, Usage.None, Format.A32B32G32R32F, Pool.Managed);
            OccludeeDataTextureDepth = new Texture(d3dDevice, textureSize, textureSize, 0, Usage.None, Format.R32F, Pool.Managed);
        }


        /// <summary>
        /// Cargar datos de occludee en textura
        /// Se proyecta cada occludee a 2D y se guarda en dos texturas.
        /// Una con boundingRect de cada occludee (x1, y1, x2, y2)
        /// Otra con el depth de cada occludee
        /// </summary>
        private void updateOccludeesData()
        {
            //Populate Occludees AABB and depth
            for (int i = 0; i < occludees.Count; i ++)
            {
                //Proyectar occludee
                GpuOcclusionUtils.BoundingBox2D meshBox2D;
                if (GpuOcclusionUtils.projectBoundingBox(occludees[i].BoundingBox, screenViewport, out meshBox2D))
                {
                    //si no pudo proyectar entonces se considera posible, skipear en shader poniendo -1 en depth
                    occludeeDepthData[i] = -1f;
                    occludeeAABBdata[i * 4] = 0;
                    occludeeAABBdata[i * 4 + 1] = 0;
                    occludeeAABBdata[i * 4 + 2] = 0;
                    occludeeAABBdata[i * 4 + 3] = 0;

                }
                else
                {
                    //Cargar datos en array de textura (x1, y1, x2, y2)
                    occludeeAABBdata[i * 4] = meshBox2D.min.X;
                    occludeeAABBdata[i * 4 + 1] = meshBox2D.min.Y;
                    occludeeAABBdata[i * 4 + 2] = meshBox2D.max.X;
                    occludeeAABBdata[i * 4 + 3] = meshBox2D.max.Y;

                    //depth
                    occludeeDepthData[i] = 1.0f - meshBox2D.depth;
                }
                
            }


            //Stores the AABB in the texure as float32 x1,y1, x2, y2 
            GraphicsStream stream = OccludeeDataTextureAABB.LockRectangle(0, LockFlags.Discard);
            stream.Write(occludeeAABBdata);
            OccludeeDataTextureAABB.UnlockRectangle(0);


            //Stores the occludee depth as int8.
            stream = OccludeeDataTextureDepth.LockRectangle(0, LockFlags.Discard);
            stream.Write(occludeeDepthData);
            OccludeeDataTextureDepth.UnlockRectangle(0);
        }



        public override void close()
        {
            foreach (TgcMeshShader mesh in occludees)
            {
                mesh.dispose();
            }
            foreach (TgcMeshShader mesh in occluders)
            {
                mesh.dispose();
            }
            OcclusionEffect.Dispose();
        }

    }
}
