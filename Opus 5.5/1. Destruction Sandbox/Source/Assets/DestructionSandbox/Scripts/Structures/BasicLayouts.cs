using System.Collections.Generic;
using UnityEngine;
using S = DestructionSandbox.GameConfig.Structures;

namespace DestructionSandbox
{
    /// <summary>Tower (D-04) and Pyramid (D-06) layouts: plain grids of boxes placed exactly face to face.</summary>
    public static class BasicLayouts
    {
        public const int TowerId = 0;
        public const int WallId = 1;
        public const int PyramidId = 2;

        public static StructureLayout BuildTower()
        {
            var pieces = new List<PieceDef>(S.TowerFootprint * S.TowerFootprint * S.TowerFloors);
            Vector3 size = S.TowerBlockSize;
            float offset = (S.TowerFootprint - 1) * 0.5f;
            for (int floor = 1; floor <= S.TowerFloors; floor++)
            {
                BlockMaterialType mat = floor <= S.TowerStoneLastFloor ? BlockMaterialType.Stone
                    : floor <= S.TowerWoodLastFloor ? BlockMaterialType.Wood : BlockMaterialType.Glass;
                // Odd floors are rotated 90 degrees (interlocked look: grain/pattern direction alternates per floor).
                Quaternion rot = (floor % 2 == 1) ? Quaternion.Euler(0f, S.TowerOddFloorYaw, 0f) : Quaternion.identity;
                float y = (floor - 0.5f) * size.y;
                for (int ix = 0; ix < S.TowerFootprint; ix++)
                {
                    for (int iz = 0; iz < S.TowerFootprint; iz++)
                    {
                        var p = new PieceDef
                        {
                            Material = mat,
                            Position = GameConfig.World.StructureOrigin + new Vector3((ix - offset) * size.x, y, (iz - offset) * size.z),
                            Rotation = rot,
                            Size = size,
                            Volume = size.x * size.y * size.z,
                            UserValue = LayoutHash.UserValue(TowerId, pieces.Count)
                        };
                        pieces.Add(p);
                    }
                }
            }
            return new StructureLayout { Name = "Tower", Pieces = pieces.ToArray() };
        }

        public static StructureLayout BuildPyramid()
        {
            var pieces = new List<PieceDef>(400);
            float cube = S.PyramidCubeSize;
            var size = new Vector3(cube, cube, cube);
            int layers = S.PyramidBaseSize;
            for (int layer = 1; layer <= layers; layer++)
            {
                int side = S.PyramidBaseSize - layer + 1;
                BlockMaterialType mat;
                if (layer > layers - S.PyramidGlassCapLayers) mat = BlockMaterialType.Glass;      // glass cap
                else mat = (layer % 2 == 1) ? BlockMaterialType.Wood : BlockMaterialType.Stone;    // wood/stone alternate
                float y = (layer - 0.5f) * cube;
                float offset = (side - 1) * 0.5f;
                for (int ix = 0; ix < side; ix++)
                {
                    for (int iz = 0; iz < side; iz++)
                    {
                        pieces.Add(new PieceDef
                        {
                            Material = mat,
                            Position = GameConfig.World.StructureOrigin + new Vector3((ix - offset) * cube, y, (iz - offset) * cube),
                            Rotation = Quaternion.identity,
                            Size = size,
                            Volume = cube * cube * cube,
                            UserValue = LayoutHash.UserValue(PyramidId, pieces.Count)
                        });
                    }
                }
            }
            return new StructureLayout { Name = "Pyramid", Pieces = pieces.ToArray() };
        }
    }
}
