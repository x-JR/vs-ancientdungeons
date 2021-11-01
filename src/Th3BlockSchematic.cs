using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Th3Dungeon
{
  internal class Th3BlockSchematic : BlockSchematic
  {
    private int _chunkSize;

    private List<Block> DoorBlocks;

    private List<BlockPos> Doors;

    public Mod Mod { get; private set; }

    public void Init(IBlockAccessor blockAccessor, Mod Mod)
    {
      base.Init(blockAccessor);
      this.Mod = Mod;
      _chunkSize = blockAccessor.ChunkSize;
    }

    public void LoadMeta(IBlockAccessor blockAccessor, IWorldAccessor worldForResolve, string fileNameForLogging)
    {
      LoadMetaInformationAndValidate(blockAccessor, worldForResolve, fileNameForLogging);
      DoorBlocks = new List<Block>
      {
        blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-north")),
        blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-east")),
        blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-south")),
        blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-west"))
      };

      Doors = new List<BlockPos>();
      for (int i = 0; i < BlockIds.Count; i++)
      {
        int storedBlockid = BlockIds[i];

        AssetLocation blockCode = BlockCodes[storedBlockid];
        Block newBlock = blockAccessor.GetBlock(blockCode);

        if (DoorBlocks.Contains(newBlock))
        {
          uint index = Indices[i];
          int dx = (int)(index & 0x1ff);
          int dy = (int)((index >> 20) & 0x1ff);
          int dz = (int)((index >> 10) & 0x1ff);
          Doors.Add(new BlockPos(dx, dy, dz));
        }
      }
    }

    public int Place(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int chunkX, int chunkZ, bool replaceMetaBlocks = true)
    {
      BlockPos curPos = new BlockPos();
      int placed = 0;

      PlaceBlockDelegate handler = null;
      switch (ReplaceMode)
      {
        case EnumReplaceMode.ReplaceAll:
          if (replaceMetaBlocks) handler = PlaceReplaceAllReplaceMeta;
          else handler = PlaceReplaceAllKeepMeta;
          break;

        case EnumReplaceMode.Replaceable:
          if (replaceMetaBlocks) handler = PlaceReplaceableReplaceMeta;
          else handler = PlaceReplaceableKeepMeta;
          break;

        case EnumReplaceMode.ReplaceAllNoAir:
          if (replaceMetaBlocks) handler = PlaceReplaceAllNoAirReplaceMeta;
          else handler = PlaceReplaceAllNoAirKeepMeta;
          break;

        case EnumReplaceMode.ReplaceOnlyAir:
          if (replaceMetaBlocks) handler = PlaceReplaceOnlyAirReplaceMeta;
          else handler = PlaceReplaceOnlyAirKeepMeta;
          break;
      }


      for (int i = 0; i < Indices.Count; i++)
      {
        uint index = Indices[i];

        int dx = (int)(index & 0x1ff);
        int dy = (int)((index >> 20) & 0x1ff);
        int dz = (int)((index >> 10) & 0x1ff);

        if ((dx + startPos.X) / _chunkSize == chunkX && (dz + startPos.Z) / _chunkSize == chunkZ)
        {
          int storedBlockid = BlockIds[i];
          AssetLocation blockCode = BlockCodes[storedBlockid];

          Block newBlock = blockAccessor.GetBlock(blockCode);

          if (newBlock == null || (replaceMetaBlocks && newBlock == undergroundBlock)) continue;


          curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

          Block oldBlock = blockAccessor.GetBlock(curPos);
          placed += handler(blockAccessor, curPos, oldBlock, newBlock);

          if (newBlock.LightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor accessor)
          {
            accessor.ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
          }
        }

      }

      if (!(blockAccessor is IBlockAccessorRevertable))
      {
        PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, startPos);
      }

      PlaceDecors(blockAccessor, startPos, false, chunkX, chunkZ);
      return placed;
    }

    public void PlaceDecors(IBlockAccessor blockAccessor, BlockPos startPos, bool synchronize, int chunkX, int chunkZ)
    {
      BlockPos curPos = new BlockPos();
      for (int i = 0; i < DecorIndices.Count; i++)
      {
        uint index = DecorIndices[i];

        int dx = (int)(index & 0x1ff);
        int dy = (int)((index >> 20) & 0x1ff);
        int dz = (int)((index >> 10) & 0x1ff);

        if ((dx + startPos.X) / _chunkSize == chunkX && (dz + startPos.Z) / _chunkSize == chunkZ)
        {
          int storedBlockid = DecorIds[i];
          byte faceIndex = (byte)(storedBlockid >> 24);
          if (faceIndex > 5) continue;

          BlockFacing face = BlockFacing.ALLFACES[faceIndex];
          storedBlockid &= 0xFFFFFF;

          AssetLocation blockCode = BlockCodes[storedBlockid];

          Block newBlock = blockAccessor.GetBlock(blockCode);

          if (newBlock == null) continue;

          curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

          IWorldChunk chunk = blockAccessor.GetChunkAtBlockPos(curPos);
          if (chunk == null) continue;
          if (synchronize) blockAccessor.MarkChunkDecorsModified(curPos);
          chunk.AddDecor(blockAccessor, newBlock, curPos, face);
          chunk.MarkModified();
        }
      }
    }
  }
}