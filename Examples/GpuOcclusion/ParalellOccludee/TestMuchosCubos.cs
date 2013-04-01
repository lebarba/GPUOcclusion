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
using TgcViewer.Utils._2D;

namespace Examples.GpuOcclusion.ParalellOccludee
{
    /// <summary>
    /// Test Muchos Cubos
    /// </summary>
    public class TestMuchosCubos : TgcExample
    {

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        List<TgcMeshShader> occludees;
        TgcBox occluderBox;


        public override string getCategory()
        {
            return "ParalellOccludee";
        }

        public override string getName()
        {
            return "Test Muchos Cubos";
        }

        public override string getDescription()
        {
            return "Test Muchos Cubos";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(150, 5, -100), new Vector3(150, 0, -1f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();

            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\OccludeesShaderMuchosCubos.fx");


            //Escenario

            //Box de occluder
            occluderBox = TgcBox.fromSize(new Vector3(150, 0, -20), new Vector3(600, 60, 5), Color.Green);

            //Crear occluder para el engine
            Occluder occluder = new Occluder(occluderBox.BoundingBox);
            occluder.update();
            occlusionEngine.Occluders.Add(occluder);


            //Texturas posibles
            TgcTexture[] textures = new TgcTexture[] {
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Door Rusty.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\dumbster.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Floor.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\fountan1.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\GlassPattern.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Grass.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Grey Bricks.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Road.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Marble.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Metal Pattern.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Path.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Red Bricks.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Road Union.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\WoodTexture.jpg"),
                TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Yellow Painted.jpg"),
            };
            
            //Occludee template
            float occludeeSize = 8;
            TgcMesh meshOccludeeTemplate = TgcBox.fromSize(new Vector3(0, 0, 0), new Vector3(occludeeSize, occludeeSize, occludeeSize), textures[0]).toMesh("occludee");



            //Crear n cubos de Occludees
            occludees = new List<TgcMeshShader>();
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    //Crear cubo
                    TgcMeshShader occludee = TgcMeshShader.fromTgcMesh(meshOccludeeTemplate, effect);
                    occludee.changeDiffuseMaps(new TgcTexture[] { textures[j % textures.Length] });
                    occludee.BoundingBox.setRenderColor(Color.White);
                    occludee.move(i * occludeeSize * 1.5f, 0, j * occludeeSize * 1.5f);

                    //Agregar occludee al engine
                    occlusionEngine.Occludees.Add(occludee);
                    occludees.Add(occludee);
                }
            }
            //meshOccludeeTemplate.dispose();

            //Iniciar engine
            occlusionEngine.init(occludees.Count);


            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);
            GuiController.Instance.Modifiers.addInterval("technique", new string[] { "Algoritmo", "Normal", "Discard" }, 0);

            //UserVars
            GuiController.Instance.UserVars.addVar("frus");
            GuiController.Instance.UserVars.addVar("occ");
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Activar culling
            occlusionEngine.OcclusionCullingEnabled = (bool)GuiController.Instance.Modifiers["occlusionCull"];

            //Actualizar visibilidad
            occlusionEngine.updateVisibility();


            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.Yellow);



            effect.Technique = (string)GuiController.Instance.Modifiers["technique"];

            //Cargar atributos de la luz
            effect.SetValue("lightColor", new ColorValue[] { ColorValue.FromColor(Color.White), ColorValue.FromColor(Color.Red), ColorValue.FromColor(Color.Blue), ColorValue.FromColor(Color.Green), ColorValue.FromColor(Color.Yellow), ColorValue.FromColor(Color.Brown) });
            effect.SetValue("lightPosition", new Vector4[] { new Vector4(100, 10, 100, 1), new Vector4(100, 10, 300, 1), new Vector4(300, 10, 100, 1), new Vector4(300, 10, 300, 1), new Vector4(200, 10, 400, 1), new Vector4(400, 10, 200, 1) });
            effect.SetValue("lightIntensity", new float[] { 20f, 20f, 20f, 20f, 20f, 20f });
            effect.SetValue("lightAttenuation", new float[] { 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f });
            effect.SetValue("eyePosition", TgcParserUtils.vector3ToFloat4Array(GuiController.Instance.FpsCamera.getPosition()));
            effect.SetValue("materialEmissiveColor", ColorValue.FromColor(Color.Black));
            effect.SetValue("materialAmbientColor", ColorValue.FromColor(Color.White));
            effect.SetValue("materialDiffuseColor", ColorValue.FromColor(Color.White));
            effect.SetValue("materialSpecularColor", ColorValue.FromColor(Color.White));
            effect.SetValue("materialSpecularExp", 9f);


            //Render de Occludees
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];

                //Cargar varibles de shader propias de Occlusion
                occlusionEngine.setOcclusionShaderValues(effect, i);

                mesh.render();
            }

            //Render de occluders
            for (int i = 0; i < occlusionEngine.Occluders.Count; i++)
            {
                Occluder occluder = occlusionEngine.Occluders[i];
                if (occluder.Enabled)
                {
                    occluder.Aabb.render();
                }
            }


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
            occlusionEngine.close();
            for (int i = 0; i < occludees.Count; i++)
            {
                occludees[i].dispose();
            }
            occluderBox.dispose();
            effect.Dispose();
        }

    }
}
