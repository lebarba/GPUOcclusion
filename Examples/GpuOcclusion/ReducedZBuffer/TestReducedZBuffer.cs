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
    /// Varios occludees uno atras del otro
    /// </summary>
    public class TestReducedZBuffer : TgcExample
    {

        Effect effect;
        OcclusionEngineReducedZBuffer occlusionEngine;
        List<TgcMeshShader> occludees;
        TgcBox occluderBox;
        TgcBox occluderBox2;
        TgcSprite depthBufferSprite;


        public override string getCategory()
        {
            return "ReducedZBuffer";
        }

        public override string getName()
        {
            return "Test Many Occludees";
        }

        public override string getDescription()
        {
            return "Test Many Occludees";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            GuiController.Instance.FpsCamera.Enable = true;
            GuiController.Instance.FpsCamera.setCamera(new Vector3(-40.1941f, 0f, 102.0864f), new Vector3(-39.92f, -0.0593f, 101.1265f));


            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineReducedZBuffer();

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



            //Occludees
            occludees = new List<TgcMeshShader>();
            TgcTexture occludeeTexture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\CiudadGrandeCerrada\\Textures\\Grey Bricks.jpg");
            for (int i = 0; i < 5; i++)
            {
                //Mesh de Occludee (se crea a partir de un TgcBox y luego se convierte a un TgcMeshShader)
                TgcMesh meshOccludee = TgcBox.fromSize(new Vector3(0, 0, -50 * (i + 1)), new Vector3(10, 30, 10), occludeeTexture).toMesh("occludee");
                TgcMeshShader occludee = TgcMeshShader.fromTgcMesh(meshOccludee, effect);
                meshOccludee.dispose();
                occludee.BoundingBox.setRenderColor(Color.White);

                //Agregar occludee al engine
                occlusionEngine.Occludees.Add(occludee);
                occludees.Add(occludee);
            }
            occlusionEngine.init(occludees.Count);



            //Debug para ver DepthBuffer
            depthBufferSprite = new TgcSprite();
            depthBufferSprite.Position = new Vector2(0, 20);
            depthBufferSprite.Texture = new TgcTexture("OcclusionResultTex", "OcclusionResultTex", occlusionEngine.HiZBufferTex, false);
            Vector2 scale = new Vector2(0.2f, 0.2f);
            depthBufferSprite.Scaling = scale;



            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("countOcclusion", "countOcclusion", false);
            GuiController.Instance.Modifiers.addBoolean("depthBuffer", "depthBuffer", false);

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
            for (int i = 0; i < occludees.Count; i++)
            {
                occlusionEngine.setOcclusionShaderValues(effect, i);
                occludees[i].render();
                occludees[i].BoundingBox.render();
            }


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


            //Debug: dibujar depthBuffer
            bool depthBuffer = (bool)GuiController.Instance.Modifiers["depthBuffer"];
            if (depthBuffer)
            {
                //Dibujar sprite
                GuiController.Instance.Drawer2D.beginDrawSprite();
                depthBufferSprite.render();
                GuiController.Instance.Drawer2D.endDrawSprite();
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
            occluderBox2.dispose();
            effect.Dispose();
        }

    }
}
