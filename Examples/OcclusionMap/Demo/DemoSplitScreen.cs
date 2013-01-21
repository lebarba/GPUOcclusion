﻿using System;
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
using TgcViewer.Utils.Shaders;
using TgcViewer.Utils;

namespace Examples.OcclusionMap
{
    /// <summary>
    /// DemoSplitScreen
    /// </summary>
    public class DemoSplitScreen : TgcExample
    {

        const float REMOTE_MESH_MOVEMENT_SPEED = 10f;
        const float REMOTE_MESH_ROTATE_SPEED = 0.04f;


        List<TgcMesh> meshes;
        List<Occluder> occluders;
        List<Occluder> enabledOccluders;
        OcclusionDll occlusionDll;
        OcclusionViewport viewport;
        List<Occludee.BoundingBox2D> occludees;
        TgcSkyBox skyBox;
        TgcFrustum frustum;
        TgcMesh remoteMesh;
        float remoteMeshOrigAngle;
        Viewport leftViewport;
        Viewport rightViewport;
        List<TgcMesh> frustumCulledMeshes;
        List<TgcMesh> occlusionCulledMeshes;


        public override string getCategory()
        {
            return "Demo";
        }

        public override string getName()
        {
            return "Split Screen";
        }

        public override string getDescription()
        {
            return "Split Screen.";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Cargar ciudad
            TgcSceneLoader loader = new TgcSceneLoader();
            TgcScene scene = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrande\\CiudadGrande-TgcScene.xml");

            //Separar occluders del resto
            meshes = new List<TgcMesh>();
            occluders = new List<Occluder>();
            enabledOccluders = new List<Occluder>();
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                TgcMesh mesh = scene.Meshes[i];
                if (mesh.Layer == "Occluders")
                {
                    Occluder oc = new Occluder();
                    oc.Mesh = mesh;
                    occluders.Add(oc);
                }
                else
                {
                    meshes.Add(mesh);
                }
            }
            frustumCulledMeshes = new List<TgcMesh>();
            occlusionCulledMeshes = new List<TgcMesh>();


            //Viewports
            leftViewport = new Viewport();
            leftViewport.X = 0;
            leftViewport.Y = 0;
            leftViewport.Width = d3dDevice.Viewport.Width / 2;
            leftViewport.Height = d3dDevice.Viewport.Height;
            leftViewport.MinZ = d3dDevice.Viewport.MinZ;
            leftViewport.MaxZ = d3dDevice.Viewport.MaxZ;

            rightViewport = new Viewport();
            rightViewport.X = d3dDevice.Viewport.Width / 2;
            rightViewport.Y = 0;
            rightViewport.Width = d3dDevice.Viewport.Width / 2;
            rightViewport.Height = d3dDevice.Viewport.Height;
            rightViewport.MinZ = d3dDevice.Viewport.MinZ;
            rightViewport.MaxZ = d3dDevice.Viewport.MaxZ;

            //Crear matriz de proyeccion para el nuevo tamaño a la mitada
            float aspectRatio = (float)leftViewport.Width / leftViewport.Height;
            d3dDevice.Transform.Projection = Matrix.PerspectiveFovLH(TgcD3dDevice.fieldOfViewY, aspectRatio, TgcD3dDevice.zNearPlaneDistance, TgcD3dDevice.zFarPlaneDistance);


            //Crear Occlusion Dll
            viewport = new OcclusionViewport(leftViewport.Width / 4, leftViewport.Height / 4);
            occlusionDll = new OcclusionDll(viewport.D3dViewport.Width, viewport.D3dViewport.Height);
            occlusionDll.clear();
            occlusionDll.fillDepthBuffer();
            occludees = new List<Occludee.BoundingBox2D>();


            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("doOcclusion", "doOcclusion", true);
            GuiController.Instance.Modifiers.addBoolean("showOcclusionCull", "showOcclusionCull", true);
            GuiController.Instance.Modifiers.addBoolean("showFrustumCull", "showFrustumCull", false);


            //Crear SkyBox
            skyBox = new TgcSkyBox();
            skyBox.Center = new Vector3(0, 0, 0);
            skyBox.Size = new Vector3(10000, 10000, 10000);
            string texturesPath = GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\SkyBoxCiudad\\";
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Up, texturesPath + "Up.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Down, texturesPath + "Down.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Left, texturesPath + "Left.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Right, texturesPath + "Right.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Front, texturesPath + "Back.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Back, texturesPath + "Front.jpg");
            skyBox.updateValues();

