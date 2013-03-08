using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer;
using Microsoft.DirectX.Direct3D;
using TgcViewer.Utils.TgcGeometry;

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Utilidades generales
    /// </summary>
    public class GpuOcclusionUtils
    {

        /// <summary>
        /// Proyectar AABB a 2D
        /// </summary>
        /// <param name="box3d">BoundingBox 3D</param>
        /// <param name="viewport">Viewport</param>
        /// <param name="box2D">Rectangulo 2D proyectado</param>
        /// <returns>True si debe considerarse visible por ser algun caso degenerado</returns>
        public static bool projectBoundingBox(TgcBoundingBox box3d, Viewport viewport, out BoundingBox2D box2D)
        {
            //Datos de viewport
            Device d3dDevice = GuiController.Instance.D3dDevice;
            Matrix view = d3dDevice.Transform.View;
            Matrix proj = d3dDevice.Transform.Projection;
            int width = viewport.Width;
            int height = viewport.Height;


            box2D = new BoundingBox2D();

            //Proyectar los 8 puntos, sin dividir aun por W
            Vector3[] corners = box3d.computeCorners();
            Matrix m = view * proj;
            Vector3[] projVertices = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector4 pOut = Vector3.Transform(corners[i], m);
                if (pOut.W < 0) return true;
                projVertices[i] = GpuOcclusionUtils.toScreenSpace(pOut, width, height);
            }

            //Buscar los puntos extremos
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            float minDepth = float.MaxValue;
            foreach (Vector3 v in projVertices)
            {
                if (v.X < min.X)
                {
                    min.X = v.X;
                }
                if (v.Y < min.Y)
                {
                    min.Y = v.Y;
                }
                if (v.X > max.X)
                {
                    max.X = v.X;
                }
                if (v.Y > max.Y)
                {
                    max.Y = v.Y;
                }

                if (v.Z < minDepth)
                {
                    minDepth = v.Z;
                }
            }


            //Clamp
            if (min.X < 0f) min.X = 0f;
            if (min.Y < 0f) min.Y = 0f;
            if (max.X >= width) max.X = width - 1;
            if (max.Y >= height) max.Y = height - 1;

            //Control de tamaño minimo
            if (max.X - min.X < 1f) return true;
            if (max.Y - min.Y < 1f) return true;

            //Cargar valores de box2D
            box2D.min = min;
            box2D.max = max;
            box2D.depth = minDepth;
            return false;
        }

        /// <summary>
        /// Pasa un punto a screen-space
        /// </summary>
        public static Vector3 toScreenSpace(Vector4 p, int width, int height)
        {
            //divido por w, (lo paso al proj. space)
            p.X = p.X / p.W;
            p.Y = p.Y / p.W;
            p.Z = p.Z / p.W;

            //lo paso a screen space
            p.X = (int)(0.5f + ((p.X + 1) * 0.5f * width));
            p.Y = (int)(0.5f + ((1 - p.Y) * 0.5f * height));

            return new Vector3(p.X, p.Y, p.Z);
        }



        /// <summary>
        /// BoundingBox 2D
        /// </summary>
        public struct BoundingBox2D
        {
            public Vector2 min;
            public Vector2 max;
            public float depth;
            public bool visible;

            public override string ToString()
            {
                return "Min(" + TgcParserUtils.printFloat(min.X) + ", " + TgcParserUtils.printFloat(min.Y) + "), Max(" + TgcParserUtils.printFloat(max.X) + ", " + TgcParserUtils.printFloat(max.Y) + "), Z: " + TgcParserUtils.printFloat(depth);
            }
        }

        public static bool isPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }

        public static int nextHighestPowerOfTwo(int x)
        {
	        --x;
            for (int i = 1; i < 32; i <<= 1) 
            {
                x = x | x >> i;
            }
            return x + 1;
        }

        public static int getNextHighestPowerOfTwo(int x)
        {
            if (!isPowerOfTwo(x))
            {
                return nextHighestPowerOfTwo(x);
            }
            return x;
        }
    }
}
