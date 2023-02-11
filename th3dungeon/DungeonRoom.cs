using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Th3Dungeon
{
    public class DungeonRoom
    {
        public readonly BlockSchematic[] Rotations;

        public DungeonRoom(ICoreServerAPI api, BlockSchematic schematic, IWorldGenBlockAccessor _chunkGenBlockAccessor, string fileName)
        {
            Rotations = new BlockSchematic[4];
            Rotations[0] = schematic.ClonePacked();

            for (int k = 0; k < 4; k++)
            {
                if (k > 0)
                {
                    Rotations[k] = Rotations[0].ClonePacked();
                    Rotations[k].TransformWhilePacked(api.World, EnumOrigin.MiddleCenter, k * 90);
                }
                Rotations[k].Init(_chunkGenBlockAccessor);
                Rotations[k].LoadMeta(_chunkGenBlockAccessor, api.World, fileName);
            }
        }
    }
}