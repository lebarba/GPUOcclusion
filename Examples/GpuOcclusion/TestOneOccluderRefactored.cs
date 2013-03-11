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

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class TestOneOccluderRefactored : TgcExample
    {

        Effect effect;
        OcclusionEngine occlusionEngine;
        TgcMeshShader occludee;
        TgcBox occluderBox;


        public override string getCategory()
        {
            return "GPUCulling";
        }

        public override string getName()
        {
            return "Test One Occluder Refactored";
        }

        public override string getDescription()
        {
            return "Test One Occluder Refactored";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-40.1941f, 0f, 102.0864f), new Vector3(-39.92f, -0.0593f, 101.1265f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngine();
            occlusionEngine.init();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\OccludeesShader.fx");
            effect.Technique = "RenderWithOcclusionEnabled";


            //Escenario


            //Box de occluder
            occluderBox = TgcBox.fromSize(new Vector3(0, 0, 0), new Vector3(100, 30, 5), Color.Green);

            //Crear occluder para el engine
            Occluder occluder = new Occluder(occluderBox.BoundingBox);
            occlusionEngine.Occluders.Add(occluder);


            //Mesh de Occludee (se crea a partir de un TgcBox y luego se convierte a un TgcMeshShader)
            TgcMesh meshOccludee = TgcBox.fromSize(new Vector3(0, 0, -50), new Vector3(10, 30, 10), Color.Red).toMesh("occludee");
            occludee = TgcMeshShader.fromTgcMesh(meshOccludee, effect);
            meshOccludee.dispose();

            //Agregar occludee al engine
            occlusionEngine.Occludees.Add(occludee);

        }


        public override void render(float elapsedTime)
        {
            //TODO: Hacer FrustumCulling previamente
            
            //Hacer Occlusion-Culling
            occlusionEngine.updateVisibility();



            //Render de Occludee. Cargar todas las variables de shader propias de Occlusion
            occlusionEngine.setOcclusionShaderValues(effect, 0);
            occludee.render();


            //Render de AABB de Occluder para debug
            occluderBox.BoundingBox.render();
        }



        public override void close()
        {
            occlusionEngine.close();
            occludee.dispose();
            occluderBox.dispose();
            effect.Dispose();
        }

    }
}
