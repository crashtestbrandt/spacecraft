// Immediate-mode wireframe drawing for the FPPhysics debug visualizers: a single ImmediateMesh whose
// Lines surface is rebuilt each pass. One instance binds one target ImmediateMesh + line material;
// Begin()/End() wrap a rebuild. Vertices are added in WORLD space (the owning MeshInstance3D uses
// TopLevel=true, so shape corners are pre-transformed to world coordinates before being emitted).
// Shape wireframes (sphere/box/capsule/mesh), AABBs, velocity/normal arrows and contact glyphs are
// emitted as colored line pairs through the Surf helper (lazy SurfaceBegin avoids empty-surface errors).
using global::Godot;

using xpTURN.Klotho.Deterministic.Math;       // FPVector3.ToVector3(), FPQuaternion.ToQuaternion()
using xpTURN.Klotho.Deterministic.Geometry;
using xpTURN.Klotho.Deterministic.Physics;

namespace xpTURN.Klotho.Godot
{
    internal sealed class GodotFPPhysicsImmediateDrawer
    {
        readonly ImmediateMesh _mesh;
        readonly StandardMaterial3D _mat;
        Surf _surf;
        Color _curColor = Colors.White;

        public GodotFPPhysicsImmediateDrawer(ImmediateMesh mesh)
        {
            _mesh = mesh;
            _mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true,
            };
        }

        // ---- Pass control ----

        public void Begin(bool alwaysOnTop)
        {
            _mesh.ClearSurfaces();
            _mat.SetFlag(BaseMaterial3D.Flags.DisableDepthTest, alwaysOnTop);
            _surf = new Surf(_mesh, Mesh.PrimitiveType.Lines, _mat);
        }

        public void End() => _surf?.End();

        // ---- Vertex sink (GL.Color/GL.Vertex equivalents) ----

        void Vertex(Vector3 v) => _surf.Vert(_curColor, v);
        void Line(Vector3 a, Vector3 b) { Vertex(a); Vertex(b); }

        // ---- Shape dispatchers ----

        public void DrawStaticColliderShape(ref FPStaticCollider sc, Color color)
        {
            _curColor = color;
            switch (sc.collider.type)
            {
                case ShapeType.Sphere:
                    DrawSphereWire(sc.collider.sphere.position.ToVector3(),
                                   sc.collider.sphere.radius.ToFloat());
                    break;
                case ShapeType.Box:
                    DrawBoxWire(sc.collider.box.position.ToVector3(),
                                sc.collider.box.rotation.ToQuaternion(),
                                sc.collider.box.halfExtents.ToVector3());
                    break;
                case ShapeType.Capsule:
                    DrawCapsuleWire(sc.collider.capsule.position.ToVector3(),
                                    sc.collider.capsule.rotation.ToQuaternion(),
                                    sc.collider.capsule.radius.ToFloat(),
                                    sc.collider.capsule.halfHeight.ToFloat());
                    break;
                case ShapeType.Mesh:
                    DrawMeshWire(sc.collider.mesh.position.ToVector3(),
                                 sc.collider.mesh.rotation.ToQuaternion(),
                                 sc.meshData);
                    break;
            }
        }

        public void DrawBodyShape(ref FPPhysicsBody body, Color color)
        {
            _curColor = color;
            switch (body.collider.type)
            {
                case ShapeType.Sphere:
                    DrawSphereWire(body.collider.sphere.position.ToVector3(),
                                   body.collider.sphere.radius.ToFloat());
                    break;
                case ShapeType.Box:
                    DrawBoxWire(body.collider.box.position.ToVector3(),
                                body.collider.box.rotation.ToQuaternion(),
                                body.collider.box.halfExtents.ToVector3());
                    break;
                case ShapeType.Capsule:
                    DrawCapsuleWire(body.collider.capsule.position.ToVector3(),
                                    body.collider.capsule.rotation.ToQuaternion(),
                                    body.collider.capsule.radius.ToFloat(),
                                    body.collider.capsule.halfHeight.ToFloat());
                    break;
                case ShapeType.Mesh:
                    DrawMeshWire(body.collider.mesh.position.ToVector3(),
                                 body.collider.mesh.rotation.ToQuaternion(),
                                 body.meshData);
                    break;
            }
        }

