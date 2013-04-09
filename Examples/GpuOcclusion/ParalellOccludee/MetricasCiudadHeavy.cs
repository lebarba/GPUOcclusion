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
    /// MetricasCiudadHeavy
    /// </summary>
    public class MetricasCiudadHeavy : TgcExample
    {

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        TgcSkyBox skyBox;
        List<CameraPos> cameraPositions;


        public override string getCategory()
        {
            return "ParalellOccludee";
        }

        public override string getName()
        {
            return "Metricas Ciudad Heavy";
        }

        public override string getDescription()
        {
            return "Metricas Ciudad Heavy";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-1473.558f, 20.0006f, 395.7999f), new Vector3(-1472.858f, 20.0678f, 395.0885f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\HeavyOccludeesShader.fx");


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


            //Posiciones fijas de camara
            cameraPositions = new List<CameraPos>();
            cameraPositions.Add(new CameraPos(new Vector3(-1473.558f, 26.8198f, 395.7999f), new Vector3(-1472.889f, 26.7767f, 395.0579f)));
            cameraPositions.Add(new CameraPos(new Vector3(-1461.971f, 26.8198f, 942.2886f), new Vector3(-1461.225f, 26.8009f, 941.6221f)));
            cameraPositions.Add(new CameraPos(new Vector3(-481.881f, 26.8198f, 941.5886f), new Vector3(-481.9601f, 26.8395f, 940.5919f)));
            cameraPositions.Add(new CameraPos(new Vector3(425.5923f, 26.8198f, 721.1125f), new Vector3(426.3636f, 26.8841f, 720.4792f)));
            cameraPositions.Add(new CameraPos(new Vector3(394.5311f, 26.8198f, 172.2037f), new Vector3(394.4579f, 26.858f, 171.2071f)));
            cameraPositions.Add(new CameraPos(new Vector3(1312.732f, 26.8198f, -2.1829f), new Vector3(1311.745f, 26.832f, -2.0233f)));
            cameraPositions.Add(new CameraPos(new Vector3(1351.24f, 26.8198f, -178.8094f), new Vector3(1350.466f, 26.7941f, -179.442f)));
            cameraPositions.Add(new CameraPos(new Vector3(1056.054f, 26.8197f, -943.3047f), new Vector3(1055.069f, 26.6945f, -943.1837f)));
            cameraPositions.Add(new CameraPos(new Vector3(1321.948f, 26.8195f, -1515.166f), new Vector3(1321.207f, 26.8419f, -1514.496f)));
            cameraPositions.Add(new CameraPos(new Vector3(1345.565f, 26.8195f, -1864.483f), new Vector3(1344.871f, 26.8583f, -1863.765f)));
            cameraPositions.Add(new CameraPos(new Vector3(1064.315f, 26.8195f, -1903.195f), new Vector3(1063.363f, 26.7869f, -1902.891f)));
            cameraPositions.Add(new CameraPos(new Vector3(472.9138f, 26.8194f, -1775.452f), new Vector3(472.6472f, 26.8023f, -1774.489f)));
            cameraPositions.Add(new CameraPos(new Vector3(138.5545f, 26.8194f, -1823.898f), new Vector3(137.8837f, 26.8335f, -1823.156f)));
            cameraPositions.Add(new CameraPos(new Vector3(-729.0896f, 26.8193f, -1901.118f), new Vector3(-728.6922f, 26.8449f, -1900.201f)));
            cameraPositions.Add(new CameraPos(new Vector3(-478.7969f, 26.8193f, -907.1873f), new Vector3(-479.1084f, 26.8154f, -906.237f)));


            //Modifiers
            GuiController.Instance.Modifiers.addInterval("camera", new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15"}, 0);
            GuiController.Instance.Modifiers.addInterval("technique", new string[] { "HeavyRender", "RenderWithOcclusionEnabled" }, 0);
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.Modifiers.addBoolean("frustumCull", "frustumCull", true);
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);
            GuiController.Instance.Modifiers.addBoolean("drawMeshes", "drawMeshes", true);
            GuiController.Instance.Modifiers.addBoolean("drawOccluders", "drawOccluders", false);
            

            //UserVars
            GuiController.Instance.UserVars.addVar("frus");
            GuiController.Instance.UserVars.addVar("occ");
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Camara
            int camIndex = int.Parse((string)GuiController.Instance.Modifiers["camera"]) - 1;
            CameraPos cameraPos = cameraPositions[camIndex];
            GuiController.Instance.FpsCamera.setCamera(cameraPos.pos, cameraPos.lookAt);



            //technique
            effect.Technique = (string)GuiController.Instance.Modifiers["technique"];

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
            GuiController.Instance.UserVars["frus"] = occlusionEngine.EnabledOccludees.Count + "/" + occlusionEngine.Occludees.Count;


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
                        occlusionEngine.EnabledOccludees[i].BoundingBox.render();
                    }
                }
                d3dDevice.RenderState.ZBufferEnable = true;
                GuiController.Instance.UserVars["occ"] = n + "/" + occlusionEngine.EnabledOccludees.Count;
            }
            else
            {
                GuiController.Instance.UserVars["occ"] = "-";
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



        private class CameraPos
        {
            public Vector3 pos;
            public Vector3 lookAt;

            public CameraPos(Vector3 pos, Vector3 lookAt)
            {
                this.pos = pos;
                this.lookAt = lookAt;
            }
        }

    }
}
