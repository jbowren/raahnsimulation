using System;
using OpenTK.Graphics.OpenGL;

namespace RaahnSimulation
{
    public class Point : Entity
    {
        private const float POINT_SIZE = 6.0f;
        private const double COLOR_R = 1.0;
        private const double COLOR_G = 0.0;
        private const double COLOR_B = 0.0;
        private const double COLOR_A = 1.0;
        private static readonly float[] VERTICIES = { 0.0f, 0.0f };
        private static readonly ushort[] INDICES = { 0 };

        private static Mesh sharedMesh = null;

        public Point(Simulator sim) : base(sim)
        {
            if (sharedMesh == null)
            {
                sharedMesh = new Mesh(2, BeginMode.Points);

                sharedMesh.SetVertices(VERTICIES, false);
                sharedMesh.SetIndices(INDICES);
                sharedMesh.Allocate(BufferUsageHint.StaticDraw);

                GL.PointSize(POINT_SIZE);
            }

            type = EntityType.POINT;

            color.x = COLOR_R;
            color.y = COLOR_G;
            color.z = COLOR_B;
            transparency = COLOR_A;
        }

        public static void CleanShared()
        {
            if (sharedMesh != null)
                sharedMesh.Free();
        }

        public override void Draw()
        {
            GL.Disable(EnableCap.Texture2D);

            GL.Color4(color.x, color.y, color.z, transparency);

            sharedMesh.MakeCurrent();

            GL.Translate(drawingVec.x, drawingVec.y, Utils.DISCARD_Z_POS);

            GL.DrawElements(BeginMode.Points, sharedMesh.GetIndexCount(), DrawElementsType.UnsignedShort, IntPtr.Zero);

            GL.Color4(1.0, 1.0, 1.0, 1.0);

            GL.Enable(EnableCap.Texture2D);
        }

        public override void ResetColor()
        {
            color.x = COLOR_R;
            color.y = COLOR_G;
            color.z = COLOR_B;
            transparency = COLOR_A;
        }
    }
}

