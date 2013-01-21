using System;
using System.Collections.Generic;
using System.Text;
using TgcViewer.Example;
using TgcViewer;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using Microsoft.DirectX;
using TgcViewer.Utils.Modifiers;

namespace Examples
{
    /// <summary>
    /// Ejemplo en Blanco. Ideal para copiar y pegar cuando queres empezar a hacer tu propio ejemplo.
    /// </summary>
    public class EjemploEnBlanco : TgcExample
    {

        CustomVertex.TransformedTextured[] vertices = null;
        Texture texture = null;

        public override string getCategory()
        {
            return "Otros";
        }

        public override string getName()
        {
            return "Ejemplo en Blanco";
        }

        public override string getDescription()
        {
            return "Ejemplo en Blanco. Ideal para copiar y pegar cuando queres empezar a hacer tu propio ejemplo.";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            vertices = new CustomVertex.TransformedTextured[4];

            // bottom left
            vertices[0].X = 0;
            vertices[0].Y = 200.0f;
            vertices[0].Z = 0.0f;
            vertices[0].Tu = 0.0f;
            vertices[0].Tv = 1.0f;

            // top left
            vertices[1].X = 100.0f;
            vertices[1].Y = 100.0f;
            vertices[1].Z = 0.0f;
            vertices[1].Tu = 0.0f;
            vertices[1].Tv = 0.0f;

            // bottom right
            vertices[2].X = 200.0f;
            vertices[2].Y = 200.0f;
            vertices[2].Z = 0.0f;
            vertices[2].Tu = 1.0f;
            vertices[2].Tv = 1.0f;

            // top right
            vertices[3].X = 200.0f;
            vertices[3].Y = 100.0f;
            vertices[3].Z = 0.0f;
            vertices[3].Tu = 1.0f;
            vertices[3].Tv = 0.0f;
        }


        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            d3dDevice.SetTexture(0, texture);
            d3dDevice.VertexFormat = CustomVertex.TransformedTextured.Format;
            d3dDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, 2, vertices);

        }

        public override void close()
        {

        }

    }
}