            //FrustumMesh
            frustum = new TgcFrustum();
            frustum.Effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\AlphaBlending.fx");
            frustum.Effect.Technique = "OnlyColorTechnique";
            frustum.AlphaBlendingValue = 0.7f;
            frustum.updateMesh(new Vector3(0, 20, 0), new Vector3(0, 20, 1));
            d3dDevice.RenderState.ReferenceAlpha = 0;


            //Cargar mesh para manejar el Frustum
            remoteMesh = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "OcclusionMap\\Auto\\Auto-TgcScene.xml").Meshes[0];
            remoteMesh.Position = new Vector3(0, 0, 0);
            remoteMeshOrigAngle = FastMath.PI;
            remoteMesh.rotateY(remoteMeshOrigAngle);

            //Camara
            GuiController.Instance.ThirdPersonCamera.Enable = true;
            GuiController.Instance.ThirdPersonCamera.setCamera(remoteMesh.Position, 40, -150);
            GuiController.Instance.ThirdPersonCamera.TargetDisplacement = new Vector3(0, 20, 0);
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;


            //Mover el remoteMesh
            Vector3 cameraPos = updateRemoteMesh(elapsedTime);


            //Calcular Occluders Silhouette 2D, previamente hacer FrustumCulling del Occluder
            enabledOccluders.Clear();
            int visibleQuads = 0;
            enabledOccluders.Clear();
            frustumCulledMeshes.Clear();
            foreach (Occluder o in occluders)
            {
                //FrustumCulling
                if (TgcCollisionUtils.classifyFrustumAABB(frustum, o.Mesh.BoundingBox) == TgcCollisionUtils.FrustumResult.OUTSIDE)
                {
                    o.Enabled = false;
                }
                else
                {
                    //Proyectar occluder
                    o.computeProjectedQuads(cameraPos, viewport);
                    if (o.ProjectedQuads.Count == 0)
                    {
                        o.Enabled = false;
                    }
                    else
                    {
                        o.Enabled = true;
                        enabledOccluders.Add(o);
                        visibleQuads += o.ProjectedQuads.Count;
                    }
                }
            }


            //Enviar todos los occluders habilitados a la DLL
            occlusionDll.clear();
            occlusionDll.convertAndAddOccluders(enabledOccluders, visibleQuads);



