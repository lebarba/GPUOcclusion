using System.Collections.Generic;
using Microsoft.DirectX.Direct3D;
using TgcViewer;
using TgcViewer.Utils.Shaders;
using Microsoft.DirectX;
using System.Drawing;
using Examples.Shaders;
using TgcViewer.Utils.TgcGeometry;
using System.IO;

namespace Examples.GpuOcclusion.ParalellOccludee
{
    /// <summary>
    /// Tecnica de Occluees en paralelo con bloques de 8x8
    /// </summary>
    public class OcclusionEngineParalellOccludee
    {
        //The maximum number of total occludees in scene.
        int occludeesTextureSize;
        int occludeesTextureExpandedSize;

        //Color todo 0
        readonly Color CERO_COLOR = Color.FromArgb(0, 0, 0, 0);

        //Color todo 1
        readonly Color ONE_COLOR = Color.FromArgb(255, 255, 255, 255);

        //Lado maximo a testear de un occludee
        const int MAX_OCCLUDEE_SIZE = 256;

        //Cantidad de bloques en los que se divide un Occludee, en una direccion
        const int MAX_OCCLUDEE_BLOQS = 32;

        //Por cuanto se divide cada lado del viewport original para definir el tamaño del ZBuffer a utilizar
        const int VIEWPORT_REDUCTION = 2;


        //Z Buffer
        Texture zBufferTex;
        int zBufferWidth;
        int zBufferHeight;
        Surface zBufferSurface;

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

        //Resultado de todos los occludees juntos, cada uno dividido en 32x32
        Texture paralellOccludeeOutputTexture;
        Surface paralellOccludeeOutputSurface;

        //Textura para primer pasada de reduccion de resultados de paralellOccludeeOutputTexture, cada occludee queda como 1x32
        Texture halfReduceOccludeeTexture;
        Surface halfReduceOccludeeSurface;

        //Textura para traer los resultados de GPU a CPU
        Texture readBackTexture;
        Surface readBackSurface;

        //Shader con todos los pasos de Occlusion
        Effect occlusionEffect;

        //The vertices that form the quad needed to execute the occlusion test pixel shaders.
        CustomVertex.TransformedTextured[] screenQuadVertices;

        //Distintos viewports utilizados en todo el proceso
        Viewport screenViewport;
        Viewport occlusionViewport;
        Viewport paralellViewport;

        //Formato de vertice para Occluders
        VertexDeclaration occluderVertexDec;

        //Matriz almacenada para calcularla una sola vez por frame
        Matrix matWorldViewProj;

        //IndexBuffer comun a todos los Occluders
        IndexBuffer occluderIndexBuffer;

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

        /// <summary>
        /// Creacion de engine de Occlusion
        /// </summary>
        public OcclusionEngineParalellOccludee()
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
            occludeesTextureSize = (int)FastMath.Ceiling(FastMath.Sqrt(maxOccludees));
            occludeesTextureSize = GpuOcclusionUtils.getNextHighestPowerOfTwo(occludeesTextureSize);
            occludeesTextureSize = occludeesTextureSize < 2 ? 2 : occludeesTextureSize;
            occludeesTextureExpandedSize = occludeesTextureSize * MAX_OCCLUDEE_BLOQS;
            //maxOccludeesCount = occludeesTextureSize * occludeesTextureSize;


            //Almacenar viewport original
            screenViewport = d3dDevice.Viewport;

            //generar viewport para Z buffer reducido a un cuarto del original
            occlusionViewport = new Viewport();
            occlusionViewport.X = screenViewport.X;
            occlusionViewport.Y = screenViewport.Y;
            occlusionViewport.MinZ = screenViewport.MinZ;
            occlusionViewport.MaxZ = screenViewport.MaxZ;
            occlusionViewport.Width = screenViewport.Width / VIEWPORT_REDUCTION;
            occlusionViewport.Height = screenViewport.Height / VIEWPORT_REDUCTION;

            //viewport para abarcar toda la textura paralellOccludeeTexture
            paralellViewport = new Viewport();
            paralellViewport.X = screenViewport.X;
            paralellViewport.Y = screenViewport.Y;
            paralellViewport.MinZ = screenViewport.MinZ;
            paralellViewport.MaxZ = screenViewport.MaxZ;
            paralellViewport.Width = occludeesTextureExpandedSize;
            paralellViewport.Height = occludeesTextureExpandedSize;



