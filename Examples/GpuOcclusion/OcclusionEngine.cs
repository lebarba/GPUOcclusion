using System.Collections.Generic;
using Microsoft.DirectX.Direct3D;
using TgcViewer;
using TgcViewer.Utils.Shaders;
using Microsoft.DirectX;
using System.Drawing;
using Examples.Shaders;
using TgcViewer.Utils.TgcGeometry;
using System.IO;

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Engine de Occlusion
    /// </summary>
    public class OcclusionEngine
    {
        //The maximum number of total occludees in scene.
        int maxOccludeesCount;
        int occludeesTextureSize; //maxOccludeesCount

        //Color todo cero
        readonly Color CERO_COLOR = Color.FromArgb(0, 0, 0, 0);

        Texture[] hiZBufferTex;
        /// <summary>
        /// The hierarchical Z-Buffer (HiZ) texture.
        /// Sepparated as even and odd mip levels. See Nick Darnells' blog.
        /// 0 is even 1 is odd.
        /// </summary>
        public Texture[] HiZBufferTex
        {
            get { return hiZBufferTex; }
        }

        //Dimensiones del HiZ (tamaño original)
        int hiZBufferWidth;
        int hiZBufferHeight;

        //The number of mip levels for the Hi Z texture;
        int mipLevels;

        Texture occlusionResultTex;
        /// <summary>
        /// The results of the occlusion test texture;
        /// </summary>
        public Texture OcclusionResultTex
        {
            get { return occlusionResultTex; }
        }


        //The surface to store the results of the occlusion test.
        Surface occlusionResultSurface;

        //The effect to render the Hi Z buffer.
        Effect occlusionEffect;

        //The textures to store the Occludees AABB and Depth.
        Texture occludeeDataTextureAABB;
        Texture occludeeDataTextureDepth;

        //The vertices that form the quad needed to execute the occlusion test pixel shaders.
        CustomVertex.TransformedTextured[] screenQuadVertices;

        //Buffers para occludees
        float[] occludeeAABBdata;
        float[] occludeeDepthData;

        Viewport screenViewport;

        //Formato de vertice para Occluders
        VertexDeclaration occluderVertexDec;

        //Matriz almacenada para calcularla una sola vez por frame
        Matrix matWorldViewProj;

        List<TgcMeshShader> enabledOccludees;
        /// <summary>
        /// Occludees que sobreviven Frustum-Culling.
        /// Son los que se tienen que usar para renderizar los meshes.
        /// </summary>
        public List<TgcMeshShader> EnabledOccludees
        {
            get { return enabledOccludees; }
            set { enabledOccludees = value; }
        }


        List<Occluder> occluders;
        /// <summary>
        /// Occluders
        /// </summary>
        public List<Occluder> Occluders
        {
            get { return occluders; }
        }

        List<TgcMeshShader> occludees;
        /// <summary>
        /// Occludees
        /// </summary>
        public List<TgcMeshShader> Occludees
        {
            get { return occludees; }
        }

        bool frustumCullingEnabled;
        /// <summary>
        /// Habilitar Frustum-Culling de occluders y occludees
        /// </summary>
        public bool FrustumCullingEnabled
        {
            get { return frustumCullingEnabled; }
            set { frustumCullingEnabled = value; }
        }

        bool occlusionCullingEnabled;
        /// <summary>
        /// Habilitar Occlusion-Culling de occludees
        /// </summary>
        public bool OcclusionCullingEnabled
        {
            get { return occlusionCullingEnabled; }
            set { occlusionCullingEnabled = value; }
        }

        public OcclusionEngine()
        {
            occluders = new List<Occluder>();
            occludees = new List<TgcMeshShader>();
            enabledOccludees = new List<TgcMeshShader>();
            frustumCullingEnabled = true;
            occlusionCullingEnabled = true;
        }

        /// <summary>
        /// Iniciar engine
        /// </summary>
        /// <param name="maxOccludees">Cantidad maxima de Occludees que tiene que soportar el engine</param>
        public void init(int maxOccludees)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Calcular cantidad de occludees
            occludeesTextureSize = (int)FastMath.Ceiling(FastMath.Log(maxOccludees, 2));
            occludeesTextureSize = GpuOcclusionUtils.getNextHighestPowerOfTwo(occludeesTextureSize);
            occludeesTextureSize = occludeesTextureSize < 2 ? 2 : occludeesTextureSize;
            maxOccludeesCount = occludeesTextureSize * occludeesTextureSize;


            //Almacenar viewport original
            screenViewport = d3dDevice.Viewport;

            //Create the Occlusion map (Hierarchical Z Buffer)
            hiZBufferTex = new Texture[2];
            hiZBufferWidth = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Width);
            hiZBufferHeight = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Height);
            hiZBufferTex[0] = new Texture(d3dDevice, hiZBufferWidth, hiZBufferHeight, 0, Usage.RenderTarget, Format.R32F, Pool.Default);
            hiZBufferTex[1] = new Texture(d3dDevice, hiZBufferWidth, hiZBufferHeight, 0, Usage.RenderTarget, Format.R32F, Pool.Default);

            //Agrandar ZBuffer y Stencil
            d3dDevice.DepthStencilSurface = d3dDevice.CreateDepthStencilSurface(hiZBufferWidth, hiZBufferHeight, DepthFormat.D24S8, MultiSampleType.None, 0, true);
            d3dDevice.PresentationParameters.MultiSample = MultiSampleType.None;


            //Get the number of mipmap levels.
            mipLevels = hiZBufferTex[0].LevelCount;

            //TODO: hacer de 16F para optimizar
            //Create the texture that will hold the results of the occlusion test.
            occlusionResultTex = new Texture(d3dDevice, occludeesTextureSize, occludeesTextureSize, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);

            //Get the surface.
            occlusionResultSurface = occlusionResultTex.GetSurfaceLevel(0);

            //Cargar shader de occlusion
            occlusionEffect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\OcclusionEngine.fx");

            //Crear VertexDeclaration para occluders
            occluderVertexDec = new VertexDeclaration(d3dDevice, Occluder.VERTEX_ELEMENTS);


            //Crear texturas para datos de Occludees
            //Get a texture size based on the max number of occludees.
            occludeeAABBdata = new float[this.maxOccludeesCount * 4];
            occludeeDepthData = new float[this.maxOccludeesCount];

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
            occludeeDataTextureAABB = new Texture(d3dDevice, occludeesTextureSize, occludeesTextureSize, 0, Usage.None, Format.A32B32G32R32F, Pool.Managed);
            occludeeDataTextureDepth = new Texture(d3dDevice, occludeesTextureSize, occludeesTextureSize, 0, Usage.None, Format.R32F, Pool.Managed);



            //Crear quad datos de occludees
            screenQuadVertices = new CustomVertex.TransformedTextured[4];

            screenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            screenQuadVertices[0].Rhw = 1.0f;
            screenQuadVertices[0].Tu = 0.0f;
            screenQuadVertices[0].Tv = 0.0f;

            screenQuadVertices[1].Position = new Vector4(occludeesTextureSize, 0f, 0f, 1f);
            screenQuadVertices[1].Rhw = 1.0f;
            screenQuadVertices[1].Tu = 1.0f;
            screenQuadVertices[1].Tv = 0.0f;

            screenQuadVertices[2].Position = new Vector4(occludeesTextureSize, occludeesTextureSize, 0f, 1f);
            screenQuadVertices[2].Rhw = 1.0f;
            screenQuadVertices[2].Tu = 1.0f;
            screenQuadVertices[2].Tv = 1.0f;

            screenQuadVertices[3].Position = new Vector4(0f, occludeesTextureSize, 0f, 1f);
            screenQuadVertices[3].Rhw = 1.0f;
            screenQuadVertices[3].Tu = 0.0f;
            screenQuadVertices[3].Tv = 1.0f;



            //DEBUG
            //string code = Effect.Disassemble(occlusionEffect, true);


        }


        /// <summary>
        /// Ejecutar proceso de Occlusion Culling y actualizar informacion de visibilidad.
        /// Tambien ejecuta previamente Frustum Culling.
        /// Previamente tienen que ser cargados los occludees y los occluders a utilizar en este frame.
        /// </summary>
        public void updateVisibility()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Calcular matriz final
            matWorldViewProj = d3dDevice.Transform.View * d3dDevice.Transform.Projection;

            //Frustum Culling de occluders
            GuiController.Instance.Frustum.updateVolume(d3dDevice.Transform.View, d3dDevice.Transform.Projection);
            frustumCullingOccluders();

            //Draw the low detail occluders. Generate the Hi Z buffer
            drawOccluders(d3dDevice);

            //Frustum Culling de occludees
            frustumCullingOccludees();

            //Perform the occlusion culling test. Obtain the visible set.
            performOcclussionCulling(d3dDevice);
        }

        public void resetVisibility()
        {

        }


        /// <summary>
        /// Hacer frustum culling para descartar los occludees fuera de pantalla
        /// </summary>
        private void frustumCullingOccludees()
        {
            enabledOccludees.Clear();
            if (frustumCullingEnabled)
            {
                for (int i = 0; i < occludees.Count; i++)
                {
                    TgcMeshShader occludee = occludees[i];

                    //FrustumCulling
                    if (TgcCollisionUtils.classifyFrustumAABB(GuiController.Instance.Frustum, occludee.BoundingBox) != TgcCollisionUtils.FrustumResult.OUTSIDE)
                    {
                        enabledOccludees.Add(occludee);
                    }
                }
            }
            else
            {
                enabledOccludees.AddRange(occludees);
            }
        }

        /// <summary>
        /// Hacer frustum culling para descartar los occluders fuera de pantalla
        /// </summary>
        private void frustumCullingOccluders()
        {
            if (frustumCullingEnabled)
            {
                for (int i = 0; i < occluders.Count; i++)
                {
                    Occluder occluder = occluders[i];

                    //FrustumCulling
                    if (TgcCollisionUtils.classifyFrustumAABB(GuiController.Instance.Frustum, occluder.Aabb) == TgcCollisionUtils.FrustumResult.OUTSIDE)
                    {
                        occluder.Enabled = false;
                    }
                    else
                    {
                        occluder.Enabled = true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < occluders.Count; i++)
                {
                    occluders[i].Enabled = true;
                }
            }
        }

        /// <summary>
        /// Mandar occluders a la GPU para generar un depth buffer
        /// </summary>
        private void drawOccluders(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Store the original render target.
            Surface pOldRT = d3dDevice.GetRenderTarget(0);

            //Get the Hierarchical zBuffer surface at mip level 0.
            Surface pHiZBufferSurface = hiZBufferTex[0].GetSurfaceLevel(0);

            //Set the render target.
            d3dDevice.SetRenderTarget(0, pHiZBufferSurface);
            d3dDevice.Viewport = screenViewport;


            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Enable Z test and Z write.
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Shader que genera depthBuffer
            d3dDevice.VertexDeclaration = occluderVertexDec;
            d3dDevice.VertexFormat = CustomVertex.PositionOnly.Format;
            occlusionEffect.Technique = "HiZBuffer";
            occlusionEffect.SetValue("matWorldViewProj", matWorldViewProj);

            //Draw the objects being occluded
            for (int i = 0; i < occluders.Count; i++)
            {
                Occluder occluder = occluders[i];
                if (occluder.Enabled)
                {
                    //Cargar vertexBuffer del occluder
                    d3dDevice.SetStreamSource(0, occluder.VertexBuffer, 0);
                    d3dDevice.Indices = occluder.IndexBuffer;

                    //Render
                    occlusionEffect.Begin(0);
                    occlusionEffect.BeginPass(0);
                    d3dDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, Occluder.INDEXED_VERTEX_COUNT, 0, Occluder.TRIANGLE_COUNT);
                    occlusionEffect.EndPass();
                    occlusionEffect.End();
                }
            }

            d3dDevice.EndScene();

            //Generar jerarquia de depthBuffer
            buildMipMapChain();


            /*
            //DEBUG ZBUFFER
             
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "depthBuffer.png", ImageFileFormat.Png, hiZBufferTex[0]);


            Surface s = hiZBufferTex[1].GetSurfaceLevel(3);
            Texture t = new Texture(d3dDevice, hiZBufferWidth / 8, hiZBufferHeight / 8, 1, Usage.Dynamic, Format.R32F, Pool.SystemMemory);
            Surface s2 = t.GetSurfaceLevel(0);
            d3dDevice.GetRenderTargetData(s, s2);
            s.Dispose();
            TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "depthBuffer.jpg", ImageFileFormat.Jpg, t);
            s2.Dispose();
            t.Dispose();
            */
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "depthBuffer.png", ImageFileFormat.Png, hiZBufferTex[0]);

            pHiZBufferSurface.Dispose();
            d3dDevice.SetRenderTarget(0, pOldRT);
        }


        /// <summary>
        /// Crear jerarquia de DepthBuffer
        /// </summary>
        private void buildMipMapChain()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Transformed vertices don't need vertex shader execution.
            CustomVertex.TransformedTextured[] mipMapQuadVertices = new CustomVertex.TransformedTextured[4];

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            d3dDevice.BeginScene();

            occlusionEffect.Technique = "HiZBufferDownSampling";

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Store the original render target.
            Surface pOldRT = d3dDevice.GetRenderTarget(0);

            const float texelOffset = 0.5f;

            //Generar mipmaps
            for (int i = 1; i < mipLevels; i++)
            {
                //Get the Hierarchical zBuffer surface.
                //If it is even set 0 in the tex array otherwise if it is odd use the 1 in the array.
                Surface pHiZBufferSurface = hiZBufferTex[i % 2].GetSurfaceLevel(i);

                //Set the render target.
                d3dDevice.SetRenderTarget(0, pHiZBufferSurface);

                //Send the PS the previous size and mip level values.
                Vector4 LastMipInfo;
                LastMipInfo.X = hiZBufferWidth >> (i - 1); //The previous mipmap width.
                LastMipInfo.Y = hiZBufferHeight >> (i - 1);
                LastMipInfo.Z = i - 1; // previous mip level.
                LastMipInfo.W = 0;

                if (LastMipInfo.X == 0) LastMipInfo.X = 1;
                if (LastMipInfo.Y == 0) LastMipInfo.Y = 1;


                //Set the texture of the previous mip level.
                occlusionEffect.SetValue("LastMipInfo", LastMipInfo);
                occlusionEffect.SetValue("LastMip", hiZBufferTex[(i - 1) % 2]);

                //Update the mipmap vertices.
                int mipWidth = pHiZBufferSurface.Description.Width;
                int mipHeight = pHiZBufferSurface.Description.Height;

                //Update the mipmap vertices.
                mipMapQuadVertices[0].Position = new Vector4(-texelOffset, -texelOffset, 0f, 1f);
                mipMapQuadVertices[0].Rhw = 1.0f;
                mipMapQuadVertices[0].Tu = 0.0f;
                mipMapQuadVertices[0].Tv = 0.0f;

                mipMapQuadVertices[1].Position = new Vector4(mipWidth - texelOffset, -texelOffset, 0f, 1f);
                mipMapQuadVertices[1].Rhw = 1.0f;
                mipMapQuadVertices[1].Tu = 1.0f;
                mipMapQuadVertices[1].Tv = 0.0f;


                mipMapQuadVertices[2].Position = new Vector4(mipWidth - texelOffset, mipHeight - texelOffset, 0f, 1f);
                mipMapQuadVertices[2].Rhw = 1.0f;
                mipMapQuadVertices[2].Tu = 1.0f;
                mipMapQuadVertices[2].Tv = 1.0f;

                mipMapQuadVertices[3].Position = new Vector4(-texelOffset, mipHeight - texelOffset, 0f, 1f);
                mipMapQuadVertices[3].Rhw = 1.0f;
                mipMapQuadVertices[3].Tu = 0.0f;
                mipMapQuadVertices[3].Tv = 1.0f;


                //render
                int numPasses = occlusionEffect.Begin(0);
                for (int n = 0; n < numPasses; n++)
                {
                    occlusionEffect.BeginPass(n);
                    d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, mipMapQuadVertices);
                    occlusionEffect.EndPass();
                }
                occlusionEffect.End();
                pHiZBufferSurface.Dispose();
            }
            d3dDevice.EndScene();
            d3dDevice.SetRenderTarget(0, pOldRT);
        }

        


        /// <summary>
        /// Mandar Occludees a la GPU
        /// </summary>
        private void performOcclussionCulling(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Store the original render target.
            Surface pOldRT = d3dDevice.GetRenderTarget(0);

            d3dDevice.SetRenderTarget(0, occlusionResultSurface);

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Clear the result surface with 0 values, which mean they are "visible".
            d3dDevice.Clear(ClearFlags.Target, CERO_COLOR, 1, 0);

            //Hacer Occlusion
            if (occlusionCullingEnabled)
            {
                //Proyectar occludees y guardarlo en las dos texturas
                updateOccludeesData();

                occlusionEffect.SetValue("OccludeeTextureSize", this.occludeesTextureSize);
                occlusionEffect.SetValue("OccludeeDataTextureAABB", occludeeDataTextureAABB);
                occlusionEffect.SetValue("OccludeeDataTextureDepth", occludeeDataTextureDepth);
                occlusionEffect.SetValue("maxOccludees", enabledOccludees.Count);

                //Tamaño del depthBuffer
                occlusionEffect.SetValue("HiZBufferWidth", (float)(hiZBufferWidth));
                occlusionEffect.SetValue("HiZBufferHeight", (float)(hiZBufferHeight));

                occlusionEffect.SetValue("maxMipLevels", mipLevels); //Send number of mipmaps.

                //Set even and odd hierarchical z buffer textures.
                occlusionEffect.SetValue("HiZBufferEvenTex", hiZBufferTex[0]);
                occlusionEffect.SetValue("HiZBufferOddTex", hiZBufferTex[1]);

                //Render quad
                occlusionEffect.Technique = "OcclusionTestPyramid";
                occlusionEffect.Begin(0);
                occlusionEffect.BeginPass(0);
                //Draw the quad making the pixel shaders inside of it execute.
                d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, screenQuadVertices);
                occlusionEffect.EndPass();
                occlusionEffect.End();
            }

            d3dDevice.EndScene();


            //Restore original renderTarget
            d3dDevice.SetRenderTarget(0, pOldRT);
            d3dDevice.Viewport = screenViewport;
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
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
            for (int i = 0; i < enabledOccludees.Count; i++)
            {
                //Proyectar occludee
                GpuOcclusionUtils.BoundingBox2D meshBox2D;
                if (GpuOcclusionUtils.projectBoundingBox(enabledOccludees[i].BoundingBox, screenViewport, out meshBox2D))
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
            GraphicsStream stream = occludeeDataTextureAABB.LockRectangle(0, LockFlags.Discard);
            stream.Write(occludeeAABBdata);
            occludeeDataTextureAABB.UnlockRectangle(0);

            //Stores the occludee depth as int8.
            stream = occludeeDataTextureDepth.LockRectangle(0, LockFlags.Discard);
            stream.Write(occludeeDepthData);
            occludeeDataTextureDepth.UnlockRectangle(0);
        }


        /// <summary>
        /// Carga en el shader los atributos necesarios para el OcclusionEngine
        /// </summary>
        /// <param name="meshEffect">shader</param>
        /// <param name="meshIndex">Indice del occludee</param>
        public void setOcclusionShaderValues(Effect meshEffect, int meshIndex)
        {
            //Indice del mesh
            meshEffect.SetValue("ocludeeIndexInTexture", meshIndex);

            //Informacion de visibilidad
            meshEffect.SetValue("OccludeeTextureSize", this.occludeesTextureSize);
            meshEffect.SetValue("occlusionResult", occlusionResultTex);
        }

        /// <summary>
        /// Devuelve el estado de visibilidad de occludee.
        /// El orden correlativo al de la lista EnabledOccludees.
        /// Esta funcion trae la textura de GPU a CPU para poder leer la informacion.
        /// Ese pasaje es muy lento. Solo debe ejecutarse a efectos de debug.
        /// </summary>
        /// <returns>Estado de occludees</returns>
        public bool[] getVisibilityData()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;
            bool[] data = new bool[enabledOccludees.Count];

            //Traer textura de GPU a CPU
            Texture debugTexture = new Texture(d3dDevice, this.occludeesTextureSize, this.occludeesTextureSize, 1, Usage.None, Format.R32F, Pool.SystemMemory);
            Surface debugSurface = debugTexture.GetSurfaceLevel(0);
            d3dDevice.GetRenderTargetData(occlusionResultSurface, debugSurface);
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "visibility.png", ImageFileFormat.Png, debugTexture);
            GraphicsStream stream = debugSurface.LockRectangle(LockFlags.ReadOnly);
            BinaryReader reader = new BinaryReader(stream);

            /*
            int total = (int)(this.occludeesTextureSize * this.occludeesTextureSize);
            float[] values = new float[total];
            for (int i = 0; i < total; i++)
            {
                float value = reader.ReadSingle();
                values[i] = value;
            }
            */

            //Leer datos textura
            for (int i = 0; i < enabledOccludees.Count; i++)
            {
                float value = reader.ReadSingle();
                data[i] = value == 0.0f ? true : false;
            }

            reader.Close();
            stream.Dispose();
            debugSurface.Dispose();
            debugTexture.Dispose();

            return data;
        }


        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void close()
        {
            hiZBufferTex[0].Dispose();
            hiZBufferTex[1].Dispose();
            occlusionResultTex.Dispose();
            occlusionResultSurface.Dispose();
            occlusionEffect.Dispose();
            occludeeDataTextureAABB.Dispose();
            occludeeDataTextureDepth.Dispose();
            occludeeAABBdata = null;
            occludeeDepthData = null;
            occluderVertexDec.Dispose();
            enabledOccludees = null;
            occluders = null;
            occludees = null;
        }

    }
}
