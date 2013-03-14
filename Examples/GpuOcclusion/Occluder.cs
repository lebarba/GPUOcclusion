using System;
using System.Collections.Generic;
using System.Text;
using TgcViewer.Utils.TgcGeometry;
using Microsoft.DirectX.Direct3D;
using TgcViewer;
using Microsoft.DirectX;

namespace Examples.GpuOcclusion
{
    /// <summary>
    /// Occluder para enviar a GPU
    /// </summary>
    public class Occluder
    {
        public const int TRIANGLE_COUNT = 12;

        /// <summary>
        /// Indices de BOX
        /// 4---------6
        /// |         |
        /// |         |
        /// 5---------7
        /// |          |
        /// |          |
        ///   0-------- 2
        ///   |         |
        ///   |         |
        ///   1--------3
        /// </summary>
        public static readonly short[] BOX_INDICES = {
                //Bottom face
                1, 2, 0,
                1, 3, 2,

                //Front face
                1, 5, 7,
                1, 7, 3,

                //Left face
                0, 4, 5,
                0, 5, 1,

                //Right face
                3, 7, 6,
                3, 6, 2,

                //Back face
                2, 6, 4,
                2, 4, 0,

                //Top face
                5, 4, 6,
                5, 6, 7
            };

        TgcBoundingBox aabb;
        /// <summary>
        /// AABB del Occluder
        /// </summary>
        public TgcBoundingBox Aabb
        {
            get { return aabb; }
            set { aabb = value; }
        }

        bool enabled;
        /// <summary>
        /// Indica si el Occluder esta activo
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        
        VertexBuffer vertexBuffer;
        /// <summary>
        /// VertexBuffer del Occluder
        /// </summary>
        public VertexBuffer VertexBuffer
        {
            get { return vertexBuffer; }
        }


        /// <summary>
        /// Crear Occluder
        /// </summary>
        public Occluder()
        {
            enabled = true;
            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionOnly), BOX_INDICES.Length, GuiController.Instance.D3dDevice,
                Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionOnly.Format, Pool.Default);
        }

        public Occluder(TgcBoundingBox aabb)
            : this()
        {
            this.aabb = aabb;
        }

        /// <summary>
        /// Actualizar VertexBuffer en base a la nueva posicion del BoundingBox.
        /// Se debe invocar manualmente si se alteró el BoundingBox.
        /// </summary>
        public void update()
        {
            CustomVertex.PositionOnly[] vertices = new CustomVertex.PositionOnly[BOX_INDICES.Length];

            Vector3 min = aabb.PMin;
            Vector3 max = aabb.PMax;

            vertices[0] = new CustomVertex.PositionOnly(min.X, min.Y, min.Z);
            vertices[1] = new CustomVertex.PositionOnly(min.X, min.Y, max.Z);
            vertices[2] = new CustomVertex.PositionOnly(max.X, min.Y, min.Z);
            vertices[3] = new CustomVertex.PositionOnly(max.X, min.Y, max.Z);
            vertices[4] = new CustomVertex.PositionOnly(min.X, max.Y, min.Z);
            vertices[5] = new CustomVertex.PositionOnly(min.X, max.Y, max.Z);
            vertices[6] = new CustomVertex.PositionOnly(max.X, max.Y, min.Z);
            vertices[7] = new CustomVertex.PositionOnly(max.X, max.Y, max.Z);

            vertexBuffer.SetData(vertices, 0, LockFlags.Discard);
        }


        /// <summary>
        /// FVF para formato de vertice de Occluder
        /// </summary>
        public static readonly VertexElement[] VERTEX_ELEMENTS = new VertexElement[]
        {
            new VertexElement(0, 0, DeclarationType.Float3,
                                    DeclarationMethod.Default,
                                    DeclarationUsage.Position, 0),
            VertexElement.VertexDeclarationEnd 
        };


    }
}