        public void DrawAABB(FPBounds3 bounds, Color color)
        {
            _curColor = color;
            DrawBoxWire(bounds.center.ToVector3(), Quaternion.Identity, bounds.extents.ToVector3());
        }

        // ---- Primitive wire drawers (verbatim geometry from FPPhysicsGLDrawer) ----

        void DrawSphereWire(Vector3 center, float radius)
        {
            DrawCircle(center, Vector3.Right, Vector3.Forward, radius); // XZ plane
            DrawCircle(center, Vector3.Right, Vector3.Up, radius);      // XY plane
            DrawCircle(center, Vector3.Up, Vector3.Forward, radius);    // YZ plane
        }

        void DrawCircle(Vector3 center, Vector3 tangent, Vector3 bitangent, float radius, int segments = 16)
        {
            Vector3 prev = center + tangent * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.Pi / segments;
                Vector3 curr = center + (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * bitangent) * radius;
                Line(prev, curr);
                prev = curr;
            }
        }

        void DrawBoxWire(Vector3 position, Quaternion rotation, Vector3 halfExtents)
        {
            float hx = halfExtents.X, hy = halfExtents.Y, hz = halfExtents.Z;

            var c = new Vector3[8];
            c[0] = new Vector3(-hx, -hy, -hz);
            c[1] = new Vector3(+hx, -hy, -hz);
            c[2] = new Vector3(+hx, +hy, -hz);
            c[3] = new Vector3(-hx, +hy, -hz);
            c[4] = new Vector3(-hx, -hy, +hz);
            c[5] = new Vector3(+hx, -hy, +hz);
            c[6] = new Vector3(+hx, +hy, +hz);
            c[7] = new Vector3(-hx, +hy, +hz);

            var trs = new Transform3D(new Basis(rotation), position);
            for (int i = 0; i < 8; i++)
                c[i] = trs * c[i];

            // Bottom face (-hz)
            Line(c[0], c[1]); Line(c[1], c[2]); Line(c[2], c[3]); Line(c[3], c[0]);
            // Top face (+hz)
            Line(c[4], c[5]); Line(c[5], c[6]); Line(c[6], c[7]); Line(c[7], c[4]);
            // 4 vertical edges
            Line(c[0], c[4]); Line(c[1], c[5]); Line(c[2], c[6]); Line(c[3], c[7]);
        }

        void DrawCapsuleWire(Vector3 position, Quaternion rotation, float radius, float halfHeight)
        {
            Vector3 axis = rotation * Vector3.Up;
            Vector3 top = position + axis * halfHeight;
            Vector3 bottom = position - axis * halfHeight;

            Vector3 perp = axis.Cross(Vector3.Up).Normalized();
            if (perp.LengthSquared() < 0.001f) perp = Vector3.Right;
            Vector3 perp2 = axis.Cross(perp);

            DrawCircle(top, perp, perp2, radius);
            DrawCircle(bottom, perp, perp2, radius);

            Line(top + perp * radius, bottom + perp * radius);
            Line(top - perp * radius, bottom - perp * radius);
            Line(top + perp2 * radius, bottom + perp2 * radius);
            Line(top - perp2 * radius, bottom - perp2 * radius);

            DrawHemiArc(top, axis, perp, radius);
            DrawHemiArc(top, axis, perp2, radius);
            DrawHemiArc(bottom, -axis, perp, radius);
            DrawHemiArc(bottom, -axis, perp2, radius);
        }

        void DrawHemiArc(Vector3 center, Vector3 poleDir, Vector3 tangent, float radius, int segments = 8)
        {
            Vector3 prev = center + tangent * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.Pi / segments;
                Vector3 curr = center + (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * poleDir) * radius;
                Line(prev, curr);
                prev = curr;
            }
        }

        void DrawMeshWire(Vector3 position, Quaternion rotation, FPMeshData data)
        {
            if (data == null) return;
            var trs = new Transform3D(new Basis(rotation), position);
            var verts = data.vertices;
            var idx = data.indices;
            for (int i = 0; i < idx.Length; i += 3)
            {
                Vector3 v0 = trs * verts[idx[i]].ToVector3();
                Vector3 v1 = trs * verts[idx[i + 1]].ToVector3();
                Vector3 v2 = trs * verts[idx[i + 2]].ToVector3();
                Line(v0, v1);
                Line(v1, v2);
                Line(v2, v0);
            }
        }

        // ---- Arrow ----

        public void DrawArrowFromVelocity(Vector3 origin, Vector3 dir, float scale, Color color)
        {
            if (dir.LengthSquared() < 0.0001f) return;
            _curColor = color;
            Vector3 dn = dir.Normalized();
            Vector3 end = origin + dir * scale;
            float headLen = dir.Length() * scale * 0.2f;
            DrawArrowHead(origin, end, dn, headLen);
        }

        public void DrawArrowFromNormal(Vector3 origin, Vector3 dir, float length, Color color)
        {
            _curColor = color;
            Vector3 end = origin + dir * length;
            float headLen = length * 0.2f;
            DrawArrowHead(origin, end, dir, headLen);
        }

        void DrawArrowHead(Vector3 origin, Vector3 end, Vector3 dn, float headLen)
        {
            Vector3 right = dn.Cross(Vector3.Up).Normalized();
            if (right.LengthSquared() < 0.001f) right = Vector3.Right;

            Line(origin, end);
            Line(end, end - dn * headLen + right * headLen * 0.5f);
            Line(end, end - dn * headLen - right * headLen * 0.5f);
        }

        // ---- Contact ----

        public void DrawContact(ref FPContact c, Color pointColor, Color normalColor,
                                float normalScale, float pointRadius, bool drawNormal)
        {
            Vector3 pt = c.point.ToVector3();
            Vector3 n = c.normal.ToVector3();
            float d = Mathf.Abs(c.depth.ToFloat());
            bool isCCD = c.isSpeculative;

            _curColor = pointColor;
            float r = pointRadius;
            Line(pt + Vector3.Right * r, pt - Vector3.Right * r);
            Line(pt + Vector3.Up * r, pt - Vector3.Up * r);
            Line(pt + Vector3.Forward * r, pt - Vector3.Forward * r);

            if (drawNormal && n.LengthSquared() > 0.001f)
            {
                Color nc = isCCD ? normalColor.Lerp(Colors.Cyan, 0.5f) : normalColor;
                float len = Mathf.Max(d * normalScale, 0.1f);
                DrawArrowFromNormal(pt, n, len, nc);
            }
        }

        // ---- Surface helper (ported from GodotFPNavMeshOverlay.Surf): lazy SurfaceBegin so an
        // empty surface (zero vertices) is never opened -> avoids "Surface must have at least one vertex". ----

        sealed class Surf
        {
            readonly ImmediateMesh _m;
            readonly Mesh.PrimitiveType _prim;
            readonly Material _mat;
            bool _open;

            public Surf(ImmediateMesh m, Mesh.PrimitiveType prim, Material mat)
            {
                _m = m;
                _prim = prim;
                _mat = mat;
            }

            public void Vert(Color c, Vector3 v)
            {
                if (!_open)
                {
                    _m.SurfaceBegin(_prim, _mat);
                    _open = true;
                }
                _m.SurfaceSetColor(c);
                _m.SurfaceAddVertex(v);
            }

            public void End()
            {
                if (_open)
                {
                    _m.SurfaceEnd();
                    _open = false;
                }
            }
        }
    }
}
