// A bullet: a 1x4 streak in the owner's color, rotated along its travel direction.
using global::Godot;

namespace Spacecraft.View
{
    public partial class BulletView : Node2D
    {
        public int EntityVersion { get; set; }
        Color _color;

        public void Setup(int ownerId)
        {
            _color = Palette.Of(ownerId).Lightened(0.3f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(-2f, -0.5f, 4f, 1f), _color);
            DrawRect(new Rect2(0f, -0.5f, 1f, 1f), new Color(1f, 1f, 1f));
        }
    }
}
