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
            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionOnly), VERTEX_COUNT, GuiController.Instance.D3dDevice,
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
            CustomVertex.PositionOnly[] vertices = new CustomVertex.PositionOnly[36];

            Vector3 extens = aabb.calculateAxisRadius();
            Vector3 center = aabb.calculateBoxCenter();
            float x = center.X + extens.X;
            float y = center.Y + extens.Y;
            float z = center.Z + extens.Z;

            // Front face
            vertices[0] = new CustomVertex.PositionOnly(-x, y, z);
            vertices[1] = new CustomVertex.PositionOnly(-x, -y, z);
            vertices[2] = new CustomVertex.PositionOnly(x, y, z);
            vertices[3] = new CustomVertex.PositionOnly(-x, -y, z);
            vertices[4] = new CustomVertex.PositionOnly(x, -y, z);
            vertices[5] = new CustomVertex.PositionOnly(x, y, z);

            // Back face (remember this is facing *away* from the camera, so vertices should be clockwise order)
            vertices[6] = new CustomVertex.PositionOnly(-x, y, -z);
            vertices[7] = new CustomVertex.PositionOnly(x, y, -z);
            vertices[8] = new CustomVertex.PositionOnly(-x, -y, -z);
            vertices[9] = new CustomVertex.PositionOnly(-x, -y, -z);
            vertices[10] = new CustomVertex.PositionOnly(x, y, -z);
            vertices[11] = new CustomVertex.PositionOnly(x, -y, -z);

            // Top face
            vertices[12] = new CustomVertex.PositionOnly(-x, y, z);
            vertices[13] = new CustomVertex.PositionOnly(x, y, -z);
            vertices[14] = new CustomVertex.PositionOnly(-x, y, -z);
            vertices[15] = new CustomVertex.PositionOnly(-x, y, z);
            vertices[16] = new CustomVertex.PositionOnly(x, y, z);
            vertices[17] = new CustomVertex.PositionOnly(x, y, -z);

            // Bottom face (remember this is facing *away* from the camera, so vertices should be clockwise order)
            vertices[18] = new CustomVertex.PositionOnly(-x, -y, z);
            vertices[19] = new CustomVertex.PositionOnly(-x, -y, -z);
            vertices[20] = new CustomVertex.PositionOnly(x, -y, -z);
            vertices[21] = new CustomVertex.PositionOnly(-x, -y, z);
            vertices[22] = new CustomVertex.PositionOnly(x, -y, -z);
            vertices[23] = new CustomVertex.PositionOnly(x, -y, z);

            // Left face
            vertices[24] = new CustomVertex.PositionOnly(-x, y, z);
            vertices[25] = new CustomVertex.PositionOnly(-x, -y, -z);
            vertices[26] = new CustomVertex.PositionOnly(-x, -y, z);
            vertices[27] = new CustomVertex.PositionOnly(-x, y, -z);
            vertices[28] = new CustomVertex.PositionOnly(-x, -y, -z);
            vertices[29] = new CustomVertex.PositionOnly(-x, y, z);

            // Right face (remember this is facing *away* from the camera, so vertices should be clockwise order)
            vertices[30] = new CustomVertex.PositionOnly(x, y, z);
            vertices[31] = new CustomVertex.PositionOnly(x, -y, z);
            vertices[32] = new CustomVertex.PositionOnly(x, -y, -z);
            vertices[33] = new CustomVertex.PositionOnly(x, y, -z);
            vertices[34] = new CustomVertex.PositionOnly(x, y, z);
            vertices[35] = new CustomVertex.PositionOnly(x, -y, -z);

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
