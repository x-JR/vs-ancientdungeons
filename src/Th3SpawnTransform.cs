using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Th3Dungeon
{
  public class Th3SpawnTransform
  {
    public Th3DungeonRoom Room;

    public BlockPos Position;

    public BlockPos OrigPosition;

    public BlockFacing Facing;

    public Th3SpawnTransform()
    {
      Position = new BlockPos();
      OrigPosition = new BlockPos();
    }

    public Th3SpawnTransform(Th3DungeonRoom room, BlockPos position, BlockFacing facing)
    {
      Room = room;
      Position = position;
      Facing = facing;
    }
  }
}