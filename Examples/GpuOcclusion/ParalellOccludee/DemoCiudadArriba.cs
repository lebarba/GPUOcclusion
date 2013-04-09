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
using TgcViewer.Utils.Shaders;
using TgcViewer.Utils.Input;
using TgcViewer.Utils;
using TgcViewer.Utils.Interpolation;
using TgcViewer.Utils.Terrain;
using Examples.Shaders;

namespace Examples.GpuOcclusion.ParalellOccludee
{
    /// <summary>
    /// DemoCiudadArriba
    /// </summary>
    public class DemoCiudadArriba : TgcExample
    {
        const float REMOTE_MESH_MOVEMENT_SPEED = 400f;
        const float REMOTE_MESH_ROTATE_SPEED = 3f;
        const float CAMERA_ZOOM_SPEED = 80f;
        const float CAMERA_ROTATION_SPEED = 0.1f;
        const float DESTROY_MESH_DOWN_SPEED = 50f;
        const float DESTROY_MESH_ROTATE_SPEED = 0.1f;

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        TgcSkyBox skyBox;
        TgcFrustum frustum;
        TgcMesh remoteMesh;
        float remoteMeshOrigAngle;
        Viewport thirdPersonViewport;
        List<TgcMesh> destroyingMeshes;
        Matrix thirdPersonView;
        Matrix fpsView;
        TgcSprite cuadroNegro;


        public override string getCategory()
        {
            return "ParalellOccludee";
        }

        public override string getName()
        {
            return "Demo Ciudad Arriba";
        }

        public override string getDescription()
        {
            return "Demo Ciudad Arriba";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;


            //Pasar a modo render customizado
            GuiController.Instance.CustomRenderEnabled = true;

            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\OccludeesShader.fx");
            effect.Technique = "RenderWithOcclusionEnabled";

            //Cargar ciudad
            TgcSceneLoader loader = new TgcSceneLoader();
            loader.MeshFactory = new CustomMeshShaderFactory();
            TgcScene scene = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\CiudadGrandeCerrada-TgcScene.xml");

            //Separar occluders y occludees
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                TgcMeshShader mesh = (TgcMeshShader)scene.Meshes[i];
                if (mesh.Layer == "Occluders")
                {
                    Occluder occluder = new Occluder(mesh.BoundingBox.clone());
                    occluder.update();
                    occlusionEngine.Occluders.Add(occluder);
                    mesh.dispose();
                }
                else
                {
                    mesh.Effect = effect;
                    occlusionEngine.Occludees.Add(mesh);
                }
            }

            //Iniciar engine de occlusion
            occlusionEngine.init(occlusionEngine.Occludees.Count);

            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.Modifiers.addBoolean("frustumCull", "frustumCull", true);
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);


            //UserVars
            GuiController.Instance.UserVars.addVar("frus");
            GuiController.Instance.UserVars.addVar("occ");
 
            

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


            //Cargar mesh para manejar el Frustum remoto
            remoteMesh = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Auto\\Auto-TgcScene.xml").Meshes[0];
            remoteMesh.Position = new Vector3(0, 0, 0);
            remoteMeshOrigAngle = FastMath.PI;
            remoteMesh.rotateY(remoteMeshOrigAngle);

            //FrustumMesh
            Vector3 lookAt = remoteMesh.Position + new Vector3(0, 20, 1);
            frustum = new TgcFrustum();
            frustum.Effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\AlphaBlending.fx");
            frustum.Effect.Technique = "OnlyColorTechnique";
            frustum.AlphaBlendingValue = 0.3f;
            frustum.Color = Color.Red;
            frustum.updateMesh(remoteMesh.Position + new Vector3(0, 20, 0), lookAt);
            d3dDevice.RenderState.ReferenceAlpha = 0;


            //Camara en tercera persona
            GuiController.Instance.ThirdPersonCamera.Enable = true;
            GuiController.Instance.ThirdPersonCamera.setCamera(remoteMesh.Position, 700, -750);
            GuiController.Instance.ThirdPersonCamera.TargetDisplacement = new Vector3(0, 800, 0);


            //Viewport para tercera persona (es el original)
            thirdPersonViewport = d3dDevice.Viewport;

            //Cuadro negro para metricas
            cuadroNegro = new TgcSprite();
            cuadroNegro.Position = new Vector2(0, 0);
            cuadroNegro.Texture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Imagenes\\Black.png");
            cuadroNegro.Scaling = new Vector2(0.75f, 0.75f);
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Mover el remoteMesh
            Vector3 fpsPos;
            Vector3 fpsLookAt;
            updateRemoteMesh(elapsedTime, out fpsPos, out fpsLookAt);

            //Actualizar visibilidad de meshes
            updateVisibility(d3dDevice);

