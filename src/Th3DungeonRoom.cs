using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Th3Dungeon
{
    public class Th3DungeonRoom
    {
        public readonly Th3BlockSchematic[] Rotations;

        public Th3DungeonRoom(ICoreServerAPI api, Th3BlockSchematic schematic, IWorldGenBlockAccessor _chunkGenBlockAccessor, string fileName)
        {
            Rotations = new Th3BlockSchematic[4];
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