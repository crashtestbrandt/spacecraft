// Short pixel burst where a ship view disappeared. View-only; frees itself after Duration seconds.
using global::Godot;

namespace Spacecraft.View
{
    public partial class ExplosionView : Node2D
    {
        const float Duration = 0.35f;
        float _t;
        float _size;
        Color _color;

        public void Setup(Color color, float size)
        {
            _color = color;
            _size = size;
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            if (_t >= Duration) { QueueFree(); return; }
            QueueRedraw();
        }

        public override void _Draw()
        {
            float k = _t / Duration;
            float r = 2f + _size * k;
            var c = new Color(_color, 1f - k);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.Tau / 8f;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                DrawRect(new Rect2(p.X - 1f, p.Y - 1f, 2f, 2f), c);
            }
            DrawRect(new Rect2(-1f, -1f, 2f, 2f), new Color(1f, 1f, 1f, 1f - k));
        }
    }
}
