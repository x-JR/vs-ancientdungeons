using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace th3dungeon.Data
{
    public class DungeonRoom
    {
        public string Name;
        public readonly BlockSchematic[] Rotations;

        public DungeonRoom(ICoreServerAPI api, BlockSchematic schematic, IBlockAccessor chunkGenBlockAccessor, string fileName)
        {
            Rotations = new BlockSchematic[4];
            Rotations[0] = schematic.ClonePacked();
            Name = fileName;

            for (var k = 0; k < 4; k++)
            {
                if (k > 0)
                {
                    Rotations[k] = Rotations[0].ClonePacked();
                    Rotations[k].TransformWhilePacked(api.World, EnumOrigin.MiddleCenter, k * 90);
                }
                Rotations[k].Init(chunkGenBlockAccessor);
                Rotations[k].LoadMeta(chunkGenBlockAccessor, api.World, fileName);
            }
        }
    }
}