using Vintagestory.API.MathTools;

namespace Th3Dungeon
{
    public class SpawnTransform
    {
        public DungeonRoom Room;

        public BlockPos Position;

        public BlockPos OrigPosition;

        public SpawnTransform()
        {
            Position = new BlockPos();
            OrigPosition = new BlockPos();
        }

        public SpawnTransform(DungeonRoom room, BlockPos position)
        {
            Room = room;
            Position = position;
        }
    }
}