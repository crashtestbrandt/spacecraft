// Scans the edited scene for static colliders and exports them as a deterministic
// FPStaticCollider array (.bytes + .json sidecar). Engine-specific scene scan + save only —
// the converter (GodotFPStaticColliderConverter), serializer, and JSON sidecar are shared.
//
// plugin.gd instantiates this [GlobalClass] and calls ExportStaticColliders().
// Wrapped in #if TOOLS so it compiles only into the editor build.
#if TOOLS
using System.Collections.Generic;
using System.IO;
using System.Text;

using global::Godot;

using xpTURN.Klotho.Deterministic.Geometry;  // ShapeType
using xpTURN.Klotho.Deterministic.Physics;   // FPStaticCollider, FPStaticColliderSerializer

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class GodotFPStaticColliderExporter : RefCounted
    {
        // Classification (Q1=A): StaticBody3D -> solid, Area3D -> trigger.
        public void ExportStaticColliders()
        {
            Node root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null) { GD.PushError("[GodotFPStaticColliderExporter] No edited scene."); return; }

            var list = new List<FPStaticCollider>();
            Collect(root, list);

            if (list.Count == 0)
            {
                GD.PushError("[GodotFPStaticColliderExporter] No StaticBody3D/Area3D colliders found. " +
                    "Add CollisionShape3D under a StaticBody3D (solid) or Area3D (trigger).");
                return;
            }

            AssignIds(list);

            // Output path: <scene_dir>/<scene>.StaticColliders.bytes (+ .json), mirroring the NavMesh exporter.
            string sceneRes = root.SceneFilePath;
            string dir = string.IsNullOrEmpty(sceneRes) ? "res://" : sceneRes.GetBaseDir();
            string sceneName = string.IsNullOrEmpty(sceneRes) ? root.Name : sceneRes.GetFile().GetBaseName();
            string bytesRes = dir.PathJoin($"{sceneName}.StaticColliders.bytes");
            string jsonRes = bytesRes.GetBaseName() + ".json";

            string bytesAbs = ProjectSettings.GlobalizePath(bytesRes);
            string jsonAbs = ProjectSettings.GlobalizePath(jsonRes);

            // Save reuses the shared serializer (System.IO.File on the globalized absolute path).
            FPStaticColliderSerializer.Save(list.ToArray(), bytesAbs);
            File.WriteAllText(jsonAbs, FPStaticColliderSerializer.ToJson(list), Encoding.UTF8);

            EditorInterface.Singleton.GetResourceFilesystem()?.Scan();
            LogSummary(list, bytesRes);
        }

        // Recursively collect CollisionShape3D whose direct parent is a StaticBody3D or Area3D.
        static void Collect(Node node, List<FPStaticCollider> list)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is CollisionShape3D shapeNode)
                {
                    bool isStatic  = shapeNode.GetParent() is StaticBody3D;
                    bool isTrigger = shapeNode.GetParent() is Area3D;
                    if (isStatic || isTrigger)
                        TryConvert(shapeNode, isTrigger, list);
                }
                Collect(child, list);
            }
        }

        static void TryConvert(CollisionShape3D shapeNode, bool isTrigger, List<FPStaticCollider> list)
        {
            if (shapeNode.Disabled) return;
            if (shapeNode.Shape == null)
            {
                GD.PushWarning($"[GodotFPStaticColliderExporter] '{shapeNode.Name}': empty Shape — skipped");
                return;
            }
            if (shapeNode.Shape is ConvexPolygonShape3D)
            {
                GD.PushWarning($"[GodotFPStaticColliderExporter] '{shapeNode.Name}': ConvexPolygonShape3D not supported — skipped");
                return;
            }

            try
            {
                list.Add(GodotFPStaticColliderConverter.Convert(shapeNode, isTrigger));
            }
            catch (System.Exception e)
            {
                GD.PushError($"[GodotFPStaticColliderExporter] '{shapeNode.Name}': {e.Message} — skipped");
            }
        }

        // Auto-assign ids for entries left at -1, starting past the highest explicit id.
        static void AssignIds(List<FPStaticCollider> list)
        {
            int next = 1;
            foreach (var sc in list)
                if (sc.id > 0 && sc.id >= next) next = sc.id + 1;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == -1)
                {
                    var sc = list[i];
                    sc.id = next++;
                    list[i] = sc;
                }
            }
        }

        static void LogSummary(List<FPStaticCollider> list, string outPath)
        {
            int sphere = 0, box = 0, capsule = 0, mesh = 0, trigger = 0;
            int minId = int.MaxValue, maxId = int.MinValue;
            foreach (var sc in list)
            {
                switch (sc.collider.type)
                {
                    case ShapeType.Sphere:  sphere++;  break;
                    case ShapeType.Box:     box++;     break;
                    case ShapeType.Capsule: capsule++; break;
                    case ShapeType.Mesh:    mesh++;    break;
                }
                if (sc.isTrigger) trigger++;
                if (sc.id < minId) minId = sc.id;
                if (sc.id > maxId) maxId = sc.id;
            }

            GD.Print($"[GodotFPStaticColliderExporter] Saved {list.Count} colliders -> {outPath}");
            GD.Print($"  Sphere:{sphere} Box:{box} Capsule:{capsule} Mesh:{mesh} | Trigger:{trigger} | id {minId}..{maxId}");
        }
    }
}
#endif
