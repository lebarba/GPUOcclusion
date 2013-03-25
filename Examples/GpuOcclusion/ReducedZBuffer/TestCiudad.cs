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
using Examples.Shaders;
using TgcViewer.Utils.Shaders;
using TgcViewer.Utils;
using TgcViewer.Utils.Terrain;

namespace Examples.GpuOcclusion.ReducedZBuffer
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class TestCiudad : TgcExample
    {

        Effect effect;
        OcclusionEngineReducedZBuffer occlusionEngine;
        TgcSkyBox skyBox;


        public override string getCategory()
        {
            return "ReducedZBuffer";
        }

        public override string getName()
        {
            return "Test Ciudad";
        }

        public override string getDescription()
        {
            return "Test Ciudad";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-465.5077f, 20.0006f, 441.59f), new Vector3(-466.4288f, 20.3778f, 441.4932f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineReducedZBuffer();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\OccludeesShader.fx");
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

            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.Modifiers.addInt("maxTexels", 0, 1000000, 10000);
            GuiController.Instance.Modifiers.addBoolean("frustumCull", "frustumCull", true);
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);
            GuiController.Instance.Modifiers.addBoolean("drawMeshes", "drawMeshes", true);
            GuiController.Instance.Modifiers.addBoolean("drawOccluders", "drawOccluders", false);
            

            //UserVars
            GuiController.Instance.UserVars.addVar("frustumCull");
            GuiController.Instance.UserVars.addVar("occlusionCull");
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Activar culling
            occlusionEngine.FrustumCullingEnabled = (bool)GuiController.Instance.Modifiers["frustumCull"];
            occlusionEngine.OcclusionCullingEnabled = (bool)GuiController.Instance.Modifiers["occlusionCull"];

            //Umbral de texels
            occlusionEngine.MaxOccludeeSizeAllowed = (int)GuiController.Instance.Modifiers["maxTexels"];

            //Actualizar visibilidad
            occlusionEngine.updateVisibility();



            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.Yellow);


            //Render de meshes
            bool drawMeshes = (bool)GuiController.Instance.Modifiers["drawMeshes"];
            if (drawMeshes)
            {
                for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
                {
                    TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];

                    //Cargar varibles de shader propias de Occlusion
                    occlusionEngine.setOcclusionShaderValues(effect, i);

                    mesh.render();
                }
            }

            //Render de occluders
            bool drawOccluders = (bool)GuiController.Instance.Modifiers["drawOccluders"];
            if (drawOccluders)
            {
                for (int i = 0; i < occlusionEngine.Occluders.Count; i++)
                {
                    Occluder occluder = occlusionEngine.Occluders[i];
                    if (occluder.Enabled)
                    {
                        occluder.Aabb.render();
                    }
                }
            }


            //Skybox
            skyBox.render();


            //Meshes visibles
            GuiController.Instance.UserVars["frustumCull"] = occlusionEngine.EnabledOccludees.Count + "/" + occlusionEngine.Occludees.Count;


            //Debug: contar la cantidad de objetos occluidos (es lento)
            bool countOcclusion = (bool)GuiController.Instance.Modifiers["countOcclusion"];
            if (countOcclusion)
            {
                d3dDevice.RenderState.ZBufferEnable = false;
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
                        occlusionEngine.Occludees[i].BoundingBox.render();
                    }
                }
                d3dDevice.RenderState.ZBufferEnable = true;
                GuiController.Instance.UserVars["occlusionCull"] = n + "/" + occlusionEngine.EnabledOccludees.Count;
            }
            else
            {
                GuiController.Instance.UserVars["occlusionCull"] = "-";
            }



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
