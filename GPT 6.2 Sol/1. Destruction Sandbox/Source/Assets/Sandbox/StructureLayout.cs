using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class StructureLayout
    {
        readonly PieceField field;
        readonly Mesh archWedge;
        readonly float wedgeArea;
        public Vector3 WedgeBounds => archWedge.bounds.size;

        public StructureLayout(PieceField field)
        {
            this.field = field;
            float radius = ArchWidth * .5f;
            float halfStep = Mathf.PI / (2 * ArchStones);
            float innerWidth = 2 * radius * Mathf.Tan(halfStep);
            float outerWidth = 2 * (radius + ArchStoneDepth) * Mathf.Tan(halfStep);
            wedgeArea = (innerWidth + outerWidth) * ArchStoneDepth * .5f;
            archWedge = Wedge(innerWidth, outerWidth, ArchStoneDepth, WallBrick.z);
        }

        public void Build(Layout layout)
        {
            switch (layout)
            {
                case Layout.Arch: Arch(); break;
                case Layout.Pyramid: Pyramid(); break;
                default: Tower(); break;
            }
        }

        void Tower()
        {
            float center = (TowerWidth - 1) * .5f;
            for (int floor = 1; floor <= TowerFloors; floor++)
            {
                Matter kind = floor <= StoneFloors ? Matter.Stone : floor <= WoodEndFloor ? Matter.Wood : Matter.Glass;
                bool odd = (floor & 1) == 1;
                var rotation = odd ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                // Offset the rotated square courses so their vertical joints do not line up.
                Vector3 bond = odd ? new Vector3(TowerBond, 0, TowerBond) : Vector3.zero;
                for (int row = 0; row < TowerWidth; row++)
                for (int col = 0; col < TowerWidth; col++)
                {
                    Vector3 flat = new((col - center) * TowerBlock.x, 0, (row - center) * TowerBlock.z);
                    Vector3 p = rotation * flat + bond + Vector3.up * ((floor - .5f) * TowerBlock.y);
                    field.AddBlock(kind, p, rotation, TowerBlock);
                }
            }
        }

        void Arch()
        {
            float radius = ArchWidth * .5f;
            float outerRadius = radius + ArchStoneDepth;
            float side = WallWidth * WallBrick.x * .5f;
            for (int row = 1; row <= WallRows; row++)
            {
                float bottom = (row - 1) * WallBrick.y;
                float hole = bottom < ArchSpring ? radius
                    : bottom < ArchSpring + outerRadius ? Mathf.Sqrt(outerRadius * outerRadius - (bottom - ArchSpring) * (bottom - ArchSpring))
                    : 0;
                float offset = (row & 1) == 0 ? WallBrick.x * .5f : 0;
                for (int col = -1; col < WallWidth; col++)
                {
                    float left = Mathf.Max(-side, -side + col * WallBrick.x + offset);
                    float right = Mathf.Min(side, -side + (col + 1) * WallBrick.x + offset);
                    if (right - left < .05f) continue;
                    float middle = (left + right) * .5f;
                    bool window = row >= WindowsStart && row <= WindowsEnd &&
                        (Mathf.Abs(Mathf.Abs(middle) - 5.25f) < .84f || Mathf.Abs(Mathf.Abs(middle) - 8.75f) < .84f);
                    Matter kind = window ? Matter.Glass : Matter.Stone;
                    if (hole == 0)
                        Brick(left, right, bottom, kind);
                    else
                    {
                        Brick(left, Mathf.Min(right, -hole), bottom, kind);
                        Brick(Mathf.Max(left, hole), right, bottom, kind);
                    }
                }
            }

            float bearing = radius + ArchStoneDepth * .5f;
            for (int i = 0; i < ArchStones; i++)
            {
                float angle = (i + .5f) * Mathf.PI / ArchStones;
                Vector3 p = new(Mathf.Cos(angle) * bearing, ArchSpring + Mathf.Sin(angle) * bearing, 0);
                var rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
                field.AddBlock(Matter.Stone, p, rotation, archWedge.bounds.size, archWedge, wedgeArea * WallBrick.z);
            }
        }

        void Brick(float left, float right, float bottom, Matter kind)
        {
            if (right - left < .06f) return;
            field.AddBlock(kind, new Vector3((left + right) * .5f, bottom + WallBrick.y * .5f, 0),
                Quaternion.identity, new Vector3(right - left, WallBrick.y, WallBrick.z));
        }

        void Pyramid()
        {
            for (int width = PyramidWidth; width >= 1; width--)
            {
                int layer = PyramidWidth - width;
                float center = (width - 1) * .5f;
                for (int row = 0; row < width; row++)
                for (int col = 0; col < width; col++)
                {
                    int lowerCap = PyramidGlass - 1;
                    bool cap = width == 1 || (width == lowerCap && row == col);
                    Matter kind = cap ? Matter.Glass : (layer & 1) == 0 ? Matter.Stone : Matter.Wood;
                    field.AddBlock(kind, new Vector3(col - center, layer + .5f, row - center), Quaternion.identity, PyramidBlock);
                }
            }
        }

        static Mesh Wedge(float inner, float outer, float height, float depth)
        {
            float a = inner * .5f, b = outer * .5f, y = height * .5f, z = depth * .5f;
            var corners = new Vector3[] {
                new(-a, -y, -z), new(a, -y, -z), new(b, y, -z), new(-b, y, -z),
                new(-a, -y, z), new(a, -y, z), new(b, y, z), new(-b, y, z)
            };
            int[] faces = { 0,3,2,1, 4,5,6,7, 0,1,5,4, 3,7,6,2, 0,4,7,3, 1,2,6,5 };
            var vertices = new Vector3[faces.Length];
            var uv = new Vector2[faces.Length];
            var triangles = new int[faces.Length / 4 * 6];
            for (int i = 0; i < faces.Length; i += 4)
            {
                for (int j = 0; j < 4; j++) vertices[i + j] = corners[faces[i + j]];
                uv[i] = Vector2.zero; uv[i + 1] = Vector2.up;
                uv[i + 2] = Vector2.one; uv[i + 3] = Vector2.right;
                int t = i / 4 * 6;
                triangles[t] = i; triangles[t + 1] = i + 1; triangles[t + 2] = i + 2;
                triangles[t + 3] = i; triangles[t + 4] = i + 2; triangles[t + 5] = i + 3;
            }
            var mesh = new Mesh { name = "Trapezoidal voussoir", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
