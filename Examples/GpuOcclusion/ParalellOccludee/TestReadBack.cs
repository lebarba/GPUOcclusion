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
    /// ReadBack
    /// </summary>
    public class TestReadBack : TgcExample
    {

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        TgcBox occluderBox;
        TgcBox occluderBox2;


        public override string getCategory()
        {
            return "ParalellOccludee";
        }

        public override string getName()
        {
            return "Test Read Back";
        }

        public override string getDescription()
        {
            return "Test Read Back";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-40.1941f, 0f, 102.0864f), new Vector3(-39.92f, -0.0593f, 101.1265f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();

            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ParalellOccludee\\OccludeesShaderReadBack.fx");


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



            //Occludees
            TgcTexture occludeeTexture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Grey Bricks.jpg");
            for (int i = 0; i < 15; i++)
            {
                //Mesh de Occludee (se crea a partir de un TgcBox y luego se convierte a un TgcMeshShader)
                TgcMesh meshOccludee = TgcBox.fromSize(new Vector3(0, 0, -50 * (i + 1)), new Vector3(10, 30, 10), occludeeTexture).toMesh("occludee");
                TgcMeshShader occludee = TgcMeshShader.fromTgcMesh(meshOccludee, effect);
                meshOccludee.dispose();
                occludee.BoundingBox.setRenderColor(Color.White);

                //Agregar occludee al engine
                occlusionEngine.Occludees.Add(occludee);
            }
            occlusionEngine.init(occlusionEngine.Occludees.Count);


            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("readBack", "readBack", false);

            GuiController.Instance.UserVars.addVar("occ");
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

            //Ver si hacemos readBack
            bool readBack = (bool)GuiController.Instance.Modifiers["readBack"];
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
                        occludee.BoundingBox.render();
                    }
                    else
                    {
                        TgcMeshShader occludee = occlusionEngine.EnabledOccludees[i];
                        occludee.BoundingBox.render();
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
                    occludee.BoundingBox.render();
                }
            }



            


            //Render de AABB de Occluder para debug
            occluderBox.BoundingBox.render();
            occluderBox2.BoundingBox.render();




            d3dDevice.EndScene();


        }



        public override void close()
        {
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                occlusionEngine.EnabledOccludees[i].dispose();
            }
            occlusionEngine.close();
            occluderBox.dispose();
            occluderBox2.dispose();
            effect.Dispose();
        }

    }
}
