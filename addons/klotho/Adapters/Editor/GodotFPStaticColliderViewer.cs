// Editor menu+dock tool that draws exported static colliders (.bytes from "Klotho: Export Static
// Colliders") in the 3D viewport — the menu/dock counterpart to the runtime FPPhysics world visualizer,
// and a verification tool for the exporter. plugin.gd instantiates this [GlobalClass] and forwards a
// menu toggle here; the controller owns the dock Control and an overlay MeshInstance3D parented under
// the edited scene root.
//
// MUCH simpler than the NavMesh visualizer: it only draws an overlay (no picking, no labels), so it needs
// NO _forward_3d_* / force-forwarding (the selection-based editor-gizmo gating is irrelevant). The overlay MeshInstance3D
// renders regardless of node selection. Drawing reuses the runtime GodotFPPhysicsImmediateDrawer.
#if TOOLS
using System;
using System.Collections.Generic;

using global::Godot;

using xpTURN.Klotho.Deterministic.Physics;   // FPStaticCollider, FPStaticColliderSerializer

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class GodotFPStaticColliderViewer : RefCounted
    {
        public Color ShapeColor = new Color(0f, 0.8f, 1f, 0.8f);
        public Color AabbColor = new Color(0f, 1f, 0f, 0.3f);

        EditorPlugin _plugin;

        VBoxContainer _dock;
        LineEdit _pathField;
        CheckBox _showShape;
        CheckBox _showAABB;
        Label _info;

        MeshInstance3D _overlay;
        ImmediateMesh _mesh;
        GodotFPPhysicsImmediateDrawer _drawer;

        List<FPStaticCollider> _colliders;
        bool _active;
        bool _attached;

        // ---- lifecycle (called by plugin.gd) ----

        public void Init(EditorPlugin plugin)
        {
            _plugin = plugin;
            BuildDock();
        }

        public void Shutdown()
        {
            if (_active) Deactivate();
            _dock?.QueueFree();
            _dock = null;
        }

        public bool IsActive() => _active;

        public void ToggleActive()
        {
            if (_active) Deactivate();
            else Activate();
        }

        void Activate()
        {
#pragma warning disable CS0618 // AddControlToDock is the supported path across 4.x (same as the NavMesh tool).
            _plugin.AddControlToDock(EditorPlugin.DockSlot.RightUl, _dock);
#pragma warning restore CS0618
            _active = true;
            TryAttachOverlay();
            Rebuild();
        }

        void Deactivate()
        {
            DetachOverlay();
#pragma warning disable CS0618
            _plugin.RemoveControlFromDocks(_dock);
#pragma warning restore CS0618
            _active = false;
        }

        // ---- dock ----

        void BuildDock()
        {
            _dock = new VBoxContainer { Name = "FPStaticColliders" };

            _pathField = new LineEdit
            {
                PlaceholderText = "res://…StaticColliders.bytes",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _dock.AddChild(_pathField);

            var btns = new HBoxContainer();
            var load = new Button { Text = "Load" };
            var unload = new Button { Text = "Unload" };
            load.Pressed += OnLoad;
            unload.Pressed += OnUnload;
            btns.AddChild(load);
            btns.AddChild(unload);
            _dock.AddChild(btns);

            _showShape = new CheckBox { Text = "Shape", ButtonPressed = true };
            _showAABB = new CheckBox { Text = "AABB" };
            _showShape.Toggled += _ => Rebuild();
            _showAABB.Toggled += _ => Rebuild();
            _dock.AddChild(_showShape);
            _dock.AddChild(_showAABB);

            _info = new Label { Text = "(no data)" };
            _dock.AddChild(_info);
        }

        // ---- overlay ----

        void TryAttachOverlay()
        {
            if (_attached) return;
            Node root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
            {
                GD.PushWarning("[GodotFPStaticColliderViewer] No edited scene — open a 3D scene to see geometry.");
                return;
            }
            _mesh = new ImmediateMesh();
            _overlay = new MeshInstance3D { Mesh = _mesh, Name = "FPStaticColliderOverlay", TopLevel = true };
            root.AddChild(_overlay);
            _overlay.Owner = null;   // editor-temporary: not serialized into the scene
            _drawer = new GodotFPPhysicsImmediateDrawer(_mesh);
            _attached = true;
        }

        void DetachOverlay()
        {
            _overlay?.QueueFree();
            _overlay = null;
            _mesh = null;
            _drawer = null;
            _attached = false;
        }

        // ---- operations (dock) ----

        void OnLoad()
        {
            string path = _pathField.Text;
            if (string.IsNullOrEmpty(path)) { GD.PushWarning("[GodotFPStaticColliderViewer] Empty path."); return; }
            if (!FileAccess.FileExists(path)) { GD.PushError($"[GodotFPStaticColliderViewer] File not found: {path}"); return; }
            byte[] bytes = FileAccess.GetFileAsBytes(path);
            if (bytes == null || bytes.Length == 0) { GD.PushError($"[GodotFPStaticColliderViewer] Empty file: {path}"); return; }

            // Parse into a local first; only swap in on success so a bad/wrong file (e.g. not an FPSC
            // export — wrong magic) reports an error and leaves the current view intact, rather than
            // throwing an unhandled exception out of the menu callback.
            List<FPStaticCollider> loaded;
            try
            {
                loaded = FPStaticColliderSerializer.Load(bytes);
            }
            catch (Exception e)
            {
                GD.PushError($"[GodotFPStaticColliderViewer] Not a valid static-collider .bytes ({path}) — " +
                             $"expected output of 'Klotho: Export Static Colliders'. {e.Message}");
                return;
            }

            _colliders = loaded;
            TryAttachOverlay();
            Rebuild();
        }

        void OnUnload()
        {
            _colliders = null;
            Rebuild();
        }

        void Rebuild()
        {
            if (_info != null)
                _info.Text = _colliders == null ? "(no data)" : $"StaticColliders: {_colliders.Count}";
            if (_drawer == null) return;

            _drawer.Begin(true);   // overlay drawn on top (depth-test off)
            if (_colliders != null)
            {
                bool shape = _showShape?.ButtonPressed ?? true;
                bool aabb = _showAABB?.ButtonPressed ?? false;
                for (int i = 0; i < _colliders.Count; i++)
                {
                    var sc = _colliders[i];
                    if (shape) _drawer.DrawStaticColliderShape(ref sc, ShapeColor);
                    if (aabb) _drawer.DrawAABB(sc.collider.GetWorldBounds(sc.meshData), AabbColor);
                }
            }
            _drawer.End();
        }
    }
}
#endif
