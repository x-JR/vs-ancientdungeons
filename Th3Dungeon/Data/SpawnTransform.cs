using Vintagestory.API.MathTools;

namespace th3dungeon.Data
{
    public class SpawnTransform
    {
        public DungeonRoom Room;

        public BlockPos Position;

        public BlockPos OrigPosition;

        public SpawnTransform()
        {
            Position = new BlockPos(0);
            OrigPosition = new BlockPos(0);
        }

        public SpawnTransform(DungeonRoom room, BlockPos position)
        {
            Room = room;
            Position = position;
        }
    }
}