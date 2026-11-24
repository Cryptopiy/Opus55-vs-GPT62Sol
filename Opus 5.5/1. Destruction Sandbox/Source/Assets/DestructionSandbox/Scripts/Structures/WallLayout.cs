using System.Collections.Generic;
using UnityEngine;
using S = DestructionSandbox.GameConfig.Structures;

namespace DestructionSandbox
{
    /// <summary>
    /// D-05: running-bond stone wall (bricks 1.0 x 0.5 x 0.5 m) with a centred semicircular arch made of wedge
    /// stones and a row of glass windows on rows 8-9.
    ///
    /// Geometry trick that keeps every piece convex and gap-free: the arch's outer outline P is a polygon whose
    /// vertices sit exactly on brick course lines (y = 1.5, 2.0 ... 5.0). Inside each course band P's boundary
    /// is then a single straight edge, so every brick next to the arch is "rectangle ∩ half-plane" (convex).
    /// Voussoir joints are radial lines through every P vertex (plus extra joints that split over-long spans),
    /// so each voussoir is a convex wedge quadrilateral. Tiny cut slivers are merged into their neighbour.
    /// </summary>
    public static class WallLayout
    {
        public static StructureLayout Build()
        {
            float brickL = S.BrickSize.x;
            float h = S.BrickSize.y;
            float depth = S.BrickSize.z;
            float halfWidth = S.WallBricksWide * brickL * 0.5f;
            float ri = S.ArchOpeningWidth * 0.5f;
            float ys = S.ArchSpringingRow * h;
            float ro = S.ArchOuterRadius;
            var center = new Vector2(0f, ys);
            Vector3 origin = GameConfig.World.StructureOrigin;

            // Left half of the outer outline P: vertices on course lines from the springing up to the crown.
            int bands = Mathf.RoundToInt((S.ArchCrownTopY - ys) / h);
            var left = new Vector2[bands + 1];
            for (int k = 0; k <= bands; k++)
            {
                float y = ys + k * h;
                float dy = y - ys;
                left[k] = new Vector2(-Mathf.Sqrt(Mathf.Max(0f, ro * ro - dy * dy)), y);
            }

            // Closed outline used for radial ray casts: left side bottom->top, crown edge, right side top->bottom.
            var outline = new List<Vector2>();
            for (int k = 0; k <= bands; k++) outline.Add(left[k]);
            for (int k = bands; k >= 0; k--) outline.Add(new Vector2(-left[k].x, left[k].y));

            var pieces = new List<PieceDef>(320);

            // ---------------- Voussoirs ----------------
            var jointAngles = new List<float>();
            var vertexAngles = new List<float>();
            for (int k = 0; k <= bands; k++) vertexAngles.Add(AngleOf(left[k] - center));
            for (int k = bands; k >= 0; k--) vertexAngles.Add(AngleOf(new Vector2(-left[k].x, left[k].y) - center));
            float maxSpan = S.ArchMaxVoussoirSpanDegrees * Mathf.Deg2Rad;
            jointAngles.Add(vertexAngles[0]);
            for (int i = 1; i < vertexAngles.Count; i++)
            {
                float a = vertexAngles[i - 1], b = vertexAngles[i];
                int parts = Mathf.Max(1, Mathf.CeilToInt((a - b) / maxSpan - 1e-4f));
                for (int p = 1; p <= parts; p++) jointAngles.Add(Mathf.Lerp(a, b, (float)p / parts));
            }

            for (int i = 0; i < jointAngles.Count - 1; i++)
            {
                float a = jointAngles[i], b = jointAngles[i + 1];
                Vector2 ia = center + Dir(a) * ri;
                Vector2 ib = center + Dir(b) * ri;
                Vector2 oa = RayHit(center, Dir(a), outline);
                Vector2 ob = RayHit(center, Dir(b), outline);
                var poly = new List<Vector2> { ia, oa, ob, ib };
                AddPiece(pieces, poly, depth, BlockMaterialType.Stone, origin, "Voussoir" + i);
            }

            // ---------------- Bricks ----------------
            var joints = new List<float>(40);
            var rowPieces = new List<RowPiece>(40);
            for (int row = 0; row < S.WallRows; row++)
            {
                float y0 = row * h, y1 = y0 + h;
                bool windowRow = row + 1 >= S.WindowFirstRow && row + 1 <= S.WindowLastRow;

                // Running bond: even rows start with a full brick, odd rows with a half brick.
                joints.Clear();
                joints.Add(-halfWidth);
                float start = (row % 2 == 0) ? -halfWidth + brickL : -halfWidth + brickL * 0.5f;
                for (float x = start; x < halfWidth - 1e-4f; x += brickL) joints.Add(x);
                joints.Add(halfWidth);
                if (y1 <= ys + 1e-4f) { AddJoint(joints, -ri); AddJoint(joints, ri); }
                if (windowRow)
                {
                    foreach (Vector2 w in S.WindowSpans) { AddJoint(joints, w.x); AddJoint(joints, w.y); }
                }
                joints.Sort();

                bool archBand = y0 >= ys - 1e-4f && y1 <= S.ArchCrownTopY + 1e-4f;
                int band = archBand ? Mathf.RoundToInt((y0 - ys) / h) : -1;

                rowPieces.Clear();
                for (int j = 0; j < joints.Count - 1; j++)
                {
                    float x0 = joints[j], x1 = joints[j + 1];
                    if (x1 - x0 < 1e-4f) continue;
                    float mid = (x0 + x1) * 0.5f;
                    if (y1 <= ys + 1e-4f && mid > -ri && mid < ri) continue;      // the opening below the arch
                    BlockMaterialType mat = windowRow && InWindow(mid) ? BlockMaterialType.Glass : BlockMaterialType.Stone;
                    rowPieces.Add(new RowPiece { X0 = x0, X1 = x1, Material = mat, Side = mid < 0f ? -1 : 1 });
                }

                if (!archBand)
                {
                    foreach (RowPiece rp in rowPieces)
                        AddPiece(pieces, PolygonUtil.Rect(rp.X0, y0, rp.X1, y1), depth, rp.Material, origin, "Brick");
                    continue;
                }

                // Arch band: clip each brick against P's straight edge in this band (left and right side).
                Vector2 a0 = left[band], a1 = left[band + 1];
                CutSide(pieces, rowPieces, -1, a0, a1, y0, y1, depth, origin);
                CutSide(pieces, rowPieces, 1, new Vector2(-a0.x, a0.y), new Vector2(-a1.x, a1.y), y0, y1, depth, origin);
            }

            return new StructureLayout { Name = "Wall + Arch", Pieces = pieces.ToArray() };
        }

