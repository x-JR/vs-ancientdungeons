using System.Collections.Generic;
using ProtoBuf;

namespace th3dungeon.Data
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DungeonSaveData
    {
        public List<ChunkPos> GeneratedDungeons;

        internal bool Modified;

        public DungeonSaveData()
        {
            GeneratedDungeons = new List<ChunkPos>();
        }
    }
}