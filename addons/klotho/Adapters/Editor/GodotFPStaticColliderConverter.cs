// Converts a Godot CollisionShape3D into a fixed-point static collider.
// Engine-specific extraction only — the deterministic types (FPStaticCollider/FPCollider/
// FP*Shape/FPMeshData) are shared. Wrapped in #if TOOLS so it compiles only into the editor build.
#if TOOLS
using System;

using global::Godot;

using xpTURN.Klotho.Deterministic.Math;       // FPVector3, FPQuaternion, ToFPVector3, ToFPQuaternion
using xpTURN.Klotho.Deterministic.Geometry;   // FPMeshData
using xpTURN.Klotho.Deterministic.Physics;    // FPStaticCollider, FPCollider, FP*Shape

namespace xpTURN.Klotho.Godot
{
    public static class GodotFPStaticColliderConverter
    {
        // isTrigger: determined by the parent body type (StaticBody3D=false, Area3D=true) — see the exporter.
        // id, restitution, friction: read from a child FPStaticColliderOverride if present, otherwise defaults.
        public static FPStaticCollider Convert(CollisionShape3D node, bool isTrigger)
        {
            Transform3D xform = node.GlobalTransform;

            // Mirror transform (det < 0) is unsupported. Godot's Basis.Scale folds the determinant sign into
            // all three components, so a component-wise sign check is unreliable — gate on the determinant.
            if (xform.Basis.Determinant() < 0)
                throw new InvalidOperationException($"'{node.Name}': Negative/mirror scale not supported");

            global::Godot.Vector3    lossyScale = xform.Basis.Scale;
            FPVector3    worldPos = xform.Origin.ToFPVector3();
            FPQuaternion worldRot = xform.Basis.GetRotationQuaternion().ToFPQuaternion();

            // Godot shapes are centered at the CollisionShape3D origin (no per-shape center offset).
            FPVector3 center = worldPos;

            FPCollider fpCollider;
            FPMeshData meshData = null;

            switch (node.Shape)
            {
                case SphereShape3D sphere:
                    fpCollider = ConvertSphere(node, sphere, lossyScale, center);
                    break;
                case BoxShape3D box:
                    fpCollider = ConvertBox(box, worldRot, lossyScale, center);
                    break;
                case CapsuleShape3D capsule:
                    fpCollider = ConvertCapsule(capsule, worldRot, lossyScale, center);
                    break;
                case ConcavePolygonShape3D concave:
                    fpCollider = FPCollider.FromMesh(new FPMeshShape(worldPos, worldRot));
                    meshData = BakeMeshData(concave, lossyScale);
                    break;
                default:
                    throw new NotSupportedException($"'{node.Name}': Unsupported shape type: {node.Shape?.GetType()}");
            }

            FPStaticColliderOverride ov = FindOverride(node);
            return new FPStaticCollider
            {
                id          = ov != null && ov.Id != 0 ? ov.Id : -1,
                collider    = fpCollider,
                meshData    = meshData,
                isTrigger   = isTrigger,
                restitution = ov != null ? FP64.FromFloat(ov.Restitution) : FP64.Zero,
                friction    = ov != null ? FP64.FromFloat(ov.Friction)    : FP64.Zero,
            };
        }

        static FPCollider ConvertSphere(CollisionShape3D node, SphereShape3D sphere,
            global::Godot.Vector3 lossyScale, FPVector3 center)
        {
            if (!Mathf.IsEqualApprox(lossyScale.X, lossyScale.Y) || !Mathf.IsEqualApprox(lossyScale.X, lossyScale.Z))
                throw new InvalidOperationException($"'{node.Name}': Non-uniform scale not supported for SphereShape3D (ellipsoid)");

            FP64 radius = FP64.FromFloat(sphere.Radius * lossyScale.X);
            return FPCollider.FromSphere(new FPSphereShape(radius, center));
        }

        static FPCollider ConvertBox(BoxShape3D box, FPQuaternion worldRot,
            global::Godot.Vector3 lossyScale, FPVector3 center)
        {
            // BoxShape3D.Size is the full size (Godot 4); halve it to get half-extents.
            FPVector3 halfExtents = new FPVector3(
                FP64.FromFloat(box.Size.X * 0.5f * lossyScale.X),
                FP64.FromFloat(box.Size.Y * 0.5f * lossyScale.Y),
                FP64.FromFloat(box.Size.Z * 0.5f * lossyScale.Z));
            return FPCollider.FromBox(new FPBoxShape(halfExtents, center, worldRot));
        }

        static FPCollider ConvertCapsule(CapsuleShape3D capsule, FPQuaternion worldRot,
            global::Godot.Vector3 lossyScale, FPVector3 center)
        {
            // Godot capsules are always Y-axis aligned, matching the FP canonical axis (FPCapsuleShape uses
            // rotation * Up). So no axis-remap rotation is needed — use the world rotation directly.
            float heightScale = lossyScale.Y;
            float radialScale = Mathf.Max(lossyScale.X, lossyScale.Z);

            // CapsuleShape3D.Height is the full tip-to-tip height (Godot 4); cylinder half-height = H/2 - radius.
            FP64 radius     = FP64.FromFloat(capsule.Radius * radialScale);
            FP64 halfHeight = FP64.FromFloat(Mathf.Max(0f, capsule.Height * 0.5f * heightScale - capsule.Radius * radialScale));
            return FPCollider.FromCapsule(new FPCapsuleShape(halfHeight, radius, center, worldRot));
        }

        // Converts faces to FP64 after applying local scale only (rotation/position delegated to FPMeshShape).
        // Godot's GetFaces() returns an index-less triangle soup (3 verts per tri) in shape-local space,
        // so we emit sequential indices [0,1,2,...]. Vertex welding is a later optimization.
        static FPMeshData BakeMeshData(ConcavePolygonShape3D concave, global::Godot.Vector3 lossyScale)
        {
            global::Godot.Vector3[] faces = concave.GetFaces();

            var fpVerts = new FPVector3[faces.Length];
            var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                global::Godot.Vector3 scaled = faces[i] * lossyScale;  // component-wise scale
                fpVerts[i] = scaled.ToFPVector3();
                indices[i] = i;
            }
            return new FPMeshData(fpVerts, indices);
        }

        static FPStaticColliderOverride FindOverride(CollisionShape3D node)
        {
            foreach (Node child in node.GetChildren())
                if (child is FPStaticColliderOverride ov)
                    return ov;
            return null;
        }
    }
}
#endif
