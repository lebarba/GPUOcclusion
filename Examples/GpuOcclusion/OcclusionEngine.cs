﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX.Direct3D;
using TgcViewer;
using TgcViewer.Utils.Shaders;
using Microsoft.DirectX;
using System.Drawing;
using Examples.Shaders;
using TgcViewer.Utils.TgcGeometry;

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Engine de Occlusion
    /// </summary>
    public class OcclusionEngine
    {
        //The maximum number of total occludees in scene.
        const int MAX_OCCLUDEES = 4096;
        const float OCCLUDEES_TEXTURE_SIZE = 64; //Raiz de MAX_OCCLUDEES

        //The hierarchical Z-Buffer (HiZ) texture.
        //Sepparated as even and odd mip levels. See Nick Darnells' blog.
        // 0 is even 1 is odd.
        Texture[] hiZBufferTex;

        //Dimensiones del HiZ (tamaño original)
        int hiZBufferWidth;
        int hiZBufferHeight;

        //The number of mip levels for the Hi Z texture;
        int mipLevels;

        //The results of the occlusion test texture;
        Texture occlusionResultTex;


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

        //Listas para occluders y occludees que sobreviven FrustumCulling
        List<TgcMeshShader> enabledOccludees;

        //Formato de vertice para Occluders
        VertexDeclaration occluderVertexDec;

        //Matriz almacenada para calcularla una sola vez por frame
        Matrix matWorldViewProj;


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


        public OcclusionEngine()
        {
            occluders = new List<Occluder>();
            occludees = new List<TgcMeshShader>();

            enabledOccludees = new List<TgcMeshShader>();
        }

        /// <summary>
        /// Iniciar engine
        /// </summary>
        public void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            screenViewport = d3dDevice.Viewport;

            //Create the Occlusion map (Hierarchical Z Buffer)
            hiZBufferTex = new Texture[2];
            hiZBufferWidth = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Width);
            hiZBufferHeight = GpuOcclusionUtils.getNextHighestPowerOfTwo(screenViewport.Height);
            hiZBufferTex[0] = new Texture(d3dDevice, hiZBufferWidth, hiZBufferHeight, 0, Usage.RenderTarget, Format.R32F, Pool.Default);
            hiZBufferTex[1] = new Texture(d3dDevice, hiZBufferWidth, hiZBufferHeight, 0, Usage.RenderTarget, Format.R32F, Pool.Default);

            //Get the number of mipmap levels.
            mipLevels = hiZBufferTex[0].LevelCount;

            //TODO: hacer de 16F para optimizar
            //Create the texture that will hold the results of the occlusion test.
            occlusionResultTex = new Texture(d3dDevice, (int)OCCLUDEES_TEXTURE_SIZE, (int)OCCLUDEES_TEXTURE_SIZE, 1, Usage.RenderTarget, /*Format.R16F*/Format.R32F, Pool.Default);

            //Get the surface.
            occlusionResultSurface = occlusionResultTex.GetSurfaceLevel(0);

            //Cargar shader de occlusion
            occlusionEffect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\OcclusionEngine.fx");

            //Crear VertexDeclaration para occluders
            occluderVertexDec = new VertexDeclaration(d3dDevice, Occluder.VERTEX_ELEMENTS);


            //Crear texturas para datos de Occludees
            //Get a texture size based on the max number of occludees.
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
            occludeeDataTextureAABB = new Texture(d3dDevice, (int)OCCLUDEES_TEXTURE_SIZE, (int)OCCLUDEES_TEXTURE_SIZE, 0, Usage.None, Format.A32B32G32R32F, Pool.Managed);
            occludeeDataTextureDepth = new Texture(d3dDevice, (int)OCCLUDEES_TEXTURE_SIZE, (int)OCCLUDEES_TEXTURE_SIZE, 0, Usage.None, Format.R32F, Pool.Managed);



            //Crear quad datos de occludees
            screenQuadVertices = new CustomVertex.TransformedTextured[4];

            screenQuadVertices[0].Position = new Vector4(0f, 0f, 0f, 1f);
            screenQuadVertices[0].Rhw = 1.0f;
            screenQuadVertices[0].Tu = 0.0f;
            screenQuadVertices[0].Tv = 0.0f;

            screenQuadVertices[1].Position = new Vector4(OCCLUDEES_TEXTURE_SIZE, 0f, 0f, 1f);
            screenQuadVertices[1].Rhw = 1.0f;
            screenQuadVertices[1].Tu = 1.0f;
            screenQuadVertices[1].Tv = 0.0f;

            screenQuadVertices[2].Position = new Vector4(OCCLUDEES_TEXTURE_SIZE, OCCLUDEES_TEXTURE_SIZE, 0f, 1f);
            screenQuadVertices[2].Rhw = 1.0f;
            screenQuadVertices[2].Tu = 1.0f;
            screenQuadVertices[2].Tv = 1.0f;

            screenQuadVertices[3].Position = new Vector4(0f, OCCLUDEES_TEXTURE_SIZE, 0f, 1f);
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

        

        /// <summary>
        /// Hacer frustum culling para descartar los occluders fuera de pantalla
        /// </summary>
        private void frustumCullingOccluders()
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

        /// <summary>
        /// Hacer frustum culling para descartar los occludees fuera de pantalla
        /// </summary>
        private void frustumCullingOccludees()
        {
            enabledOccludees.Clear();
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

            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Enable Z test and Z write.
            d3dDevice.SetRenderState(RenderStates.ZEnable, true);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, true);

            //Shader que genera depthBuffer
            d3dDevice.VertexDeclaration = occluderVertexDec;
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

                    //Render
                    occlusionEffect.Begin(0);
                    occlusionEffect.BeginPass(0);
                    d3dDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, Occluder.TRIANGLE_COUNT);
                    occlusionEffect.EndPass();
                    occlusionEffect.End();
                }
            }

            d3dDevice.EndScene();

            //Generar jerarquia de depthBuffer
            buildMipMapChain();

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
        }

        


        /// <summary>
        /// Mandar Occludees a la GPU
        /// </summary>
        private void performOcclussionCulling(Device d3dDevice)
        {
            d3dDevice.BeginScene();

            //Set the vertex format for the quad.
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;

            d3dDevice.SetRenderTarget(0, occlusionResultSurface);

            d3dDevice.SetRenderState(RenderStates.ZEnable, false);
            d3dDevice.SetRenderState(RenderStates.ZBufferWriteEnable, false);

            //Clear the result surface with 0 values, which mean they are "visible".
            d3dDevice.Clear(ClearFlags.Target, Color.FromArgb(0, 0, 0, 0), 1, 0);


            //Proyectar occludees y guardarlo en las dos texturas
            updateOccludeesData();

            occlusionEffect.SetValue("OccludeeDataTextureAABB", occludeeDataTextureAABB);
            occlusionEffect.SetValue("OccludeeDataTextureDepth", occludeeDataTextureDepth);
            occlusionEffect.SetValue("maxOccludees", occludees.Count);

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
            d3dDevice.EndScene();
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


        public void close()
        {
            //TODO: liberar todo
        }

    }
}