            //Crear zBuffer
            zBufferWidth = occlusionViewport.Width;
            zBufferHeight = occlusionViewport.Height;
            zBufferTex = new Texture(d3dDevice, zBufferWidth, zBufferHeight, 1, Usage.RenderTarget, Format.R32F, Pool.Default);
            zBufferSurface = zBufferTex.GetSurfaceLevel(0);

            //TODO: hacer de 16F para optimizar
            //Create the texture that will hold the results of the occlusion test.
            occlusionResultTex = new Texture(d3dDevice, occludeesTextureSize, occludeesTextureSize, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);
            occlusionResultSurface = occlusionResultTex.GetSurfaceLevel(0);

            //TODO: hacer de 16F para optimizar
            //Crear textura para almacenar el resultado de los bloques de todos los occludees
            paralellOccludeeOutputTexture = new Texture(d3dDevice, occludeesTextureExpandedSize, occludeesTextureExpandedSize, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);
            paralellOccludeeOutputSurface = paralellOccludeeOutputTexture.GetSurfaceLevel(0);

            //TODO: hacer de 16F para optimizar
            //Crear textura para hacer la primer pasada de reduccion a 1x32 de paralellOccludeeOutputTexture
            halfReduceOccludeeTexture = new Texture(d3dDevice, occludeesTextureSize, occludeesTextureExpandedSize, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);
            halfReduceOccludeeSurface = halfReduceOccludeeTexture.GetSurfaceLevel(0);

            //TODO: hacer de 16F para optimizar
            //Crear textura para traer los datos de GPU a CPU (es para debug)
            readBackTexture = new Texture(d3dDevice, this.occludeesTextureSize, this.occludeesTextureSize, 1, Usage.None, Format.R32F/*Format.R16F*/, Pool.SystemMemory);
            readBackSurface = readBackTexture.GetSurfaceLevel(0);


            //Cargar shader de occlusion
            occlusionEffect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\OcclusionEngineParalellOccludee.fx");

            //Crear VertexDeclaration para occluders
            occluderVertexDec = new VertexDeclaration(d3dDevice, Occluder.VERTEX_ELEMENTS);

            //Crear IndexBuffer para occluders
            occluderIndexBuffer = new IndexBuffer(typeof(short), Occluder.BOX_INDICES.Length, GuiController.Instance.D3dDevice, Usage.WriteOnly, Pool.Default);
            occluderIndexBuffer.SetData(Occluder.BOX_INDICES, 0, LockFlags.None);

            //Crear quad datos de occludees
            screenQuadVertices = new CustomVertex.TransformedTextured[4];

            screenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            screenQuadVertices[0].Rhw = 1.0f;
            screenQuadVertices[0].Tu = 0.0f;
            screenQuadVertices[0].Tv = 0.0f;

            screenQuadVertices[1].Position = new Vector4(1, 0f, 0f, 1f);
            screenQuadVertices[1].Rhw = 1.0f;
            screenQuadVertices[1].Tu = 1.0f;
            screenQuadVertices[1].Tv = 0.0f;

            screenQuadVertices[2].Position = new Vector4(1, 1, 0f, 1f);
            screenQuadVertices[2].Rhw = 1.0f;
            screenQuadVertices[2].Tu = 1.0f;
            screenQuadVertices[2].Tv = 1.0f;

            screenQuadVertices[3].Position = new Vector4(0f, 1, 0f, 1f);
            screenQuadVertices[3].Rhw = 1.0f;
            screenQuadVertices[3].Tu = 0.0f;
            screenQuadVertices[3].Tv = 1.0f;

        }


        /// <summary>
        /// Ejecutar proceso de Occlusion Culling y actualizar informacion de visibilidad.
        /// Tambien ejecuta previamente Frustum Culling.
        /// Previamente tienen que ser cargados los occludees y los occluders a utilizar en este frame.
        /// </summary>
        public void updateVisibility()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Store the original render target.
            Surface pOldRT = d3dDevice.GetRenderTarget(0);

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


