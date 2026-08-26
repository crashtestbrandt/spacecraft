// One ship on screen: a pixel-map sprite in the owner's color, a hit-point bar when damaged, and a
// selection ring when the local player has it selected. Rotation snaps to 16 directions for a
// sprite-sheet look. ArenaView owns placement and lifetime.
using global::Godot;

namespace Spacecraft.View
{
    public partial class ShipView : Node2D
    {
        public int OwnerId { get; private set; }
        public int Slot    { get; private set; }
        public int Kind    { get; private set; }
        public int EntityVersion { get; set; }

        int  _hp, _maxHp;
        bool _selected;
        string[] _map;
        Color _color;

        public void Setup(int ownerId, int slot, int kind, int maxHp)
        {
            OwnerId = ownerId;
            Slot = slot;
            Kind = kind;
            _maxHp = maxHp;
            _hp = maxHp;
            _map = Palette.MapFor(kind);
            _color = Palette.Of(ownerId);
            QueueRedraw();
        }

        public void SetHp(int hp)
        {
            if (hp == _hp) return;
            _hp = hp;
            QueueRedraw();
        }

        public void SetSelected(bool selected)
        {
            if (selected == _selected) return;
            _selected = selected;
            QueueRedraw();
        }

        // Facing in radians (0 = +x). Sprites are drawn facing up, so +90 degrees maps up to +x.
        public void SetFacing(float radians)
        {
            const float step = Mathf.Tau / 16f;
            float snapped = Mathf.Round((radians + Mathf.Pi / 2f) / step) * step;
            Rotation = snapped;
        }

        public override void _Draw()
        {
            if (_map == null) return;
            int h = _map.Length, w = _map[0].Length;
            float ox = -w / 2f, oy = -h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = _map[y][x];
                    if (c == '.') continue;
                    DrawRect(new Rect2(ox + x, oy + y, 1, 1), Palette.Shade(_color, c));
                }

            // Selection ring and HP bar are drawn in the un-rotated frame so they stay level.
            var inv = Transform2D.Identity.Rotated(-Rotation);
            DrawSetTransformMatrix(inv);
            float r = Mathf.Max(w, h) / 2f + 2f;
            if (_selected)
                DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 24, new Color(1f, 1f, 1f, 0.9f), 1f);
            if (_hp < _maxHp && _maxHp > 0)
            {
                float barW = w + 2f;
                float fill = barW * Mathf.Clamp((float)_hp / _maxHp, 0f, 1f);
                var top = new Vector2(-barW / 2f, r + 1f);
                DrawRect(new Rect2(top, new Vector2(barW, 1f)), new Color(0f, 0f, 0f, 0.7f));
                DrawRect(new Rect2(top, new Vector2(fill, 1f)), _hp * 3 < _maxHp ? new Color(1f, 0.3f, 0.2f) : new Color(0.5f, 1f, 0.4f));
            }
        }
    }
}
