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

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class TestTwoOccludees : TgcExample
    {

        Effect effect;
        OcclusionEngine occlusionEngine;
        TgcMeshShader occludee;
        TgcMeshShader occludee2;
        TgcBox occluderBox;
        TgcBox occluderBox2;


        public override string getCategory()
        {
            return "GPUCulling";
        }

        public override string getName()
        {
            return "Test 2 Occludees";
        }

        public override string getDescription()
        {
            return "Test 2 Occludees";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-40.1941f, 0f, 102.0864f), new Vector3(-39.92f, -0.0593f, 101.1265f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngine();
            occlusionEngine.init(2);


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\OccludeesShader.fx");
            effect.Technique = "RenderWithOcclusionEnabled";


            //Escenario

            //Box de occluder
            occluderBox = TgcBox.fromSize(new Vector3(0, 0, -20), new Vector3(100, 30, -15), Color.Green);

            //Crear occluder para el engine
            Occluder occluder = new Occluder(occluderBox.BoundingBox);
            occluder.update();
            occlusionEngine.Occluders.Add(occluder);


            //Occluder 2
            occluderBox2 = TgcBox.fromSize(new Vector3(50, 0, -50), new Vector3(5, 30, 100), Color.Green);
            Occluder occluder2 = new Occluder(occluderBox2.BoundingBox);
            occluder2.update();
            occlusionEngine.Occluders.Add(occluder2);



            //Mesh de Occludee (se crea a partir de un TgcBox y luego se convierte a un TgcMeshShader)
            TgcTexture occludeeTexture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Grey Bricks.jpg");
            TgcMesh meshOccludee = TgcBox.fromSize(new Vector3(0, 0, -50), new Vector3(10, 30, 10), occludeeTexture).toMesh("occludee");
            occludee = TgcMeshShader.fromTgcMesh(meshOccludee, effect);
            meshOccludee.dispose();
            occludee.BoundingBox.setRenderColor(Color.White);

            //Agregar occludee al engine
            occlusionEngine.Occludees.Add(occludee);

            //Occludee2
            TgcMesh meshOccludee2 = TgcBox.fromSize(new Vector3(-30, 0, -50), new Vector3(10, 30, 10), occludeeTexture).toMesh("occludee2");
            occludee2 = TgcMeshShader.fromTgcMesh(meshOccludee2, effect);
            meshOccludee2.dispose();
            occludee2.BoundingBox.setRenderColor(Color.White);
            occlusionEngine.Occludees.Add(occludee2);

            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.UserVars.addVar("occlusionCull");
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;


            //TODO: Hacer FrustumCulling previamente

            //Hacer Occlusion-Culling
            occlusionEngine.updateVisibility();



            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.Yellow);


            //Render de Occludee. Cargar todas las variables de shader propias de Occlusion
            occlusionEngine.setOcclusionShaderValues(effect, 0);
            occludee.render();
            occlusionEngine.setOcclusionShaderValues(effect, 1);
            occludee2.render();

            //Debug: Mostrar AABB de occludees
            occludee.BoundingBox.render();
            occludee2.BoundingBox.render();

            //Render de AABB de Occluder para debug
            occluderBox.BoundingBox.render();
            occluderBox2.BoundingBox.render();




            //Debug: contar la cantidad de objetos occluidos (es lento)
            bool countOcclusion = (bool)GuiController.Instance.Modifiers["countOcclusion"];
            if (countOcclusion)
            {
                bool[] data = occlusionEngine.getVisibilityData();
                int n = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i]) n++;
                }
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
            occlusionEngine.close();
            occludee.dispose();
            occludee2.dispose();
            occluderBox.dispose();
            occluderBox2.dispose();
            effect.Dispose();
        }

    }
}
