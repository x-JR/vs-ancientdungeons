using System;
using ProtoBuf;

namespace th3dungeon.Data
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ChunkPos
    {
        public int X;

        public int Z;

        public ChunkPos()
        {
        }

        public ChunkPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int Distance(ChunkPos pos)
        {
            var deltaX = X - pos.X;
            var deltaZ = Z - pos.Z;
            return (int)Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            ChunkPos otherPos = (ChunkPos)obj;
            return X == otherPos.X && Z == otherPos.Z;
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Z;
        }
    }
}