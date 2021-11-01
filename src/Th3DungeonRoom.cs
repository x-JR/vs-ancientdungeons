using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace Th3Dungeon
{
  public class Th3DungeonRoom
  {
    public readonly Th3BlockSchematic[] rotations;

    public Th3DungeonRoom(Th3BlockSchematic schematic, ICoreServerAPI api, IWorldGenBlockAccessor _chunkGenBlockAccessor, string fileName)
    {
      rotations = new Th3BlockSchematic[4];
      rotations[0] = schematic.ClonePacked();

      for (int k = 0; k < 4; k++)
      {
        if (k > 0)
        {
          rotations[k] = rotations[0].ClonePacked();
          rotations[k].TransformWhilePacked(api.World, EnumOrigin.MiddleCenter, k * 90);
        }
        rotations[k].Init(_chunkGenBlockAccessor);
        rotations[k].LoadMeta(_chunkGenBlockAccessor, api.World, fileName);
      }
    }
  }
}