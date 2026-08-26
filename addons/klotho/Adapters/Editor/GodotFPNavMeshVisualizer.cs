// Controller for the Godot FPNavMesh visualizer. plugin.gd instantiates this [GlobalClass]
// and forwards the 3D editor virtuals here. The controller owns the data/overlay/interaction/sim
// subsystems, the dock Control, and the overlay MeshInstance3D lifecycle. EditorPlugin-only calls
// (AddControlToDock / RemoveControlFromDocks / UpdateOverlays) go through the injected plugin ref.
#if TOOLS
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class GodotFPNavMeshVisualizer : RefCounted
    {
        private const float LabelMaxDist = 40f;

        private EditorPlugin _plugin;
        private GodotFPNavMeshVisualizerData _data;
        private GodotFPNavMeshOverlay _overlay;
        private GodotFPNavMeshInteraction _interaction;
        private GodotFPNavMeshAgentSimulator _agentSim;
        private GodotFPNavMeshVisualizerDock _dock;
        private ScrollContainer _dockScroll;

        private Camera3D _camera;
        private Font _font;
        private bool _active;
        private bool _attached;

        internal GodotFPNavMeshVisualizerData Data => _data;
        internal GodotFPNavMeshOverlay Overlay => _overlay;
        internal GodotFPNavMeshAgentSimulator AgentSim => _agentSim;
        internal GodotFPNavMeshInteraction Interaction => _interaction;

        // ---- lifecycle (called by plugin.gd) ----

        public void Init(EditorPlugin plugin)
        {
            _plugin = plugin;
            _data = new GodotFPNavMeshVisualizerData();
            _overlay = new GodotFPNavMeshOverlay();
            _interaction = new GodotFPNavMeshInteraction();
            _agentSim = new GodotFPNavMeshAgentSimulator();

            _overlay.SetData(_data);
            _overlay.SetAgentSimulator(_agentSim);
            _interaction.SetData(_data);

            _interaction.OnStartPointSet += OnStartPointSet;
            _interaction.OnEndPointSet += OnEndPointSet;
            _interaction.OnTriangleSelected += OnTriangleSelected;
            _interaction.OnAgentPlaced += OnAgentPlaced;
            _interaction.OnAgentDestinationSet += OnAgentDestinationSet;

            _font = ThemeDB.FallbackFont;
            _dock = new GodotFPNavMeshVisualizerDock();
            _dock.Init(this);

            // Wrap the dock in a ScrollContainer so its (tall) content does not impose a large
            // minimum size on the dock column — that would force the editor to re-layout and can
            // collapse the bottom panel. The ScrollContainer fills the tab and scrolls vertically.
            _dockScroll = new ScrollContainer
            {
                Name = "FPNavMesh",
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _dock.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _dockScroll.AddChild(_dock);
        }

        public void Shutdown()
        {
            if (_active) Deactivate();
            _dockScroll?.QueueFree();   // frees the dock child too
            _dockScroll = null;
            _dock = null;
        }

        public bool IsActive() => _active;

        public void ToggleActive()
        {
            if (_active) Deactivate();
            else Activate();
        }

        private void Activate()
        {
#pragma warning disable CS0618 // AddControlToDock is the supported path across 4.x; AddDock(EditorDock) is newer.
            _plugin.AddControlToDock(EditorPlugin.DockSlot.RightUl, _dockScroll);
#pragma warning restore CS0618
            _active = true;
            TryAttachOverlay();
            _overlay.RebuildStatic();
            _overlay.RebuildDynamic();
            _dock.Refresh();
            _plugin.UpdateOverlays();
        }

        private void Deactivate()
        {
            _overlay.Detach();
            _attached = false;
#pragma warning disable CS0618
            _plugin.RemoveControlFromDocks(_dockScroll);
#pragma warning restore CS0618
            _active = false;
            _plugin.UpdateOverlays();
        }

        private void TryAttachOverlay()
        {
            if (_attached) return;
            Node root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
            {
                GD.PushWarning("[GodotFPNavMeshVisualizer] No edited scene — overlay geometry will not show until a 3D scene is open.");
                return;
            }
            _overlay.Attach(root);
            _attached = true;
        }

        // ---- 3D editor virtuals (forwarded from plugin.gd) ----

        public int HandleInput(Camera3D camera, InputEvent ev)
        {
            _camera = camera;
            if (_data == null || !_data.IsLoaded) return (int)EditorPlugin.AfterGuiInput.Pass;

            var prevCell = _interaction.HoveredCell;
            int prevTri = _interaction.HoveredTriangleIndex;

            bool consumed = _interaction.ProcessInput(camera, ev);

            _overlay.HoveredTriangleIndex = _interaction.HoveredTriangleIndex;
            _overlay.HoveredCell = _interaction.HoveredCell;

            if (_interaction.HoveredCell != prevCell)
            {
                _overlay.RebuildDynamic();
                _plugin.UpdateOverlays();
                _dock.Refresh();
            }
            else if (_interaction.HoveredTriangleIndex != prevTri)
            {
                _plugin.UpdateOverlays();
                _dock.Refresh();
            }

            return consumed ? (int)EditorPlugin.AfterGuiInput.Stop : (int)EditorPlugin.AfterGuiInput.Pass;
        }

        public void DrawLabels(Control overlay)
        {
            if (_camera == null || _data == null || !_data.IsLoaded) return;
            float maxDistSqr = LabelMaxDist * LabelMaxDist;
            Vector3 camPos = _camera.GlobalPosition;

            foreach (var (pos, text) in _overlay.Labels)
            {
                if (_camera.IsPositionBehind(pos)) continue;
                if (camPos.DistanceSquaredTo(pos) > maxDistSqr) continue;
                Vector2 screen = _camera.UnprojectPosition(pos);
                overlay.DrawString(_font, screen, text, HorizontalAlignment.Center, -1f, 10);
            }
        }

        public void OnProcess(double delta)
        {
            if (!_active || _agentSim == null) return;
            if (_agentSim.OnEditorUpdate(delta))
            {
                _overlay.RebuildDynamic();
                _plugin.UpdateOverlays();
                _dock.Refresh();
            }
        }

        // ---- operations (called by the dock) ----

        internal void Load(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) { GD.PushWarning("[GodotFPNavMeshVisualizer] Empty path."); return; }
            if (!FileAccess.FileExists(resPath)) { GD.PushError($"[GodotFPNavMeshVisualizer] File not found: {resPath}"); return; }

            byte[] bytes = FileAccess.GetFileAsBytes(resPath);
            if (bytes == null || bytes.Length == 0) { GD.PushError($"[GodotFPNavMeshVisualizer] Empty file: {resPath}"); return; }

            _agentSim.ClearAllAgents();
            if (_data.LoadFromBytes(bytes))
            {
                _agentSim.Initialize(_data);
                TryAttachOverlay();
                _overlay.RebuildStatic();
                _overlay.RebuildDynamic();
                _plugin.UpdateOverlays();
            }
            _dock.Refresh();
        }

        internal void Unload()
        {
            _data.Unload();
            _agentSim.ClearAllAgents();
            _overlay.RebuildStatic();
            _overlay.RebuildDynamic();
            _plugin.UpdateOverlays();
            _dock.Refresh();
        }

        internal void RequestStaticRedraw()
        {
            _overlay.RebuildStatic();
            _plugin.UpdateOverlays();
        }

        internal void RequestDynamicRedraw()
        {
            _overlay.RebuildDynamic();
            _plugin.UpdateOverlays();
        }

        internal void FindPath()
        {
            if (_data.HasStart && _data.HasEnd)
            {
                if (!_data.FindPath(_data.StartPoint, _data.EndPoint))
                    GD.PushWarning("[GodotFPNavMeshVisualizer] Path not found.");
            }
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        internal void ClearPath()
        {
            _data.ClearPath();
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        internal void SpawnAgentByPosition(Vector3 start, Vector3 dest)
        {
            int startTri = _data.FindTriangleAtPosition(start);
            int destTri = _data.FindTriangleAtPosition(dest);
            if (startTri < 0) { GD.PushWarning("[GodotFPNavMeshVisualizer] Start is off the NavMesh."); return; }
            if (destTri < 0) { GD.PushWarning("[GodotFPNavMeshVisualizer] Dest is off the NavMesh."); return; }

            _agentSim.ClearAllAgents();
            int idx = _agentSim.AddAgent(start);
            if (idx >= 0)
            {
                _agentSim.SetAgentDestination(idx, dest);
                _interaction.SelectedAgentIndex = idx;
            }
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        // ---- interaction event handlers ----

        private void OnStartPointSet(Vector3 point)
        {
            _data.StartPoint = point;
            _data.HasStart = true;
            _interaction.Mode = InteractionMode.SetEnd;
            if (_data.HasStart && _data.HasEnd)
                _data.FindPath(_data.StartPoint, _data.EndPoint);
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        private void OnEndPointSet(Vector3 point)
        {
            _data.EndPoint = point;
            _data.HasEnd = true;
            if (_data.HasStart && _data.HasEnd)
                _data.FindPath(_data.StartPoint, _data.EndPoint);
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        private void OnTriangleSelected(int triIdx)
        {
            _plugin.UpdateOverlays();
            _dock.Refresh();
        }

        private void OnAgentPlaced(Vector3 point)
        {
            int idx = _agentSim.AddAgent(point);
            if (idx >= 0) _interaction.SelectedAgentIndex = idx;
            RequestDynamicRedraw();
            _dock.Refresh();
        }

        private void OnAgentDestinationSet(int agentIdx, Vector3 dest)
        {
            _agentSim.SetAgentDestination(agentIdx, dest);
            RequestDynamicRedraw();
            _dock.Refresh();
        }
    }
}
#endif