            //Dibujar escena vista desde arriba en Tercera Persona
            drawThirdPersonScene(d3dDevice);
        }
        

        /// <summary>
        /// Movimiento del remoteMesh
        /// </summary>
        private void updateRemoteMesh(float elapsedTime, out Vector3 fpsPos, out Vector3 fpsLookAt)
        {
            float moveForward = 0f;
            float rotation = 0f;

            //Forward
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.W) || GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.UpArrow))
            {
                moveForward = -REMOTE_MESH_MOVEMENT_SPEED;
            }
            //Backward
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.S) || GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.DownArrow))
            {
                moveForward = REMOTE_MESH_MOVEMENT_SPEED;
            }

            //Rotate Left
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.A) || GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.LeftArrow) || GuiController.Instance.D3dInput.keyPressed(Microsoft.DirectX.DirectInput.Key.PageUp))
            {
                rotation = -REMOTE_MESH_ROTATE_SPEED;
            }
            //Rotate Right
            if (GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.D) || GuiController.Instance.D3dInput.keyDown(Microsoft.DirectX.DirectInput.Key.RightArrow) || GuiController.Instance.D3dInput.keyPressed(Microsoft.DirectX.DirectInput.Key.PageDown))
            {
                rotation = REMOTE_MESH_ROTATE_SPEED;
            }

            //Rotar y mover
            float amountToRotate = rotation * elapsedTime;
            remoteMesh.rotateY(amountToRotate);
            remoteMesh.moveOrientedY(moveForward * elapsedTime);

            //Calcular Look y View
            fpsPos = remoteMesh.Position + new Vector3(0, /*10*/50, 0);
            float currentAngle = remoteMesh.Rotation.Y - remoteMeshOrigAngle;
            Vector3 remoteMeshLookAtVec = new Vector3(FastMath.Sin(currentAngle), 0, FastMath.Cos(currentAngle));
            fpsLookAt = fpsPos + remoteMeshLookAtVec * 10f;
            fpsView = Matrix.LookAtLH(fpsPos, fpsLookAt, new Vector3(0, 1, 0));

            //Actualizar mesh de Frustum
            if (moveForward != 0f || rotation != 0f)
            {
                frustum.updateMesh(fpsPos, fpsLookAt);
            }

            //Actualizar camara
            GuiController.Instance.ThirdPersonCamera.Target = fpsPos;
            GuiController.Instance.ThirdPersonCamera.rotateY(amountToRotate);
            GuiController.Instance.ThirdPersonCamera.updateCamera();
            GuiController.Instance.ThirdPersonCamera.updateViewMatrix(GuiController.Instance.D3dDevice);
            thirdPersonView = GuiController.Instance.D3dDevice.Transform.View;


        }

        /// <summary>
        /// Actualizar visibilidad de meshes, segun Occlusion
        /// </summary>
        private void updateVisibility(Device d3dDevice)
        {
            //Activar culling
            occlusionEngine.FrustumCullingEnabled = (bool)GuiController.Instance.Modifiers["frustumCull"];
            occlusionEngine.OcclusionCullingEnabled = (bool)GuiController.Instance.Modifiers["occlusionCull"];

            //Configurar camara desde el punto de vista del fps
            d3dDevice.Transform.View = fpsView;

            //Actualizar visibilidad
            occlusionEngine.updateVisibility();

            //Restaurar vista de tercera persona
            d3dDevice.Transform.View = thirdPersonView;
        }

        /// <summary>
        /// Dibujar escena vista desde arriba en Tercera Persona
        /// </summary>
        private void drawThirdPersonScene(Device d3dDevice)
        {
            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Dibujar recuadro para metricas
            GuiController.Instance.Drawer2D.beginDrawSprite();
            cuadroNegro.render();
            GuiController.Instance.Drawer2D.endDrawSprite();

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.LawnGreen);



            //Skybox
            skyBox.render();

            //Remote mesh
            remoteMesh.render();

            //Render de meshes opacos
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];
                if (!mesh.AlphaBlendEnable)
                {
                    //Cargar varibles de shader propias de Occlusion
                    occlusionEngine.setOcclusionShaderValues(effect, i);

                    mesh.render();
                }
            }

            //Render de meshes con alpha
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];
                if (mesh.AlphaBlendEnable)
                {
                    //Cargar varibles de shader propias de Occlusion
                    occlusionEngine.setOcclusionShaderValues(effect, i);

                    mesh.render();
                }
            }

            //Debug: contar la cantidad de objetos occluidos (es lento)
            bool countOcclusion = (bool)GuiController.Instance.Modifiers["countOcclusion"];
            if (countOcclusion)
            {
                //d3dDevice.RenderState.ZBufferEnable = false;
                bool[] data = occlusionEngine.getVisibilityData();
                int n = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i])
                    {
                        n++;
                    }
                    else
                    {
                        occlusionEngine.EnabledOccludees[i].BoundingBox.render();
                    }
                }
                //d3dDevice.RenderState.ZBufferEnable = true;
                GuiController.Instance.UserVars["occ"] = n + "/" + occlusionEngine.EnabledOccludees.Count;
            }
            else
            {
                GuiController.Instance.UserVars["occ"] = "-";
            }

            //Dibujar Frustum
            frustum.updateVolume(fpsView, d3dDevice.Transform.Projection);
            frustum.render();


            //Meshes visibles
            GuiController.Instance.UserVars["frus"] = occlusionEngine.EnabledOccludees.Count + "/" + occlusionEngine.Occludees.Count;


            



            d3dDevice.EndScene();

        }


        public override void close()
        {
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                occlusionEngine.EnabledOccludees[i].dispose();
            }
            occlusionEngine.close();
            occlusionEngine = null;
            effect.Dispose();
            skyBox.dispose();
        }

    }
}