        private struct RowPiece
        {
            public float X0, X1;
            public BlockMaterialType Material;
            public int Side;
        }

        private static void AddJoint(List<float> joints, float x)
        {
            for (int i = 0; i < joints.Count; i++) if (Mathf.Abs(joints[i] - x) < 1e-4f) return;
            joints.Add(x);
        }

        private static bool InWindow(float x)
        {
            foreach (Vector2 w in S.WindowSpans) if (x > w.x && x < w.y) return true;
            return false;
        }

        /// <summary>
        /// Cuts the bricks of one side of an arch band by the straight edge p0-p1 of P. Pieces that would end up
        /// as slivers are merged with the next piece further away from the arch (same half-plane => still convex).
        /// </summary>
        private static void CutSide(List<PieceDef> pieces, List<RowPiece> rowPieces, int side, Vector2 p0, Vector2 p1,
            float y0, float y1, float depth, Vector3 origin)
        {
            // Half-plane "outside P": keep Dot(n, p) <= d with n pointing from the wall into the arch.
            Vector2 e = p1 - p0;
            Vector2 n = side < 0 ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
            float d = Vector2.Dot(n, p0);

            // Walk from the arch outwards so slivers can be merged into the following (outer) piece.
            var order = new List<RowPiece>();
            foreach (RowPiece rp in rowPieces) if (rp.Side == side) order.Add(rp);
            order.Sort((u, v) => side < 0 ? v.X0.CompareTo(u.X0) : u.X0.CompareTo(v.X0));

            float pendingInner = float.NaN;   // inner edge carried over from a merged sliver
            foreach (RowPiece rp in order)
            {
                float x0 = rp.X0, x1 = rp.X1;
                if (!float.IsNaN(pendingInner))
                {
                    if (side < 0) x1 = pendingInner; else x0 = pendingInner;
                }
                List<Vector2> poly = PolygonUtil.ClipHalfPlane(PolygonUtil.Rect(x0, y0, x1, y1), n, d);
                if (poly.Count < 3) { pendingInner = float.NaN; continue; }        // fully inside the arch
                float area = Mathf.Abs(PolygonUtil.SignedArea(poly));
                bool cut = !PolygonUtil.IsAxisAlignedRect(poly) || Mathf.Abs(area - (x1 - x0) * (y1 - y0)) > 1e-4f;
                if (cut && area < S.MinCutPieceArea)
                {
                    pendingInner = side < 0 ? x1 : x0;                             // merge into the next piece
                    continue;
                }
                pendingInner = float.NaN;
                AddPiece(pieces, poly, depth, rp.Material, origin, cut ? "CutStone" : "Brick");
            }
        }

