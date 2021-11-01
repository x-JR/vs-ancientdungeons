using Vintagestory.API.MathTools;

namespace Th3Dungeon
{
  public class Th3DoorPos
  {
    public BlockPos Position;

    public BlockFacing Facing;

    public Th3DoorPos(BlockPos pos, BlockFacing facing)
    {
      Position = pos;
      Facing = facing;
    }
  }
}