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

namespace Examples.GpuOcclusion.ReducedZBuffer
{
    /// <summary>
    /// Test Discard VS
    /// </summary>
    public class TestDiscardVS : TgcExample
    {

        Effect effect;
        List<TgcMeshShader> meshes;


        public override string getCategory()
        {
            return "ReducedZBuffer";
        }

        public override string getName()
        {
            return "Test Discard VS";
        }

        public override string getDescription()
        {
            return "Test Discard VS";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-310.197f, 237.8115f, 157.7287f), new Vector3(-309.5159f, 237.4767f, 157.0775f));


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\ReducedZBuffer\\DiscardVS.fx");


            meshes = new List<TgcMeshShader>();
            TgcSceneLoader loader = new TgcSceneLoader();
            loader.MeshFactory = new CustomMeshShaderFactory();

            for (int i = 0; i < 15; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    TgcMeshShader mesh = (TgcMeshShader)loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\TanqueFuturistaOrugas\\TanqueFuturistaOrugas-TgcScene.xml").Meshes[0];
                    mesh.Effect = effect;
                    Vector3 size = mesh.BoundingBox.calculateSize();
                    mesh.move(i * size.X * 1.5f, 0, j * size.Z * 1.5f);
                    meshes.Add(mesh);
                } 
            }


            GuiController.Instance.Modifiers.addInterval("technique", new string[] { "Normal", "Discard", "DiscardTexel", "DiscardTexelIf" }, 0);
            GuiController.Instance.Modifiers.addBoolean("render", "render", true);

        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;


            effect.Technique = (string)GuiController.Instance.Modifiers["technique"];
            bool renderEnabled = (bool)GuiController.Instance.Modifiers["render"];


            foreach (TgcMeshShader mesh in meshes)
            {
                if (renderEnabled)
                {
                    mesh.render();
                }
            }

        }



        public override void close()
        {
            foreach (TgcMeshShader mesh in meshes)
            {
                mesh.dispose();
            }
            effect.Dispose();
        }

    }
}
