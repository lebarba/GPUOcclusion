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

namespace Examples.GpuOcclusion.ParalellOccludee
{
    /// <summary>
    /// Ciudad con Read Back
    /// </summary>
    public class TestCiudadReadBack : TgcExample
    {

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        TgcSkyBox skyBox;


        public override string getCategory()
        {
            return "ParalellOccludee";
        }

        public override string getName()
        {
            return "Test Ciudad Read Back";
        }

        public override string getDescription()
        {
            return "Test Ciudad Read Back";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-465.5077f, 20.0006f, 441.59f), new Vector3(-466.4288f, 20.3778f, 441.4932f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\OccludeesShaderReadBack.fx");


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
            GuiController.Instance.Modifiers.addBoolean("readBack", "readBack", false);
            GuiController.Instance.Modifiers.addBoolean("showHidden", "showHidden", false);
            GuiController.Instance.Modifiers.addBoolean("frustumCull", "frustumCull", true);
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);
            

            //UserVars
            GuiController.Instance.UserVars.addVar("frus");
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Activar culling
            occlusionEngine.FrustumCullingEnabled = (bool)GuiController.Instance.Modifiers["frustumCull"];
            occlusionEngine.OcclusionCullingEnabled = (bool)GuiController.Instance.Modifiers["occlusionCull"];

            //Actualizar visibilidad
            occlusionEngine.updateVisibility();



            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.Yellow);

            //Skybox
            skyBox.render();


            //Ver si hacemos readBack
            bool readBack = (bool)GuiController.Instance.Modifiers["readBack"];
            bool showHidden = (bool)GuiController.Instance.Modifiers["showHidden"];
            if (readBack)
            {
                effect.Technique = "NormalRender";

                //Traer datos de visibilidad de gpu
                bool[] data = occlusionEngine.getVisibilityData();
                for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
                {
                    //Solo dibujar si es visible
                    if (data[i])
                    {
                        TgcMeshShader occludee = occlusionEngine.EnabledOccludees[i];
                        occlusionEngine.setOcclusionShaderValues(effect, i);
                        occludee.render();
                    }
                }

                //Dibujar AABB de ocultos
                if (showHidden)
                {
                    d3dDevice.RenderState.ZBufferEnable = false;
                    for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
                    {
                        //Oculto
                        if (!data[i])
                        {
                            TgcMeshShader occludee = occlusionEngine.EnabledOccludees[i];
                            occludee.BoundingBox.render();
                        }
                    }
                }
            }
            else
            {
                effect.Technique = "RenderWithOcclusionEnabled";

                //Render de Occludee. Cargar todas las variables de shader propias de Occlusion
                for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
                {
                    TgcMeshShader occludee = occlusionEngine.EnabledOccludees[i];

                    occlusionEngine.setOcclusionShaderValues(effect, i);
                    occludee.render();
                }
            }




            


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
