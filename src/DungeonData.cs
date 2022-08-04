using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Th3Dungeon
{
    public class DungeonData
    {
        public SpawnTransform NextSpawn;

        public List<DoorPos> DoorPos;

        public List<Cuboidi> GeneratedRooms;

        public BlockSchematic Schematic;

        public DungeonConfig DungeonConfig;

        public int ChunkX { get; }

        public int ChunkZ { get; }

        public bool Initialized;

        public List<GeneratedStructure> GeneratedStructures;

        public DungeonData(int chunkX, int chunkZ, List<GeneratedStructure> generatedStructures)
        {
            DoorPos = new List<DoorPos>();
            NextSpawn = new SpawnTransform();
            GeneratedRooms = new List<Cuboidi>();
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            GeneratedStructures = generatedStructures;
        }
    }
}