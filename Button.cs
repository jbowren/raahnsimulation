using System;
using SFML.Window;
using Tao.OpenGl;

namespace RaahnSimulation
{
    public class Button : ClickableEntity
    {
        private const double CHAR_HEIGHT_PERCENTAGE = 0.8;

        private Text label;

        public Button(Simulator sim, string text) : base(sim)
        {
            Construct(text);
        }

        public Button(Simulator sim, string text, double width, double height, double x, double y) : base(sim)
        {
            Construct(text);
            SetBounds(x, y, width, height, false);
        }

        public void SetBounds(double x, double y, double Width, double Height, bool fromCenter)
        {
            width = Width;
            height = Height;

            aabb.SetSize(width, height);

            label.SetMaxLength(width);

            if (fromCenter)
            {
                drawingVec.x = x - (width / 2.0);
                drawingVec.y = y - (height / 2.0);
            }
            else
            {
                drawingVec.x = x;
                drawingVec.y = y;
            }

            double charHeight = height * CHAR_HEIGHT_PERCENTAGE;

            label.SetCharBounds(drawingVec.x + (width / 2.0), drawingVec.y + (height / 2.0), charHeight, charHeight, true);

            if (label.GetWidth() > width)
                label.SetCharBounds(drawingVec.x, drawingVec.y, charHeight, charHeight, false);
        }

        public override void Update()
        {
            base.Update();

            if (hovering)
                transparency = 0.5;
            else
                transparency = 1.0;

            label.Update();
        }

        public override void UpdateEvent(Event e)
        {
            base.UpdateEvent(e);

            label.UpdateEvent(e);
        }

        public override void Draw()
        {
            base.Draw();

            Gl.glColor4d(1.0, 1.0, 1.0, transparency);

            Gl.glPushMatrix();

            Gl.glTranslated(drawingVec.x, drawingVec.y, Utils.DISCARD_Z_POS);
            Gl.glScaled(width, height, Utils.DISCARD_Z_SCALE);

            Gl.glDrawElements(mesh.GetRenderMode(), mesh.GetIndexCount(), Gl.GL_UNSIGNED_SHORT, IntPtr.Zero);

            Gl.glPopMatrix();

            Gl.glColor4d(1.0, 1.0, 1.0, transparency);

            label.Draw();
        }

        public override void DebugDraw()
        {
            Gl.glPushMatrix();

            base.DebugDraw();

            Gl.glPopMatrix();

            Gl.glPushMatrix();

            label.DebugDraw();

            Gl.glPopMatrix();
        }

        private void Construct(string text)
        {
            label = new Text(context, text);
            label.SetTransformUsage(false);
            label.SetColor(1.0, 1.0, 1.0);

            texture = TextureManager.TextureType.BUTTON;
            transparency = 1.0;
        }
    }
}
