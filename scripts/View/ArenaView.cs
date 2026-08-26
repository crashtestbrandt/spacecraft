// The 2D render of the simulation plus the local player's RTS input.
//
// Klotho's own view layer (EntityViewNode / EntityViewUpdaterNode) is Node3D-based, so this node does
// the same two jobs in 2D: on every executed tick it reconciles child views against the predicted
// frame's live entities, and every rendered frame it interpolates each view between the previous and
// current predicted frames by the engine's render alpha.
//
// Input: left-drag box-selects the local player's ships (a click picks the nearest), Ctrl+A selects
// all, right-click queues a move order for the selection. Orders go to the OrderQueue; the sim
// callbacks turn them into commands. Selection is view-local state keyed by army slot.
using System.Collections.Generic;
using global::Godot;
using Spacecraft.Sim;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.View
{
    public partial class ArenaView : Node2D
    {
        IKlothoEngine _engine;
        OrderQueue _orders;
        int _localPlayerId = -1;

        readonly Dictionary<int, ShipView>   _ships   = new();
        readonly Dictionary<int, BulletView> _bullets = new();
        readonly List<int> _stale = new();
        EntityRef[] _scratch;

        int _selectionMask;
        bool _dragging;
        Vector2 _dragStart, _dragEnd;
        Vector2 _orderMarker;
        float _orderMarkerAge = 10f;

        public int LiveShipCount => _ships.Count;

        public void Initialize(IKlothoEngine engine, int localPlayerId, OrderQueue orders)
        {
            Cleanup();
            _engine = engine;
            _localPlayerId = localPlayerId;
            _orders = orders;
            _engine.OnTickExecuted += OnTickExecuted;
        }

        public void Cleanup()
        {
            if (_engine != null) _engine.OnTickExecuted -= OnTickExecuted;
            _engine = null;
            foreach (var v in _ships.Values) v.QueueFree();
            foreach (var v in _bullets.Values) v.QueueFree();
            _ships.Clear();
            _bullets.Clear();
            _selectionMask = 0;
            _dragging = false;
            QueueRedraw();
        }

        // Ships alive per player, read from the predicted frame (HUD counts).
        public void CountShips(int[] counts)
        {
            for (int i = 0; i < counts.Length; i++) counts[i] = 0;
            var frame = _engine?.PredictedFrame.Frame;
            if (frame == null) return;
            var f = frame.Filter<ShipComponent, OwnerComponent>();
            while (f.Next(out var e))
            {
                int o = frame.GetReadOnly<OwnerComponent>(e).OwnerId;
                if (o >= 0 && o < counts.Length) counts[o]++;
            }
        }

        // ── reconcile (per executed tick) ──

        void OnTickExecuted(int tick)
        {
            var frame = _engine?.PredictedFrame.Frame;
            if (frame == null) return;

            if (_scratch == null || _scratch.Length < frame.MaxEntities)
                _scratch = new EntityRef[frame.MaxEntities];
            int n = frame.GetAllLiveEntities(_scratch);

            // Mark everything stale, then un-mark what is still present.
            var presentShips = new HashSet<int>();
            var presentBullets = new HashSet<int>();

            for (int i = 0; i < n; i++)
            {
                var e = _scratch[i];
                if (!frame.Has<TransformComponent>(e) || !frame.Has<OwnerComponent>(e)) continue;
                int owner = frame.GetReadOnly<OwnerComponent>(e).OwnerId;

                if (frame.Has<ShipComponent>(e))
                {
                    ref readonly var ship = ref frame.GetReadOnly<ShipComponent>(e);
                    if (!_ships.TryGetValue(e.Index, out var view) || view.EntityVersion != e.Version)
                    {
                        if (view != null) view.QueueFree();
                        view = new ShipView { EntityVersion = e.Version };
                        view.Setup(owner, ship.Slot, ship.Kind, Rules.Hp[ship.Kind]);
                        AddChild(view);
                        _ships[e.Index] = view;
                        Place(view, frame, e, 1f);
                    }
                    view.SetHp(ship.Hp);
                    view.SetSelected(owner == _localPlayerId && (_selectionMask & (1 << ship.Slot)) != 0);
                    presentShips.Add(e.Index);
                }
                else if (frame.Has<BulletComponent>(e))
                {
                    if (!_bullets.TryGetValue(e.Index, out var view) || view.EntityVersion != e.Version)
                    {
                        if (view != null) view.QueueFree();
                        view = new BulletView { EntityVersion = e.Version };
                        view.Setup(owner);
                        AddChild(view);
                        _bullets[e.Index] = view;
                        Place(view, frame, e, 1f);
                    }
                    presentBullets.Add(e.Index);
                }
            }

            _stale.Clear();
            foreach (var kv in _ships) if (!presentShips.Contains(kv.Key)) _stale.Add(kv.Key);
            foreach (int idx in _stale)
            {
                var v = _ships[idx];
                SpawnExplosion(v.Position, Palette.Of(v.OwnerId), v.Kind == Rules.KindFlagship ? 14f : 7f);
                v.QueueFree();
                _ships.Remove(idx);
            }
            _stale.Clear();
            foreach (var kv in _bullets) if (!presentBullets.Contains(kv.Key)) _stale.Add(kv.Key);
            foreach (int idx in _stale) { _bullets[idx].QueueFree(); _bullets.Remove(idx); }
        }

        void SpawnExplosion(Vector2 at, Color color, float size)
        {
            var fx = new ExplosionView { Position = at };
            fx.Setup(color, size);
            AddChild(fx);
        }

        // ── interpolate (per rendered frame) ──

        public override void _Process(double delta)
        {
            _orderMarkerAge += (float)delta;
            if (_orderMarkerAge < 0.6f || _dragging) QueueRedraw();

            var curr = _engine?.PredictedFrame.Frame;
            if (curr == null) return;
            float alpha = _engine.RenderClock.PredictedAlpha;
            foreach (var kv in _ships)   Place(kv.Value, curr, new EntityRef(kv.Key, kv.Value.EntityVersion), alpha);
            foreach (var kv in _bullets) Place(kv.Value, curr, new EntityRef(kv.Key, kv.Value.EntityVersion), alpha);
        }

        void Place(Node2D view, Frame curr, EntityRef e, float alpha)
        {
            if (!curr.Entities.IsAlive(e) || !curr.Has<TransformComponent>(e)) return;
            ref readonly var t = ref curr.GetReadOnly<TransformComponent>(e);
            var pos = new Vector2(t.Position.x.ToFloat(), t.Position.y.ToFloat());
            float rot = t.Rotation.ToFloat();

            var prev = _engine.PredictedPreviousFrame.Frame;
            if (alpha < 1f && prev != null && prev.Entities.IsAlive(e) && prev.Has<TransformComponent>(e))
            {
                ref readonly var pt = ref prev.GetReadOnly<TransformComponent>(e);
                var ppos = new Vector2(pt.Position.x.ToFloat(), pt.Position.y.ToFloat());
                pos = ppos.Lerp(pos, alpha);
                rot = Mathf.LerpAngle(pt.Rotation.ToFloat(), rot, alpha);
            }

            view.Position = pos.Round();
            if (view is ShipView s) s.SetFacing(rot);
            else view.Rotation = rot;
        }

        // ── input ──

        public override void _UnhandledInput(InputEvent ev)
        {
            if (_engine == null || _localPlayerId < 0) return;

            if (ev is InputEventMouseButton mb)
            {
                var p = GetGlobalMousePosition();
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed) { _dragging = true; _dragStart = _dragEnd = p; }
                    else if (_dragging) { _dragging = false; _dragEnd = p; FinishSelect(); }
                    QueueRedraw();
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    IssueMove(p);
                }
            }
            else if (ev is InputEventMouseMotion && _dragging)
            {
                _dragEnd = GetGlobalMousePosition();
            }
            else if (ev is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.A && k.CtrlPressed)
            {
                SelectAll();
            }
        }

        void FinishSelect()
        {
            var rect = new Rect2(_dragStart, Vector2.Zero).Expand(_dragEnd).Abs();
            bool click = rect.Size.Length() < 4f;
            int mask = 0;
            float bestDist = 12f;
            int bestSlot = -1;
            foreach (var v in _ships.Values)
            {
                if (v.OwnerId != _localPlayerId) continue;
                if (click)
                {
                    float d = v.Position.DistanceTo(_dragEnd);
                    if (d < bestDist) { bestDist = d; bestSlot = v.Slot; }
                }
                else if (rect.HasPoint(v.Position)) mask |= 1 << v.Slot;
            }
            if (click && bestSlot >= 0) mask = 1 << bestSlot;
            if (click && bestSlot < 0) mask = 0;
            ApplySelection(mask);
        }

        void SelectAll()
        {
            int mask = 0;
            foreach (var v in _ships.Values) if (v.OwnerId == _localPlayerId) mask |= 1 << v.Slot;
            ApplySelection(mask);
        }

        void ApplySelection(int mask)
        {
            _selectionMask = mask;
            foreach (var v in _ships.Values)
                v.SetSelected(v.OwnerId == _localPlayerId && (mask & (1 << v.Slot)) != 0);
            QueueRedraw();
        }

        void IssueMove(Vector2 target)
        {
            if (_selectionMask == 0) return;
            _orders.EnqueueMove(_selectionMask, FP64.FromFloat(target.X), FP64.FromFloat(target.Y));
            _orderMarker = target.Round();
            _orderMarkerAge = 0f;
            QueueRedraw();
        }

        // ── backdrop + overlay ──

        // Static starfield, view-only. Fixed-seed LCG so every peer draws the same sky.
        static readonly (float x, float y, float a)[] Stars = MakeStars(120, 0x5EED);

        static (float, float, float)[] MakeStars(int count, uint seed)
        {
            var stars = new (float, float, float)[count];
            for (int i = 0; i < count; i++)
            {
                seed = seed * 1664525u + 1013904223u; float x = (seed >> 8) % Rules.ArenaWidth;
                seed = seed * 1664525u + 1013904223u; float y = 24 + (seed >> 8) % (Rules.ArenaHeight - 24);
                seed = seed * 1664525u + 1013904223u; float a = 0.15f + ((seed >> 8) % 100) / 100f * 0.6f;
                stars[i] = (x, y, a);
            }
            return stars;
        }

        public override void _Draw()
        {
            foreach (var (x, y, a) in Stars)
                DrawRect(new Rect2(x, y, 1f, 1f), new Color(0.8f, 0.85f, 1f, a));

            if (_dragging)
            {
                var rect = new Rect2(_dragStart, Vector2.Zero).Expand(_dragEnd).Abs();
                DrawRect(rect, new Color(1f, 1f, 1f, 0.08f));
                DrawRect(rect, new Color(1f, 1f, 1f, 0.8f), false, 1f);
            }
            if (_orderMarkerAge < 0.6f)
            {
                float k = _orderMarkerAge / 0.6f;
                float r = 8f * (1f - k) + 2f;
                var c = new Color(Palette.Of(_localPlayerId), 1f - k);
                DrawArc(_orderMarker, r, 0f, Mathf.Tau, 16, c, 1f);
            }
        }
    }
}