            //Restore original values
            d3dDevice.SetRenderTarget(0, pOldRT);
            d3dDevice.Viewport = screenViewport;
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
        }

        /// <summary>
        /// Marcar todos los occludees como visibles
        /// </summary>
        public void resetVisibility()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Habilitar todos los occludees
            enabledOccludees.Clear();
            enabledOccludees.AddRange(occludees);

            //Cargar la textura de occludees como todo visible
            markAllOccludeesAsVisibleInTexture(d3dDevice);
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

            //Set the render target.
            d3dDevice.SetRenderTarget(0, zBufferSurface);
            d3dDevice.Viewport = occlusionViewport;

            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Enable Z test and Z write.
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Shader que genera depthBuffer
            d3dDevice.VertexDeclaration = occluderVertexDec;
            d3dDevice.VertexFormat = CustomVertex.PositionOnly.Format;
            occlusionEffect.Technique = "ZBuffer";
            occlusionEffect.SetValue("matWorldViewProj", matWorldViewProj);
            d3dDevice.Indices = occluderIndexBuffer;

            //Draw the objects being occluded
            for (int i = 0; i < occluders.Count; i++)
            {
                Occluder occluder = occluders[i];
                if (occluder.Enabled)
                {
                    //Cargar vertexBuffer del occluder
                    d3dDevice.SetStreamSource(0, occluder.VertexBuffer, 0);
  
                    //Render
                    occlusionEffect.Begin(0);
                    occlusionEffect.BeginPass(0);
                    d3dDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, Occluder.BOX_INDICES.Length, 0, Occluder.TRIANGLE_COUNT);
                    occlusionEffect.EndPass();
                    occlusionEffect.End();
                }
            }

            d3dDevice.EndScene();
        }


        /// <summary>
        /// Mandar Occludees a la GPU, de a uno por vez, dividido en bloques de 8x8 (hasta 32x32 bloques)
        /// Se descartan los Occludees que mas de 256x256
        /// </summary>
        private void performOcclussionCulling(Device d3dDevice)
        {
            //Si occlusion esta desactivado, marcar todo como visible
            if (!occlusionCullingEnabled)
            {
                markAllOccludeesAsVisibleInTexture(d3dDevice);
                return;
            }


            //Comenzar Occlusion
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Viewport extendido para que entre la textura de salida
            d3dDevice.Viewport = paralellViewport;
            
            //Salida: textura de 32x32 de todos los occludees
            d3dDevice.SetRenderTarget(0, paralellOccludeeOutputSurface);

            //Sin ZBuffer
            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Limpiar como todo invisible (1)
            d3dDevice.Clear(ClearFlags.Target, ONE_COLOR, 1, 0);

            //Tamaño del zBuffer en float
            float zBufferWidthF = (float)(zBufferWidth);
            float zBufferHeightF = (float)(zBufferHeight);

            //Enviar Occludees a testear visibilidad a GPU, en bloques de 8x8
            for (int i = 0; i < enabledOccludees.Count; i++)
            {
                //Proyectar occludee a 2D (usando viewport reducido)
                GpuOcclusionUtils.BoundingBox2D meshBox2D;
                bool consideredVisible = GpuOcclusionUtils.projectBoundingBox(enabledOccludees[i].BoundingBox, occlusionViewport, out meshBox2D);

                //Ubicacion del quad dentro de la textura de salida
                int quadMinX = (i * MAX_OCCLUDEE_BLOQS) % occludeesTextureExpandedSize;
                int quadMinY = ((i * MAX_OCCLUDEE_BLOQS) / occludeesTextureExpandedSize) * MAX_OCCLUDEE_BLOQS;

                //Ver que el tamaño del occludee no supere el umbral maximo permitido
                int occWidth = 0;
                int occHeight = 0;
                bool allowedSize = false;
                if (!consideredVisible)
                {
                    occWidth = (int)(meshBox2D.max.X - meshBox2D.min.X);
                    occHeight = (int)(meshBox2D.max.Y - meshBox2D.min.Y);
                    allowedSize = occWidth < MAX_OCCLUDEE_SIZE && occHeight < MAX_OCCLUDEE_SIZE;
                }


                //Si el occludee no se pudo proyectar o supera el tamaño maximo permitido, entonces marcarlo como visible
                if (consideredVisible || !allowedSize)
                {
                    //Guardar un pixel en 0 de los 32x32 de la textura, en la posicion que le corresponde a este occludee
                    occlusionEffect.Technique = "MarkAsVisibleOccludee";

                    //Ubicar quad (quad de un solo pixel)
                    screenQuadVertices[0].Position = new Vector4(quadMinX, quadMinY, 0f, 1f);
                    screenQuadVertices[1].Position = new Vector4(quadMinX + 1.0f, quadMinY, 0f, 1f);
                    screenQuadVertices[2].Position = new Vector4(quadMinX + 1.0f, quadMinY + 1.0f, 0f, 1f);
                    screenQuadVertices[3].Position = new Vector4(quadMinX, quadMinY + 1.0f, 0f, 1f);

                    //Render quad
                    occlusionEffect.Begin(0);
                    occlusionEffect.BeginPass(0);
                    d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, screenQuadVertices);
                    occlusionEffect.EndPass();
                    occlusionEffect.End();
                }

                //Caso comun: chequear visibilidad
                else
                {
                    //Calcular cantidad de bloques de 8x8
                    int quadWidth = (int)FastMath.Ceiling(occWidth / 8f);
                    int quadHeight = (int)FastMath.Ceiling(occHeight / 8f);

                    //Calcular punto extremo del quad para ubicarlo en la textura de resultado de todos los occludees
                    int quadMaxX = quadMinX + quadWidth;
                    int quadMaxY = quadMinY + quadHeight;

                    //Ubicar quad
                    screenQuadVertices[0].Position = new Vector4(quadMinX, quadMinY, 0f, 1f);
                    screenQuadVertices[1].Position = new Vector4(quadMaxX, quadMinY, 0f, 1f);
                    screenQuadVertices[2].Position = new Vector4(quadMaxX, quadMaxY, 0f, 1f);
                    screenQuadVertices[3].Position = new Vector4(quadMinX, quadMaxY, 0f, 1f);

                    //Parametros de shader
                    occlusionEffect.Technique = "ParalellOverlapTest";
                    occlusionEffect.SetValue("zBufferTex", zBufferTex);
                    occlusionEffect.SetValue("zBufferWidth", zBufferWidthF);
                    occlusionEffect.SetValue("zBufferHeight", zBufferHeightF);
                    occlusionEffect.SetValue("occludeeMin", new float[] { meshBox2D.min.X, meshBox2D.min.Y });
                    occlusionEffect.SetValue("occludeeMax", new float[] { meshBox2D.max.X, meshBox2D.max.Y });
                    occlusionEffect.SetValue("occludeeDepth", 1.0f - meshBox2D.depth);
                    occlusionEffect.SetValue("quadSize", new float[] { quadWidth, quadHeight });

                    //Render quad
                    occlusionEffect.Begin(0);
                    occlusionEffect.BeginPass(0);
                    d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, screenQuadVertices);
                    occlusionEffect.EndPass();
                    occlusionEffect.End();



                    //DEBUG
                    //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "paralellOccludeeOutputTexture.png", ImageFileFormat.Png, paralellOccludeeOutputTexture);


                }
            }
            d3dDevice.EndScene();


            //Reducir resultados hasta poder cargar la textura occlusionResultTex con la informacion de visibilidad de cada occludee
            reduceResults1erPass(d3dDevice);
            reduceResults2doPass(d3dDevice);
        }

        /// <summary>
        /// Cargamos la textura occlusionResultTex toda con 0, para indicar que todos los occludees son visibles
        /// </summary>
        /// <param name="d3dDevice"></param>
        private void markAllOccludeesAsVisibleInTexture(Device d3dDevice)
        {
            //Limpiar todo para dejarlo visible
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Salida en textura de visibilidad de occludees
            d3dDevice.Viewport = paralellViewport;
            d3dDevice.SetRenderTarget(0, occlusionResultSurface);

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Clear the result surface with 0 values, which mean they are "visible".
            d3dDevice.Clear(ClearFlags.Target, CERO_COLOR, 1, 0);
            d3dDevice.EndScene();
        }


        /// <summary>
        /// Reduccion horizontal de resultados de visibilidad
        /// </summary>
        private void reduceResults1erPass(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Salida: textura de todos los occludees pero con 1x32 para cada uno (reduccion horizontal)
            d3dDevice.Viewport = paralellViewport;
            d3dDevice.SetRenderTarget(0, halfReduceOccludeeSurface);

            //Sin ZBuffer
            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Limpiar como todo invisible (1)
            d3dDevice.Clear(ClearFlags.Target, ONE_COLOR, 1, 0);

            //Parametros de shader
            occlusionEffect.Technique = "Reduce1erPass";
            occlusionEffect.SetValue("quadSize", new float[] { occludeesTextureSize, occludeesTextureExpandedSize });
            occlusionEffect.SetValue("paralellOccludeeOutputTexture", paralellOccludeeOutputTexture);
            occlusionEffect.SetValue("resultsTexWidth", (float)(occludeesTextureExpandedSize));
            occlusionEffect.SetValue("resultsTexHeight", (float)(occludeesTextureExpandedSize));

            //Quad que abarque todos los occludees de forma 1x32
            screenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            screenQuadVertices[1].Position = new Vector4(occludeesTextureSize, 0f, 0f, 1f);
            screenQuadVertices[2].Position = new Vector4(occludeesTextureSize, occludeesTextureExpandedSize, 0f, 1f);
            screenQuadVertices[3].Position = new Vector4(0f, occludeesTextureExpandedSize, 0f, 1f);

            //Render quad
            occlusionEffect.Begin(0);
            occlusionEffect.BeginPass(0);
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, screenQuadVertices);
            occlusionEffect.EndPass();
            occlusionEffect.End();



            //DEBUG
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "halfReduceOccludeeTexture.png", ImageFileFormat.Png, halfReduceOccludeeTexture);


            d3dDevice.EndScene();
        }

        /// <summary>
        /// Reduccion verticar de resultados de visibilidad
        /// </summary>
        private void reduceResults2doPass(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            //Salida: textura final de todo el proceso. Un pixel en 0 o 1 que indica visibilidad para cada occludee
            d3dDevice.Viewport = paralellViewport;
            d3dDevice.SetRenderTarget(0, occlusionResultSurface);

            //Sin ZBuffer
            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Limpiar como todo invisible (1)
            d3dDevice.Clear(ClearFlags.Target, ONE_COLOR, 1, 0);

            //Parametros de shader
            occlusionEffect.Technique = "Reduce2doPass";
            occlusionEffect.SetValue("quadSize", new float[] { occludeesTextureSize, occludeesTextureSize });
            occlusionEffect.SetValue("halfReduceOccludeeTexture", halfReduceOccludeeTexture);
            occlusionEffect.SetValue("resultsTexWidth", (float)(occludeesTextureSize));
            occlusionEffect.SetValue("resultsTexHeight", (float)(occludeesTextureExpandedSize));

            //Quad que abarque todos los occludees de forma 1x1
            screenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            screenQuadVertices[1].Position = new Vector4(occludeesTextureSize, 0f, 0f, 1f);
            screenQuadVertices[2].Position = new Vector4(occludeesTextureSize, occludeesTextureSize, 0f, 1f);
            screenQuadVertices[3].Position = new Vector4(0f, occludeesTextureSize, 0f, 1f);

            //Render quad
            occlusionEffect.Begin(0);
            occlusionEffect.BeginPass(0);
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleFan, 2, screenQuadVertices);
            occlusionEffect.EndPass();
            occlusionEffect.End();


            //DEBUG
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "occlusionResultTex.png", ImageFileFormat.Png, occlusionResultTex);


            d3dDevice.EndScene();
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
        /// True significa que el occludee es visible. False que fue descartado por occlusion.
        /// </summary>
        /// <returns>Estado de occludees</returns>
        public bool[] getVisibilityData()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Traer textura de GPU a CPU
            d3dDevice.GetRenderTargetData(occlusionResultSurface, readBackSurface);
            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "visibility.png", ImageFileFormat.Png, debugTexture);
            float[] textureValues = (float[])readBackSurface.LockRectangle(typeof(float), LockFlags.ReadOnly, enabledOccludees.Count);
            readBackSurface.UnlockRectangle();

            //Pasar a array de boolean
            bool[] visibilityData = new bool[textureValues.Length];
            for (int i = 0; i < textureValues.Length; i++)
            {
                visibilityData[i] = textureValues[i] == 0.0f ? true : false;
            }

            return visibilityData;
        }


        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void close()
        {
            zBufferTex.Dispose();
            occlusionResultTex.Dispose();
            occlusionResultSurface.Dispose();
            paralellOccludeeOutputTexture.Dispose();
            paralellOccludeeOutputSurface.Dispose();
            halfReduceOccludeeTexture.Dispose();
            halfReduceOccludeeSurface.Dispose();
            occlusionEffect.Dispose();
            occluderVertexDec.Dispose();
            enabledOccludees = null;
            occluders = null;
            occludees = null;
        }

    }
}