            //Calcular visibilidad de meshes
            bool doOcclusion = (bool)GuiController.Instance.Modifiers["doOcclusion"];
            occludees.Clear();
            int visibleFrustum = 0;
            if (doOcclusion)
            {
                foreach (TgcMesh mesh in meshes)
                {
                    //FrustumCulling
                    if (TgcCollisionUtils.classifyFrustumAABB(frustum, mesh.BoundingBox) == TgcCollisionUtils.FrustumResult.OUTSIDE)
                    {
                        mesh.Enabled = false;
                        frustumCulledMeshes.Add(mesh);
                    }
                    //Occlusion
                    else
                    {
                        visibleFrustum++;

                        //Chequear visibilidad de AABB proyectado del mesh contra DLL
                        Occludee.BoundingBox2D meshBox2D;
                        if (Occludee.projectBoundingBox(mesh.BoundingBox, viewport, out meshBox2D))
                        {
                            mesh.Enabled = true;
                        }
                        else
                        {
                            mesh.Enabled = occlusionDll.convertAndTestOccludee(meshBox2D);
                            meshBox2D.visible = mesh.Enabled;
                            occludees.Add(meshBox2D);
                            if (!mesh.Enabled)
                            {
                                occlusionCulledMeshes.Add(mesh);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (TgcMesh mesh in meshes)
                {
                    mesh.Enabled = true;
                }
            }




            //Dibujar Viewport left
            drawLeftViewport(d3dDevice);

            //Dibujar Viewport Right
            drawRightViewport(d3dDevice);

        }

        

        /// <summary>
        /// Dibujar Viewport left
        /// </summary>
        private void drawLeftViewport(Device d3dDevice)
        {
            //Dibujar Viewport left
            d3dDevice.Viewport = leftViewport;
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Blue, 1.0f, 0);




            //Dibujar los mesh habilitados
            //Opacas
            foreach (TgcMesh mesh in meshes)
            {
                if (mesh.Enabled && !mesh.AlphaBlendEnable)
                {
                    mesh.render();
                }
            }

            //Skybox
            skyBox.render();

            //Dibujar remoteMesh
            remoteMesh.render();

            //Alpha
            foreach (TgcMesh mesh in meshes)
            {
                if (mesh.Enabled && mesh.AlphaBlendEnable)
                {
                    mesh.render();
                }
            }
        }


        /// <summary>
        /// Dibujar Viewport Right
        /// </summary>
        private void drawRightViewport(Device d3dDevice)
        {
            //Dibujar Viewport Right
            d3dDevice.Viewport = rightViewport;
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Blue, 1.0f, 0);

            //Dibujar los mesh habilitados
            //Opacas
            foreach (TgcMesh mesh in meshes)
            {
                if (mesh.Enabled && !mesh.AlphaBlendEnable)
                {
                    mesh.render();
                }
            }

            //Skybox
            skyBox.render();

            //Dibujar remoteMesh
            remoteMesh.render();

            //Alpha
            foreach (TgcMesh mesh in meshes)
            {
                if (mesh.Enabled && mesh.AlphaBlendEnable)
                {
                    mesh.render();
                }
            }


            //Dibujar los ocultos por Occlusion culling
            bool showOcclusionCull = (bool)GuiController.Instance.Modifiers["showOcclusionCull"];
            if (showOcclusionCull)
            {
                foreach (TgcMesh mesh in occlusionCulledMeshes)
                {
                    mesh.BoundingBox.setRenderColor(Color.Yellow);
                    mesh.BoundingBox.render();
                }
            }

            //Dibujar los ocultos por Frustum culling
            bool showFrustumCull = (bool)GuiController.Instance.Modifiers["showFrustumCull"];
            if (showFrustumCull)
            {
                foreach (TgcMesh mesh in frustumCulledMeshes)
                {
                    mesh.BoundingBox.setRenderColor(Color.LightBlue);
                    mesh.BoundingBox.render();
                }
            }


            //Dibujar Frustum
            frustum.updateVolume(viewport.View, d3dDevice.Transform.Projection);
            frustum.render();


        }



        /// <summary>
        /// Movimiento del remoteMesh
        /// </summary>
        private Vector3 updateRemoteMesh(float elapsedTime)
        {
            float moveForward = 0f;
            float rotation = 0f;

            //Forward
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.W))
            {
                moveForward = -REMOTE_MESH_MOVEMENT_SPEED;
            }
            //Backward
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.S))
            {
                moveForward = REMOTE_MESH_MOVEMENT_SPEED;
            }

            //Rotate Left
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.A))
            {
                rotation = -REMOTE_MESH_ROTATE_SPEED;
            }
            //Rotate Right
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.D))
            {
                rotation = REMOTE_MESH_ROTATE_SPEED;
            }

            //Rotar y mover
            float amountToRotate = rotation;
            remoteMesh.rotateY(amountToRotate);
            remoteMesh.moveOrientedY(moveForward);

            //Calcular Look y View
            Vector3 cameraPos = remoteMesh.Position;
            float currentAngle = remoteMesh.Rotation.Y - remoteMeshOrigAngle;
            Vector3 remoteMeshLookAtVec = new Vector3(FastMath.Sin(currentAngle), 0, FastMath.Cos(currentAngle));
            Vector3 remoteMeshLookAt = cameraPos + remoteMeshLookAtVec * 10f;
            viewport.View = Matrix.LookAtLH(cameraPos, remoteMeshLookAt, new Vector3(0, 1, 0));

            
            //Actualizar mesh de Frustum
            if (moveForward != 0f || rotation != 0f)
            {
                frustum.updateMesh(cameraPos, remoteMeshLookAt);
            }

            //Actualizar camara
            GuiController.Instance.ThirdPersonCamera.Target = cameraPos;
            GuiController.Instance.ThirdPersonCamera.rotateY(amountToRotate);

            return cameraPos;
        }




        public override void close()
        {
            foreach (TgcMesh mesh in meshes)
            {
                mesh.dispose();
            }
            foreach (Occluder o in occluders)
            {
                o.dispose();
            }

            occlusionDll.dispose();
            skyBox.dispose();
            frustum.dispose();
        }

    }
}