        private static void AddPiece(List<PieceDef> pieces, List<Vector2> poly, float depth, BlockMaterialType mat, Vector3 origin, string name)
        {
            if (PolygonUtil.SignedArea(poly) < 0f) poly.Reverse();
            float area = PolygonUtil.SignedArea(poly);
            if (area < 1e-5f) return;
            int index = pieces.Count;
            var def = new PieceDef
            {
                Material = mat,
                Rotation = Quaternion.identity,
                Volume = area * depth,
                UserValue = LayoutHash.UserValue(BasicLayouts.WallId, index)
            };

            if (PolygonUtil.IsAxisAlignedRect(poly))
            {
                PolygonUtil.Bounds(poly, out Vector2 min, out Vector2 max);
                Vector2 c = (min + max) * 0.5f;
                def.Position = origin + new Vector3(c.x, c.y, 0f);
                def.Size = new Vector3(max.x - min.x, max.y - min.y, depth);
            }
            else
            {
                Vector2 c = PolygonUtil.Centroid(poly);
                var local = new List<Vector2>(poly.Count);
                foreach (Vector2 p in poly) local.Add(p - c);
                PolygonUtil.Bounds(local, out Vector2 min, out Vector2 max);
                def.Position = origin + new Vector3(c.x, c.y, 0f);
                def.Size = new Vector3(max.x - min.x, max.y - min.y, depth);
                def.PrismMesh = MeshLibrary.BuildPrism(local, depth, name + index);
                def.PrismShards = PrismShards(local, depth);
            }
            pieces.Add(def);
        }

        /// <summary>
        /// D-11 for convex prisms: split the outline's bounding box into 2x2 cells (x2 in depth = 8 pieces).
        /// Each piece keeps its exact volume (correct mass share); its collision box is the cell clip's bounds
        /// scaled to the same area, centred on the clip's centroid.
        /// </summary>
        private static ShardBox[] PrismShards(List<Vector2> local, float depth)
        {
            PolygonUtil.Bounds(local, out Vector2 min, out Vector2 max);
            Vector2 mid = (min + max) * 0.5f;
            var result = new List<ShardBox>(8);
            for (int qx = 0; qx < 2; qx++)
            {
                for (int qy = 0; qy < 2; qy++)
                {
                    var cMin = new Vector2(qx == 0 ? min.x : mid.x, qy == 0 ? min.y : mid.y);
                    var cMax = new Vector2(qx == 0 ? mid.x : max.x, qy == 0 ? mid.y : max.y);
                    List<Vector2> clip = PolygonUtil.ClipRect(local, cMin, cMax);
                    if (clip.Count < 3) continue;
                    float area = Mathf.Abs(PolygonUtil.SignedArea(clip));
                    if (area * depth * 0.5f < GameConfig.Fracture.MinShardVolume) continue;
                    PolygonUtil.Bounds(clip, out Vector2 bMin, out Vector2 bMax);
                    Vector2 size = bMax - bMin;
                    float scale = Mathf.Sqrt(area / Mathf.Max(1e-6f, size.x * size.y));
                    Vector2 cen = PolygonUtil.Centroid(clip);
                    for (int qz = -1; qz <= 1; qz += 2)
                    {
                        result.Add(new ShardBox
                        {
                            LocalCenter = new Vector3(cen.x, cen.y, qz * depth * 0.25f),
                            Size = new Vector3(size.x * scale, size.y * scale, depth * 0.5f),
                            Volume = area * depth * 0.5f
                        });
                    }
                }
            }
            return result.ToArray();
        }

        private static float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x);
        private static Vector2 Dir(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        /// <summary>First intersection of a ray from the arch centre with the outer outline.</summary>
        private static Vector2 RayHit(Vector2 origin, Vector2 dir, List<Vector2> outline)
        {
            float best = float.MaxValue;
            for (int i = 0; i < outline.Count - 1; i++)
            {
                Vector2 a = outline[i], b = outline[i + 1];
                Vector2 e = b - a;
                float denom = dir.x * e.y - dir.y * e.x;
                if (Mathf.Abs(denom) < 1e-8f) continue;
                Vector2 ao = a - origin;
                float t = (ao.x * e.y - ao.y * e.x) / denom;     // distance along the ray
                float u = (ao.x * dir.y - ao.y * dir.x) / denom; // position along the edge
                if (t > 0f && u >= -1e-4f && u <= 1f + 1e-4f && t < best) best = t;
            }
            return origin + dir * best;
        }
    }
}
