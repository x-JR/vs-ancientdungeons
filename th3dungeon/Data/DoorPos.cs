using Vintagestory.API.MathTools;

namespace th3dungeon.Data
{
  public class DoorPos
  {
    public BlockPos Position;

    public BlockFacing Facing;

    public DoorPos(BlockPos pos, BlockFacing facing)
    {
      Position = pos;
      Facing = facing;
    }
  }
}