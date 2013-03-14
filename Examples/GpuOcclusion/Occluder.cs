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
        public const int VERTEX_COUNT = 36;
        public const int INDEXED_VERTEX_COUNT = 8;

        //Indices de BOX
        private static readonly short[] BOX_INDICES = {
                0,1,2, // Front Face
                1,3,2, // Front Face
                4,5,6, // Back Face
                6,5,7, // Back Face
                0,5,4, // Top Face
                0,2,5, // Top Face
                1,6,7, // Bottom Face
                1,7,3, // Bottom Face
                0,6,1, // Left Face
                4,6,0, // Left Face
                2,3,7, // Right Face
                5,2,7 // Right Face
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

        IndexBuffer indexBuffer;
        /// <summary>
        /// IndexBuffer del Occluder
        /// </summary>
        public IndexBuffer IndexBuffer
        {
            get { return indexBuffer; }
        }

        /// <summary>
        /// Crear Occluder
        /// </summary>
        public Occluder()
        {
            enabled = true;
            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionOnly), INDEXED_VERTEX_COUNT, GuiController.Instance.D3dDevice,
                Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionOnly.Format, Pool.Default);

            indexBuffer = new IndexBuffer(typeof(short), BOX_INDICES.Length, GuiController.Instance.D3dDevice, Usage.WriteOnly, Pool.Default);
            indexBuffer.SetData(BOX_INDICES, 0, LockFlags.None);
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
            CustomVertex.PositionOnly[] vertices = new CustomVertex.PositionOnly[INDEXED_VERTEX_COUNT];

            Vector3 min = aabb.PMin;
            Vector3 max = aabb.PMax;

            vertices[0] = new CustomVertex.PositionOnly(min.X, max.Y, max.Z);
            vertices[1] = new CustomVertex.PositionOnly(min.X, min.Y, max.Z);
            vertices[2] = new CustomVertex.PositionOnly(max.X, max.Y, max.Z);
            vertices[3] = new CustomVertex.PositionOnly(max.X, min.Y, max.Z);
            vertices[4] = new CustomVertex.PositionOnly(min.X, max.Y, min.Z);
            vertices[5] = new CustomVertex.PositionOnly(max.X, max.Y, min.Z);
            vertices[6] = new CustomVertex.PositionOnly(min.X, min.Y, min.Z);
            vertices[7] = new CustomVertex.PositionOnly(max.X, min.Y, min.Z);

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
